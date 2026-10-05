using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyController))]
[RequireComponent(typeof(WBH_EnemyMovement))]
public class WBH_EnemyStatusEffectController : WBH_StatusEffectController
{
    [SerializeField] private Transform statusEffectRoot;

    [Header("Status Effect")]
    [SerializeField] private WBH_EffectData burnEffect;
    [SerializeField] private WBH_EffectData freezeEffect;
    [SerializeField] private WBH_EffectData electricEffect;
    [SerializeField] private WBH_EffectData slowEffect;
    [SerializeField] private WBH_EffectData knockbackEffect;
    [SerializeField] private WBH_EffectData airborneEffect;
    [SerializeField] private WBH_EffectData stunEffect;
    [SerializeField] private WBH_EffectData markedEffect;

    [Header("Burn")]
    [Tooltip("SW 수정: 보스의 화상 피해 배율입니다. 1은 기존 피해, 0은 면역입니다.")]
    [SerializeField, Range(0f, 1f)] private float bossBurnDamageMultiplier = 1f;

    private readonly Dictionary<WBH_StatusEffectType, WBH_Effect> activeEffects = new();
    private readonly HashSet<WBH_StatusEffectType> controlBlockingEffects = new();
    private readonly HashSet<(WBH_ICombat, uint, int)> burnResponses = new();
    private readonly Queue<(WBH_ICombat, uint, int)> burnResponseOrder = new();

    /// <summary>SW 수정: 유효한 화상 시도의 저항·면역을 알립니다. true는 면역입니다.</summary>
    public event System.Action<bool> OnBurnResponse;

    private WBH_EffectSpawner effectSpawner;
    private WBH_EnemyStatus status;
    private WBH_EnemyController controller;
    private WBH_EnemyMovement movement;
    private Mirror.NetworkIdentity networkIdentity;
    private Coroutine knockbackRoutine;
    private Coroutine airborneRoutine;
    private float airborneGroundY; // 에어본 시작 전 지면 높이. 도중에 넉백이 끼어들 때 지면으로 되돌리기 위해 기억해둔다.

    private void Awake()
    {
        status = GetComponent<WBH_EnemyStatus>();
        controller = GetComponent<WBH_EnemyController>();
        movement = GetComponent<WBH_EnemyMovement>();
        networkIdentity = GetComponent<Mirror.NetworkIdentity>();
    }

    /// <summary>SW 수정: 일반 적은 기존 피해를 받고 보스만 Inspector의 화상 저항을 사용합니다.</summary>
    public float BurnDamageMultiplier => controller != null && controller.Info != null &&
        controller.Info.enemyGrade == EnemyGrade.Boss ? Mathf.Clamp01(bossBurnDamageMultiplier) : 1f;

    public void Initialize(WBH_EffectSpawner effectSpawner)
    {
        // SW 수정: 재사용 전에 이전 화상·이동 제약·이펙트를 제거합니다.
        ClearAllStatusEffects();
        burnResponses.Clear();
        burnResponseOrder.Clear();
        this.effectSpawner = effectSpawner;

        controlBlockingEffects.Clear();
        movement.SetStatusEffectControlBlock(false);
    }

    /// <summary>SW 수정: 서버의 살아 있는 적에게만 적용하며, 화상 면역이면 등록하지 않습니다.</summary>
    public override bool CanApplyStatusEffect(WBH_StatusEffectData data)
    {
        return CanReceiveStatusEffect(data) && !IsControlEffectImmune(data.Type) &&
            (data.Type != WBH_StatusEffectType.Burn || BurnDamageMultiplier > 0f);
    }

    private bool IsControlEffectImmune(WBH_StatusEffectType type)
    {
        EnemyGrade grade = controller.Info.enemyGrade;

        bool immuneGrade = grade == EnemyGrade.Elite || grade == EnemyGrade.Boss;

        if (!immuneGrade)
            return false;
        return type == WBH_StatusEffectType.Airborne || type == WBH_StatusEffectType.KnockBack || type == WBH_StatusEffectType.Stun;
    }

    /// <summary>SW 수정: 잘못된 요청과 죽은 적을 제외합니다. 면역도 유효한 요청에는 반응을 표시합니다.</summary>
    private bool CanReceiveStatusEffect(WBH_StatusEffectData data)
    {
        if (!isActiveAndEnabled || status == null || status.IsDead ||
            (networkIdentity != null && !networkIdentity.isServer))
            return false;
        return base.CanApplyStatusEffect(data);
    }

