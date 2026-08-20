using UnityEngine;

/// <summary>
/// 아크 버스터(GunnerSkillController) 전용 투사체 - 직선으로 날아가다 적에게 닿으면(관통 없이) 그
/// 자리에서 범위 폭발한다. WBH_Projectile(BH님 소유)의 두 경로 중 어느 쪽도 정확히 안 맞아서
/// (Initialize는 직선 이동이지만 단일 타격만, InitializeGrenade는 접촉 폭발이지만 포물선 유탄 전용)
/// WJ 전용으로 가볍게 새로 만들었다.
///
/// 풀링은 안 한다 - 프로젝트 전체에서 WBH_ProjectilePoolManager가 아직 아무 데도 연결 안 돼 있어서
/// (109번에서 확인, 적 프리팹조차 미연결) 당장은 Instantiate/Destroy로 충분하다.
/// </summary>
public class GunnerArcProjectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float maxDistance;
    private float explosionRadius;
    private bool explodeOnHit;
    private LayerMask targetLayer;
    private WBH_DamageRequest damageRequest;

    private Vector3 startPosition;
    private bool initialized;

    /// <summary>explodeOnHit=false면 폭발 반경 판정 없이 실제로 맞은 대상 하나에게만 데미지를 준다
    /// (아크 버스터 진화2 "트리플 슈팅"이 폭발 속성을 빼기 위해 사용 - 118번).</summary>
    public void Initialize(Vector3 direction, float speed, float maxDistance, float explosionRadius,
        LayerMask targetLayer, WBH_DamageRequest damageRequest, bool explodeOnHit = true)
    {
        this.direction = direction.normalized;
        this.speed = speed;
        this.maxDistance = maxDistance;
        this.explosionRadius = explosionRadius;
        this.targetLayer = targetLayer;
        this.damageRequest = damageRequest;
        this.explodeOnHit = explodeOnHit;

        startPosition = transform.position;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        transform.position += direction * speed * Time.deltaTime;

        if (Vector3.Distance(startPosition, transform.position) >= maxDistance)
            Destroy(gameObject); // 관통 없이 최대 사거리에 도달하면 폭발 없이 소멸
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized)
            return;

        if (((1 << other.gameObject.layer) & targetLayer.value) == 0)
            return; // 대상 레이어가 아니면 무시(적이 아니면 관통)

        if (explodeOnHit)
            Explode();
        else
            HitSingleTarget(other);
    }

    private void Explode()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, targetLayer);

        foreach (Collider hit in hits)
            DealDamage(hit);

        initialized = false;
        Destroy(gameObject);
    }

    /// <summary>폭발 없이(explodeOnHit=false) 실제로 충돌한 대상 하나에게만 데미지를 준다.</summary>
    private void HitSingleTarget(Collider target)
    {
        DealDamage(target);

        initialized = false;
        Destroy(gameObject);
    }

    private void DealDamage(Collider target)
    {
        if (!target.TryGetComponent<WBH_ICombat>(out var combatTarget))
            return;

        WBH_DamageRequest hitRequest = new WBH_DamageRequest(damageRequest.Attacker, combatTarget,
            damageRequest.AttackType, damageRequest.ElementType, damageRequest.DamageMultiplier, damageRequest.StatusEffect);
        WBH_CombatManager.ProcessDamage(hitRequest);
    }
}
