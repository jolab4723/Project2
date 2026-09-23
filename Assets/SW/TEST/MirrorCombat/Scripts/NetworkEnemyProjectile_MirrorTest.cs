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
public sealed class NetworkEnemyProjectile_MirrorTest : NetworkBehaviour
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

    private static readonly Dictionary<uint, List<NetworkEnemyProjectile_MirrorTest>> GravityWellFieldsByOwner = new();
    // SW 수정: 별도 Manager 없이 현재 네트워크 투사체만으로 소유자별 지연 폭발 상한을 관리합니다.
    private static readonly Dictionary<uint, List<NetworkEnemyProjectile_MirrorTest>> SingularityExplosionsByOwner = new();

    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
    [SerializeField] private LayerMask playerLayer = 1 << 15;

    [SyncVar] private bool missile;
    [SyncVar] private bool playerShot;
    [SyncVar] private uint playerOwnerNetId;
    [SyncVar] private string shotItemId;
    [SyncVar] private GunnerWeaponType shotWeaponType;
    [SyncVar] private float gravityWellRadius;
    [SyncVar] private Color gravityWellColor;
    // SW 수정: 반경과 색을 먼저 역직렬화한 뒤 활성 Hook이 링을 만들도록 선언 순서를 유지합니다.
    [SyncVar(hook = nameof(OnGravityWellActiveChanged))] private bool gravityWellActive;
    [SyncVar] private float singularityExplosionRadius;
    [SyncVar] private Color singularityWarningColor;
    [SyncVar] private double singularityDetonatesAt;
    // SW 수정: 반경·색·기폭 시각을 먼저 복제한 뒤 활성 Hook이 경고 링을 만들도록 선언 순서를 유지합니다.
    [SyncVar(hook = nameof(OnSingularityActiveChanged))] private bool singularityActive;

    private NetworkEnemyAuthority_MirrorTest owner;
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
    private PlayerContext playerOwner;
    private ElementType shotElement;
    private uint shotAttackId;
    private int shotSceneHandle;
    private double shotExpiresAt;
    private UniqueEffectSO shotUniqueEffect;
    private int playerShotCollisionMask;
    private GameObject playerProjectileVisual;
    private GameObject playerImpactVisualPrefab;
    private float visualBindUntil;
    private double gravityWellExpiresAt;
    private double gravityWellNextApplyAt;
    private float gravityWellSlowMultiplier;
    private float gravityWellSlowRefreshSeconds;
    private GameObject gravityWellVisual;
    private readonly HashSet<NetworkEnemyAuthority_MirrorTest> gravityWellTargets = new();
    private float singularityDamageMultiplier;
    private GameObject singularityVisual;
    private readonly HashSet<NetworkEnemyAuthority_MirrorTest> singularityTargets = new();

    public bool IsMissile => missile;
    public bool IsPlayerShot => playerShot;
    public uint PlayerOwnerNetId => playerOwnerNetId;
    private bool IsPlayerShotAvailable => playerOwner != null && playerOwner.CombatAuthority?.CanContinueGunnerProjectile == true &&
        SceneManager.GetActiveScene().handle == shotSceneHandle && NetworkTime.time < shotExpiresAt;

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
        GravityWellFieldsByOwner.Clear();
        SingularityExplosionsByOwner.Clear();
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
            TryBindPlayerVisual();
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
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (playerShot) ServerPlayerSpawnCount++;
        else ServerSpawnCount++;
        if (missile && !playerShot)
            ServerMissileSpawnCount++;
    }

    /// <summary>SW 수정: 서버가 장판·지연 폭발 네트워크 객체를 제거할 때 소유자별 활성 목록도 함께 정리합니다.</summary>
    public override void OnStopServer()
    {
        UnregisterGravityWellField();
        UnregisterSingularityExplosion();
        base.OnStopServer();
    }

    [Server]
    public void InitializeServer(
        NetworkEnemyAuthority_MirrorTest attackOwner,
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
        NetworkEnemyAuthority_MirrorTest attackOwner,
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

    /// <summary>적 투사체의 이동 경계를 재사용하는 Gunner 기본 공격 시험판. 피해 공식은 기존 SW resolver만 사용한다.</summary>
    /// <param name="instanceId">투사체 귀속 정책에서 장착 세대 추적이 불필요해 사용되지 않음. 호환 목적으로 유지.</param>
    /// <param name="equipGeneration">동일. 사용되지 않음.</param>
    [Server]
    public void InitializePlayerServer(PlayerContext attackOwner, GunnerWeaponType weaponType, string itemId,
        ElementType element, Vector3 moveDirection, float moveSpeed, float maxDistance, Vector3 impactPoint,
        float explosionRadius, uint attackId, string instanceId, uint equipGeneration, GlassRailExtraHitUniqueEffectSO effect)
    {
        InitializePlayerServer(attackOwner, weaponType, itemId, element, moveDirection, moveSpeed, maxDistance, impactPoint, explosionRadius, attackId, effect);
    }

    /// <summary>
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
        shotSceneHandle = SceneManager.GetActiveScene().handle;
        shotExpiresAt = NetworkTime.time + 20d;
        playerShotCollisionMask = LayerMask.GetMask("Enemy", "Wall", "Prop", "Ground") | (1 << 10);
        InitializeServer(null, moveDirection, moveSpeed, maxDistance);
        if (weaponType == GunnerWeaponType.GrenadeLauncher)
        {
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
        if (isClient && playerShot && !gravityWellActive && !singularityActive && playerProjectileVisual == null && Time.unscaledTime <= visualBindUntil)
            TryBindPlayerVisual();
        if (!isServer)
            return;

        if (gravityWellActive)
        {
            UpdateGravityWellField();
            return;
        }

        if (singularityActive)
        {
            UpdateSingularityExplosion();
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

    private void OnTriggerEnter(Collider other)
    {
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
    /// 중력 우물은 고정 둔화 장판으로, 특이점 박격포는 고정 지연 폭발 예약체로 전환합니다.
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
            playerShotTargets.Clear();
            if (missile)
            {
                foreach (Collider hit in Physics.OverlapSphere(point, missileExplosionRadius, 1 << 10, QueryTriggerInteraction.Collide))
                    ApplyPlayerDamage(hit);
            }
            else if (directHit != null) ApplyPlayerDamage(directHit);
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
        singularityDamageMultiplier = effect.damageMultiplier;
        singularityWarningColor = effect.warningColor;
        singularityDetonatesAt = NetworkTime.time + effect.delaySeconds;
        singularityActive = true;
        if (projectileCollider != null)
            projectileCollider.enabled = false;

        RegisterSingularityExplosion(effect.maxPendingExplosions);
        if (isClient)
            ShowSingularityVisual();
    }

    /// <summary>
    /// SW 수정: 서버가 예약 수명과 소유자 상태를 확인하고 기폭 시 살아 있는 적을 한 번씩만 판정합니다.
    /// 다음 프레임 효과이므로 현재 서버 Stat을 새로 읽으며 직접 공격 트리거는 다시 발동하지 않습니다.
    /// </summary>
    [Server]
    private void UpdateSingularityExplosion()
    {
        if (playerOwner == null || playerOwner.RuntimeState?.IsDead == true ||
            SceneManager.GetActiveScene().handle != shotSceneHandle)
        {
            NetworkServer.Destroy(gameObject);
            return;
        }
        if (NetworkTime.time < singularityDetonatesAt)
            return;

        singularityTargets.Clear();
        uint effectAttackId = shotAttackId ^ 0x80000000u;
        if (effectAttackId == 0u)
            effectAttackId = uint.MaxValue;
        foreach (Collider hit in Physics.OverlapSphere(transform.position, singularityExplosionRadius,
                     1 << 10, QueryTriggerInteraction.Collide))
        {
            NetworkEnemyAuthority_MirrorTest authority = hit.GetComponentInParent<NetworkEnemyAuthority_MirrorTest>();
            WBH_ICombat target = PlayerCombatAuthority_MirrorTest.FindCombatTarget(hit);
            if (authority == null || target == null || target.Status == null || target.Status.IsDead ||
                !singularityTargets.Add(authority))
                continue;

            if (WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(playerOwner, target, shotElement,
                    singularityDamageMultiplier, null, out WBH_DamageResult result,
                    DamageCause.Effect, effectAttackId))
                playerOwner.CombatAuthority?.ServerRecordGunnerHit(target, result);
        }
        playerOwner.CombatAuthority?.ServerPresentGunnerImpact(shotItemId, shotWeaponType, transform.position, Vector3.up);
        NetworkServer.Destroy(gameObject);
    }

    /// <summary>SW 수정: 같은 소유자의 예약이 상한을 넘으면 가장 오래된 특이점부터 취소합니다.</summary>
    [Server]
    private void RegisterSingularityExplosion(int maxPendingExplosions)
    {
        if (!SingularityExplosionsByOwner.TryGetValue(playerOwnerNetId, out List<NetworkEnemyProjectile_MirrorTest> explosions))
            SingularityExplosionsByOwner[playerOwnerNetId] = explosions = new List<NetworkEnemyProjectile_MirrorTest>();
        explosions.RemoveAll(explosion => explosion == null || !explosion.singularityActive);
        while (explosions.Count >= maxPendingExplosions)
        {
            NetworkEnemyProjectile_MirrorTest oldest = explosions[0];
            explosions.RemoveAt(0);
            if (oldest != null)
                NetworkServer.Destroy(oldest.gameObject);
        }
        explosions.Add(this);
    }

    /// <summary>SW 수정: 지연 폭발이 끝나거나 취소될 때 소유자별 예약 목록에서 안전하게 제거합니다.</summary>
    [Server]
    private void UnregisterSingularityExplosion()
    {
        if (!SingularityExplosionsByOwner.TryGetValue(playerOwnerNetId, out List<NetworkEnemyProjectile_MirrorTest> explosions))
            return;
        explosions.Remove(this);
        if (explosions.Count == 0)
            SingularityExplosionsByOwner.Remove(playerOwnerNetId);
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
        gravityWellSlowMultiplier = effect.slowMultiplier;
        gravityWellSlowRefreshSeconds = effect.slowRefreshSeconds;
        gravityWellExpiresAt = NetworkTime.time + effect.durationSeconds;
        gravityWellNextApplyAt = 0d;
        gravityWellActive = true;
        if (projectileCollider != null)
            projectileCollider.enabled = false;

        RegisterGravityWellField(effect.maxConcurrentFields);
        if (isClient)
            ShowGravityWellVisual();
    }

    /// <summary>
    /// SW 수정: 서버가 장판 안의 살아 있는 적을 한 번씩만 찾아 짧은 둔화를 갱신합니다.
    /// 짧은 갱신 방식이라 적이 범위를 벗어나면 곧바로 원래 속도로 돌아옵니다.
    /// </summary>
    [Server]
    private void UpdateGravityWellField()
    {
        if (playerOwner == null || playerOwner.RuntimeState?.IsDead == true ||
            SceneManager.GetActiveScene().handle != shotSceneHandle || NetworkTime.time >= gravityWellExpiresAt)
        {
            NetworkServer.Destroy(gameObject);
            return;
        }

        if (NetworkTime.time < gravityWellNextApplyAt)
            return;

        gravityWellNextApplyAt = NetworkTime.time + gravityWellSlowRefreshSeconds;
        gravityWellTargets.Clear();
        foreach (Collider hit in Physics.OverlapSphere(transform.position, gravityWellRadius, 1 << 10, QueryTriggerInteraction.Collide))
        {
            NetworkEnemyAuthority_MirrorTest target = hit.GetComponentInParent<NetworkEnemyAuthority_MirrorTest>();
            if (target == null || !gravityWellTargets.Add(target))
                continue;

            var slow = new WBH_StatusEffectData(WBH_StatusEffectType.Slow,
                gravityWellSlowRefreshSeconds + 0.1f, gravityWellSlowMultiplier)
            {
                Attacker = playerOwner.Controller,
                AttackId = shotAttackId,
            };
            target.ServerTryApplyStatusEffect(slow);
        }
    }

    /// <summary>
    /// SW 수정: 같은 플레이어가 허용 수보다 많은 중력 우물을 만들면 가장 오래된 장판부터 제거합니다.
    /// 별도 Manager 없이 현재 네트워크 투사체 목록만 사용합니다.
    /// </summary>
    [Server]
    private void RegisterGravityWellField(int maxConcurrentFields)
    {
        if (!GravityWellFieldsByOwner.TryGetValue(playerOwnerNetId, out List<NetworkEnemyProjectile_MirrorTest> fields))
            GravityWellFieldsByOwner[playerOwnerNetId] = fields = new List<NetworkEnemyProjectile_MirrorTest>();
        fields.RemoveAll(field => field == null || !field.gravityWellActive);
        while (fields.Count >= maxConcurrentFields)
        {
            NetworkEnemyProjectile_MirrorTest oldest = fields[0];
            fields.RemoveAt(0);
            if (oldest != null)
                NetworkServer.Destroy(oldest.gameObject);
        }
        fields.Add(this);
    }

    /// <summary>SW 수정: 서버 장판이 끝날 때 소유자별 활성 목록에서도 안전하게 제거합니다.</summary>
    [Server]
    private void UnregisterGravityWellField()
    {
        if (!GravityWellFieldsByOwner.TryGetValue(playerOwnerNetId, out List<NetworkEnemyProjectile_MirrorTest> fields))
            return;
        fields.Remove(this);
        if (fields.Count == 0)
            GravityWellFieldsByOwner.Remove(playerOwnerNetId);
    }

    [Server]
    private void ApplyPlayerDamage(Collider hit)
    {
        WBH_ICombat target = PlayerCombatAuthority_MirrorTest.FindCombatTarget(hit);
        if (target is not Component component || component.GetComponentInParent<NetworkEnemyAuthority_MirrorTest>() == null ||
            !playerShotTargets.Add(target)) return;
        // 원본 WBH_DamageRequest도 공격자 객체를 보관하므로 피해는 명중 시의 실제 Stat으로 계산된다.
        // 무기 종류·속성·속도·사거리·VFX는 발사 시 값을 유지하고, 새 피해 공식을 복제하지 않는다.
        if (playerOwner == null || playerOwner.CombatAuthority == null ||
            !playerOwner.CombatAuthority.TryBeginGunnerHitScope(shotAttackId, shotWeaponType, shotUniqueEffect, out System.IDisposable hitScope))
            return;

        using (hitScope)
        {
            if (WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(playerOwner, target, shotElement, 1f,
                    PlayerCombatAuthority_MirrorTest.GetStatusEffectForElement(shotElement),
                    out WBH_DamageResult result, DamageCause.Direct, shotAttackId))
                playerOwner.CombatAuthority.ServerRecordGunnerHit(target, result);
        }
    }

    private void TryBindPlayerVisual()
    {
        if (!NetworkClient.spawned.TryGetValue(playerOwnerNetId, out NetworkIdentity identity)) return;
        GunnerWeaponVfxBinding binding = GunnerCombatPresentation_MirrorTest.FindBinding(identity.gameObject, shotItemId, shotWeaponType);
        if (binding == null) return;
        // 발사 후 장착 외형이 바뀌어도 이 탄은 처음 확보한 VFX 참조를 유지한다.
        playerImpactVisualPrefab = binding.ImpactVisualPrefab;
        if (binding.ProjectileVisualPrefab == null || playerProjectileVisual != null) return;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
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
        if (!isClient || gravityWellVisual != null)
            return;
        if (playerProjectileVisual != null)
            playerProjectileVisual.SetActive(false);
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = false;

        gravityWellVisual = new GameObject("GravityWellFieldVisual");
        gravityWellVisual.transform.SetParent(transform, false);
        AreaRingVisual ring = gravityWellVisual.AddComponent<AreaRingVisual>();
        ring.SetColor(gravityWellColor);
        ring.SetRadius(gravityWellRadius);
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
        if (!isClient || singularityVisual != null)
            return;
        if (playerProjectileVisual != null)
            playerProjectileVisual.SetActive(false);
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = false;

        singularityVisual = new GameObject("SingularityDelayedExplosionVisual");
        singularityVisual.transform.SetParent(transform, false);
        AreaRingVisual ring = singularityVisual.AddComponent<AreaRingVisual>();
        ring.SetColor(singularityWarningColor);
        ring.SetRadius(singularityExplosionRadius);
    }

    [ClientRpc]
    private void RpcPlayerImpact(Vector3 point, Vector3 hitDirection)
    {
        if (playerImpactVisualPrefab != null)
            GunnerVfxPlayback.SpawnTransient(playerImpactVisualPrefab, point, hitDirection);
        else if (missile)
            SkillRangeVisual.ShowSector(point, Vector3.forward, missileExplosionRadius, 360f,
                new Color(1f, 0.6f, 0.2f, 0.35f), 0.2f);
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
