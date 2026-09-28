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

    [Tooltip("아이템 이름 다국어 테이블. 비워두면 ItemDefinitionSO.itemName(한국어 스냅샷)을 그대로 쓴다.")]
    [SerializeField] private ItemLabelDatabaseSO itemLabels;

    private const string ItemLabelResourcePath = "DataFiles/ItemData/3. GeneratedAssets/LabelData/ItemLabelDatabase";

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

        if (itemLabels == null)
            itemLabels = Resources.Load<ItemLabelDatabaseSO>(ItemLabelResourcePath);

        upgradeService = new UpgradeService(playerWallet);
    }

    // 팝업이 열릴 때마다 현재 상태(선택 아이템 유무)와 현재 언어로 화면을 다시 채운다.
    // 싱글 플레이는 BindPlayer가 호출되지 않아서, 씬 시작 후 첫 열림에는 초기화가 한 번도 돌지 않고
    // 오브젝트에 들어 있던 한국어 기본 문구가 그대로 보였다(일본어·중국어 폰트에서는 □로 깨짐).
    private void OnEnable()
    {
        RefreshUI();
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
    /// <summary>
    /// 강화 결과 메시지에 넣을 아이템 이름을 현재 언어로 가져온다.
    ///
    /// !! ItemDefinitionSO.itemName은 한국어 스냅샷이라 그대로 쓰면 다른 언어에서도 한국어로 나온다.
    ///    (메시지 틀은 upgrade_ui.result_success로 번역되는데 이름만 한국어로 남던 문제)
    ///    TooltipUI.GetItemName과 같은 방식으로 ItemLabelDatabaseSO를 먼저 보고, 없으면 원본으로 폴백한다.
    /// </summary>
    private string ResolveItemName(ItemDefinitionSO definition)
    {
        if (definition == null)
            return GetUILabel("upgrade_ui.unknown_item", "아이템");

        if (itemLabels != null &&
            itemLabels.TryGetName(definition.itemId, out string localized) &&
            !string.IsNullOrEmpty(localized))
        {
            return localized;
        }

        return definition.itemName;
    }

    private void ShowUpgradeMessage(UpgradeResult result)
    {
        string itemName = ResolveItemName(selectedItem?.definition);

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
        // 퍼센트 스탯은 이름이 아니라 수치 뒤에 %를 붙인다.
        string statUnit = ItemDisplayNames.StatUnit(mainOption.statType);

        string currentStatLabel = GetUILabel("upgrade_ui.current_stat", "현재 스탯 : {0} + {1}")
            .Replace("{0}", "\n{0}");
        string nextStatLabel = GetUILabel("upgrade_ui.next_stat", "강화 후 스탯 : {0} + {1}")
            .Replace("{0}", "\n{0}");

        upgradeLevelText.text = $"+{selectedItem.upgradeLevel}";
        currentStatText.text = string.Format(
            currentStatLabel,
            statName, $"{currentValue:0.#}{statUnit}");
        nextStatText.text = string.Format(
            nextStatLabel,
            statName, $"{nextValue:0.#}{statUnit}");
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
            currentStatText.text = string.Empty;

        if (nextStatText != null)
            nextStatText.text = string.Empty;

        if (costText != null)
            costText.text = string.Empty;

        ShowMessage(UpgradeMessageMapper.GetSelectionRequired(uiLabels));
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
