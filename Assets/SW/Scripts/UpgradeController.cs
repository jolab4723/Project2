using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UpgradeController : MonoBehaviour
{
    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private int fixedUpgradeCost = 500;

    [SerializeField] private TextMeshProUGUI upgradeLevelText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private TextMeshProUGUI currentStatText;
    [SerializeField] private TextMeshProUGUI nextStatText;
    [SerializeField] private Image itemImage;

    [SerializeField] private ItemDefinitionSO testItemDefinition;

    private FixedStatValue mainOptions;

    private ItemInstance selectedItem;

    public void SetItem(ItemInstance item)
    {
        selectedItem = item;
        mainOptions = selectedItem.definition.mainOptions[0];
        RefreshUI();
    }

    public void ClearItem()
    {
        selectedItem = null;
        RefreshUI();
    }

    public void TryUpgrade()
    {
        if (selectedItem == null)
            return;

        int cost = GetUpgradeCost(selectedItem);

        if (!playerWallet.TrySpendGold(cost))
        {
            logText.text = "골드가 부족합니다.";
            return;
        }

        selectedItem.upgradeLevel++;

        logText.text = $"+{selectedItem.upgradeLevel} 강화 성공";

        RefreshUI();
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
        float bonusPerLevel = 0.1f;

        return baseValue * (1f + previewUpgradeLevel * bonusPerLevel);
    }

    private void RefreshUI()
    {
        float currentValue = GetMainOptionValue(selectedItem, selectedItem.upgradeLevel);
        float nextValue = GetMainOptionValue(selectedItem, selectedItem.upgradeLevel + 1);

        itemImage.sprite = selectedItem.definition.icon;
        upgradeLevelText.text = $"+{selectedItem.upgradeLevel.ToString()}";
        currentStatText.text = $"현재 스탯 : {ItemDisplayNames.StatNames[mainOptions.statType]} + {currentValue:0.#}";
        nextStatText.text = $"강화 후 스탯 : {ItemDisplayNames.StatNames[mainOptions.statType]} + {nextValue:0.#}";
        costText.text = $"강화비용 : {GetUpgradeCost(selectedItem)}";
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
