using UnityEngine;

[DisallowMultipleComponent]
public sealed class WBHCombatDroneDestructionTarget : MonoBehaviour,
    WBH_ICombat,
    WBH_ICombatStatus
{
    [SerializeField, Min(0.01f)] private float maxHealth = 1f;
    [SerializeField, Min(0f)] private float directionalForce = 4.5f;

    private CombatDroneArtificerDestruction destruction;
    private float currentHealth;

    public WBH_ICombatStatus Status => this;
    public float CurrentHp => currentHealth;
    public float MaxHealth => maxHealth;
    public float AttackPower => 0f;
    public float DefensePower => 0f;
    public float CritRate => 0f;
    public float CritMult => 1f;
    public float FireBonus => 0f;
    public float IceBonus => 0f;
    public float ElectricBonus => 0f;

    public void Configure(float health, float force)
    {
        maxHealth = Mathf.Max(0.01f, health);
        currentHealth = maxHealth;
        directionalForce = Mathf.Max(0f, force);
        destruction = GetComponent<CombatDroneArtificerDestruction>();
    }

    public void TakeDamage(WBH_DamageResult result)
    {
        if (currentHealth <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - result.FinalDamage);
        if (currentHealth > 0f)
        {
            return;
        }

        if (destruction == null)
        {
            destruction = GetComponent<CombatDroneArtificerDestruction>();
        }

        if (destruction == null)
        {
            return;
        }

        Component attacker = result.Attacker as Component;
        Vector3 attackerPosition = attacker != null
            ? attacker.transform.position
            : transform.position - transform.forward;

        Vector3 attackDirection = transform.position - attackerPosition;
        attackDirection.y = 0f;
        if (attackDirection.sqrMagnitude <= 0.0001f)
        {
            attackDirection = transform.forward;
        }
        attackDirection.Normalize();

        Collider hitCollider = GetComponent<Collider>();
        Vector3 impactPoint = hitCollider != null
            ? hitCollider.ClosestPoint(attackerPosition)
            : transform.position;

        destruction.TriggerDestruction(
            impactPoint,
            attackDirection,
            directionalForce);
    }

    private void Awake()
    {
        destruction = GetComponent<CombatDroneArtificerDestruction>();
        currentHealth = maxHealth;
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(0.01f, maxHealth);
        directionalForce = Mathf.Max(0f, directionalForce);
    }
}
