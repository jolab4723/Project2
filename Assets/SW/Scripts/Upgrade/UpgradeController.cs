using System;
using ItemSystem;
using TMPro;
using UnityEngine;
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
    private InventoryController ownershipSource;
    private UpgradeService upgradeService;
    private Action upgradeRequest;
    private YJ_LanguageManager languageManager;
    private UpgradeResult? displayedResult;
    private string displayedMessageKey;
    private string displayedMessageFallback;

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
        languageManager = YJ_LanguageManager.Instance;
        if (languageManager != null) languageManager.LanguageChanged += RefreshLanguage;
        SubscribeOwnership();
        RefreshUI();
    }

    private void OnDisable()
    {
        if (languageManager != null) languageManager.LanguageChanged -= RefreshLanguage;
        UnsubscribeOwnership();
        ClearItem();
    }

    /// <summary>
    /// 지갑 주인의 소유권 상실(삭제·월드 드롭·판매)을 구독해 선택을 해제한다.
    /// 선택 참조가 남으면 소유하지 않는 아이템에 골드를 내고 강화할 수 있었다.
    /// </summary>
    private void SubscribeOwnership()
    {
        UnsubscribeOwnership();
        ownershipSource = ResolveOwnerInventory();
        if (ownershipSource != null)
            ownershipSource.OnItemOwnershipLost += HandleItemOwnershipLost;
    }

    private void UnsubscribeOwnership()
    {
        if (ownershipSource != null)
            ownershipSource.OnItemOwnershipLost -= HandleItemOwnershipLost;
        ownershipSource = null;
    }

    private void HandleItemOwnershipLost(InventoryItem item)
    {
        if (selectedItem != null && ReferenceEquals(item?.itemData, selectedItem))
            ClearItem();
    }

    /// <summary>ReportRejection과 같은 기준으로 현재 지갑의 주인 인벤토리를 찾는다.</summary>
    private InventoryController ResolveOwnerInventory()
    {
        InventoryController owner = InventoryController.Instance;
        return owner != null && playerWallet != null && owner.PlayerWallet == playerWallet ? owner : null;
    }

    /// <summary>결제 직전 선택 아이템이 아직 주인의 가방이나 장착 슬롯에 있는지 확인한다.</summary>
    private bool IsSelectedItemOwned()
    {
        InventoryController owner = ResolveOwnerInventory();

        // 주인을 찾지 못하는 배선(별도 테스트 씬 등)은 기존 동작을 유지한다.
        if (owner == null)
            return true;

        if (owner.PlayerGrid != null)
        {
            foreach (InventoryItem item in owner.PlayerGrid.GetAllItems())
            {
                if (ReferenceEquals(item?.itemData, selectedItem))
                    return true;
            }
        }

        if (owner.EquipmentSystem != null)
        {
            foreach (var pair in owner.EquipmentSystem.GetEquippedItems())
            {
                if (ReferenceEquals(pair.Value?.itemData, selectedItem))
                    return true;
            }
        }

        return false;
    }

    private void RefreshLanguage(GameLanguage _)
    {
        var result = displayedResult;
        string key = displayedMessageKey;
        string fallback = displayedMessageFallback;
        RefreshUI();
        if (result.HasValue) ShowUpgradeMessage(result.Value, false);
        else if (key != null) ShowLocalizedMessage(key, fallback);
    }

    public bool BindPlayer(PlayerWallet wallet, EquipmentSystem equipment)
    {
        if (wallet == null || equipment == null)
            return false;

        ClearItem();
        playerWallet = wallet;
        equipmentSystem = equipment;
        upgradeService = new UpgradeService(playerWallet);
        if (isActiveAndEnabled)
            SubscribeOwnership();
        return true;
    }

    public void UnbindPlayer(PlayerWallet wallet)
    {
        if (playerWallet != wallet)
            return;

        ClearItem();
        UnsubscribeOwnership();
        playerWallet = null;
        equipmentSystem = null;
        upgradeService = new UpgradeService(null);
    }

    /// <summary>강화 입력을 외부 요청에 연결한다. 연결된 동안 로컬 비용과 강화 수치를 변경하지 않는다.</summary>
    public void BindUpgradeRequest(Action request)
    {
        if (request != null)
            upgradeRequest = request;
    }

    /// <summary>현재 연결된 요청이 지정한 요청과 같을 때만 해제한다.</summary>
    public void UnbindUpgradeRequest(Action request)
    {
        if (upgradeRequest == request)
            upgradeRequest = null;
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
    /// 외부 요청이 연결되어 있으면 그 요청을 전달한다.
    /// 그 외에는 선택된 아이템의 비용 결제와 강화 수치 변경을 UpgradeService에 요청하고,
    /// 장착 중인 아이템이면 성공 후 장비 변경 이벤트도 알린다.
    /// </summary>
    public void TryUpgrade()
    {
        if (upgradeRequest != null)
        {
            upgradeRequest();
            return;
        }

        if (selectedItem == null)
        {
            string selectionRequired = UpgradeMessageMapper.GetSelectionRequired(uiLabels);
            ShowMessage(selectionRequired);
            ReportRejection(selectionRequired);
            return;
        }

        if (!IsSelectedItemOwned())
        {
            ClearItem();
            ShowUpgradeMessage(UpgradeResult.InvalidItem);
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

    public void ShowUpgradeMessage(UpgradeResult result, bool reportRejection = true)
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
        displayedResult = result;
        if (reportRejection && result != UpgradeResult.Success) ReportRejection(message);
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
        displayedResult = null;
        displayedMessageKey = null;
        if (logText != null)
        {
            logText.text = message;
        }
    }

    public void ShowLocalizedMessage(string key, string fallback)
    {
        ShowMessage(GetUILabel(key, fallback));
        displayedMessageKey = key;
        displayedMessageFallback = fallback;
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
        TMP_FontAsset font = YJ_LanguageManager.Instance?.GetCurrentFont();
        if (font != null)
            foreach (TMP_Text text in new TMP_Text[] { upgradeLevelText, costText, logText, currentStatText, nextStatText })
                if (text != null) text.font = font;

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

}
