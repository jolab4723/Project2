using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(EnemyDestructionLink))]
public sealed class WBHEnemyDestructionAdapter : MonoBehaviour
{
    private static readonly int HitStrengthId =
        Shader.PropertyToID("_HitStrength");

    [SerializeField, InspectorName("피격 지점 계산용 콜라이더")]
    private Collider targetCollider;

    private WBH_EnemyStatus status;
    private EnemyDestructionLink destructionLink;
    private Renderer[] renderers;
    private MaterialPropertyBlock rendererProperties;

    private int recordedFrame = -1;
    private Vector3 recordedImpactPoint;
    private Vector3 recordedAttackDirection;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        rendererProperties = new MaterialPropertyBlock();
    }

    /// <summary>
    /// 정확한 피격 정보가 있는 공격은 ProcessDamage 직전에 호출한다.
    /// 같은 프레임의 다음 OnDamaged 한 번에서만 소비된다.
    /// </summary>
    public void RecordHit(
        Vector3 impactPoint,
        Vector3 attackDirection)
    {
        recordedImpactPoint = impactPoint;
        recordedAttackDirection = attackDirection;
        recordedFrame = Time.frameCount;
    }

    private void OnEnable()
    {
        ResetHitFlash();

        status = GetComponent<WBH_EnemyStatus>();
        destructionLink = GetComponent<EnemyDestructionLink>();
        if (targetCollider == null)
        {
            targetCollider = GetComponent<Collider>();
        }

        recordedFrame = -1;
        status.OnDamaged += HandleDamaged;
    }

    private void ResetHitFlash()
    {
        if (renderers == null || rendererProperties == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(rendererProperties);
            rendererProperties.SetFloat(HitStrengthId, 0f);
            targetRenderer.SetPropertyBlock(rendererProperties);
            rendererProperties.Clear();
        }
    }

    private void OnDisable()
    {
        if (status != null)
        {
            status.OnDamaged -= HandleDamaged;
        }

        recordedFrame = -1;
    }

    private void HandleDamaged(WBH_DamageResult result)
    {
        bool useRecordedHit = recordedFrame == Time.frameCount;
        Vector3 impactPoint = recordedImpactPoint;
        Vector3 attackDirection = recordedAttackDirection;

        // 치명 피해 여부를 보기 전에 소비해 non-lethal 정보가 남지 않게 한다.
        recordedFrame = -1;

        if (status == null ||
            destructionLink == null ||
            status.CurrentHp > 0f)
        {
            return;
        }

        Component attacker = result.Attacker as Component;
        Vector3 attackerPosition = attacker != null
            ? attacker.transform.position
            : transform.position - transform.forward;

        if (!useRecordedHit)
        {
            attackDirection = transform.position - attackerPosition;
            impactPoint = targetCollider != null
                ? targetCollider.ClosestPoint(attackerPosition)
                : transform.position;
        }

        if (attackDirection.sqrMagnitude <= 0.0001f)
        {
            attackDirection = transform.forward;
        }

        destructionLink.TryPlayDeath(
            impactPoint,
            attackDirection.normalized,
            result.FinalDamage,
            status.MaxHealth);
    }

    private void OnValidate()
    {
        if (targetCollider == null)
        {
            targetCollider = GetComponent<Collider>();
        }
    }
}
