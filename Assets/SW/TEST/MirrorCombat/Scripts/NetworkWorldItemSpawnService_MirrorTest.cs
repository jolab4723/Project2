using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 플레이어 드래그 드랍과 적 보상 드랍이 함께 사용하는 Mirror 테스트용 생성 경계다.
/// <para>아이템 내용은 기존 JSON 스냅샷을 그대로 사용하고, 서버만 생성·등록·파괴를 수행한다.</para>
/// </summary>
public static class NetworkWorldItemSpawnService_MirrorTest
{
    [Server]
    public static bool TryCreateUnspawned(
        NetworkWorldItem_MirrorTest prefab,
        string snapshotJson,
        Vector3 origin,
        Vector3 position,
        out NetworkWorldItem_MirrorTest pickup)
    {
        pickup = null;
        if (!NetworkServer.active || prefab == null || string.IsNullOrWhiteSpace(snapshotJson))
            return false;

        if (!TryResolveDropPosition(origin, position, out position))
            return false;

        pickup = Object.Instantiate(prefab, position, Quaternion.identity);
        if (pickup == null)
            return false;

        pickup.InitializeServer(snapshotJson);
        return true;
    }

    /// <summary>드롭이 벽이나 절벽을 넘어가지 않도록 출발점과 연결된 이동 영역 안에 둔다.</summary>
    public static bool TryResolveDropPosition(Vector3 origin, Vector3 desired, out Vector3 position)
    {
        position = default;
        if (!NavMesh.SamplePosition(origin, out NavMeshHit start, 2f, NavMesh.AllAreas))
        {
            // 점프 중 처치된 적도 바로 아래 지면에 보상을 남긴다.
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit floor, 6f,
                    LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore) ||
                !NavMesh.SamplePosition(floor.point, out start, 0.5f, NavMesh.AllAreas))
                return false;
        }

        desired.y = start.position.y;
        if (NavMesh.Raycast(start.position, desired, out NavMeshHit edge, NavMesh.AllAreas))
            desired = Vector3.MoveTowards(edge.position, start.position, 0.2f);

        if (!NavMesh.SamplePosition(desired, out NavMeshHit ground, 0.25f, NavMesh.AllAreas) ||
            NavMesh.Raycast(start.position, ground.position, out _, NavMesh.AllAreas))
            ground = start;

        position = ground.position + Vector3.up * 0.5f;
        return true;
    }

    [Server]
    public static bool TrySpawnCreated(NetworkWorldItem_MirrorTest pickup)
    {
        if (!NetworkServer.active || pickup == null)
            return false;

        NetworkServer.Spawn(pickup.gameObject);
        return pickup.netId != 0;
    }

    [Server]
    public static bool TrySpawnNew(
        NetworkWorldItem_MirrorTest prefab,
        string snapshotJson,
        Vector3 origin,
        Vector3 position,
        out NetworkWorldItem_MirrorTest pickup)
    {
        if (!TryCreateUnspawned(prefab, snapshotJson, origin, position, out pickup))
            return false;

        if (TrySpawnCreated(pickup))
            return true;

        if (pickup != null)
            Object.Destroy(pickup.gameObject);
        pickup = null;
        return false;
    }
}
