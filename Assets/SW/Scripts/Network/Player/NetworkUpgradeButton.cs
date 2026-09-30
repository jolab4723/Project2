using System.Collections.Generic;
using Core;
using ItemSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기존 강화 화면의 공용 요청을 Mirror 서버 강화 요청에 연결한다.
/// <para>원본 버튼의 <c>TryUpgrade</c> 연결은 유지하고,
/// <see cref="UpgradeController"/>의 외부 요청을 통해
/// <see cref="PlayerInventorySync"/>에 서버 강화를 요청한다.</para>
/// <para>서버 응답이 올 때까지 버튼을 잠그고, 성공하면 서버 스냅샷으로 교체된 같은 instance를
/// 다시 선택해 강화 수치와 비용을 갱신한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkUpgradeButton : MonoBehaviour
{
    [SerializeField] private UpgradeController upgradeController;
    [SerializeField] private Button upgradeButton;

    private PlayerContext context;
    private PlayerInventorySync inventorySync;
    private uint pendingRequestId;
    private string pendingInstanceId;
    private bool idleInteractable = true;
    private bool initialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        Bind(context);
    }

    private void OnDisable()
    {
        ReleaseBindings();
    }

    private void OnDestroy()
    {
        ReleaseBindings();
        context = null;
    }

    /// <summary>강화 화면을 로컬 플레이어에 연결한다. null이면 서버 연결 대기 상태를 유지한다.</summary>
    public void Bind(PlayerContext playerContext)
    {
        ReleaseBindings();
        EnsureInitialized();

        context = playerContext;
        if (!isActiveAndEnabled)
            return;

        if (upgradeController != null)
            upgradeController.BindUpgradeRequest(HandleUpgradeClicked);

        if (!initialized)
            return;

        inventorySync = context != null
            ? context.GetComponent<PlayerInventorySync>()
            : null;

        if (inventorySync != null)
            inventorySync.RequestCompleted += HandleRequestCompleted;
    }

    /// <summary>플레이어 연결과 대기 요청을 해제하고, 활성 화면은 서버 연결 대기 상태로 둔다.</summary>
    public void Unbind()
    {
        ReleaseBindings();
        context = null;

        // 연결 대기 중에도 원본 TryUpgrade가 로컬 골드와 아이템을 변경하지 않게 한다.
        if (isActiveAndEnabled && upgradeController != null)
            upgradeController.BindUpgradeRequest(HandleUpgradeClicked);
    }

    private void ReleaseBindings()
    {
        if (upgradeController != null)
            upgradeController.UnbindUpgradeRequest(HandleUpgradeClicked);

        if (inventorySync != null)
            inventorySync.RequestCompleted -= HandleRequestCompleted;

        inventorySync = null;
        pendingRequestId = 0;
        pendingInstanceId = null;

        if (upgradeButton != null)
            upgradeButton.interactable = idleInteractable;
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        upgradeController ??= GetComponentInChildren<UpgradeController>(true);
        upgradeButton ??= FindUpgradeButton();

        if (upgradeController == null || upgradeButton == null)
        {
            Debug.LogError(
                "[NetworkUpgradeButton] 강화 Controller 또는 버튼을 찾지 못했습니다.",
                this);
            return;
        }

        idleInteractable = upgradeButton.interactable;
        initialized = true;
    }

    private Button FindUpgradeButton()
    {
        if (upgradeController == null)
            return null;

        foreach (Button candidate in GetComponentsInChildren<Button>(true))
        {
            int listenerCount = candidate.onClick.GetPersistentEventCount();
            for (int i = 0; i < listenerCount; i++)
            {
                if (candidate.onClick.GetPersistentTarget(i) == upgradeController &&
                    candidate.onClick.GetPersistentMethodName(i) == nameof(UpgradeController.TryUpgrade))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private void HandleUpgradeClicked()
    {
        if (pendingRequestId != 0)
            return;

        ItemInstance selectedItem = upgradeController.SelectedItem;

        if (selectedItem?.definition == null)
        {
            upgradeController.ShowLocalizedMessage("upgrade_ui.selection_required", "강화할 아이템을 선택하세요.");
            return;
        }

        if (!initialized || inventorySync == null ||
            !inventorySync.TryRequestUpgradeItem(selectedItem.instanceId, out uint requestId))
        {
            upgradeController.ShowLocalizedMessage("upgrade_ui.request_failed", "서버 강화 요청을 시작하지 못했습니다.");
            return;
        }

        pendingRequestId = requestId;
        pendingInstanceId = selectedItem.instanceId;
        idleInteractable = upgradeButton.interactable;
        upgradeButton.interactable = false;
        upgradeController.ShowLocalizedMessage("upgrade_ui.request_pending", "서버가 강화 가능 여부와 골드를 확인하고 있습니다.");
    }

    private void HandleRequestCompleted(MirrorInventoryRequestCompleted completed)
    {
        if (completed.RequestId != pendingRequestId ||
            completed.Operation != MirrorInventoryOperation.UpgradeItem)
        {
            return;
        }

        string instanceId = pendingInstanceId;
        pendingRequestId = 0;
        pendingInstanceId = null;
        upgradeButton.interactable = idleInteractable;

        context?.GetComponent<NetworkShopPlayerState>()?.RestoreLocalGold();

        ItemInstance refreshedItem = FindOwnedItem(instanceId);
        if (refreshedItem != null)
            upgradeController.TrySetItem(refreshedItem);
        else
            upgradeController.ClearItem();

        if (completed.Result == MirrorInventoryRequestResult.Success)
        {
            upgradeController.ShowUpgradeMessage(UpgradeResult.Success);
            return;
        }

        ShowFailure(completed.Result);
    }

    private ItemInstance FindOwnedItem(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || context == null)
            return null;

        IReadOnlyList<InventoryItem> gridItems =
            context.Inventory?.GetAllInventoryItems();
        if (gridItems != null)
        {
            foreach (InventoryItem item in gridItems)
            {
                if (item?.itemData?.instanceId == instanceId)
                    return item.itemData;
            }
        }

        if (context.Equipment == null)
            return null;

        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in
                 context.Equipment.GetEquippedItems())
        {
            if (pair.Value?.itemData?.instanceId == instanceId)
                return pair.Value.itemData;
        }

        return null;
    }

    private void ShowFailure(MirrorInventoryRequestResult result)
    {
        switch (result)
        {
            case MirrorInventoryRequestResult.NotEnoughGold:
                upgradeController.ShowUpgradeMessage(UpgradeResult.NotEnoughGold); break;
            case MirrorInventoryRequestResult.UpgradeUnavailable:
                upgradeController.ShowUpgradeMessage(UpgradeResult.InvalidItem); break;
            case MirrorInventoryRequestResult.StaleRevision:
                upgradeController.ShowLocalizedMessage("upgrade_ui.state_changed", "아이템 상태가 먼저 바뀌어 강화를 취소했습니다. 다시 선택해 주세요."); break;
            case MirrorInventoryRequestResult.ItemUnavailable:
                upgradeController.ShowLocalizedMessage("upgrade_ui.item_missing", "인벤토리에서 해당 아이템을 찾지 못했습니다."); break;
            default:
                upgradeController.ShowLocalizedMessage("upgrade_ui.result_failed", "강화에 실패했습니다."); break;
        }
    }
}
