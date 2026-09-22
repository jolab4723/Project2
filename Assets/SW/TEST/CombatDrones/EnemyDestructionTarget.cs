using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDestructionTarget : MonoBehaviour,
    WBH_ICombat,
    WBH_ICombatStatus
{
    [SerializeField, Min(0.01f)] private float maxHealth = 1f;
    [SerializeField, Min(0f)] private float directionalForce = 3f;

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
    public float Pen => 0f;
    public float FireBonus => 0f;
    public float IceBonus => 0f;
    public float ElectricBonus => 0f;
    public float DamageTakenModifier => 1f; // WBH_ICombatStatus에 추가된 항목 - 인터페이스 구현을 위한 단순 추가(118번)
    public float NormalDamageModifier => 1f; // 파괴 연출용 더미라 공격하지 않는다 - 인터페이스 구현만 채움
    public float SkillDamageModifier => 1f;
    public bool IsDead => true; // 0729 WBH 추가. 인터페이스 구현을 위한 단순 추가

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
        if (destructionVisualPrefab == null || destructionVisualPool == null)
        {
            Debug.LogError(
                "[Enemy Manual Test] 파괴 연출 프리팹과 테스트 풀이 모두 필요합니다: " + name,
                this);
            return;
        }

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
                {
                    gameObject.SetActive(false);
                }
            });
    }

    // 0729 WBH 추가. 인터페이스 구현을 위한 단순 추가 
    public void AddStatusEffect(WBH_StatusEffectData data) {}

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(0.01f, maxHealth);
        directionalForce = Mathf.Max(0f, directionalForce);
    }
}
