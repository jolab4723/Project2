using System.Collections;
using UnityEngine;

/// <summary>
/// 폭탄 투척(GunnerSkillController) 전용 투사체 - 목표 위치로 포물선을 그리며 날아가고, 비행 중이든
/// 착지 후든 적에게 닿으면 즉시 폭발한다. 적과 안 닿아도 착지(투척 완료) 후 fuseSeconds가 지나면
/// 자동 폭발한다.
///
/// BH님 소유 WBH_Projectile.InitializeGrenade와 포물선 이동 자체는 비슷하지만, 착지 즉시 터지는
/// WBH_Projectile과 달리 착지 후 퓨즈 시간만큼 더 살아있어야 해서(사용자 스펙 - "투척 완료로부터 2초
/// 후에 폭발") WJ 전용으로 새로 만들었다. 풀링은 GunnerArcProjectile(110번)과 같은 이유로 안 한다
/// (WBH_ProjectilePoolManager가 프로젝트 전체에서 미연결).
///
/// 진화1(집속 폭탄)/진화2(에너지 폭발)/진화3(글리터 폭탄) 전용 파라미터는 SkillDefinitionSO를 직접
/// 참조하지 않고(다른 투사체와 같은 스타일로 결합도를 낮춤) Initialize의 선택 파라미터로 받는다 -
/// 0이면 해당 효과 없음(118번). 진화3의 Marked 상태이상은 BH님 소유 WBH_StatusEffectType/
/// WBH_CombatManager에 새로 추가한 것(사용자 승인 후 118번에서 같이 작업).
/// </summary>
public class GunnerBomb : MonoBehaviour
{
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float arcHeight;
    private float travelTime;
    private float currentTime;
    private float fuseSeconds;
    private float explosionRadius;
    private LayerMask targetLayer;
    private WBH_DamageRequest damageRequest;

    // 진화1: 집속 폭탄 - 첫 폭발 후 delay초 뒤 radius 범위로 (첫 데미지 x damageMultiplier)만큼 2차 폭발.
    // delay<=0이면 2차 폭발 없음(기본 폭탄/다른 진화).
    private float secondExplosionDelay;
    private float secondExplosionRadius;
    private float secondExplosionDamageMultiplier;

    // 진화2: 에너지 폭발 - 첫 폭발에 맞은 적에게 stunDuration초 기절, 폭발 지점에 slowZoneDuration초 동안
    // 유지되는 슬로우 영역(반경 slowZoneRadius, 이동속도 배율 slowSpeedMultiplier) 생성.
    // stunDuration<=0이면 기절 없음, slowZoneDuration<=0이거나 slowZoneRadius<=0이면 슬로우 영역 없음.
    private float stunDurationOnHit;
    private float slowZoneRadius;
    private float slowZoneDuration;
    private float slowSpeedMultiplier;

    // 진화3: 글리터 폭탄 - 첫 폭발에 맞은 적에게 markDuration초 동안 Marked(받는 모든 데미지
    // markDamageMultiplier배) 부여. markDuration<=0이면 마커 없음.
    private float markDuration;
    private float markDamageMultiplier;

    private bool landed;
    private bool initialized;

