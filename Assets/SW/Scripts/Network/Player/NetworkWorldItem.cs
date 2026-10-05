using System.Collections;
using ItemSystem;
using Mirror;
using UnityEngine;

/// <summary>
/// SW 수정 : <c>WorldItemCube.prefab</c> 기반의 네트워크 월드 아이템을 서버 권한으로 관리한다.
/// 서버가 만든 기존 ItemSaveData JSON을 SyncVar로 전달하고, 각 Client의 ItemDataStorage를
/// 같은 instanceId와 옵션으로 복원한다. 획득 선점과 파괴 권한은 서버에만 있다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(ItemDataStorage), typeof(WorldItemPickupClaim))]
public sealed class NetworkWorldItem : NetworkBehaviour
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

    /// <summary>
    /// 서버 획득 성공 후 모든 Client에 동일한 흡수 연출을 시작하고, 연출 종료 뒤 서버에서만 제거한다.
    /// </summary>
    [Server]
    public void PlayPickupAndDestroy(uint pickerNetId)
    {
        WorldItemPickupPresentation.Prepare(gameObject);
        RpcPlayPickup(pickerNetId);
        StartCoroutine(DestroyAfterPickupPresentation());
    }

    public ItemInstance CreateItemInstance()
    {
        return PlayerInventorySync.CreateItemInstance(snapshotJson);
    }

    private void HandleSnapshotChanged(string _, string newSnapshotJson)
    {
        ApplySnapshot(newSnapshotJson);
    }

    /// <summary>
    /// 각 Client가 획득 플레이어를 찾아 네트워크 루트가 아닌 로컬 시각만 이동시킨다.
    /// </summary>
    [ClientRpc]
    private void RpcPlayPickup(uint pickerNetId)
    {
        Transform target =
            NetworkClient.spawned.TryGetValue(
                pickerNetId,
                out NetworkIdentity picker)
                ? picker.transform
                : null;

        StartCoroutine(WorldItemPickupPresentation.Play(
            gameObject,
            target));
    }

    /// <summary>
    /// ClientRpc가 표시될 짧은 시간을 보장한 뒤 네트워크 오브젝트를 권한 있는 서버에서 제거한다.
    /// </summary>
    [Server]
    private IEnumerator DestroyAfterPickupPresentation()
    {
        yield return new WaitForSecondsRealtime(
            WorldItemPickupPresentation.Duration);

        if (gameObject != null)
            NetworkServer.Destroy(gameObject);
    }

    private void ApplySnapshot(string value)
    {
        ItemInstance item = PlayerInventorySync.CreateItemInstance(value);
        if (item == null || storage == null)
            return;

        storage.Init(item);
        if (TryGetComponent(out WorldItemCategoryVisualView categoryVisualView))
            categoryVisualView.Apply(item.definition);

        if (TryGetComponent(out WorldItemRarityColorView rarityView))
            rarityView.Apply(item.definition.rarity);
    }
}
