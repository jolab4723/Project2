using System;
using UnityEngine;

/// <summary>
/// 플레이어 전투 스탯을 WBH 전투 시스템(WBH_ICombatStatus)에 제공하는 어댑터.
///
/// 같은 GameObject에 WJ 스탯 시스템(PlayerStatManager / PlayerHealthManager)이 붙어 있으면
/// 레벨+장비+버프+패시브가 합산된 그쪽 값을 노출하고, 체력 증감도 PlayerHealthManager로 넘긴다.
/// 붙어 있지 않으면 아래 [Fallback Stat] 필드로 기존과 똑같이 동작한다
/// (WBH_PlayerStatus는 Fighter/Gunner 프리팹과 여러 테스트 씬에서 공용으로 쓰이기 때문).
///
/// !! 단위 주의: WJ 스탯은 치명타 확률/치명타 피해/속성 보너스를 "퍼센트 숫자"로 들고 있다
///    (FighterStatData.json의 baseCritRate 5.0 = 5%, ElementBonusConfig의 elementBonusValue 5 = +5%).
///    반면 WBH_CombatManager는 CritRate를 Random.value와 비교하는 분수(0.05),
///    FireBonus 등을 damage * (1 + bonus)에 쓰는 분수(0.05), CritMult를 damage *= 로 쓰는 배율(1.05)로
///    기대한다. 그래서 이 어댑터에서 PercentToFraction / PercentToMultiplier로 변환해준다.
/// </summary>
public class WBH_PlayerStatus : MonoBehaviour, WBH_ICombatStatus
{
    /// <summary>퍼센트 숫자(5)를 분수(0.05)로. 치명타 확률/속성 보너스용.</summary>
    private static float PercentToFraction(float percent) => percent / 100f;

    /// <summary>퍼센트 숫자(10)를 배율(1.10)로. 치명타 피해용.</summary>
    private static float PercentToMultiplier(float percent) => 1f + percent / 100f;

    [Header("Fallback Stat (WJ 스탯 시스템이 없을 때만 사용)")]
    [SerializeField] private float maxHp = 100;
    [SerializeField] private float attackPower = 5;
    [SerializeField] private float defensePower = 3;
    [SerializeField] private float attackSpeed = 1;
    [SerializeField] private float moveSpeed = 6;
    [SerializeField] private float criticalChance = 1;
    [SerializeField] private float criticalMultiplier = 1;
    [SerializeField] private float fireBonus = 1;
    [SerializeField] private float iceBonus = 1;
    [SerializeField] private float electricBonus = 1;

    //--- 플레이어 속성에 추가 필요 . (WJ 스탯 시스템에 대응 값이 없어서 항상 아래 값을 사용)
    [Header("Local Only (WJ 스탯 시스템에 대응 값 없음)")]
    [SerializeField] private float dodgeDistance = 5f;
    [SerializeField] private float dodgeDuration = 0.5f;
    [SerializeField] private float dodgeCooltime = 6f;

    [SerializeField] private float fighterAttackRange = 2f;
    [SerializeField] private float gunnerAttackRange = 10f;
    [SerializeField] private float gunnerBulletSpeed = 10f;
    //---

    private float currentAttackPower = 5;
    private float currentAttackSpeed = 1;
    private float currentMoveSpeed = 6;

    private T_PlayerController playerController;
    private float currentHp;

    // WJ 스탯 시스템 연결부. 둘 다 없으면 위 Fallback 필드로 동작한다.
    private PlayerStatManager statManager;
    private PlayerHealthManager healthManager;
    private bool managersResolved;

    /// <summary>OnStatChanged를 구독 중인 PlayerStat. 중복 구독/해제 누락을 막기 위해 들고 있는다.</summary>
    private PlayerStat subscribedStat;

    public event Action<float, float> OnHpChanged;
    public event Action<float> OnAtkSpeedChanged; // 애니메이션 모션 속도를 공격속도와 연동되게끔 하기 위함
    public event Action OnDead;

    /// <summary>
    /// 같은 GameObject의 WJ 스탯 컴포넌트를 찾아둔다.
    /// 같은 오브젝트 내 Awake 순서는 보장되지 않고(T_PlayerController.Awake가 MoveSpeed를 읽는다)
    /// 프로퍼티가 Awake보다 먼저 호출될 수 있어서, 첫 접근 시점에 지연 해석한다.
    /// </summary>
    private void ResolveManagers()
    {
        if (managersResolved)
            return;

        managersResolved = true;
        statManager = GetComponent<PlayerStatManager>();
        healthManager = GetComponent<PlayerHealthManager>();
    }

