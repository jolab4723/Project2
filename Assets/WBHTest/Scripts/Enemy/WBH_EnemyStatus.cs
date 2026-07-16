using ItemSystem;
using System;
using Unity.VisualScripting;
using UnityEngine;


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
    private float fireBonus;
    private float iceBonus;
    private float electricBonus;
    private float moveSpeed;
    private float attackSpeed;
    private float projectileSpeed;
    private float minDamage = 1f;

    //-- 이벤트
    public event Action<float, float> OnHpChanged;
    public event Action <WBH_DamageResult> OnDamaged; // 구독중 : WBH_EnemyView
    public event Action OnDead;

    //-- 인터페이스 구현
    public float MaxHealth => maxHp;
    public float CurrentHp => currentHp;
    public float AttackPower => attackPower;
    public float DefensePower => defensePower;
    public float CritRate => criticalChance;
    public float CritMult => criticalMultiplier;
    public float FireBonus => fireBonus;
    public float IceBonus => iceBonus;
    public float ElectricBonus => electricBonus;

    // -- 외부 사용을 위한 프로퍼티
    public float MoveSpeed => moveSpeed;
    public float AttackSpeed => attackSpeed;
    public float ProjectileSpeed => projectileSpeed;
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
    public void MultiplyMoveSpeed(float multiplier)
    {
        moveSpeed *= multiplier;
        movement.SetMoveSpeed(this.moveSpeed);
    }
    public void MultiplyAttackSpeed(float multiplier)
    {
        attackSpeed *= multiplier;
    }
    public void MultiplyAttack(float multiplier)
    {
        attackPower *= multiplier;
    }
}
