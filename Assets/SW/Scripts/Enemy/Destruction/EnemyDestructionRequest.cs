using UnityEngine;

internal readonly struct EnemyDestructionRequest
{
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly Vector3 WorldScale;
    public readonly Vector3 ImpactPoint;
    public readonly Vector3 AttackDirection;
    public readonly float KillingDamage;
    public readonly float MaxHealth;

    public EnemyDestructionRequest(
        Vector3 position,
        Quaternion rotation,
        Vector3 worldScale,
        Vector3 impactPoint,
        Vector3 attackDirection,
        float killingDamage,
        float maxHealth)
    {
        Position = position;
        Rotation = rotation;
        WorldScale = worldScale;
        ImpactPoint = impactPoint;
        AttackDirection = attackDirection;
        KillingDamage = killingDamage;
        MaxHealth = maxHealth;
    }
}
