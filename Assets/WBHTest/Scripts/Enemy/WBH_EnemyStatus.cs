using ItemSystem;
using System;
using UnityEngine;

// 해당 클래스에서는 상태이상으로 인한 능력치 변동만 적용하기에 모든 능력치를 base - current 로 이원화하지 않음.
[RequireComponent(typeof(WBH_EnemyCombat))]
[RequireComponent(typeof(WBH_EnemyController))]
[RequireComponent(typeof(WBH_EnemyMovement))]
public class WBH_EnemyStatus : MonoBehaviour, WBH_ICombatStatus, IStatBuffTarget
{
    private WBH_EnemyController controller;
    private WBH_EnemyMovement movement;
    private WBH_EnemyCombat combat;

    private float maxHp;
    private float currentHp;
    private float attackPower;
    private float defensePower;
    private float criticalChance;
    private float criticalMultiplier;
    private float pen;
    private float fireBonus;
    private float iceBonus;
    private float electricBonus;
    private float attackRange;
    private float moveSpeed;
    private float attackSpeed;
    private float projectileSpeed;

    private float currentMaxHp;
    private float currentAttackPower;
    private float currentMoveSpeed;
    private float currentAttackSpeed;
    private float currentDefensePower;
    private float currentCriticalChance;
    private float currentCriticalMultiplier;
    private float currentPen;
    private float currentFireBonus;
    private float currentIceBonus;
    private float currentElectricBonus;

    // 상태이상(WBH_EnemyStatusEffectController)이 거는 배율. 컨트롤러가 여러 상태이상을 합산한 최종
    // 배율을 넘겨주므로 여기서는 마지막 값만 기억하면 된다. 공격/방어/이동/공격속도만 상태이상 대상.
    private float statusAttackModifier = 1f;
    private float statusDefenseModifier = 1f;
    private float statusMoveSpeedModifier = 1f;
    private float statusAttackSpeedModifier = 1f;

    // 버프/디버프(아이템 고유효과, 스킬 등 - EnemyBuffManager)가 거는 가산치. 상태이상과 별개 레이어라
    // 최종 수치는 base를 버프로 가산한 뒤 상태이상 배율을 곱하는 순서로 합성한다(RecalculateAll 참고).
    private StatSet buffStatSet;

    //-- 이벤트
    public event Action<float, float> OnHpChanged;
    public event Action <WBH_DamageResult> OnDamaged; // 구독중 : WBH_EnemyView
    public event Action OnDead;

    //-- 인터페이스 구현
    public float MaxHealth => currentMaxHp;
    public float CurrentHp => currentHp;
    public float AttackPower => currentAttackPower;
    public float DefensePower => currentDefensePower;
    public float CritRate => currentCriticalChance;
    public float CritMult => currentCriticalMultiplier;
    public float Pen => currentPen;
    public float FireBonus => currentFireBonus;
    public float IceBonus => currentIceBonus;
    public float ElectricBonus => currentElectricBonus;

    // -- 외부 사용을 위한 프로퍼티
    public float MoveSpeed => currentMoveSpeed;
    public float AttackSpeed => currentAttackSpeed;
    public float ProjectileSpeed => projectileSpeed;
    public float AttackRange => attackRange;
    public bool IsDead => currentHp <= 0;

    private void Awake()
    {
        controller = GetComponent<WBH_EnemyController>();
        movement = GetComponent<WBH_EnemyMovement>();
        combat = GetComponent<WBH_EnemyCombat>();
    }

    public void Initialize(WBH_EnemyInfo info)
    {
        maxHp = info.maxHP;
        attackPower = info.attack;
        defensePower = info.defense;
        moveSpeed = info.moveSpeed;
        attackSpeed = info.attackSpeed;
        projectileSpeed = info.projectileSpeed;
        attackRange = info.attackRange;

        buffStatSet = StatSet.Zero;
        statusAttackModifier = statusDefenseModifier = statusMoveSpeedModifier = statusAttackSpeedModifier = 1f;

        RecalculateAll();
        currentHp = currentMaxHp;
    }

    public void TakeDamage(WBH_DamageResult result)
    {
        if (IsDead)
            return;

        currentHp -= result.FinalDamage;
        currentHp = Mathf.Max(currentHp, 0);

        OnHpChanged?.Invoke(currentHp, MaxHealth);
        OnDamaged?.Invoke(result);
        Debug.Log($"남은 체력 {currentHp}");
        if (currentHp == 0)
        {
            OnDead?.Invoke();
        }
    }

