using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ItemSystem;

/// <summary>
/// 일반 원거리 적의 Mirror 테스트용 서버 투사체다.
/// <para>서버만 이동과 충돌을 계산하고 클라이언트는 NetworkTransform이 전달한 외형만 본다.</para>
/// <para>충돌한 Collider의 부모에서 정확한 <see cref="PlayerContext"/>를 찾으며 임의의 로컬 플레이어로 대신하지 않는다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(Collider))]
public sealed class NetworkEnemyProjectile : NetworkBehaviour
{
    /// <summary>
    /// 현재 프로세스에서 서버가 생성한 투사체 누적 횟수다. 테스트 실행마다 0으로 초기화된다.
    /// </summary>
    public static uint ServerSpawnCount { get; private set; }

    /// <summary>
    /// 현재 프로세스의 Client가 Spawn 메시지로 확인한 투사체 누적 횟수다.
    /// Host에서는 서버와 로컬 Client가 함께 있으므로 두 값이 같이 증가한다.
    /// </summary>
    public static uint ClientObservedCount { get; private set; }

    public static uint ServerMissileSpawnCount { get; private set; }
    public static uint ServerMissileImpactCount { get; private set; }
    public static uint ClientMissileObservedCount { get; private set; }
    public static uint ServerPlayerSpawnCount { get; private set; }
    public static uint ClientPlayerObservedCount { get; private set; }

    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
    [SerializeField] private LayerMask playerLayer = 1 << 15;

    [SyncVar] private bool missile;
    [SyncVar] private bool playerShot;
    [SyncVar] private uint playerOwnerNetId;
    [SyncVar] private string shotItemId;
    [SyncVar] private GunnerWeaponType shotWeaponType;
    [SyncVar] private bool chargedWarhead;
    private GameObject chargedVisual;
    [SyncVar] private float gravityWellRadius;
    [SyncVar] private Color gravityWellColor;
    // SW 수정: 반경과 색을 먼저 역직렬화한 뒤 활성 Hook이 링을 만들도록 선언 순서를 유지합니다.
    [SyncVar(hook = nameof(OnGravityWellActiveChanged))] private bool gravityWellActive;
    [SyncVar] private float singularityExplosionRadius;
    [SyncVar] private Color singularityWarningColor;
    [SyncVar] private double singularityDetonatesAt;
    // SW 수정: 반경·색·기폭 시각을 먼저 복제한 뒤 활성 Hook이 경고 링을 만들도록 선언 순서를 유지합니다.
    [SyncVar(hook = nameof(OnSingularityActiveChanged))] private bool singularityActive;
    [SyncVar] private float sunfallBurnFieldRadius;
    [SyncVar] private Color sunfallBurnFieldColor;
    // 클라이언트가 장판 종료 직전에 흐려지는 연출을 맞추도록 서버 시각 기준 종료 시각을 함께 복제한다.
    [SyncVar] private double sunfallBurnFieldEndsAt;
    // SW 수정 : 반경과 색이 복제된 뒤 활성 Hook으로 일식 장판을 표시한다.
    [SyncVar(hook = nameof(OnSunfallBurnFieldActiveChanged))] private bool sunfallBurnFieldActive;

    private NetworkEnemyAuthority owner;
    private Vector3 direction;
    private float speed;
    private float remainingDistance;
    private Collider projectileCollider;
    private bool consumed;
    private Vector3 missileStart;
    private Vector3 missileImpactPoint;
    private float missileFlightDuration;
    private float missileElapsed;
    private float missileArcHeight;
    [SyncVar] private float missileExplosionRadius;
    private readonly HashSet<PlayerContext> missileTargets = new();
    private readonly HashSet<WBH_ICombat> playerShotTargets = new();
    // SW 수정: 반물질 기본 라이플의 한 이동 구간 충돌만 거리순으로 처리한다.
    private readonly List<(Collider collider, float distance)> piercingHits = new();
    private PlayerContext playerOwner;
    private ElementType shotElement;
    private uint shotAttackId;
    private int shotSceneHandle;
    private double shotExpiresAt;
    private UniqueEffectSO shotUniqueEffect;
    private float shotNinjaBonus;
    private WBH_EffectData shotHitEffectData;
    private int playerShotCollisionMask;
    private GameObject playerProjectileVisual;
    private float visualBindUntil;
    private GameObject gravityWellVisual;
    private GameObject singularityVisual;
    private GameObject sunfallBurnFieldVisual;

