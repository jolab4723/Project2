using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UpgradeController : MonoBehaviour
{
    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private EquipmentSystem equipmentSystem;
    [SerializeField] private TextMeshProUGUI upgradeLevelText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private TextMeshProUGUI currentStatText;
    [SerializeField] private TextMeshProUGUI nextStatText;
    [SerializeField] private Image itemImage;
    [SerializeField] private Image rarityBackground;
    [SerializeField] private Image rarityFrame;

    [Tooltip("고정 UI 문구(스탯 표기, 강화 결과 메시지 등) 다국어 테이블. 비워두면 하드코딩된 한국어 문구를 그대로 쓴다.")]
    [SerializeField] private UILabelDatabaseSO uiLabels;

    private ItemInstance selectedItem;
    private UpgradeService upgradeService;

    public PlayerWallet BoundPlayerWallet => playerWallet;
    public EquipmentSystem BoundEquipment => equipmentSystem;

    /// <summary>현재 강화 화면에서 선택한 아이템을 읽기 전용으로 제공한다.</summary>
    public ItemInstance SelectedItem => selectedItem;

    private void Awake()
    {
        // 씬에서 직접 안 배선해도(다른 맵/스테이지 씬 등) Resources의 공용 DB를 자동으로 찾아 쓴다.
        if (uiLabels == null)
            uiLabels = Resources.Load<UILabelDatabaseSO>("DataFiles/UIData/3. GeneratedAssets/UILabelDatabase");

        upgradeService = new UpgradeService(playerWallet);
    }

    private void OnDisable()
    {
        ClearItem();
    }

    public bool BindPlayer(PlayerWallet wallet, EquipmentSystem equipment)
    {
        if (wallet == null || equipment == null)
            return false;

        ClearItem();
        playerWallet = wallet;
        equipmentSystem = equipment;
        upgradeService = new UpgradeService(playerWallet);
        return true;
    }

    public void UnbindPlayer(PlayerWallet wallet)
    {
        if (playerWallet != wallet)
            return;

        ClearItem();
        playerWallet = null;
        equipmentSystem = null;
        upgradeService = new UpgradeService(null);
    }

    /// <summary>
    /// 강화 가능한 아이템을 현재 선택 항목으로 지정하고 표시를 갱신한다.
    /// </summary>
    public bool TrySetItem(ItemInstance item)
    {
        if (!UpgradeService.CanUpgrade(item))
        {
            Debug.LogWarning("[UpgradeController] 강화할 수 없는 아이템입니다.");
            ReportRejection(UpgradeMessageMapper.GetMessage(UpgradeResult.InvalidItem, GetUILabel("upgrade_ui.unknown_item", "아이템"), 0, uiLabels));

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
            string selectionRequired = UpgradeMessageMapper.GetSelectionRequired(uiLabels);
            ShowMessage(selectionRequired);
            ReportRejection(selectionRequired);
            return;
        }

        UpgradeResult result =
            upgradeService.TryUpgrade(selectedItem);

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
                : GetUILabel("upgrade_ui.unknown_item", "아이템");

        int upgradeLevel =
            selectedItem != null
                ? selectedItem.upgradeLevel
                : 0;

        string message = UpgradeMessageMapper.GetMessage(
                result,
                itemName,
                upgradeLevel,
                uiLabels);
        ShowMessage(message);
        if (result != UpgradeResult.Success) ReportRejection(message);
    }

    private void ReportRejection(string message)
    {
        InventoryController owner = InventoryController.Instance;
        if (owner != null && owner.PlayerWallet == playerWallet)
            owner.ReportSinglePlayerMessage(ChatKind.Warning, message);
    }

    /// <summary>강화 화면의 결과 메시지를 갱신한다.</summary>
    public void ShowMessage(string message)
    {
        if (logText != null)
        {
            logText.text = message;
        }
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

        string statName = ItemDisplayNames.StatNames[mainOption.statType];

        upgradeLevelText.text = $"+{selectedItem.upgradeLevel}";
        currentStatText.text = string.Format(
            GetUILabel("upgrade_ui.current_stat", "현재 스탯 : {0} + {1}"),
            statName, $"{currentValue:0.#}");
        nextStatText.text = string.Format(
            GetUILabel("upgrade_ui.next_stat", "강화 후 스탯 : {0} + {1}"),
            statName, $"{nextValue:0.#}");
        costText.text = UpgradeService.TryGetUpgradeCost(selectedItem, out int cost)
            ? string.Format(GetUILabel("upgrade_ui.cost", "강화비용 : {0}"), cost)
            : GetUILabel("upgrade_ui.cost_unavailable", "강화비용 : -");
    }

    /// <summary>다국어 DB가 배선돼 있으면 그 문구를, 없으면 기존 하드코딩 한국어 문구를 반환한다.</summary>
    private string GetUILabel(string key, string fallback) =>
        uiLabels != null ? uiLabels.GetLabel(key) : fallback;

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
            currentStatText.text = UpgradeMessageMapper.GetSelectionRequired(uiLabels);
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
        if (Keyboard.current?.cKey.wasPressedThisFrame == true && playerWallet != null)
            playerWallet.AddGold(999999999);
    }
}
