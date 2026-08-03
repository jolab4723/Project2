using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDestructionLink : MonoBehaviour
{
    [SerializeField, InspectorName("파괴 연출 프리팹")]
    private GameObject destructionVisualPrefab;

    [SerializeField, Min(0f), InspectorName("공격 방향 기본 힘")]
    private float baseDirectionalForce = 3f;

    private bool deathRequestedThisLife;

    internal GameObject DestructionVisualPrefab => destructionVisualPrefab;

    /// <summary>
    /// 실제 적의 사망 판정 직후 호출한다. 실제 적을 끄거나 풀에 반환하지
    /// 않으며, 한 번 활성화된 생명에서는 첫 요청만 소비한다.
    /// </summary>
    public bool TryPlayDeath(
        Vector3 impactPoint,
        Vector3 attackDirection,
        float killingDamage,
        float maxHealth)
    {
        if (deathRequestedThisLife)
        {
            return false;
        }

        // 성공 여부가 아니라 이 생명의 사망 요청 자체를 한 번만 소비한다.
        deathRequestedThisLife = true;

        var request = new EnemyDestructionRequest(
            transform.position,
            transform.rotation,
            transform.lossyScale,
            impactPoint,
            attackDirection,
            killingDamage,
            maxHealth);

        if (destructionVisualPrefab == null)
        {
            Debug.LogWarning(
                $"[{nameof(EnemyDestructionLink)}] {name}: " +
                "파괴 연출 프리팹이 연결되지 않았습니다.",
                this);
            return false;
        }

        if (!EnemyDestructionService.TryGet(
                gameObject.scene,
                out EnemyDestructionService service))
        {
            Debug.LogWarning(
                $"[{nameof(EnemyDestructionLink)}] {name}: " +
                "같은 씬에 활성 EnemyDestructionService가 정확히 하나 있어야 합니다.",
                this);
            return false;
        }

        return service.TryPlay(
            destructionVisualPrefab,
            baseDirectionalForce,
            request);
    }

    private void OnEnable()
    {
        deathRequestedThisLife = false;
    }

    private void OnValidate()
    {
        baseDirectionalForce = Mathf.Max(0f, baseDirectionalForce);
    }
}