    // 상태이상으로 인한 데미지를 받을 때를 위한 오버로드
    public void TakeDamage(float damage)
    {
        if (IsDead)
            return;

        currentHp -= damage;

        WBH_DamageResult result = new WBH_DamageResult(null, damage, false, ItemSystem.ElementType.Fire);

        OnHpChanged?.Invoke(currentHp, MaxHealth);
    }

    void Update()
    {
        //Debug.Log(currentHp);
    }

    public void Heal (float amount)
    {
        currentHp += amount;
        currentHp = Mathf.Min(currentHp, currentMaxHp);
        OnHpChanged?.Invoke(currentHp, MaxHealth);
    }

    // base 스탯에 버프(flat/percent 가산)를 합성한다. 상태이상 배율은 대상 스탯에서 이 결과에 곱한다.
    private static float Combine(float baseValue, float flat, float percent) => baseValue * (1f + percent / 100f) + flat;

    // 버프/상태이상 변경 시마다 전체 스탯을 다시 계산한다 - 두 레이어가 서로 다른 시점에 걸려도
    // 항상 같은 기준(base -> 버프 가산 -> 상태이상 배율)으로 재합성되어 어긋나지 않는다.
    private void RecalculateAll()
    {
        currentMaxHp = Mathf.Max(1f, Combine(maxHp, buffStatSet.maxHealthFlat, buffStatSet.maxHealthPercent));
        currentHp = Mathf.Min(currentHp, currentMaxHp);

        currentAttackPower = Mathf.Max(0f, Combine(attackPower, buffStatSet.attackPowerFlat, buffStatSet.attackPowerPercent) * statusAttackModifier);
        currentDefensePower = Mathf.Max(0f, Combine(defensePower, buffStatSet.defensePowerFlat, buffStatSet.defensePowerPercent) * statusDefenseModifier);
        currentMoveSpeed = Mathf.Max(0f, Combine(moveSpeed, buffStatSet.moveSpeedFlat, buffStatSet.moveSpeedPercent) * statusMoveSpeedModifier);
        currentAttackSpeed = Mathf.Max(0f, Combine(attackSpeed, buffStatSet.attackSpeedFlat, buffStatSet.attackSpeedPercent) * statusAttackSpeedModifier);

        currentCriticalChance = Mathf.Max(0f, criticalChance + buffStatSet.critRateFlat);
        currentCriticalMultiplier = Mathf.Max(0f, Combine(criticalMultiplier, buffStatSet.critMultFlat, buffStatSet.critMultPercent));
        currentPen = Mathf.Max(0f, Combine(pen, buffStatSet.penFlat, buffStatSet.penPercent));
        currentFireBonus = Combine(fireBonus, buffStatSet.fireBonusFlat, buffStatSet.fireBonusPercent);
        currentIceBonus = Combine(iceBonus, buffStatSet.iceBonusFlat, buffStatSet.iceBonusPercent);
        currentElectricBonus = Combine(electricBonus, buffStatSet.electricBonusFlat, buffStatSet.electricBonusPercent);

        movement?.SetMoveSpeed(currentMoveSpeed);
    }

    /// <summary>아이템 고유효과/스킬 등 버프-디버프 시스템(BuffTracker 기반)이 계산한 합산 스탯을 반영한다.
    /// EnemyBuffManager가 버프 목록이 바뀔 때마다 호출한다.</summary>
    public void ApplyBuffStatSet(StatSet statSet)
    {
        buffStatSet = statSet;
        RecalculateAll();
    }

    // -- 이동속도, 공격속도, 공격 배율 조정 (상태이상 등)
    public void MultiplyMoveSpeed(float modifier)
    {
        statusMoveSpeedModifier = modifier;
        RecalculateAll();
    }
    public void MultiplyAttackSpeed(float modifier)
    {
        statusAttackSpeedModifier = modifier;
        RecalculateAll();
    }
    public void MultiplyAttack(float modifier)
    {
        statusAttackModifier = modifier;
        RecalculateAll();
    }
    public void MultiplyDefense(float modifier)
    {
        statusDefenseModifier = modifier;
        RecalculateAll();
    }
}
