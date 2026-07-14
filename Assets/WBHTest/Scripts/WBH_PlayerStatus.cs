using UnityEngine;

public class WBH_PlayerStatus : MonoBehaviour
{
    [Header("Test Stat")]
    [SerializeField] private float maxHp = 100;
    [SerializeField] private float attackPower = 5;
    [SerializeField] private float defensePower = 3;
    [SerializeField] private float attackSpeed = 1;
    [SerializeField] private float moveSpeed = 1;

    //--- 추가 필요.
    [SerializeField] private float dodgeDistance = 5f;
    [SerializeField] private float dodgeDuration = 0.5f;
    [SerializeField] private float dodgeCooltime = 6f;

    [SerializeField] private float fighterAttackRange = 2f;
    [SerializeField] private float gunnerAttackRange = 10f;
    [SerializeField] private float fighterAttackDamage = 15f;
    [SerializeField] private float gunnerAttackDamage = 10f;
    [SerializeField] private float gunnerBulletSpeed = 10f;
    //---

    private T_PlayerController playerController;
    private float currentHp;

    public float MaxHp => maxHp;
    public float CurrentHp => currentHp;
    public float AttackPower => attackPower;
    public float DefensePower => defensePower;
    public float AttackSpeed => attackSpeed;
    public float MoveSpeed => moveSpeed;
    public float DodgeDistance => dodgeDistance;
    public float DodgeDuration => dodgeDuration;
    public float DodgeCooltime => dodgeCooltime;
    public float FighterAttackRange => fighterAttackRange;
    public float GunnerAttackRange => gunnerAttackRange;
    public float FighterAttackDamage => fighterAttackDamage;
    public float GunnerAttackDamage => gunnerAttackDamage;
    public float GunnerBulletSpeed => gunnerBulletSpeed;

    public bool IsDead => currentHp <= 0;

    private float minDamage;


    public void Initialize(T_PlayerController playerController)
    {
        this.playerController = playerController;
        currentHp = maxHp;
    }

    public void ApplyDamage(float damage)
    {
        damage = Mathf.Max(minDamage, damage - defensePower);

        currentHp -= damage;

        Debug.Log($"현재 체력 {currentHp}");

        currentHp = Mathf.Max(currentHp, 0);

        if(IsDead)
        {
            playerController.Die();
        }
    }

    public void Heal(float amount)
    {
        currentHp += amount;
        currentHp = Mathf.Min(currentHp, maxHp);
    }

    public void MultiplyMoveSpeed(float multiplier)
    {
        moveSpeed *= multiplier;
        playerController.SetMoveSpeed(this.moveSpeed);
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
