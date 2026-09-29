using System.Collections.Generic;
using Core;
using ItemSystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 기존 강화 선택 화면을 그대로 사용하면서 강화 버튼만 서버 요청으로 바꾸는 Mirror 테스트 어댑터다.
/// <para>원본과의 차이: <see cref="UpgradeController"/>는 아이템 선택과 미리보기 표시만 담당한다.
/// 이 컴포넌트가 원본 버튼의 로컬 <c>TryUpgrade</c> 호출을 실행 중에 끄고,
/// <see cref="PlayerInventorySync"/>의 서버 강화 요청을 대신 보낸다.</para>
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

    private void OnDestroy()
    {
        Unbind();

        if (upgradeButton != null)
            upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);
    }

    public void Bind(PlayerContext playerContext)
    {
        Unbind();
        EnsureInitialized();

        context = playerContext;
        inventorySync = context != null
            ? context.GetComponent<PlayerInventorySync>()
            : null;

        if (inventorySync != null)
            inventorySync.RequestCompleted += HandleRequestCompleted;
    }

    public void Unbind()
    {
        if (inventorySync != null)
            inventorySync.RequestCompleted -= HandleRequestCompleted;

        inventorySync = null;
        context = null;
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
        DisableOriginalUpgradeCall();
        upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);
        upgradeButton.onClick.AddListener(HandleUpgradeClicked);
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

    private void DisableOriginalUpgradeCall()
    {
        int listenerCount = upgradeButton.onClick.GetPersistentEventCount();
        for (int i = 0; i < listenerCount; i++)
        {
            if (upgradeButton.onClick.GetPersistentTarget(i) == upgradeController &&
                upgradeButton.onClick.GetPersistentMethodName(i) == nameof(UpgradeController.TryUpgrade))
            {
                // 테스트 Variant의 고정 버튼 한 개만 가로챈다. 정식 전환 때는 원본 버튼 연결을 교체한다.
                upgradeButton.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
        }
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

        if (inventorySync == null ||
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