    /// <summary>WJ 스탯 시스템이 붙어 있고 Stat이 준비됐는지. Stat은 Awake에서 만들어지므로 매번 확인한다.</summary>
    private bool UseStatManager
    {
        get
        {
            ResolveManagers();
            return statManager != null && statManager.Stat != null;
        }
    }

    /// <summary>
    /// PlayerHealthManager를 쓸 수 있는지. MaxHealth는 PlayerHealthManager.Start()에서 채워지므로,
    /// 그 전(MaxHealth가 0)에는 아직 준비 안 된 것으로 보고 Fallback을 쓴다.
    /// (안 그러면 첫 프레임에 CurrentHealth 0 = 사망으로 오판해서 T_PlayerCombat이 멈춘다.)
    /// </summary>
    private bool UseHealthManager
    {
        get
        {
            ResolveManagers();
            return healthManager != null && healthManager.MaxHealth > 0f;
        }
    }

    // -- combatManager 계산을 위한 인터페이스
    public float MaxHealth => UseStatManager ? statManager.Stat.maxHealth : maxHp;
    public float CurrentHp => UseHealthManager ? healthManager.CurrentHealth : currentHp;
    public float AttackPower => UseStatManager ? statManager.Stat.attackPower : currentAttackPower;
    public float DefensePower => UseStatManager ? statManager.Stat.defensePower : defensePower;
    public float CritRate => UseStatManager ? PercentToFraction(statManager.Stat.critRate) : criticalChance;
    public float CritMult => UseStatManager ? PercentToMultiplier(statManager.Stat.critMult) : criticalMultiplier;
    public float FireBonus => UseStatManager ? PercentToFraction(statManager.Stat.fireBonus) : fireBonus;
    public float IceBonus => UseStatManager ? PercentToFraction(statManager.Stat.iceBonus) : iceBonus;
    public float ElectricBonus => UseStatManager ? PercentToFraction(statManager.Stat.electricBonus) : electricBonus;

    /// <summary>
    /// 현재 장착 무기에 인챈트된 속성. 모든 공격은 이 속성의 공격으로 간주되어 동일 속성 피해 보너스를 받는다.
    /// WJ 스탯 시스템이 없거나 무기 정보를 못 가져오면 무속성(None)으로 취급한다.
    /// </summary>
    public ItemSystem.ElementType CurrentElement =>
        UseStatManager && statManager.TryGetEquippedWeaponInfo(out EquippedWeaponInfo weaponInfo)
            ? weaponInfo.elementType
            : ItemSystem.ElementType.None;

    //-- 외부 사용을 위한 프로퍼티
    public float AttackSpeed => UseStatManager ? statManager.Stat.attackSpeed : currentAttackSpeed;
    public float MoveSpeed => UseStatManager ? statManager.Stat.moveSpeed : currentMoveSpeed;
    public float DodgeDistance => dodgeDistance;
    public float DodgeDuration => dodgeDuration;
    public float DodgeCooltime => dodgeCooltime;
    public float FighterAttackRange => fighterAttackRange;
    public float GunnerAttackRange => gunnerAttackRange;
    public float GunnerBulletSpeed => gunnerBulletSpeed;

    public bool IsDead => UseHealthManager ? healthManager.CurrentHealth <= 0f : currentHp <= 0;

    private float minDamage;


    private void Awake()
    {
        ResolveManagers();
    }

    private void OnEnable()
    {
        ResolveManagers();

        // PlayerHealthManager가 체력을 관리할 때도 기존 OnHpChanged/OnDead 구독자가 그대로 동작하도록 중계한다.
        if (healthManager != null)
        {
            healthManager.OnHealthChanged += RelayHealthChanged;
            healthManager.OnDeath += RelayDeath;
        }

        // 비활성화 후 다시 켜진 경우에도 이동속도 동기화가 살아있도록 재구독한다.
        // (첫 OnEnable 시점엔 Stat/playerController가 아직 없을 수 있는데, 그건 Initialize에서 처리한다)
        SubscribeStatChanges();
        ApplyMoveSpeedToController();
    }

    private void OnDisable()
    {
        if (healthManager != null)
        {
            healthManager.OnHealthChanged -= RelayHealthChanged;
            healthManager.OnDeath -= RelayDeath;
        }

        UnsubscribeStatChanges();
    }

    private void RelayHealthChanged() => OnHpChanged?.Invoke(CurrentHp, MaxHealth);
    private void RelayDeath() => OnDead?.Invoke();

    public void Initialize(T_PlayerController playerController)
    {
        this.playerController = playerController;

        // PlayerHealthManager가 있으면 현재 체력은 그쪽이 Start()에서 풀피로 초기화한다.
        if (healthManager == null)
        {
            currentHp = maxHp;
            currentAttackPower = attackPower; 
            currentAttackSpeed = attackSpeed;
            currentMoveSpeed = moveSpeed;
        }

        // T_PlayerController.Start()에서 호출되므로 이 시점엔 PlayerStatManager.Awake()가 이미 끝나
        // Stat이 만들어져 있다. 여기서 구독하고 초기 이동속도를 한 번 적용한다.
        SubscribeStatChanges();
        ApplyMoveSpeedToController();
    }

