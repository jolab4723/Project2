using UnityEngine;

/// <summary>
/// 거너 무기 외형 프리팹과 해당 무기의 총구(Muzzle)·투사체(Projectile)·명중(Impact) VFX를
/// 한 묶음으로 연결해 두는 "무기별 VFX 연결표"입니다.
///
/// 초보 팀원을 위한 전체 흐름:
/// 1. PlayerWeaponVisualPresenter가 장착한 무기 외형 프리팹을 캐릭터 손에 표시합니다.
/// 2. T_PlayerCombat은 현재 활성화된 무기 외형에서 이 컴포넌트를 찾습니다.
/// 3. 공격 순간에는 PlayMuzzle(...)로 총구 VFX와 산탄 공격 연출을 재생합니다.
/// 4. 라이플과 유탄발사기는 WBH_ProjectileSpawner에 ProjectileVisualPrefab과 ImpactVisualPrefab을 전달합니다.
/// 5. 산탄총은 중앙 투사체를 만들지 않으므로 ProjectileVisualPrefab을 비워 두고,
///    총구의 부채꼴 연출과 실제 피해 대상 위치의 ImpactVisualPrefab만 사용합니다.
/// 6. WBH_Projectile은 기존 오브젝트 풀과 이동·충돌·데미지 흐름을 그대로 사용하면서,
///    전달받은 시각 프리팹만 붙여 재생합니다.
///
/// 이 컴포넌트가 하지 않는 일:
/// - 이 컴포넌트는 공격 판정, 데미지, 이동, 충돌 또는 오브젝트 풀을 소유하지 않습니다.
/// - 무기마다 별도 전투 스크립트를 만들지 않고 "어떤 VFX를 사용할지"만 알려줍니다.
/// - Collider, Rigidbody, 데미지 컴포넌트를 시각 프리팹에 추가하지 마세요.
/// - 무기 외형의 Muzzle은 총열 위치를 확인하기 위한 기준점으로 보존합니다.
/// - 총구 VFX, 실제 투사체와 공격 판정의 시작 위치·방향은 모두 플레이어 루트의 공통 FirePoint가 결정합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GunnerWeaponVfxBinding : MonoBehaviour
{
    // 산탄의 퍼지는 각도와 입체감은 유지하고, 화면에서 너무 멀리 뻗는 이동 거리만 줄입니다.
    // 기존 파티클은 10m 공격 범위의 끝까지 입자 중심이 이동한 뒤 긴 스트레치까지 더해져 실제보다 넓게 보였으므로,
    // 중심 이동과 스트레치 길이를 72%로 줄여 시각 끝부분이 실제 공격 범위 안에서 읽히게 합니다.
    private const float AuthoredShotgunAttackRange = 10f;
    private const float ShotgunVisibleRangeRatio = 0.72f;

    [Tooltip("이 외형이 사용하는 전투 분기입니다. Rifle, Shotgun, Grenade 중 실제 무기 타입과 일치해야 합니다.")]
    [SerializeField] private GunnerWeaponType weaponType;

    [Tooltip("무기 외형의 총열 끝을 확인하기 위한 기준점입니다. 실제 재생 위치는 모든 무기가 공유하는 플레이어 FirePoint를 사용합니다.")]
    [SerializeField] private Transform muzzle;

    [Tooltip("발사 순간 플레이어 FirePoint에서 짧게 재생되는 VFX입니다. 공격 판정과 투사체 이동에는 관여하지 않습니다.")]
    [SerializeField] private GameObject muzzleVisualPrefab;

    [Tooltip("라이플·유탄발사기의 기존 WBH_Projectile 풀 오브젝트에 붙는 비행 VFX입니다. 산탄총은 중앙 탄환을 사용하지 않으므로 비워 둡니다.")]
    [SerializeField] private GameObject projectileVisualPrefab;

    [Tooltip("WBH_Projectile이 충돌하거나 폭발한 위치에서 한 번 재생하는 순수 시각 프리팹입니다.")]
    [SerializeField] private GameObject impactVisualPrefab;

    // 총구 VFX는 발사할 때마다 Instantiate하지 않습니다. 무기 외형 하나당 한 번만 만들고,
    // 이후 공격에서는 ParticleSystem과 TrailRenderer를 초기화해 같은 인스턴스를 재사용합니다.
    // 이 방식은 연사 무기의 불필요한 생성/삭제와 순간 GC 할당을 줄입니다.
    private GameObject muzzleVisualInstance;

    // 산탄총은 총구에 붙는 짧은 섬광과 정면으로 멀리 뻗는 산탄 연출을 같은 원본 프리팹에서 사용합니다.
    // 긴 연출은 손목 애니메이션을 따라 기울어지면 실제 공격 범위와 어긋나므로 별도 인스턴스로 재사용합니다.
    private GameObject shotgunAttackVisualInstance;
    private float appliedShotgunDistanceScale = 1f;

    // 외부 전투 코드에는 읽기 전용으로 공개합니다. 런타임에 필드를 바꾸지 않게 하여
    // 프리팹에 검증된 무기 타입과 VFX 세트가 공격 도중 뒤섞이는 것을 방지합니다.
    public GunnerWeaponType WeaponType => weaponType;
    public Transform Muzzle => muzzle;
    public GameObject ProjectileVisualPrefab => projectileVisualPrefab;
    public GameObject ImpactVisualPrefab => impactVisualPrefab;

    /// <summary>
    /// 현재 무기의 총구 VFX를 플레이어 공통 FirePoint 위치와 정면 방향에서 처음부터 재생합니다.
    /// 무기와 손의 애니메이션으로 총열이 기울어져도 총구 VFX, 실제 투사체와 공격 판정이 같은 위치·방향을 사용합니다.
    /// VFX가 비어 있으면 기존 공격 자체는 막지 않고 조용히 건너뜁니다.
    /// 따라서 VFX 연결 실수 때문에 데미지나 투사체 발사가 중단되지는 않습니다.
    /// </summary>
    public void PlayMuzzle(Vector3 attackOrigin, Vector3 attackForward, float attackRange)
    {
        if (muzzleVisualPrefab == null)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(attackForward, Vector3.up);
        forward = forward.sqrMagnitude > 0.0001f
            ? forward.normalized
            : Vector3.forward;
        Quaternion firePointRotation = Quaternion.LookRotation(forward, Vector3.up);

        if (muzzleVisualInstance == null)
        {
            // FirePoint에서 재생한 VFX가 캐릭터 이동이나 손 애니메이션을 뒤늦게 따라가지 않도록
            // 무기의 자식으로 두지 않고 독립된 런타임 인스턴스로 한 번만 생성해 재사용합니다.
            muzzleVisualInstance = Instantiate(muzzleVisualPrefab);
            muzzleVisualInstance.name = $"{muzzleVisualPrefab.name}_Runtime";

            if (weaponType == GunnerWeaponType.Shotgun)
                KeepOnlyShotgunParticleGroup(muzzleVisualInstance, keepAttackGroup: false);

            DisableProjectileLikeMuzzleParticles(muzzleVisualInstance);
        }

        muzzleVisualInstance.transform.SetPositionAndRotation(attackOrigin, firePointRotation);
        GunnerVfxPlayback.Restart(muzzleVisualInstance);

        if (weaponType != GunnerWeaponType.Shotgun)
            return;

        if (shotgunAttackVisualInstance == null)
        {
            // 산탄 공격 연출은 매번 새로 만들지 않고 장착 중 한 번만 만든 뒤 반복 재생합니다.
            // 총구용 복사본과 반대로, 여기에는 멀리 뻗는 파티클만 남깁니다.
            shotgunAttackVisualInstance = Instantiate(muzzleVisualPrefab);
            shotgunAttackVisualInstance.name = $"{muzzleVisualPrefab.name}_AttackRuntime";
            KeepOnlyShotgunParticleGroup(shotgunAttackVisualInstance, keepAttackGroup: true);
        }

        // 기본 10m용으로 제작된 산탄 연출을 현재 실제 공격 사거리에 맞춥니다.
        // 파티클 중심은 공격 범위보다 조금 안쪽에서 끝나고 긴 스트레치가 남은 구간을 채우므로,
        // 화면에서는 VFX 끝과 실제 공격 범위가 비슷하게 읽힙니다.
        float targetDistanceScale = Mathf.Max(
            0.01f,
            attackRange / AuthoredShotgunAttackRange * ShotgunVisibleRangeRatio);
        ApplyShotgunTravelDistanceScale(
            shotgunAttackVisualInstance,
            targetDistanceScale / appliedShotgunDistanceScale);
        appliedShotgunDistanceScale = targetDistanceScale;

        // 짧은 총구 섬광과 마찬가지로 실제 산탄 판정과 같은 공통 시작점·수평 정면을 사용합니다.
        shotgunAttackVisualInstance.transform.SetPositionAndRotation(
            attackOrigin,
            firePointRotation);
        GunnerVfxPlayback.Restart(shotgunAttackVisualInstance);
    }

    /// <summary>
    /// 산탄 VFX의 좌우·상하 발사 각도와 입자 굵기는 그대로 두고 이동 거리만 줄입니다.
    /// 속도를 낮춰 타격감이 둔해지지 않도록 입자의 수명, 원거리 방출 위치와 스트레치 길이만 같은 비율로 조정합니다.
    /// 원본 프리팹은 바꾸지 않고 장착 중 재사용하는 공격 연출 인스턴스에 한 번만 적용합니다.
    /// </summary>
    private static void ApplyShotgunTravelDistanceScale(GameObject instance, float relativeScale)
    {
        foreach (ParticleSystem particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!IsShotgunAttackParticle(particleSystem.name))
                continue;

            particleSystem.transform.localPosition *= relativeScale;

            ParticleSystem.MainModule main = particleSystem.main;
            main.startLifetimeMultiplier *= relativeScale;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            if (shape.enabled)
            {
                // 전기 포크처럼 먼 위치의 Box에서 바로 생성되는 파티클도 같은 비율로 안쪽에 배치합니다.
                // 모든 축을 같은 비율로 줄이므로 원형 콘의 상하 각도만 별도로 납작하게 만들지 않습니다.
                shape.position *= relativeScale;
                shape.scale *= relativeScale;
            }

            ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null && particleRenderer.renderMode == ParticleSystemRenderMode.Stretch)
                particleRenderer.lengthScale *= relativeScale;
        }
    }

    private void OnDisable()
    {
        // 장비를 교체하거나 캐릭터가 비활성화될 때 화면에 이전 발사의 잔상이 남지 않게 정리합니다.
        GunnerVfxPlayback.StopAndClear(muzzleVisualInstance);
        GunnerVfxPlayback.StopAndClear(shotgunAttackVisualInstance);
    }

    private void OnDestroy()
    {
        // 두 연출 모두 무기 자식이 아닌 FirePoint 위치에서 독립적으로 재생되므로 직접 제거합니다.
        if (muzzleVisualInstance != null)
            Destroy(muzzleVisualInstance);

        if (shotgunAttackVisualInstance != null)
            Destroy(shotgunAttackVisualInstance);
    }

    /// <summary>
    /// 하나의 산탄총 VFX 프리팹을 총구용과 정면 공격용으로 나눕니다.
    /// 파티클을 제거하거나 원본 프리팹을 바꾸지 않고, 각 재생 인스턴스에서 필요 없는 방출과 렌더링만 끕니다.
    /// </summary>
    private static void KeepOnlyShotgunParticleGroup(GameObject instance, bool keepAttackGroup)
    {
        foreach (ParticleSystem particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            bool isAttackParticle = IsShotgunAttackParticle(particleSystem.name);
            if (isAttackParticle == keepAttackGroup)
                continue;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = false;

            ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
                particleRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 총구 프리팹 안에 섞여 들어온 장거리 투사체 파티클만 총구 복사본에서 끕니다.
    /// 얼음 원본의 liz01은 총구 장식이 아니라 5~7m를 날아가는 얼음 흐름이며,
    /// 라이플과 유탄발사기는 별도의 ProjectileVisual에서 같은 역할을 이미 재생합니다.
    /// 원본 프리팹과 실제 투사체 VFX는 그대로 두므로 총구 근처의 얼음 링과 점화 입자는 유지됩니다.
    /// </summary>
    private static void DisableProjectileLikeMuzzleParticles(GameObject instance)
    {
        foreach (ParticleSystem particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particleSystem.name != "liz01")
                continue;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = false;

            ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
                particleRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 실제 산탄 범위를 보여 주며 멀리 뻗는 파티클 이름만 한곳에서 구분합니다.
    /// 불꽃 계열의 Nozzle은 이름과 달리 총구 섬광이 아니라 수 미터를 이동하는 화염 줄기이므로 공격 연출에 포함합니다.
    /// 여기에 없는 접촉 섬광, 압력 연기, 짧은 점화 불꽃, 얼음 링과 전기 코어는 실제 총구를 따라갑니다.
    /// </summary>
    private static bool IsShotgunAttackParticle(string particleName)
    {
        switch (particleName)
        {
            case "PelletPeripheralGlints":
            case "PelletCoreStreaks":
            case "PelletShortStreaks":
            case "ShotgunFan_Long":
            case "ShotgunFan_Accent":
            case "SFA_V2_ModularIgnition_Derived":
            case "DustLinger":
            case "Nozzle":
            case "liz01":
            case "SecondaryForks":
            case "FarForks":
                return true;
            default:
                return false;
        }
    }
}

/// <summary>
/// 총구·투사체·명중 VFX가 풀링 또는 반복 재생될 때 이전 프레임의 파티클과 Trail이 남지 않도록
/// 같은 순서로 초기화하는 공용 재생 도우미입니다.
///
/// 이 도우미는 거너 VFX 코드 안에서만 사용합니다. 게임 전체를 관리하는 새 Manager가 아니며,
/// 투사체를 빌려 주고 되돌려 받는 일은 기존 WBH_ProjectilePoolManager가 계속 담당합니다.
/// </summary>
internal static class GunnerVfxPlayback
{
    // 명중 이펙트가 캐릭터나 작은 적을 통째로 가리지 않도록 원본 크기의 65%로 재생합니다.
    // 54개 무기 프리팹을 하나씩 고치지 않고 이 공용 값만 바꾸면 모든 명중 이펙트 크기가 같이 맞춰집니다.
    private const float HitEffectScale = 0.65f;

    /// <summary>
    /// 명중 VFX를 월드 위치에 한 번 재생하고, 프리팹 안의 실제 재생 시간이 끝나면 제거합니다.
    /// 프리팹의 로컬 +Z는 탄환이 날아온 반대쪽, 즉 피격면 바깥 방향을 향합니다.
    /// </summary>
    public static void SpawnTransient(
        GameObject prefab,
        Vector3 position,
        Vector3 outward,
        float fallbackLifetime = 0.45f)
    {
        if (prefab == null)
            return;

        Vector3 forward = outward.sqrMagnitude > 0.0001f
            ? outward.normalized
            : Vector3.up;
        Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f
            ? Vector3.forward
            : Vector3.up;
        GameObject instance = Object.Instantiate(
            prefab,
            position,
            Quaternion.LookRotation(forward, up));

        // 프리팹이 가진 무늬와 파티클 비율은 유지하고, 화면에서 차지하는 전체 크기만 줄입니다.
        instance.transform.localScale *= HitEffectScale;

        Restart(instance);
        Object.Destroy(
            instance,
            GetTransientLifetime(instance, fallbackLifetime));
    }

    /// <summary>
    /// 이전 재생 흔적을 완전히 지운 뒤 활성화하고 Trail과 ParticleSystem을 처음부터 시작합니다.
    /// 연속 대여되는 풀 오브젝트에서도 이전 탄환의 꼬리나 입자가 순간적으로 보이지 않게 합니다.
    /// </summary>
    public static void Restart(GameObject instance)
    {
        if (instance == null)
            return;

        // SetActive(true) 전에 먼저 지워야, 활성화되는 한 프레임에 이전 잔상이 노출되지 않습니다.
        StopAndClear(instance);
        instance.SetActive(true);

        foreach (TrailRenderer trail in instance.GetComponentsInChildren<TrailRenderer>(true))
        {
            // 프리팹 제작자가 의도적으로 꺼 둔 선택 분기까지 강제로 켜지 않습니다.
            if (!trail.gameObject.activeInHierarchy)
                continue;

            trail.Clear();
            trail.emitting = true;
        }

        foreach (ParticleSystem particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!particleSystem.gameObject.activeInHierarchy)
                continue;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    /// <summary>
    /// 모든 Trail과 ParticleSystem의 방출 및 누적 데이터를 지운 뒤 루트를 비활성화합니다.
    /// 풀 반환, 시각 프리팹 교체, 공격 취소 시 반드시 이 정리 순서를 사용합니다.
    /// </summary>
    public static void StopAndClear(GameObject instance)
    {
        if (instance == null)
            return;

        foreach (TrailRenderer trail in instance.GetComponentsInChildren<TrailRenderer>(true))
        {
            if (!trail.gameObject.activeInHierarchy)
                continue;

            trail.emitting = false;
            trail.Clear();
        }

        foreach (ParticleSystem particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!particleSystem.gameObject.activeInHierarchy)
                continue;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
        }

        instance.SetActive(false);
    }

    /// <summary>
    /// 일회성 명중 VFX를 언제 회수해도 되는지 프리팹 설정에서 계산합니다.
    /// startDelay + duration + 최대 lifetime과 Trail 유지 시간을 모두 포함합니다.
    ///
    /// 0.05~0.8초로 제한하는 이유:
    /// - 지나치게 짧으면 첫 프레임 전에 사라질 수 있습니다.
    /// - 이번에 만든 명중 연출은 무거운 유탄도 최대 0.8초 동안 보이도록 정했으므로,
    ///   잘못 설정된 무한/과도한 수명이 런타임 오브젝트를 오래 남기지 않게 합니다.
    /// </summary>
    public static float GetTransientLifetime(GameObject instance, float fallback)
    {
        float lifetime = 0f;

        foreach (ParticleSystem particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particleSystem.main;
            lifetime = Mathf.Max(
                lifetime,
                main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
        }

        foreach (TrailRenderer trail in instance.GetComponentsInChildren<TrailRenderer>(true))
            lifetime = Mathf.Max(lifetime, trail.time);

        return Mathf.Clamp(lifetime > 0f ? lifetime : fallback, 0.05f, 0.8f);
    }
}
