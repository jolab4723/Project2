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
    [Header("폭발 범위 표시")]
    [SerializeField] private bool showExplosionRange = true;
    [SerializeField] private Color explosionRangeColor = new Color(1f, 0.45f, 0.1f, 0.35f);
    [SerializeField] private float explosionRangeDuration = 0.25f;


    private Vector3 direction;
    private float speed;
    private float maxDistance;
    private float explosionRadius;
    private bool explodeOnHit;
    private LayerMask targetLayer;
    private WBH_DamageRequest damageRequest;

    private Vector3 startPosition;
    private bool initialized;

    private WBH_PlayerEffect effectOwner;
    private WBH_PlayerEffectCue explosionEffectCue = WBH_PlayerEffectCue.None;
    private Vector3 explosionEffectScale = Vector3.one;

    /// <summary>explodeOnHit=false면 폭발 반경 판정 없이 실제로 맞은 대상 하나에게만 데미지를 준다
    /// (아크 버스터 진화2 "아크 불릿"이 폭발 속성을 빼기 위해 사용 - 118번).
    /// visualScale은 프리팹 원본 크기에 곱하는 배율(기본 1 = 그대로) - 아크 캐논(진화3)처럼 폭발 반경이
    /// 커진 진화가 실제 판정 크기에 맞게 더 커 보이도록 쓴다(133번 후속).</summary>
    public void Initialize(Vector3 direction, float speed, float maxDistance, float explosionRadius,
        LayerMask targetLayer, WBH_DamageRequest damageRequest, bool explodeOnHit = true, float visualScale = 1f)
    {
        this.direction = direction.normalized;
        this.speed = speed;
        this.maxDistance = maxDistance;
        this.explosionRadius = explosionRadius;
        this.targetLayer = targetLayer;
        this.damageRequest = damageRequest;
        this.explodeOnHit = explodeOnHit;

        if (!Mathf.Approximately(visualScale, 1f))
            transform.localScale *= visualScale;

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
        if(showExplosionRange)
        {
            SkillRangeVisual.ShowSector(transform.position, Vector3.forward, explosionRadius, 360, explosionRangeColor, explosionRangeDuration);
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, targetLayer);

        foreach (Collider hit in hits)
            DealDamage(hit);

        PlayExplosionEffect();

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

    // 이펙트 재생을 위한 준비 메서드
    public void ConfigureExplosionEffect(WBH_PlayerEffect effectOwner, WBH_PlayerEffectCue cue, Vector3 scaleMultiplier)
    {
        this.effectOwner = effectOwner;
        explosionEffectCue = cue;
        explosionEffectScale = scaleMultiplier;
    }

    private void PlayExplosionEffect()
    {
        if (effectOwner == null || explosionEffectCue == WBH_PlayerEffectCue.None)
            return;

        effectOwner.PlayWorldEffect(explosionEffectCue, transform.position, Quaternion.identity, explosionEffectScale);
    }
}
