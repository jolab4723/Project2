using Core;
using ItemSystem;
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

    private int usedFreeRerollCount;

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
    public int RemainingFreeRerolls => Mathf.Max(0, TotalFreeRerolls - usedFreeRerollCount);
    public bool IsFree => RemainingFreeRerolls > 0;

    private void Awake()
    {
        button ??= GetComponent<Button>();

        if (stockInitializer == null)
            stockInitializer = GetComponentInParent<ShopStockInitializer>();

        if (shopController == null)
            shopController = GetComponentInParent<ShopController>() ?? ShopController.Instance;
    }

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(HandleRerollClicked);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleRerollClicked);
    }

    private void HandleRerollClicked()
    {
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
