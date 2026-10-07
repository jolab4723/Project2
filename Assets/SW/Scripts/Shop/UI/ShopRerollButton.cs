using Core;
using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class ShopRerollButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private ShopStockInitializer stockInitializer;
    [SerializeField] private ShopController shopController;

    [Header("리롤 규칙")]
    [SerializeField, Min(0)] private int baseFreeRerollCount = 1;
    [SerializeField, Min(0)] private int paidRerollCost = 100;

    [Header("표시")]
    [Tooltip("비용 + '리롤' 문구를 함께 그리는 텍스트. 고정 문구가 섞여 있어 UILabelText가 아니라 이 스크립트가 채운다.")]
    [SerializeField] private TMP_Text costText;

    [Tooltip("남은 무료 리롤 횟수 텍스트.")]
    [SerializeField] private TMP_Text countText;

    [Tooltip("고정 문구 다국어 테이블. 비워두면 Resources의 공용 DB를 자동으로 찾아 쓴다.")]
    [SerializeField] private UILabelDatabaseSO uiLabels;

    private const string UiLabelResourcePath = "DataFiles/UIData/3. GeneratedAssets/UILabelDatabase";

    private int usedFreeRerollCount;
    private System.Action rerollRequest;
    private System.Func<int> remainingFreeRerolls;
    private System.Func<int> rerollCost;

    public int BaseFreeRerollCount => baseFreeRerollCount;
    public int PaidRerollCost => paidRerollCost;
    public int UsedFreeRerollCount => usedFreeRerollCount;

    public int ExtraFreeRerollCount
    {
        get
        {
            var passiveManager = PassiveSkillManager.Instance;
            if (passiveManager == null)
                return 0;

            int level = passiveManager.GetCurrentLevel(PassiveSkillId.ShopEnhance);
            if (level <= 0)
                return 0;

            var definition = passiveManager.GetDefinition(PassiveSkillId.ShopEnhance);
            return definition != null ? definition.extraRerollCount : 0;
        }
    }

    public int TotalFreeRerolls => Mathf.Max(0, baseFreeRerollCount + ExtraFreeRerollCount);
    public int RemainingFreeRerolls => remainingFreeRerolls != null
        ? Mathf.Max(0, remainingFreeRerolls())
        : Mathf.Max(0, TotalFreeRerolls - usedFreeRerollCount);
    public bool IsFree => RemainingFreeRerolls > 0;

    public void BindRerollRequest(System.Action request, System.Func<int> remaining, System.Func<int> cost)
    {
        rerollRequest = request;
        remainingFreeRerolls = remaining;
        rerollCost = cost;
        RefreshView();
    }

    public void UnbindRerollRequest(System.Action request)
    {
        if (rerollRequest != request)
            return;

        rerollRequest = null;
        remainingFreeRerolls = null;
        rerollCost = null;
        RefreshView();
    }

    private void Awake()
    {
        button ??= GetComponent<Button>();

        if (stockInitializer == null)
            stockInitializer = GetComponentInParent<ShopStockInitializer>();

        if (shopController == null)
            shopController = GetComponentInParent<ShopController>() ?? ShopController.Instance;

        if (uiLabels == null)
            uiLabels = Resources.Load<UILabelDatabaseSO>(UiLabelResourcePath);
    }

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(HandleRerollClicked);

        // 상점을 다시 열 때마다 최신 상태로 그린다(패시브 해금으로 무료 횟수가 늘었을 수 있다).
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;

        RefreshView();
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleRerollClicked);

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(GameLanguage _) => RefreshView();

    /// <summary>
    /// 비용/남은 횟수 표시를 현재 상태로 다시 그린다.
    ///
    /// !! 비용 텍스트는 숫자와 "리롤" 문구가 섞여 있어 UILabelText(고정 문구 전용)를 쓸 수 없다.
    ///    UILabelText는 언어가 바뀔 때마다 DB 문구로 텍스트를 통째로 덮어써서 런타임 값이 날아간다.
    ///    대신 포맷 문자열 하나를 받아 여기서 조립한다 - 언어별로 숫자와 문구의 어순도 바꿀 수 있다.
    /// </summary>
    public void RefreshView()
    {
        if (costText != null)
        {
            costText.text = IsFree
                ? GetLabel("shop_ui.reroll_free", "무료 리롤")
                : string.Format(GetLabel("shop_ui.reroll_cost_format", "<color=#FFEB04>{0}</color> 리롤"), rerollCost != null ? rerollCost() : paidRerollCost);
        }

        if (countText != null)
            countText.text = string.Format(GetLabel("shop_ui.reroll_count_format", "[ {0} ]"), RemainingFreeRerolls);
    }

    private string GetLabel(string key, string fallback)
    {
        if (uiLabels == null)
            return fallback;

        string value = uiLabels.GetLabel(key);
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }

    private void HandleRerollClicked()
    {
        // 외부 요청이 연결되어 있으면 로컬 재고나 골드를 변경하지 않는다.
        if (rerollRequest != null)
        {
            rerollRequest();
            return;
        }

        if (stockInitializer == null)
        {
            stockInitializer = GetComponentInParent<ShopStockInitializer>();
            if (stockInitializer == null)
            {
                Debug.LogWarning("[ShopRerollButton] ShopStockInitializer가 연결되지 않았습니다.", this);
                return;
            }
        }

        if (shopController == null)
            shopController = GetComponentInParent<ShopController>() ?? ShopController.Instance;

        if (IsFree)
        {
            if (!stockInitializer.TryRerollStock())
            {
                shopController?.SetLogMessage("상점 새로고침에 실패했습니다.", isWarning: true);
                return;
            }

            usedFreeRerollCount++;
            RefreshView();
            string msg = $"상점 상품을 새로고침했습니다. (남은 무료: {RemainingFreeRerolls}회)";
            shopController?.SetLogMessage(msg);
            return;
        }

        PlayerWallet wallet = ResolvePlayerWallet();
        if (wallet == null)
        {
            string msg = "플레이어 지갑 정보를 찾을 수 없습니다.";
            shopController?.SetLogMessage(msg, isWarning: true);
            return;
        }

        if (!wallet.TrySpendGold(paidRerollCost))
        {
            string msg = $"골드가 부족하여 새로고침할 수 없습니다. (필요: {paidRerollCost}G / 보유: {wallet.Gold}G)";
            shopController?.SetLogMessage(msg, isWarning: true);
            return;
        }

        if (!stockInitializer.TryRerollStock())
        {
            wallet.AddGold(paidRerollCost);
            shopController?.SetLogMessage("상점 새로고침에 실패하여 골드가 환불되었습니다.", isWarning: true);
            return;
        }

        RefreshView();
        string paidMsg = $"{paidRerollCost}골드를 지불하고 상점 상품을 새로고침했습니다.";
        shopController?.SetLogMessage(paidMsg);
    }

    private PlayerWallet ResolvePlayerWallet()
    {
        if (shopController != null && shopController.BoundPlayer != null && shopController.BoundPlayer.PlayerWallet != null)
            return shopController.BoundPlayer.PlayerWallet;

        if (InventoryController.Instance != null && InventoryController.Instance.PlayerWallet != null)
            return InventoryController.Instance.PlayerWallet;

        return null;
    }
}
