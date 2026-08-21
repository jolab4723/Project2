using Mirror;
using System.Collections.Generic;
using UnityEngine;

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

    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
    [SerializeField] private LayerMask playerLayer = 1 << 15;

    [SyncVar] private bool missile;

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
    private float missileExplosionRadius;
    private readonly HashSet<PlayerContext> missileTargets = new();

    public bool IsMissile => missile;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDiagnostics()
    {
        ServerSpawnCount = 0;
        ClientObservedCount = 0;
        ServerMissileSpawnCount = 0;
        ServerMissileImpactCount = 0;
        ClientMissileObservedCount = 0;
    }

    private void Awake()
    {
        projectileCollider = GetComponent<Collider>();
        projectileCollider.isTrigger = true;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ClientObservedCount++;
        if (missile)
            ClientMissileObservedCount++;
        if (!isServer && projectileCollider != null)
            projectileCollider.enabled = false;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ServerSpawnCount++;
        if (missile)
            ServerMissileSpawnCount++;
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

    private void Update()
    {
        if (!isServer || consumed)
            return;

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
                playerLayer,
                QueryTriggerInteraction.Collide))
        {
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
        transform.position = nextPosition;

        Vector3 motion = nextPosition - previousPosition;
        if (motion.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(motion.normalized);

        if (t < 1f)
            return;

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

    [Server]
    private void ServerDestroy()
    {
        if (consumed)
            return;

        consumed = true;
        NetworkServer.Destroy(gameObject);
    }
}
