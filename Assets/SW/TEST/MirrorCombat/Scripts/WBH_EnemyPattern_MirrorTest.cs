using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// BH 일반 적 Pattern의 Mirror 테스트용 서버 AI다.
/// <para>원본과 달리 플레이어를 <c>Find</c>로 검색하지 않고, 서버가 등록한
/// <see cref="MirrorTestNetworkManager.ServerPlayerContexts"/>에서 살아 있는 가장 가까운 대상을 고른다.</para>
/// <para>현재 대상이 죽거나 접속이 끊긴 때에만 다시 고르며 이동과 공격 시작은 서버에서만 실행한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class WBH_EnemyPattern_MirrorTest : MonoBehaviour
{
    [SerializeField] private NetworkEnemyAuthority_MirrorTest authority;
    [SerializeField] private WBH_EnemyMovement movement;
    [SerializeField] private WBH_EnemyStatus status;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform firePoint;

    private PlayerContext target;

    public PlayerContext Target => target;
    public Transform FirePoint => firePoint != null ? firePoint : transform;

    private void Awake()
    {
        ResolveReferences();
        enabled = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif

    [Server]
    public void InitializeServer(NetworkEnemyAuthority_MirrorTest owner)
    {
        authority = owner;
        ResolveReferences();
        enabled = authority != null && authority.isServer;
    }

    [Server]
    public void StopServer()
    {
        target = null;
        authority?.ServerSetTarget(null);
        StopMovementIfPossible();
        enabled = false;
    }

    private void Update()
    {
        if (authority == null || !authority.isServer || authority.IsDead || status == null)
            return;

        if (!IsCurrentTargetValid())
            target = SelectNearestAlivePlayer();

        authority.ServerSetTarget(target);
        if (target == null)
        {
            StopMovementIfPossible();
            return;
        }

        Vector3 offset = target.transform.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        if (distance > status.AttackRange)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                movement.Move(target.transform.position);
            return;
        }

        StopMovementIfPossible();
        if (offset.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(offset.normalized);

        authority.ServerTryBeginAttack(target);
    }

    private bool IsCurrentTargetValid()
    {
        if (target == null || !target.gameObject.activeInHierarchy || target.RuntimeState?.IsDead == true)
            return false;

        MirrorTestNetworkManager manager = NetworkManager.singleton as MirrorTestNetworkManager;
        if (manager == null)
            return false;

        foreach (PlayerContext candidate in manager.ServerPlayerContexts)
        {
            if (candidate == target)
                return true;
        }

        return false;
    }

    private PlayerContext SelectNearestAlivePlayer()
    {
        MirrorTestNetworkManager manager = NetworkManager.singleton as MirrorTestNetworkManager;
        if (manager == null)
            return null;

        PlayerContext nearest = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (PlayerContext candidate in manager.ServerPlayerContexts)
        {
            if (candidate == null ||
                !candidate.gameObject.activeInHierarchy ||
                candidate.RuntimeState?.IsDead == true)
            {
                continue;
            }

            float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
                continue;

            nearest = candidate;
            nearestSqrDistance = sqrDistance;
        }

        return nearest;
    }

    private void StopMovementIfPossible()
    {
        if (movement != null && agent != null && agent.enabled && agent.isOnNavMesh)
            movement.Stop();
    }

    private void ResolveReferences()
    {
        authority ??= GetComponent<NetworkEnemyAuthority_MirrorTest>();
        movement ??= GetComponent<WBH_EnemyMovement>();
        status ??= GetComponent<WBH_EnemyStatus>();
        agent ??= GetComponent<NavMeshAgent>();

        if (firePoint != null)
            return;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == "FirePoint")
            {
                firePoint = child;
                break;
            }
        }
    }
}
