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

    //--- 플레이어 속성에 추가 필요 . (WJ 스탯 시스템에 대응 값이 없어서 항상 아래 값을 사용)
    [Header("Local Only (WJ 스탯 시스템에 대응 값 없음)")]
    [SerializeField] private float dodgeDistance = 5f;
    [SerializeField] private float dodgeDuration = 0.5f;
    [SerializeField] private float dodgeCooltime = 6f;

    [SerializeField] private float fighterAttackRange = 2f;
    [SerializeField] private const float fighterAttackAngle = 230f;
    [SerializeField] private float gunnerAttackRange = 10f;
    [SerializeField] private float gunnerBulletSpeed = 10f;
    //---
    // -- combatManager 계산을 위한 인터페이스 및 외부 사용을 위한 프로퍼티
    public float MaxHealth => healthManager.MaxHealth;
    public float MaxMana => manaManager.MaxMana;
    public float CurrentHp => healthManager.CurrentHealth;
    public float CurrentMp => manaManager.CurrentMana;
    public float AttackPower => statManager.Stat.attackPower;
    public float DefensePower => statManager.Stat.defensePower;
    public float Pen => statManager.Stat.pen;
    public float CritRate => PercentToFraction(statManager.Stat.critRate);
    public float CritMult => PercentToMultiplier(statManager.Stat.critMult);
    public float FireBonus => PercentToFraction(statManager.Stat.fireBonus);
    public float IceBonus => PercentToFraction(statManager.Stat.iceBonus);
    public float ElectricBonus => PercentToFraction(statManager.Stat.electricBonus);
    // 플레이어를 대상으로 한 Marked(받는 데미지 증가) 디버프는 아직 안 쓰여서 항상 1(영향 없음).
    public float DamageTakenModifier => 1f;
    public float CurrentLevel => statManager.CurrentLevel;
    public float CurrentExp => statManager.CurrentExp;
    public float MaxExp => statManager.ExpToNextLevel;

    /// <summary>
    /// 현재 장착 무기에 인챈트된 속성. 모든 공격은 이 속성의 공격으로 간주되어 동일 속성 피해 보너스를 받는다.
    /// 무기 정보를 못 가져오면 무속성(None)으로 취급한다.
    /// </summary>
    public ItemSystem.ElementType CurrentElement =>
        statManager.TryGetEquippedWeaponInfo(out EquippedWeaponInfo weaponInfo)
            ? weaponInfo.elementType
            : ItemSystem.ElementType.None;
    public float AttackSpeed => statManager.Stat.attackSpeed;
    public float MoveSpeed => statManager.Stat.moveSpeed;
    // 아직 statManager 에 구현되지 않은 능력치 차후 구현되면 위처럼 스탯매니저에서 값을 받아오는 형식의 코드로 변경
    public float DodgeDistance => dodgeDistance;
    public float DodgeDuration => dodgeDuration;
    public float DodgeCooltime => dodgeCooltime;
    public float FighterAttackRange => fighterAttackRange;
    public float FighterAttackAngle => fighterAttackAngle;

    public float GunnerAttackRange => gunnerAttackRange;
    public float GunnerBulletSpeed => gunnerBulletSpeed;
    public bool IsDead => healthManager.CurrentHealth <= 0f;

    private float minDamage;

    private T_PlayerController playerController;
    private PlayerStatManager statManager;
    private PlayerHealthManager healthManager;
    private PlayerManaManager manaManager;
    private bool managersResolved;

    /// <summary>OnStatChanged를 구독 중인 PlayerStat. 중복 구독/해제 누락을 막기 위해 들고 있는다.</summary>
    private PlayerStat subscribedStat;

    public event Action<float> OnAtkSpeedChanged; // 애니메이션 모션 속도를 공격속도와 연동되게끔 하기 위함
    public event Action OnDead;

    private void Awake()
    {
        statManager = GetComponent<PlayerStatManager>();
        healthManager = GetComponent<PlayerHealthManager>();
        manaManager = GetComponent<PlayerManaManager>();
    }

    private void OnEnable()
    {
        if(healthManager != null)
        {
            healthManager.OnDeath += HandleDeath;
        }

        if(statManager?.Stat != null)
        {
            statManager.Stat.OnStatChanged += HandleStatChanged;
        }
    }
    private void OnDisable()
    {
        if (healthManager != null)
        {
            healthManager.OnDeath -= HandleDeath;
        }

        if (statManager?.Stat != null)
        {
            statManager.Stat.OnStatChanged -= HandleStatChanged;
        }
    }

    public void Initialize(T_PlayerController playerController)
    {
        this.playerController = playerController;

        ApplyMoveSpeed();
        ApplyAtkSpeed();
    }

    private void HandleDeath()
    {
        OnDead?.Invoke();
    }

    private void HandleStatChanged()
    {
        ApplyMoveSpeed();
        ApplyAtkSpeed();
    }

    private void ApplyMoveSpeed()
    {
        playerController?.SetMoveSpeed(MoveSpeed);
    }
    private void ApplyAtkSpeed()
    {
        OnAtkSpeedChanged?.Invoke(AttackSpeed);
    }

    public void TakeDamage(WBH_DamageResult result)
    {
        if (IsDead)
            return;

        healthManager.TakeDamage(result.FinalDamage);
    }

    // 상태이상으로 인한 데미지를 받을 때를 위한 오버로드
    public void TakeDamage(float damage)
    {
        WBH_DamageResult result = new WBH_DamageResult(null, damage, false, ItemSystem.ElementType.Fire);
        healthManager.TakeDamage(result.FinalDamage);
    }

    public bool TryUseMana(float amount)
    {
        if (amount <= 0f)
            return true;

        return manaManager.UseMana(amount);
    }

    public void Heal(float amount)
    {
            healthManager.Heal(amount);
            return;
    }

    public void MultiplyMoveSpeed(float modifier)
    {
        //if (WarnIfStatManagerOwnsStats(nameof(MultiplyMoveSpeed)))
        //    return;

        //currentMoveSpeed = moveSpeed * modifier;
        //Debug.Log($"CurrentMoveSpeed : {currentMoveSpeed}");
        //playerController.SetMoveSpeed(currentMoveSpeed);
    }
    public void MultiplyAttackSpeed(float modifier)
    {
        //if (WarnIfStatManagerOwnsStats(nameof(MultiplyAttackSpeed)))
        //    return;

        //currentAttackSpeed = attackSpeed * modifier;
        //OnAtkSpeedChanged?.Invoke(currentAttackSpeed);
    }
    public void MultiplyAttack(float modifier)
    {
        //if (WarnIfStatManagerOwnsStats(nameof(MultiplyAttack)))
        //    return;

        //currentAttackPower = attackPower * modifier;
    }
}
