using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDestructionTarget : MonoBehaviour,
    WBH_ICombat,
    WBH_ICombatStatus
{
    [SerializeField, Min(0.01f)] private float maxHealth = 1f;
    [SerializeField, Min(0f)] private float directionalForce = 3f;

    private CombatDroneArtificerDestruction destruction;
    private EnemyDestructionVisualPool destructionVisualPool;
    private GameObject destructionVisualPrefab;
    private DestructionDamageStrengthScaler damageStrengthScaler;
    private float currentHealth;

    public WBH_ICombatStatus Status => this;
    public EnemyGrade enemyGrade;

    public float CurrentHp => currentHealth;
    public float MaxHealth => maxHealth;
    public float AttackPower => 0f;
    public float DefensePower => 0f;
    public float CritRate => 0f;
    public float CritMult => 1f;
    public float FireBonus => 0f;
    public float IceBonus => 0f;
    public float ElectricBonus => 0f;

    public void Configure(
        float health,
        float force,
        EnemyDestructionVisualPool newDestructionVisualPool = null,
        GameObject newDestructionVisualPrefab = null,
        DestructionDamageStrengthScaler newDamageStrengthScaler = null)
    {
        maxHealth = Mathf.Max(0.01f, health);
        currentHealth = maxHealth;
        directionalForce = Mathf.Max(0f, force);
        destructionVisualPool = newDestructionVisualPool;
        destructionVisualPrefab = newDestructionVisualPrefab;
        damageStrengthScaler = newDamageStrengthScaler;
        destruction = GetComponent<CombatDroneArtificerDestruction>();
    }

    // WBH 전투 시스템 연동부: 기존 플레이어 공격 결과를 테스트 적의 체력과 파괴 요청으로 변환한다.
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

        Core.ItemManager.Instance.DropRandomItem(enemyGrade, transform.position);
        if (destructionVisualPrefab != null && destructionVisualPool != null)
        {
            destructionVisualPool.PlayWithDamage(
                destructionVisualPrefab,
                transform.position,
                transform.rotation,
                impactPoint,
                attackDirection,
                directionalForce,
                result.FinalDamage,
                maxHealth,
                damageStrengthScaler,
                () =>
                {
                    if (this != null && gameObject != null)
                        gameObject.SetActive(false);
                });
            return;
        }

        if (destruction == null)
        {
            destruction = GetComponent<CombatDroneArtificerDestruction>();
        }

        if (destruction == null)
        {
            Debug.LogError(
                "[Enemy Manual Test] 직접 파괴 컴포넌트 또는 파괴 연출 프리팹이 연결되지 않았습니다: " + name,
                this);
            return;
        }

        float damageMultiplier = damageStrengthScaler != null
            ? damageStrengthScaler.EvaluateMultiplier(
                result.FinalDamage,
                maxHealth)
            : 1f;
        destruction.TriggerDestruction(
            impactPoint,
            attackDirection,
            directionalForce,
            damageMultiplier);
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
