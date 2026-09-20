using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InventoryView : MonoBehaviour
{
    [Header("Optional single-player owner")]
    [SerializeField] private InventoryController initialOwner;

    [Header("Grid view")]
    [SerializeField] private RectTransform gridRect;
    [SerializeField] private RectTransform itemsContainer;
    [SerializeField] private GridHighlightUI highlight;

    [Header("Inventory view")]
    [SerializeField] private EquipSlotUI[] equipmentSlots;
    [SerializeField] private TMP_Text logText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private InventoryItemUISpawner itemSpawner;
    [SerializeField] private TooltipManager tooltipManager;
    [SerializeField] private ShopController shopController;
    [SerializeField] private UpgradeController upgradeController;

    private InventoryController owner;
    private InventoryController boundPlayerServicesOwner;

    public InventoryController Owner => owner;
    public EquipSlotUI[] EquipmentSlots => equipmentSlots;

    private void OnEnable()
    {
        if (initialOwner != null)
            Bind(initialOwner);
    }

    private void OnDisable()
    {
        Unbind();
    }

    public bool Bind(InventoryController newOwner)
    {
        if (newOwner == null ||
            newOwner.PlayerGrid == null ||
            newOwner.PlayerWallet == null ||
            gridRect == null ||
            itemsContainer == null ||
            itemSpawner == null)
        {
            Debug.LogError(
                "[InventoryView] owner 또는 필수 화면 참조가 비어 있습니다.",
                this);
            return false;
        }

        Unbind();

        owner = newOwner;
        owner.PlayerGrid.BindView(gridRect, itemsContainer, highlight);
        owner.BindEquipmentSlots(equipmentSlots);
        owner.OnLogMessage += HandleLogMessage;
        owner.PlayerWallet.OnGoldChanged += HandleGoldChanged;
        HandleGoldChanged(owner.PlayerWallet.Gold);

        if (itemSpawner.Bind(owner, this))
            return true;

        Unbind();
        return false;
    }

    public bool Bind(PlayerContext context)
    {
        if (context == null || !context.IsComplete || !Bind(context.Inventory))
            return false;

        if (tooltipManager != null)
            tooltipManager.Bind(context.Stats, context.Equipment);

        bool shopBound = shopController == null || shopController.BindPlayer(context.Inventory);
        bool upgradeBound = upgradeController == null ||
            upgradeController.BindPlayer(context.Wallet, context.Equipment);

        if (shopBound && upgradeBound)
        {
            boundPlayerServicesOwner = context.Inventory;
            return true;
        }

        if (shopBound)
            shopController?.UnbindPlayer(context.Inventory);

        if (upgradeBound)
            upgradeController?.UnbindPlayer(context.Wallet);

        Unbind();
        return false;
    }

    public void Unbind()
    {
        if (boundPlayerServicesOwner != null)
        {
            shopController?.UnbindPlayer(boundPlayerServicesOwner);
            upgradeController?.UnbindPlayer(boundPlayerServicesOwner.PlayerWallet);
            boundPlayerServicesOwner = null;
        }

        if (owner == null)
            return;

        itemSpawner?.Unbind(owner);
        tooltipManager?.Unbind(owner.EquipmentSystem);
        owner.OnLogMessage -= HandleLogMessage;
        owner.PlayerWallet.OnGoldChanged -= HandleGoldChanged;
        owner.UnbindEquipmentSlots(equipmentSlots);
        owner.PlayerGrid.UnbindView(itemsContainer);
        owner = null;
    }

    private void HandleLogMessage(string message)
    {
        if (logText != null)
            logText.text = message;
    }

    private void HandleGoldChanged(int gold)
    {
        if (goldText != null)
            goldText.text = gold.ToString();
    }
}
