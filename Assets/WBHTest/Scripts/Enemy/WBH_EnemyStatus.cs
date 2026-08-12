using ItemSystem;
using System;
using UnityEngine;

// 해당 클래스에서는 상태이상으로 인한 능력치 변동만 적용하기에 모든 능력치를 base - current 로 이원화하지 않음.
[RequireComponent(typeof(WBH_EnemyCombat))]
[RequireComponent(typeof(WBH_EnemyController))]
[RequireComponent(typeof(WBH_EnemyMovement))]
public class WBH_EnemyStatus : MonoBehaviour, WBH_ICombatStatus
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

    private float currentAttackPower;
    private float currentMoveSpeed;
    private float currentAttackSpeed;

    //-- 이벤트
    public event Action<float, float> OnHpChanged;
    public event Action <WBH_DamageResult> OnDamaged; // 구독중 : WBH_EnemyView
    public event Action OnDead;

    //-- 인터페이스 구현
    public float MaxHealth => maxHp;
    public float CurrentHp => currentHp;
    public float AttackPower => currentAttackPower;
    public float DefensePower => defensePower;
    public float CritRate => criticalChance;
    public float CritMult => criticalMultiplier;
    public float Pen => pen;
    public float FireBonus => fireBonus;
    public float IceBonus => iceBonus;
    public float ElectricBonus => electricBonus;

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
        currentHp = maxHp;
        attackPower = info.attack;
        defensePower = info.defense;
        moveSpeed = info.moveSpeed;
        attackSpeed = info.attackSpeed;
        projectileSpeed = info.projectileSpeed;
        attackRange = info.attackRange;

        currentAttackPower = attackPower;
        currentAttackSpeed = attackSpeed;
        currentMoveSpeed = moveSpeed;
    }

    public void TakeDamage(WBH_DamageResult result)
    {
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
        currentHp = Mathf.Min(currentHp, maxHp);
        OnHpChanged?.Invoke(currentHp, MaxHealth);
    }


    // -- 이동속도, 공격속도, 공격 배율 조정 (버프 등)
    public void MultiplyMoveSpeed(float modifier)
    {
        currentMoveSpeed = moveSpeed * modifier;
        movement.SetMoveSpeed(this.moveSpeed);
    }
    public void MultiplyAttackSpeed(float modifier)
    {
        currentAttackSpeed = attackSpeed * modifier;
    }
    public void MultiplyAttack(float modifier)
    {
        currentAttackPower = attackPower * modifier;
    }
}
