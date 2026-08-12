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
    [SerializeField] private Image rarityBackground;
    [SerializeField] private Image rarityFrame;

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

    /// <summary>
    /// 강화 가능한 아이템을 현재 선택 항목으로 지정하고 표시를 갱신한다.
    /// </summary>
    public bool TrySetItem(ItemInstance item)
    {
        if (!UpgradeService.CanUpgrade(item))
        {
            Debug.LogWarning("[UpgradeController] 강화할 수 없는 아이템입니다.");

            return false;
        }

        bool isDifferentItem =
        !ReferenceEquals(selectedItem, item);

        selectedItem = item;

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
        ShowMessage(string.Empty);
        RefreshUI();
    }

    /// <summary>
    /// 선택된 아이템의 비용 결제와 강화 수치 변경을 UpgradeService에 요청한다.
    /// 장착 중인 아이템이면 성공 후 장비 변경 이벤트도 알린다.
    /// </summary>
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

        float baseValue = item.definition.mainOptions[0].value;
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

        ApplyRarityVisuals(selectedItem.definition.rarity);

        float currentValue = GetMainOptionValue(selectedItem, selectedItem.upgradeLevel);
        float nextValue = GetMainOptionValue(selectedItem, selectedItem.upgradeLevel + 1);
        FixedStatValue mainOption = selectedItem.definition.mainOptions[0];

        upgradeLevelText.text = $"+{selectedItem.upgradeLevel}";
        currentStatText.text = $"현재 스탯 : {ItemDisplayNames.StatNames[mainOption.statType]} + {currentValue:0.#}";
        nextStatText.text = $"강화 후 스탯 : {ItemDisplayNames.StatNames[mainOption.statType]} + {nextValue:0.#}";
        costText.text = $"강화비용 : {GetUpgradeCost(selectedItem)}";
    }

    private void ShowEmptyState()
    {
        if (itemImage != null)
        {
            itemImage.sprite = null;
            itemImage.enabled = false;
        }

        SetRarityVisualsEnabled(false);

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

    private void ApplyRarityVisuals(ItemRarity rarity)
    {
        if (!ItemDisplayNames.GradeColorHex.TryGetValue(rarity, out string colorHex) ||
            !ColorUtility.TryParseHtmlString(colorHex, out Color gradeColor))
        {
            gradeColor = Color.white;
        }

        gradeColor.a = 1f;
        Color frameColor = gradeColor;
        frameColor.a = 0.5f;

        if (rarityBackground != null)
        {
            rarityBackground.color = gradeColor;
            rarityBackground.enabled = true;
        }

        if (rarityFrame != null)
        {
            rarityFrame.color = frameColor;
            rarityFrame.enabled = true;
        }
    }

    private void SetRarityVisualsEnabled(bool isEnabled)
    {
        if (rarityBackground != null)
            rarityBackground.enabled = isEnabled;

        if (rarityFrame != null)
            rarityFrame.enabled = isEnabled;
    }

    private void Update()
    {
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            playerWallet.AddGold(999999999);

        }
    }
}
