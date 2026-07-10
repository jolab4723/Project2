using UnityEngine;


[RequireComponent(typeof(WBH_EnemyCombat))]
[RequireComponent(typeof(WBH_EnemyController))]
[RequireComponent(typeof(WBH_EnemyMovement))]
public class WBH_EnemyStatus : MonoBehaviour
{
    private WBH_EnemyController controller;
    private WBH_EnemyMovement movement;
    private WBH_EnemyCombat combat;

    public float MaxHp => maxHp;
    public float CurrentHp => currentHp;
    public float Attack => attack;
    public float Defense => defense;
    public float MoveSpeed => moveSpeed;
    public float AttackSpeed => attackSpeed;
    public float ProjectileSpeed => projectileSpeed;
    public bool IsDead => currentHp <= 0;

    private float maxHp;
    private float currentHp;
    private float attack;
    private float defense;
    private float moveSpeed;
    private float attackSpeed;
    private float projectileSpeed;
    private float minDamage = 1f;


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
        attack = info.attack;
        defense = info.defense;
        moveSpeed = info.moveSpeed;
        attackSpeed = info.attackSpeed;
        projectileSpeed = info.projectileSpeed;
    }

    public void ApplyDamage(float damage)
    {
        damage = Mathf.Max(minDamage, damage - defense);

        currentHp -= damage;

        currentHp = Mathf.Max(currentHp, 0);
    }

    public void Heal (float amount)
    {
        currentHp += amount;
        currentHp = Mathf.Min(currentHp, maxHp);
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
        attack *= multiplier;
    }
}
