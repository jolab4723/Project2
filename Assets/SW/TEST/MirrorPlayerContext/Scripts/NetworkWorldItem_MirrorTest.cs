using ItemSystem;
using Mirror;
using UnityEngine;

/// <summary>
/// SW 원본 <c>WorldItemCube.prefab</c>의 Mirror C단계 테스트 Variant에만 붙는 네트워크 픽업이다.
/// 서버가 만든 기존 ItemSaveData JSON을 SyncVar로 전달하고, 각 Client의 ItemDataStorage를
/// 같은 instanceId와 옵션으로 복원한다. 획득 선점과 파괴 권한은 서버에만 있다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(ItemDataStorage), typeof(WorldItemPickupClaim))]
public sealed class NetworkWorldItem_MirrorTest : NetworkBehaviour
{
    [SerializeField] private ItemDataStorage storage;
    [SerializeField] private WorldItemPickupClaim pickupClaim;

    [SyncVar(hook = nameof(HandleSnapshotChanged))]
    private string snapshotJson;

    public string SnapshotJson => snapshotJson;

    private void Awake()
    {
        storage ??= GetComponent<ItemDataStorage>();
        pickupClaim ??= GetComponent<WorldItemPickupClaim>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        storage ??= GetComponent<ItemDataStorage>();
        pickupClaim ??= GetComponent<WorldItemPickupClaim>();
    }
#endif

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplySnapshot(snapshotJson);
    }

    [Server]
    public void InitializeServer(string newSnapshotJson)
    {
        snapshotJson = newSnapshotJson;
        ApplySnapshot(snapshotJson);
    }

    [Server]
    public bool TryClaimServer()
    {
        return pickupClaim != null && pickupClaim.TryClaim();
    }

    [Server]
    public void ReleaseClaimServer()
    {
        pickupClaim?.Release();
    }

    public ItemInstance CreateItemInstance()
    {
        return PlayerInventorySync_MirrorTest.CreateItemInstance(snapshotJson);
    }

    private void HandleSnapshotChanged(string _, string newSnapshotJson)
    {
        ApplySnapshot(newSnapshotJson);
    }

    private void ApplySnapshot(string value)
    {
        ItemInstance item = PlayerInventorySync_MirrorTest.CreateItemInstance(value);
        if (item == null || storage == null)
            return;

        storage.Init(item);
        if (TryGetComponent(out WorldItemRarityColorView rarityView))
            rarityView.Apply(item.definition.rarity);
    }
}