    /// <summary>스탯이 재계산될 때마다 NavMeshAgent 속도를 다시 맞추도록 구독한다.</summary>
    private void SubscribeStatChanges()
    {
        ResolveManagers();

        if (statManager == null || statManager.Stat == null)
            return;

        if (subscribedStat == statManager.Stat)
            return;

        UnsubscribeStatChanges();
        subscribedStat = statManager.Stat;
        subscribedStat.OnStatChanged += ApplyMoveSpeedToController;
    }

    private void UnsubscribeStatChanges()
    {
        if (subscribedStat == null)
            return;

        subscribedStat.OnStatChanged -= ApplyMoveSpeedToController;
        subscribedStat = null;
    }

    /// <summary>
    /// PlayerStatManager가 합산한 최종 이동속도(캐릭터+장비+버프+패시브)를 NavMeshAgent에 그대로 적용한다.
    /// !! 배율이 아니라 절대값이다 - NavMeshAgent에 미리 설정된 speed는 무시되고 Stat.moveSpeed가 실제 속도가 된다.
    ///    T_PlayerController.Awake()의 'agent.speed *= status.MoveSpeed'는 Start 시점에 이 값으로 덮어써진다.
    /// </summary>
    private void ApplyMoveSpeedToController()
    {
        if (playerController == null || !UseStatManager)
            return;

        playerController.SetMoveSpeed(statManager.Stat.moveSpeed);
    }

    public void TakeDamage(WBH_DamageResult result)
    {
        if (UseHealthManager)
        {
            // OnHpChanged/OnDead는 PlayerHealthManager 이벤트를 중계하면서 발행된다.
            healthManager.TakeDamage(result.FinalDamage);
            return;
        }

        currentHp -= result.FinalDamage;
        currentHp = Mathf.Max(currentHp, 0);

        OnHpChanged?.Invoke(currentHp, MaxHealth);

        if (currentHp == 0)
        {
            OnDead?.Invoke();
        }
    }

    // 상태이상으로 인한 데미지를 받을 때를 위한 오버로드
    public void TakeDamage(float damage)
    {
        currentHp -= damage;

        WBH_DamageResult result = new WBH_DamageResult(null, damage, false, ItemSystem.ElementType.Fire);

        OnHpChanged?.Invoke(currentHp, MaxHealth);
        Log.Print($"{CurrentHp}");
    }

    public void Heal(float amount)
    {
        if (UseHealthManager)
        {
            healthManager.Heal(amount);
            return;
        }

        currentHp += amount;
        currentHp = Mathf.Min(currentHp, maxHp);
        OnHpChanged?.Invoke(currentHp, MaxHealth);
    }

    public void MultiplyMoveSpeed(float modifier)
    {
        if (WarnIfStatManagerOwnsStats(nameof(MultiplyMoveSpeed)))
            return;

        currentMoveSpeed = moveSpeed * modifier;
        Debug.Log($"CurrentMoveSpeed : {currentMoveSpeed}");
        playerController.SetMoveSpeed(currentMoveSpeed);
    }
    public void MultiplyAttackSpeed(float modifier)
    {
        if (WarnIfStatManagerOwnsStats(nameof(MultiplyAttackSpeed)))
            return;

        currentAttackSpeed = attackSpeed * modifier;
        OnAtkSpeedChanged?.Invoke(currentAttackSpeed);
    }
    public void MultiplyAttack(float modifier)
    {
        if (WarnIfStatManagerOwnsStats(nameof(MultiplyAttack)))
            return;

        currentAttackPower = attackPower * modifier;
    }

    /// <summary>
    /// WJ 스탯 시스템이 스탯을 소유한 상태에서는 Fallback 필드를 곱해도 실제 스탯이 바뀌지 않는다.
    /// 이 경우 조용히 무시되지 않도록 경고를 남기고 true를 반환한다.
    /// (일시적인 배율 변경은 PlayerBuffManager.ApplyBuff로 처리해야 함)
    /// </summary>
    private bool WarnIfStatManagerOwnsStats(string methodName)
    {
        if (!UseStatManager)
            return false;

        Debug.LogWarning($"[WBH_PlayerStatus] {methodName}은(는) PlayerStatManager가 스탯을 관리할 때 효과가 없습니다. " +
                         "PlayerBuffManager.ApplyBuff(BuffDefinitionSO)로 처리해주세요.");
        return true;
    }
}
