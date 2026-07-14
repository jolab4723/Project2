using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UpgradeController : MonoBehaviour
{
    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private float upgradeCostMultiplier = 1.15f;
    [SerializeField] private EquipmentSystem equipmentSystem;
    [SerializeField] private TextMeshProUGUI upgradeLevelText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private TextMeshProUGUI currentStatText;
    [SerializeField] private TextMeshProUGUI nextStatText;
    [SerializeField] private Image itemImage;

    [SerializeField] private ItemDefinitionSO testItemDefinition;

    private FixedStatValue mainOptions;
    private ItemInstance selectedItem;
    
    private UpgradeService upgradeService;

    private void Awake()
    {
        upgradeService = new UpgradeService(playerWallet);
    }

    private void OnDisable()
    {
        ClearItem();
    }

    public bool TrySetItem(ItemInstance item)
    {
        if (item?.definition?.mainOptions == null ||
            item.definition.mainOptions.Length == 0)
        {
            Debug.LogWarning(
                "[UpgradeController] 강화할 아이템 또는 메인 옵션이 유효하지 않습니다.");

            return false;
        }

        bool isDifferentItem =
        !ReferenceEquals(selectedItem, item);

        selectedItem = item;
        mainOptions = item.definition.mainOptions[0];

        if (isDifferentItem)
        {
            ShowMessage(string.Empty);
        }

        RefreshUI();
        return true;
    }

    public void ClearItem()
    {
        selectedItem = null;
        mainOptions = default;
        ShowMessage(string.Empty);
        RefreshUI();
    }

    public void TryUpgrade()
    {
        if (selectedItem == null)
        {
            ShowMessage(UpgradeMessageMapper.SelectionRequired);
            return;
        }

        int cost = GetUpgradeCost(selectedItem);

        UpgradeResult result =
            upgradeService.TryUpgrade(selectedItem, cost);

        if (result == UpgradeResult.Success)
        {
            equipmentSystem?.NotifyEquippedItemChanged(selectedItem);
            RefreshUI();
        }

        if (result == UpgradeResult.WalletUnavailable)
        {
            Debug.LogError(
                "[UpgradeController] PlayerWallet이 연결되지 않아 강화할 수 없습니다.");
        }

        ShowUpgradeMessage(result);
    }
    private void ShowUpgradeMessage(UpgradeResult result)
    {
        string itemName =
            selectedItem?.definition != null
                ? selectedItem.definition.itemName
                : "아이템";

        int upgradeLevel =
            selectedItem != null
                ? selectedItem.upgradeLevel
                : 0;

        ShowMessage(
            UpgradeMessageMapper.GetMessage(
                result,
                itemName,
                upgradeLevel));
    }

    private void ShowMessage(string message)
    {
        if (logText != null)
        {
            logText.text = message;
        }
    }
    private int GetUpgradeCost(ItemInstance item)
    {
        // 추후 연동
        int upgradeCost = Mathf.CeilToInt(500 * Mathf.Pow(upgradeCostMultiplier, item.upgradeLevel) / 10)  * 10;
        return upgradeCost;
    }

    private float GetMainOptionValue(ItemInstance item, int previewUpgradeLevel)
    {
        if (item == null || item.definition == null)
            return 0f;

        if (item.definition.mainOptions == null || item.definition.mainOptions.Length == 0)
            return 0f;

        float baseValue = mainOptions.value;
        float bonusPerLevel = item.definition.upgradeBonusPerLevel;

        return baseValue * (1f + previewUpgradeLevel * bonusPerLevel);
    }

    private void RefreshUI()
    {
        if (selectedItem?.definition == null)
        {
            ShowEmptyState();
            return;
        }

        if (itemImage != null)
        {
            itemImage.enabled = true;
            itemImage.sprite = selectedItem.definition.icon;
        }

        float currentValue = GetMainOptionValue(selectedItem, selectedItem.upgradeLevel);
        float nextValue = GetMainOptionValue(selectedItem, selectedItem.upgradeLevel + 1);

        upgradeLevelText.text = $"+{selectedItem.upgradeLevel.ToString()}";
        currentStatText.text = $"현재 스탯 : {ItemDisplayNames.StatNames[mainOptions.statType]} + {currentValue:0.#}";
        nextStatText.text = $"강화 후 스탯 : {ItemDisplayNames.StatNames[mainOptions.statType]} + {nextValue:0.#}";
        costText.text = $"강화비용 : {GetUpgradeCost(selectedItem)}";
    }

    private void ShowEmptyState()
    {
        if (itemImage != null)
        {
            itemImage.sprite = null;
            itemImage.enabled = false;
        }

        if (upgradeLevelText != null)
            upgradeLevelText.text = "";

        if (currentStatText != null)
        {
            currentStatText.text = UpgradeMessageMapper.SelectionRequired;
        }

        if (nextStatText != null)
            nextStatText.text = string.Empty;

        if (costText != null)
            costText.text = string.Empty;
    }

    private void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            //ItemInstance instance = ItemOptionRoller.Generate(testItemDefinition);
            //SetItem(instance);
        }

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            playerWallet.AddGold(999999999);

        }
    }
}