    public bool IsMissile => missile;
    public bool IsPlayerShot => playerShot;
    public uint PlayerOwnerNetId => playerOwnerNetId;
    private bool IsPlayerShotAvailable => playerOwner != null && playerOwner.CombatAuthority?.CanContinueGunnerProjectile == true &&
        SceneManager.GetActiveScene().handle == shotSceneHandle && NetworkTime.time < shotExpiresAt;
    private bool IsPiercingPlayerShot => playerShot && !missile && shotWeaponType == GunnerWeaponType.Rifle &&
        shotUniqueEffect is AntimatterPiercingShotUniqueEffectSO;

    /// <summary>SW 수정: Play Mode를 새로 시작할 때 장판·지연 폭발 포함 투사체 진단값과 활성 목록을 초기화합니다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDiagnostics()
    {
        ServerSpawnCount = 0;
        ClientObservedCount = 0;
        ServerMissileSpawnCount = 0;
        ServerMissileImpactCount = 0;
        ClientMissileObservedCount = 0;
        ServerPlayerSpawnCount = 0;
        ClientPlayerObservedCount = 0;
    }

    private void Awake()
    {
        projectileCollider = GetComponent<Collider>();
        projectileCollider.isTrigger = true;
    }

    /// <summary>SW 수정: 클라이언트가 투사체 또는 이미 활성화된 장판·지연 폭발을 처음 관찰할 때 외형을 연결합니다.</summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (playerShot)
        {
            ClientPlayerObservedCount++;
            visualBindUntil = Time.unscaledTime + 0.5f;
            // 초기 역직렬화 Hook이 장판을 먼저 만들었을 수 있으므로, 비행 외형은 장판이 아닐 때만 만든다.
            TryBindPlayerVisual();
            if (chargedWarhead && chargedVisual == null && !IsPersistentFieldActive) chargedVisual = ChargedShotVisual.Create(transform);
        }
        else ClientObservedCount++;
        if (missile && !playerShot)
            ClientMissileObservedCount++;
        if (!isServer && projectileCollider != null)
            projectileCollider.enabled = false;
        if (gravityWellActive)
            ShowGravityWellVisual();
        if (singularityActive)
            ShowSingularityVisual();
        if (sunfallBurnFieldActive)
            ShowSunfallBurnFieldVisual();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (playerShot) ServerPlayerSpawnCount++;
        else ServerSpawnCount++;
        if (missile && !playerShot)
            ServerMissileSpawnCount++;
    }

    [Server]
    public void InitializeServer(
        NetworkEnemyAuthority attackOwner,
        Vector3 moveDirection,
        float moveSpeed,
        float maxDistance)
    {
        owner = attackOwner;
        direction = moveDirection.sqrMagnitude > 0.001f
            ? moveDirection.normalized
            : transform.forward;
        speed = Mathf.Max(0.01f, moveSpeed);
        remainingDistance = Mathf.Max(0.1f, maxDistance);
        transform.forward = direction;
    }

    [Server]
    public void InitializeMissileServer(
        NetworkEnemyAuthority attackOwner,
        Vector3 impactPoint,
        float flightDuration,
        float explosionRadius)
    {
        owner = attackOwner;
        missile = true;
        missileStart = transform.position;
        missileImpactPoint = impactPoint;
        missileFlightDuration = Mathf.Max(0.05f, flightDuration);
        missileExplosionRadius = Mathf.Max(0.01f, explosionRadius);
        float horizontalDistance = Vector3.Distance(
            new Vector3(missileStart.x, 0f, missileStart.z),
            new Vector3(missileImpactPoint.x, 0f, missileImpactPoint.z));
        missileArcHeight = Mathf.Max(3f, horizontalDistance * 0.25f);
    }

    /// <summary>
    /// 적 투사체의 이동 경계를 재사용하는 Gunner 기본 공격 시험판. 피해 공식은 기존 SW resolver만 사용한다.
    /// SW 수정: 플레이어가 발사한 탄의 무기·아이템·속성과 발사 시 고유효과를 서버에 보관합니다.
    /// 장착을 바꿔도 이미 발사한 탄은 이 스냅샷으로 충돌 결과를 처리합니다.
    /// </summary>
    [Server]
    public void InitializePlayerServer(PlayerContext attackOwner, GunnerWeaponType weaponType, string itemId,
        ElementType element, Vector3 moveDirection, float moveSpeed, float maxDistance, Vector3 impactPoint,
        float explosionRadius, uint attackId, UniqueEffectSO uniqueEffect = null)
    {
        playerShot = true;
        playerOwner = attackOwner;
        playerOwnerNetId = attackOwner.GetComponent<NetworkIdentity>().netId;
        shotItemId = itemId ?? string.Empty;
        shotWeaponType = weaponType;
        shotElement = element;
        shotAttackId = attackId;
        shotUniqueEffect = uniqueEffect;
        playerShotTargets.Clear();
        piercingHits.Clear();
        shotNinjaBonus = attackOwner.Effects.ReserveNinjaDodgeBonus(attackId);
        shotHitEffectData = GunnerCombatPresentation.GetHitEffectData(attackOwner.gameObject, weaponType);
        shotSceneHandle = SceneManager.GetActiveScene().handle;
        shotExpiresAt = NetworkTime.time + 20d;
        playerShotCollisionMask = LayerMask.GetMask("Enemy", "Wall", "Prop", "Ground") | (1 << 10);
        InitializeServer(null, moveDirection, moveSpeed, maxDistance);
        if (weaponType == GunnerWeaponType.GrenadeLauncher)
        {
            chargedWarhead = uniqueEffect is WorldEnderChargedBlastUniqueEffectSO charged &&
                attackOwner.Effects.ReserveWorldEnderShot(attackId, charged);
            Vector3 offset = Vector3.ClampMagnitude(impactPoint - transform.position, remainingDistance);
            InitializeMissileServer(null, transform.position + offset,
                Mathf.Max(1f, offset.magnitude / speed), explosionRadius);
            float ratio = Mathf.Clamp01(offset.magnitude / remainingDistance);
            missileArcHeight = Mathf.Lerp(1f, 3f, ratio * ratio);
        }
    }

    /// <summary>
    /// SW 수정: 서버는 비행 중인 탄과 충돌 후 고정된 장판·지연 폭발을 같은 네트워크 객체에서 갱신합니다.
    /// 클라이언트는 발사 외형을 연결하고 서버가 복제한 지속 상태만 표시합니다.
    /// </summary>
    private void Update()
    {
        if (isClient && playerShot && !IsPersistentFieldActive &&
            playerProjectileVisual == null && Time.unscaledTime <= visualBindUntil)
            TryBindPlayerVisual();
        if (!isServer)
            return;

        if (gravityWellActive)
        {
            return;
        }

        if (singularityActive)
        {
            return;
        }

        if (sunfallBurnFieldActive)
        {
            return;
        }

        if (consumed)
            return;

        if (playerShot && !IsPlayerShotAvailable)
        {
            ServerDestroy();
            return;
        }

        if (missile)
        {
            UpdateMissile();
            return;
        }

        float distance = Mathf.Min(speed * Time.deltaTime, remainingDistance);
        if (distance <= 0f)
        {
            ServerDestroy();
            return;
        }
        if (IsPiercingPlayerShot)
        {
            MovePiercingPlayerShot(distance);
            return;
        }

        if (Physics.SphereCast(
                transform.position,
                collisionRadius,
                direction,
                out RaycastHit hit,
                distance,
                playerShot ? playerShotCollisionMask : playerLayer.value,
                QueryTriggerInteraction.Collide))
        {
            if (playerShot)
            {
                FinishPlayerImpact(hit.point, -direction, hit.collider);
                return;
            }
            PlayerContext target = hit.collider.GetComponentInParent<PlayerContext>();
            if (target != null && target.RuntimeState?.IsDead != true)
                owner?.ServerDamagePlayer(target);

            ServerDestroy();
            return;
        }

        transform.position += direction * distance;
        remainingDistance -= distance;
        if (remainingDistance <= 0f)
            ServerDestroy();
    }

    /// <summary>SW 수정: 원본 Trigger 충돌을 유지하고 반물질 라이플은 서버 이동 구간 검사만 피해·종료를 확정한다.</summary>
    private void OnTriggerEnter(Collider other)
    {
        if (IsPiercingPlayerShot)
            return;
        if (isServer && !consumed && playerShot)
        {
            if ((playerShotCollisionMask & (1 << other.gameObject.layer)) != 0)
                FinishPlayerImpact(transform.position, -transform.forward, other);
            return;
        }
        if (!isServer || consumed || missile ||
            (playerLayer.value & (1 << other.gameObject.layer)) == 0)
            return;

        PlayerContext target = other.GetComponentInParent<PlayerContext>();
        if (target != null && target.RuntimeState?.IsDead != true)
            owner?.ServerDamagePlayer(target);

        ServerDestroy();
    }

    /// <summary>SW 수정: 서버가 시작 겹침과 이동 충돌을 거리순으로 검사하고 벽·사거리·세 번째 유효 적에서 관통을 종료한다.</summary>
    [Server]
    private void MovePiercingPlayerShot(float distance)
    {
        Vector3 origin = transform.position;
        piercingHits.Clear();
        foreach (Collider overlap in Physics.OverlapSphere(origin, collisionRadius, playerShotCollisionMask, QueryTriggerInteraction.Collide))
            piercingHits.Add((overlap, 0f));
        foreach (RaycastHit hit in Physics.SphereCastAll(origin, collisionRadius, direction, distance,
            playerShotCollisionMask, QueryTriggerInteraction.Collide))
            piercingHits.Add((hit.collider, hit.distance));
        int obstacleLayerMask = LayerMask.GetMask("Wall", "Prop", "Ground");
        piercingHits.Sort((left, right) =>
        {
            int distanceComparison = left.distance.CompareTo(right.distance);
            if (distanceComparison != 0)
                return distanceComparison;

            return ((obstacleLayerMask & (1 << right.collider.gameObject.layer)) != 0).CompareTo(
                (obstacleLayerMask & (1 << left.collider.gameObject.layer)) != 0);
        });
        foreach (var hit in piercingHits)
        {
            if (consumed)
                return;
            if (!IsPlayerShotAvailable)
            {
                ServerDestroy();
                return;
            }
            if (hit.collider == null || hit.collider == projectileCollider || hit.collider.transform.IsChildOf(transform))
                continue;

            transform.position = origin + direction * hit.distance;
            Vector3 point = hit.collider.ClosestPoint(transform.position);
            if ((obstacleLayerMask & (1 << hit.collider.gameObject.layer)) != 0)
            {
                playerOwner.CombatAuthority.ServerPresentGunnerImpact(shotItemId, shotWeaponType, point, -direction);
                ServerDestroy();
                return;
            }
            if (!TryProcessPiercingPlayerHit(hit.collider, point))
                continue;

            playerOwner.CombatAuthority.ServerPresentGunnerImpact(shotItemId, shotWeaponType, point, -direction);
            var effect = (AntimatterPiercingShotUniqueEffectSO)shotUniqueEffect;
            if (playerShotTargets.Count >= Mathf.Clamp(effect.maxTargets, 1, 3))
            {
                ServerDestroy();
                return;
            }
        }
        transform.position = origin + direction * distance;
        remainingDistance -= distance;
        if (remainingDistance <= 0f)
            ServerDestroy();
    }

    /// <summary>SW 수정: 서버의 첫 유효 적은 기존 직접 피해, 후속은 원본 공격·속성·계수를 보존한 비치명 Effect로 적마다 현재 스탯을 읽는다.</summary>
    [Server]
    private bool TryProcessPiercingPlayerHit(Collider hit, Vector3 point)
    {
        WBH_ICombat target = PlayerCombatAuthority.FindCombatTarget(hit);
        if (target is not Component component || component == null || !component.gameObject.activeInHierarchy ||
            component.GetComponentInParent<NetworkEnemyAuthority>() == null || target.Status == null || target.Status.IsDead ||
            playerShotTargets.Contains(target) || !IsPlayerShotAvailable ||
            !playerOwner.CombatAuthority.TryBeginGunnerHitScope(shotAttackId, shotWeaponType, shotUniqueEffect, out System.IDisposable scope))
            return false;
        bool firstHit = playerShotTargets.Count == 0;
        var effect = (AntimatterPiercingShotUniqueEffectSO)shotUniqueEffect;
        var request = new WBH_DamageRequest(playerOwner.Controller, target, WBH_AttackType.Normal, shotElement,
            T_PlayerCombat.GunnerBasicDamageMultiplier(shotWeaponType) * effect.GetDamageMultiplier(playerShotTargets.Count),
            PlayerCombatAuthority.GetStatusEffectForElement(shotElement),
            shotHitEffectData, point, -direction, firstHit ? DamageCause.Direct : DamageCause.Effect, shotAttackId);
        using (scope)
        using (playerOwner.Effects.BeginGunnerHitScope(shotAttackId, shotWeaponType, shotUniqueEffect, shotNinjaBonus))
        {
            WBH_DamageResult result;
            bool processed = firstHit
                ? WBH_CombatResolver.TryProcessPlayerDamage(playerOwner, request, out result)
                : PlayerDamageResolver.TryProcessPlayerDamage(playerOwner, request, out result, canCrit: false);
            if (!processed) return false;
            playerShotTargets.Add(target);
            playerOwner.CombatAuthority.ServerRecordGunnerHit(target, result);
            return true;
        }
    }

    [Server]
    private void UpdateMissile()
    {
        Vector3 previousPosition = transform.position;
        missileElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(missileElapsed / missileFlightDuration);
        Vector3 nextPosition = Vector3.Lerp(missileStart, missileImpactPoint, t);
        nextPosition.y += 4f * missileArcHeight * t * (1f - t);
        Vector3 travel = nextPosition - previousPosition;
        if (playerShot && travel.sqrMagnitude > 0.000001f && Physics.SphereCast(previousPosition, collisionRadius,
            travel.normalized, out RaycastHit playerHit, travel.magnitude, playerShotCollisionMask, QueryTriggerInteraction.Collide))
        {
            FinishPlayerImpact(playerHit.point, -travel.normalized, playerHit.collider);
            return;
        }
        transform.position = nextPosition;

        Vector3 motion = nextPosition - previousPosition;
        if (motion.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(motion.normalized);

        if (t < 1f)
            return;

        if (playerShot)
        {
            FinishPlayerImpact(missileImpactPoint, Vector3.up, null);
            return;
        }

        ApplyMissileAreaDamage();
        owner?.ServerPlayBossImpactCue(WBH_EnemyEffectCue.Boss_Act1_MissileExplosion,
            missileImpactPoint, Quaternion.identity);
        ServerMissileImpactCount++;
        ServerDestroy();
    }

    [Server]
    private void ApplyMissileAreaDamage()
    {
        missileTargets.Clear();
        Collider[] hits = Physics.OverlapSphere(
            missileImpactPoint,
            missileExplosionRadius,
            playerLayer,
            QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            PlayerContext target = hit.GetComponentInParent<PlayerContext>();
            if (target == null || target.RuntimeState?.IsDead == true || !missileTargets.Add(target))
                continue;

            owner?.ServerDamagePlayer(target);
        }
    }

    /// <summary>
    /// SW 수정: 서버가 플레이어 탄의 직접 피해와 충돌 연출을 확정합니다.
    /// 중력 우물·일식 기관은 고정 상태 장판으로, 특이점 박격포는 고정 지연 폭발 예약체로 전환합니다.
    /// </summary>
    [Server]
    private void FinishPlayerImpact(Vector3 point, Vector3 hitDirection, Collider directHit)
    {
        if (consumed) return;
        consumed = true;
        bool keepAfterImpact = false;
        try
        {
            if (!IsPlayerShotAvailable) return;
            var chargedEffect = chargedWarhead ? shotUniqueEffect as WorldEnderChargedBlastUniqueEffectSO : null;
            var chargedImpact = chargedEffect != null ? playerOwner.Effects.PrepareWorldEnderImpact(point, chargedEffect) : default;
            chargedWarhead = false;
            playerShotTargets.Clear();
            if (missile)
            {
                foreach (Collider hit in Physics.OverlapSphere(point, missileExplosionRadius, 1 << 10, QueryTriggerInteraction.Collide))
                    ApplyPlayerDamage(hit);
            }
            else if (directHit != null) ApplyPlayerDamage(directHit);
            if (chargedEffect != null)
                playerOwner.Effects.ResolveWorldEnderImpact(chargedImpact, point, shotAttackId, chargedEffect);
            playerOwner?.CombatAuthority?.ServerPresentGunnerImpact(shotItemId, shotWeaponType, point, hitDirection);
            if (missile && shotUniqueEffect is GravityWellFieldUniqueEffectSO gravityWell)
            {
                ActivateGravityWellField(point, gravityWell);
                keepAfterImpact = true;
            }
            else if (missile && shotUniqueEffect is SingularityDelayedExplosionUniqueEffectSO singularity)
            {
                ActivateSingularityExplosion(point, singularity);
                keepAfterImpact = true;
            }
            else if (missile && shotUniqueEffect is SunfallBurnFieldUniqueEffectSO sunfall)
            {
                ActivateSunfallBurnField(point, sunfall);
                keepAfterImpact = true;
            }
        }
        finally
        {
            if (!keepAfterImpact)
                NetworkServer.Destroy(gameObject);
        }
    }

    /// <summary>
    /// SW 수정: 충돌한 유탄을 서버 시간 기준 지연 폭발 예약체로 전환하고 소유자별 최대 개수를 지킵니다.
    /// 발사 뒤 무기 교체에는 유지하지만 소유자 사망·씬 전환에는 취소합니다.
    /// </summary>
    [Server]
    private void ActivateSingularityExplosion(Vector3 point, SingularityDelayedExplosionUniqueEffectSO effect)
    {
        transform.SetPositionAndRotation(point, Quaternion.identity);
        missile = false;
        speed = 0f;
        singularityExplosionRadius = effect.explosionRadius;
        singularityWarningColor = effect.warningColor;
        singularityDetonatesAt = NetworkTime.time + effect.delaySeconds;
        singularityActive = true;
        if (projectileCollider != null)
            projectileCollider.enabled = false;

        gameObject.AddComponent<PlayerGrenadeEffect>().Initialize(playerOwner, effect, shotAttackId, shotElement,
            () => playerOwner.CombatAuthority?.ServerPresentGunnerImpact(shotItemId, shotWeaponType, transform.position, Vector3.up),
            () => NetworkServer.Destroy(gameObject));
        if (isClient)
            ShowSingularityVisual();
    }

    /// <summary>
    /// SW 수정: 충돌한 유탄을 서버 권한의 고정 장판으로 바꾸고 소유자별 최대 개수를 지킵니다.
    /// 장판은 무기 교체와 무관하게 유지되며 소유자 사망·장면 변경·지속시간 종료 때 사라집니다.
    /// </summary>
    [Server]
    private void ActivateGravityWellField(Vector3 point, GravityWellFieldUniqueEffectSO effect)
    {
        transform.SetPositionAndRotation(point, Quaternion.identity);
        missile = false;
        speed = 0f;
        gravityWellRadius = effect.radius;
        gravityWellColor = effect.fieldColor;
        gravityWellActive = true;
        if (projectileCollider != null)
            projectileCollider.enabled = false;

        gameObject.AddComponent<PlayerGrenadeEffect>().Initialize(playerOwner, effect, shotAttackId, shotElement,
            null, () => NetworkServer.Destroy(gameObject));
        if (isClient)
            ShowGravityWellVisual();
    }

    /// <summary>
    /// SW 수정 : 충돌한 기본 유탄을 서버 권한의 일식 화상 장판으로 전환한다.
    /// 기존 공통 수명·종류별 상한을 사용하고 클라이언트에는 위치·반경·색상·활성 상태만 전달한다.
    /// </summary>
    [Server]
    private void ActivateSunfallBurnField(Vector3 point, SunfallBurnFieldUniqueEffectSO effect)
    {
        transform.SetPositionAndRotation(point, Quaternion.identity);
        missile = false;
        speed = 0f;
        sunfallBurnFieldRadius = effect.radius;
        sunfallBurnFieldColor = effect.fieldColor;
        sunfallBurnFieldEndsAt = NetworkTime.time + effect.durationSeconds;
        sunfallBurnFieldActive = true;
        if (projectileCollider != null)
            projectileCollider.enabled = false;

        gameObject.AddComponent<PlayerGrenadeEffect>().Initialize(playerOwner, effect, shotAttackId, shotElement,
            null, () => NetworkServer.Destroy(gameObject));
        if (isClient)
            ShowSunfallBurnFieldVisual();
    }

    [Server]
    private void ApplyPlayerDamage(Collider hit)
    {
        WBH_ICombat target = PlayerCombatAuthority.FindCombatTarget(hit);
        if (target is not Component component || component.GetComponentInParent<NetworkEnemyAuthority>() == null ||
            !playerShotTargets.Add(target)) return;
        // 원본 WBH_DamageRequest도 공격자 객체를 보관하므로 피해는 명중 시의 실제 Stat으로 계산된다.
        // 무기 종류·속성·속도·사거리·VFX는 발사 시 값을 유지하고, 새 피해 공식을 복제하지 않는다.
        if (playerOwner == null || playerOwner.CombatAuthority == null ||
            !playerOwner.CombatAuthority.TryBeginGunnerHitScope(shotAttackId, shotWeaponType, shotUniqueEffect, out System.IDisposable hitScope))
            return;

        using (hitScope)
        using (playerOwner.Effects.BeginGunnerHitScope(shotAttackId, shotWeaponType, shotUniqueEffect, shotNinjaBonus))
        {
            Vector3 hitPosition = hit.ClosestPoint(transform.position);
            Vector3 hitDirection = missile ? transform.position - hitPosition : -direction;
            if (hitDirection.sqrMagnitude <= 0.0001f) hitDirection = transform.position - hit.bounds.center;
            var request = new WBH_DamageRequest(playerOwner.Controller, target, WBH_AttackType.Normal,
                shotElement, T_PlayerCombat.GunnerBasicDamageMultiplier(shotWeaponType), PlayerCombatAuthority.GetStatusEffectForElement(shotElement),
                shotHitEffectData, hitPosition, hitDirection, DamageCause.Direct, shotAttackId);
            if (WBH_CombatResolver.TryProcessPlayerDamage(playerOwner, request, out WBH_DamageResult result))
                playerOwner.CombatAuthority.ServerRecordGunnerHit(target, result);
        }
    }

    private void TryBindPlayerVisual()
    {
        if (!NetworkClient.spawned.TryGetValue(playerOwnerNetId, out NetworkIdentity identity)) return;
        GunnerWeaponVfxBinding binding = GunnerCombatPresentation.FindBinding(identity.gameObject, shotItemId, shotWeaponType);
        if (binding == null) return;
        // 발사 후 장착 외형이 바뀌어도 이 탄은 처음 확보한 VFX 참조를 유지한다.
        // 충돌 후 지속 장판 상태에서는 비행 외형을 새로 만들지 않는다.
        // 최초 관찰 시 SyncVar Hook이 만든 장판 Renderer를 끄지 않도록 OnStartClient 경로도 같은 조건을 따른다.
        if (binding.ProjectileVisualPrefab == null || playerProjectileVisual != null || IsPersistentFieldActive) return;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            if ((chargedVisual == null || !renderer.transform.IsChildOf(chargedVisual.transform)) && !IsFieldVisualRenderer(renderer))
                renderer.enabled = false;
        playerProjectileVisual = Instantiate(binding.ProjectileVisualPrefab, transform, false);
        playerProjectileVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        GunnerVfxPlayback.Restart(playerProjectileVisual);
    }

    /// <summary>SW 수정: 중력 우물 활성 상태가 복제되면 각 클라이언트가 같은 반경의 바닥 링을 표시하거나 정리합니다.</summary>
    private void OnGravityWellActiveChanged(bool _, bool active)
    {
        if (active)
            ShowGravityWellVisual();
        else if (gravityWellVisual != null)
            Destroy(gravityWellVisual);
    }

    /// <summary>SW 수정: 기존 투사체 외형을 숨기고 네트워크로 받은 위치·반경·색상으로 장판 링을 만듭니다.</summary>
    private void ShowGravityWellVisual()
    {
        if (!isClient)
            return;
        HideFlightVisual();
        // 이미 만든 장판은 재생성하지 않고 표시 상태만 보장한다.
        if (gravityWellVisual != null)
        {
            if (!gravityWellVisual.activeSelf) gravityWellVisual.SetActive(true);
            return;
        }

        gravityWellVisual = PlayerGrenadeEffect.CreateRing(transform, "GravityWellFieldVisual", gravityWellRadius, gravityWellColor);
    }

    /// <summary>SW 수정: 지연 폭발 활성 상태가 복제되면 각 클라이언트의 경고 링을 생성하거나 정리합니다.</summary>
    private void OnSingularityActiveChanged(bool _, bool active)
    {
        if (active)
            ShowSingularityVisual();
        else if (singularityVisual != null)
            Destroy(singularityVisual);
    }

    /// <summary>SW 수정: 충돌 위치와 폭발 반경을 보라색 바닥 링으로 표시해 기폭 전 위험 범위를 알립니다.</summary>
    private void ShowSingularityVisual()
    {
        if (!isClient)
            return;
        HideFlightVisual();
        // 이미 만든 장판은 재생성하지 않고 표시 상태만 보장한다.
        if (singularityVisual != null)
        {
            if (!singularityVisual.activeSelf) singularityVisual.SetActive(true);
            return;
        }

        singularityVisual = PlayerGrenadeEffect.CreateRing(transform, "SingularityDelayedExplosionVisual", singularityExplosionRadius, singularityWarningColor);
    }

    /// <summary>SW 수정 : 일식 장판의 지속 상태를 받은 클라이언트가 같은 바닥 링을 표시하거나 정리한다.</summary>
    private void OnSunfallBurnFieldActiveChanged(bool _, bool active)
    {
        if (active)
            ShowSunfallBurnFieldVisual();
        else if (sunfallBurnFieldVisual != null)
            Destroy(sunfallBurnFieldVisual);
    }

    /// <summary>SW 수정 : 비행 외형을 숨기고 서버에서 복제한 일식 장판 반경으로 공용 장판 표시(전용 일식 VFX, 없으면 원형 선)를 만든다.</summary>
    private void ShowSunfallBurnFieldVisual()
    {
        if (!isClient)
            return;
        HideFlightVisual();
        // 이미 만든 장판은 재생성하지 않고 표시 상태만 보장한다.
        if (sunfallBurnFieldVisual != null)
        {
            if (!sunfallBurnFieldVisual.activeSelf) sunfallBurnFieldVisual.SetActive(true);
            return;
        }

        sunfallBurnFieldVisual = PlayerGrenadeEffect.CreateRing(transform, "SunfallBurnFieldVisual", sunfallBurnFieldRadius, sunfallBurnFieldColor,
            (float)(sunfallBurnFieldEndsAt - NetworkTime.time));
    }

    /// <summary>충돌 후 장판·지연 폭발처럼 투사체 외형 대신 바닥 표시를 쓰는 상태인지 반환합니다.</summary>
    private bool IsPersistentFieldActive => gravityWellActive || singularityActive || sunfallBurnFieldActive;

    /// <summary>장판 표시 객체 아래의 Renderer인지 확인해 비행 외형 정리 범위와 구분합니다.</summary>
    private bool IsFieldVisualRenderer(Renderer renderer)
    {
        Transform t = renderer.transform;
        return gravityWellVisual != null && t.IsChildOf(gravityWellVisual.transform) ||
               singularityVisual != null && t.IsChildOf(singularityVisual.transform) ||
               sunfallBurnFieldVisual != null && t.IsChildOf(sunfallBurnFieldVisual.transform);
    }

    /// <summary>원래 투사체·비행 외형 Renderer만 숨기고 장판 표시 Renderer는 건드리지 않습니다.</summary>
    private void HideFlightVisual()
    {
        if (playerProjectileVisual != null)
            playerProjectileVisual.SetActive(false);
        if (chargedVisual != null)
            chargedVisual.SetActive(false);
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            if (!IsFieldVisualRenderer(renderer))
                renderer.enabled = false;
    }

    [Server]
    private void ServerDestroy()
    {
        if (consumed)
            return;

        consumed = true;
        NetworkServer.Destroy(gameObject);
    }
}
