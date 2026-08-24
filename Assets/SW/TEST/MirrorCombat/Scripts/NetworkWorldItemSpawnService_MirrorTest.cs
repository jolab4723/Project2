using Mirror;
using UnityEngine;

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
        Vector3 position,
        out NetworkWorldItem_MirrorTest pickup)
    {
        pickup = null;
        if (!NetworkServer.active || prefab == null || string.IsNullOrWhiteSpace(snapshotJson))
            return false;

        pickup = Object.Instantiate(prefab, position, Quaternion.identity);
        if (pickup == null)
            return false;

        pickup.InitializeServer(snapshotJson);
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
        Vector3 position,
        out NetworkWorldItem_MirrorTest pickup)
    {
        if (!TryCreateUnspawned(prefab, snapshotJson, position, out pickup))
            return false;

        if (TrySpawnCreated(pickup))
            return true;

        if (pickup != null)
            Object.Destroy(pickup.gameObject);
        pickup = null;
        return false;
    }
}