    /// <summary>SW 수정: 게임 규칙 적용 뒤 저항·면역을 한 번 알리며 DoT 틱에는 표시하지 않습니다.</summary>
    public override void AddStatusEffect(WBH_StatusEffectData data)
    {
        if (!CanReceiveStatusEffect(data))
            return;

        base.AddStatusEffect(data);
        if (data.Type != WBH_StatusEffectType.Burn || BurnDamageMultiplier >= 1f)
            return;

        // SW 수정: 서로 다른 플레이어의 같은 공격 번호는 구별합니다.
        // 번호 없는 기존 공격은 같은 프레임에서만 합치고 다음 프레임에는 다시 표시합니다.
        var key = (data.Attacker, data.AttackId, data.AttackId == 0 ? Time.frameCount : 0);
        if (!burnResponses.Add(key))
            return;
        burnResponseOrder.Enqueue(key);
        // 최근 64개만 보관해 오래 살아 있는 보스의 기록이 계속 늘지 않게 합니다.
        if (burnResponseOrder.Count > 64)
            burnResponses.Remove(burnResponseOrder.Dequeue());
        OnBurnResponse?.Invoke(BurnDamageMultiplier <= 0f);
    }

    /// <summary>SW 수정: 재사용한 적은 이전 공격 기록과 원격 화면에만 재생한 잔여 이펙트를 이어받지 않습니다.</summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        foreach (WBH_Effect effect in activeEffects.Values)
            if (effect != null) effect.StopEffect();
        activeEffects.Clear();
        burnResponses.Clear();
        burnResponseOrder.Clear();
    }

    // 몬스터 등급별 예외처리
    protected override void UpdateEffects(float deltaTime)
    {
        if (controller == null || controller.Info == null || status == null || status.IsDead ||
            (networkIdentity != null && !networkIdentity.isServer))
            return;

        base.UpdateEffects(deltaTime);
    }

    // 상태이상 생성 요청
    protected override WBH_IStatusEffect CreateEffect(WBH_StatusEffectData data)
    {
        return WBH_StatusEffectFactory.Create(this, data); // !@
    }

    // 능력치 변경
    public override void ApplyMoveSpeedModifier(float modifier)
    {
        status.MultiplyMoveSpeed(modifier);
    }
    public override void ApplyAttackSpeedModifier(float modifier)
    {
        status.MultiplyAttackSpeed(modifier);
    }
    public override void ApplyAttackModifier(float modifier)
    {
        status.MultiplyAttack(modifier);
    }
    public override void ApplyDefenseModifier(float modifier)
    {
        status.MultiplyDefense(modifier);
    }
    public override void ApplyDamageTakenModifier(float modifier)
    {
        status.MultiplyDamageTaken(modifier);
    }

    // 움직임 가능 여부 판단 (에어본, 스턴 등)
    public override void SetStatusControlBlock(WBH_StatusEffectType source, bool block)
    {
        if (block)
            controlBlockingEffects.Add(source);
        else 
            controlBlockingEffects.Remove(source);

        movement.SetStatusEffectControlBlock(controlBlockingEffects.Count > 0);
        //animator.enabled = enabled; // 애니메이션 사용을 막고 싶을 경우 위의 조건으로 if문 작성하여 추가
    }

    // 도트데미지 (화상)
    public override void ApplyDotDamage(float damage)
    {
        ApplyDotDamage(damage, default);
    }

    /// <summary>
    /// SW 수정: 화상을 적용한 공격자에게 DoT 처치를 귀속합니다.
    /// 대상 최대 체력 비례 계산은 유지하고 보스 저항을 한 번만 적용합니다.
    /// </summary>
    public override void ApplyDotDamage(float damage, WBH_StatusEffectData data)
    {
        if (!isActiveAndEnabled || status == null || status.IsDead ||
            (networkIdentity != null && !networkIdentity.isServer))
            return;
        damage *= BurnDamageMultiplier;
        if (!float.IsFinite(damage) || damage <= 0f)
            return;
        WBH_ICombat attacker = data.Attacker;
        if (attacker is Object unityObject && unityObject == null)
            attacker = null;
        status.TakeDamage(new WBH_DamageResult(attacker, damage, false,
            ItemSystem.ElementType.Fire, null, null, transform.position, null, DamageCause.DoT, data.AttackId));
    }

    // 넉백
    public override void ApplyKnockback(Vector3 direction, float force, float duration)
    {
        if (knockbackRoutine != null)
            StopCoroutine(knockbackRoutine);

        if (airborneRoutine != null)
        {
            // 에어본 도중 넉백이 끼어들면, 넉백은 수평 이동만 하고 Y를 안 건드리기 때문에
            // 뜬 높이를 그대로 시작점으로 삼아 넉백이 끝나도 계속 공중에 남는 문제가 있었다.
            // 넉백을 시작하기 전에 먼저 지면으로 되돌린다.
            StopCoroutine(airborneRoutine);
            airborneRoutine = null;

            Vector3 grounded = transform.position;
            grounded.y = airborneGroundY;
            transform.position = grounded;
        }

        knockbackRoutine = StartCoroutine(KnockbackRoutine(direction, force, duration));
    }
    private IEnumerator KnockbackRoutine(Vector3 direction, float force, float duration)
    {
        Vector3 start = transform.position;
        Vector3 end = start + direction.normalized * force;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            transform.position = Vector3.Lerp(start, end, elapsed / duration);

            yield return null;
        }

        knockbackRoutine = null;
    }

    // 에어본
    public override void ApplyAirborne(float height, float duration)
    {
        if (airborneRoutine != null)
            StopCoroutine(airborneRoutine);

        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
        }

        airborneGroundY = transform.position.y;
        airborneRoutine = StartCoroutine(AirborneRoutine(height, duration));
    }
    private IEnumerator AirborneRoutine(float height, float duration)
    {
        Vector3 start = transform.position;

        float time = 0f;

        while(time < duration)
        {
            time += Time.deltaTime;

            float t = time / duration;

            float y = Mathf.Sin(t * Mathf.PI) * height;

            transform.position = start + Vector3.up * y;

            yield return null;
        }

        transform.position = start;

        airborneRoutine = null;
    }

    // 상태이상 이펙트 재생
    /// <summary>SW 수정: 기존 적 등급으로 동일한 시각 효과 재생 경로를 사용합니다.</summary>
    public override void PlayStatusEffect(WBH_StatusEffectType type)
    {
        PlayStatusEffect(type, controller.Info.enemyGrade);
    }

    /// <summary>SW 수정: 피해나 스탯을 변경하지 않고 지정 등급의 상태이상 연출만 재생합니다.</summary>
    public void PlayStatusEffect(WBH_StatusEffectType type, EnemyGrade grade)
    {
        if (activeEffects.ContainsKey(type))
            return;

        WBH_EffectData data = GetEffectData(type);

        // SW 수정 : 효과 데이터나 생성기가 없으면 상태 판정은 유지하고 시각 연출 생성만 생략한다.
        if (data == null || effectSpawner == null)
            return;

        WBH_Effect effect = effectSpawner.SpawnPersistentEffect(data, statusEffectRoot);

        if (effect == null)
            return;

        float scale = GetEffectScale(grade);

        effect.transform.localScale *= scale;
        activeEffects.Add(type, effect);
    }

    public override void StopStatusEffect(WBH_StatusEffectType type)
    {
        if (!activeEffects.TryGetValue(type, out var effect))
            return;

        effect.StopEffect();

        activeEffects.Remove(type);
    }


    // 상태이상 종류 입력 시, 그에 맞는 이펙트 데이터 반환
    private WBH_EffectData GetEffectData(WBH_StatusEffectType type)
    {
        return type switch
        {
            WBH_StatusEffectType.Burn => burnEffect,
            WBH_StatusEffectType.Freeze => freezeEffect,
            WBH_StatusEffectType.Electric => electricEffect,
            WBH_StatusEffectType.Slow => slowEffect,
            WBH_StatusEffectType.KnockBack => knockbackEffect,
            WBH_StatusEffectType.Airborne => airborneEffect,
            WBH_StatusEffectType.Stun => stunEffect,
            WBH_StatusEffectType.Marked => markedEffect,
            _ => null
        };
    }

    // 몬스터 등급별 이펙트 크기 조정
    /// <summary>SW 수정: 원본과 원격 표시가 같은 등급별 이펙트 크기를 사용합니다.</summary>
    private static float GetEffectScale(EnemyGrade grade)
    {
        return grade switch
        {
            EnemyGrade.Normal => 1f,
            EnemyGrade.Advanced => 1.2f,
            EnemyGrade.Elite => 1.5f,
            EnemyGrade.Boss => 2.5f,
            EnemyGrade.Hidden => 0.9f,
            _ => 1f
        };
    }



    // 사운드. 아직 미구현
    public override void PlayStatusSound(WBH_StatusEffectType type)
    {
        // !@
    }

    public override float GetMaxHealth()
    {
        return status.MaxHealth;
    }
}
