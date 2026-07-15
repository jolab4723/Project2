using System;
using UnityEngine;

public class WBH_PlayerStatus : MonoBehaviour, WBH_ICombatStatus
{
    [Header("Test Stat")]
    [SerializeField] private float maxHp = 100;
    [SerializeField] private float attackPower = 5;
    [SerializeField] private float defensePower = 3;
    [SerializeField] private float attackSpeed = 1;
    [SerializeField] private float moveSpeed = 1;
    [SerializeField] private float criticalChance = 1;
    [SerializeField] private float criticalMultiplier = 1;
    [SerializeField] private float fireBonus = 1;
    [SerializeField] private float iceBonus = 1;
    [SerializeField] private float electricBonus = 1;

    //--- 플레이어 속성에 추가 필요 .
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

    public event Action<float, float> OnHpChanged;
    public event Action OnDead;

    // -- combatManager 계산을 위한 인터페이스
    public float MaxHealth => maxHp;
    public float CurrentHp => currentHp;
    public float AttackPower => attackPower;
    public float DefensePower => defensePower;
    public float CritRate => criticalChance;
    public float CritMult => criticalMultiplier;
    public float FireBonus => fireBonus;
    public float IceBonus => iceBonus;
    public float ElectricBonus => electricBonus;
    
    //-- 외부 사용을 위한 프로퍼티
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

    public void TakeDamage(WBH_DamageResult result)
    {
        currentHp -= result.FinalDamage;
        currentHp = Mathf.Max(currentHp, 0);

        OnHpChanged?.Invoke(currentHp, MaxHealth);
        Debug.Log(currentHp);
        if(currentHp == 0)
        {
            OnDead?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        currentHp += amount;
        currentHp = Mathf.Min(currentHp, maxHp);
        OnHpChanged?.Invoke(currentHp, MaxHealth);
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