    public void Initialize(Vector3 targetPosition, float throwSpeed, float arcHeight, float fuseSeconds,
        float explosionRadius, LayerMask targetLayer, WBH_DamageRequest damageRequest,
        float secondExplosionDelay = 0f, float secondExplosionRadius = 0f, float secondExplosionDamageMultiplier = 0f,
        float stunDurationOnHit = 0f, float slowZoneRadius = 0f, float slowZoneDuration = 0f, float slowSpeedMultiplier = 1f,
        float markDuration = 0f, float markDamageMultiplier = 1f)
    {
        startPosition = transform.position;
        this.targetPosition = targetPosition;
        this.arcHeight = arcHeight;
        this.fuseSeconds = fuseSeconds;
        this.explosionRadius = explosionRadius;
        this.targetLayer = targetLayer;
        this.damageRequest = damageRequest;

        this.secondExplosionDelay = secondExplosionDelay;
        this.secondExplosionRadius = secondExplosionRadius;
        this.secondExplosionDamageMultiplier = secondExplosionDamageMultiplier;

        this.stunDurationOnHit = stunDurationOnHit;
        this.slowZoneRadius = slowZoneRadius;
        this.slowZoneDuration = slowZoneDuration;
        this.slowSpeedMultiplier = slowSpeedMultiplier;

        this.markDuration = markDuration;
        this.markDamageMultiplier = markDamageMultiplier;

        float distance = Vector3.Distance(startPosition, targetPosition);
        travelTime = Mathf.Max(0.2f, distance / throwSpeed);
        currentTime = 0f;
        landed = false;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (!landed)
            MoveArc();
        else
            UpdateFuse();
    }

    private void MoveArc()
    {
        currentTime += Time.deltaTime;
        float t = Mathf.Clamp01(currentTime / travelTime);

        Vector3 position = Vector3.Lerp(startPosition, targetPosition, t);
        position.y += arcHeight * 4f * t * (1f - t);
        transform.position = position;

        if (t >= 1f)
        {
            landed = true;
            currentTime = 0f; // 착지 후 퓨즈 타이머로 재사용
        }
    }

    private void UpdateFuse()
    {
        currentTime += Time.deltaTime;
        if (currentTime >= fuseSeconds)
            Explode();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized)
            return;

        if (((1 << other.gameObject.layer) & targetLayer.value) == 0)
            return; // 대상 레이어가 아니면 무시

        Explode();
    }

    private void Explode()
    {
        foreach (Collider hit in Physics.OverlapSphere(transform.position, explosionRadius, targetLayer))
        {
            DealDamage(hit, damageRequest.DamageMultiplier);

            if (!hit.TryGetComponent<WBH_ICombat>(out var effectTarget))
                continue;

            if (stunDurationOnHit > 0f)
                effectTarget.AddStatusEffect(new WBH_StatusEffectData(WBH_StatusEffectType.Stun, duration: stunDurationOnHit));

            if (markDuration > 0f)
                effectTarget.AddStatusEffect(new WBH_StatusEffectData(WBH_StatusEffectType.Marked, duration: markDuration, value: markDamageMultiplier));
        }

        if (slowZoneDuration > 0f && slowZoneRadius > 0f)
        {
            var zoneGO = new GameObject("[GunnerBomb] SlowZone");
            zoneGO.transform.position = transform.position;
            zoneGO.AddComponent<GunnerSlowZone>().Initialize(slowZoneRadius, slowZoneDuration, slowSpeedMultiplier, targetLayer);
        }

        initialized = false;

        if (secondExplosionDelay > 0f)
            StartCoroutine(DelayedSecondExplosion());
        else
            Destroy(gameObject);
    }

    /// <summary>진화1(집속 폭탄) - 첫 폭발 데미지의 secondExplosionDamageMultiplier배만큼 더 넓은 범위로 한 번 더 터진다.</summary>
    private IEnumerator DelayedSecondExplosion()
    {
        yield return new WaitForSeconds(secondExplosionDelay);

        foreach (Collider hit in Physics.OverlapSphere(transform.position, secondExplosionRadius, targetLayer))
            DealDamage(hit, damageRequest.DamageMultiplier * secondExplosionDamageMultiplier);

        Destroy(gameObject);
    }

    private void DealDamage(Collider target, float damageMultiplier)
    {
        if (!target.TryGetComponent<WBH_ICombat>(out var combatTarget))
            return;

        WBH_DamageRequest hitRequest = new WBH_DamageRequest(damageRequest.Attacker, combatTarget,
            damageRequest.AttackType, damageRequest.ElementType, damageMultiplier, damageRequest.StatusEffect);
        WBH_CombatManager.ProcessDamage(hitRequest);
    }
}
