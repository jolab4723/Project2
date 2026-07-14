using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UpgradeController : MonoBehaviour
{
    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private int fixedUpgradeCost = 500;
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
    public bool TrySetItem(ItemInstance item)
    {
        if (item?.definition?.mainOptions == null ||
            item.definition.mainOptions.Length == 0)
        {
            Debug.LogWarning(
                "[UpgradeController] 강화할 아이템 또는 메인 옵션이 유효하지 않습니다.");

            return false;
        }

        selectedItem = item;
        mainOptions = item.definition.mainOptions[0];

        RefreshUI();
        return true;
    }

    public void ClearItem()
    {
        selectedItem = null;
        mainOptions = default;
        RefreshUI();
    }

    public void TryUpgrade()
    {
        if (selectedItem == null)
        {
            logText.text = "강화할 아이템을 선택하세요.";
            return;
        }

        int cost = GetUpgradeCost(selectedItem);

        UpgradeResult result =
            upgradeService.TryUpgrade(selectedItem, cost);

        switch (result)
        {
            case UpgradeResult.Success:
                equipmentSystem?.NotifyEquippedItemChanged(selectedItem);

                logText.text =
                    $"+{selectedItem.upgradeLevel} 강화 성공";

                RefreshUI();
                break;

            case UpgradeResult.NotEnoughGold:
                logText.text = "골드가 부족합니다.";
                break;

            case UpgradeResult.InvalidItem:
                logText.text = "강화할 수 없는 아이템입니다.";
                break;

            case UpgradeResult.InvalidCost:
                logText.text = "강화 비용 설정이 올바르지 않습니다.";
                break;

            case UpgradeResult.WalletUnavailable:
                logText.text = "플레이어 지갑이 연결되지 않았습니다.";
                break;
        }
    }

    private int GetUpgradeCost(ItemInstance item)
    {
        // 추후 연동
        int upgradeCost = Mathf.CeilToInt(500 * Mathf.Pow(1.15f, item.upgradeLevel) / 10)  * 10;
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

        itemImage.sprite = selectedItem.definition.icon;
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
            upgradeLevelText.text = "-";

        if (currentStatText != null)
            currentStatText.text = "강화할 아이템을 선택하세요.";

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
