using Mirror;
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

    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
    [SerializeField] private LayerMask playerLayer = 1 << 15;

    private NetworkEnemyAuthority_MirrorTest owner;
    private Vector3 direction;
    private float speed;
    private float remainingDistance;
    private Collider projectileCollider;
    private bool consumed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDiagnostics()
    {
        ServerSpawnCount = 0;
        ClientObservedCount = 0;
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
        if (!isServer && projectileCollider != null)
            projectileCollider.enabled = false;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ServerSpawnCount++;
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

    private void Update()
    {
        if (!isServer || consumed)
            return;

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
        if (!isServer || consumed || (playerLayer.value & (1 << other.gameObject.layer)) == 0)
            return;

        PlayerContext target = other.GetComponentInParent<PlayerContext>();
        if (target != null && target.RuntimeState?.IsDead != true)
            owner?.ServerDamagePlayer(target);

        ServerDestroy();
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
