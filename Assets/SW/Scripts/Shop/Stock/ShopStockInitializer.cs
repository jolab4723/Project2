using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ShopController))]
public sealed class ShopStockInitializer : MonoBehaviour
{
    [SerializeField] private ShopController shopController;
    [SerializeField] private ItemDatabaseSO itemDatabase;

    [SerializeField, Min(1)]
    private int initialStockCount = 6;

    [SerializeField]
    private ShopRarityChance[] rarityChances;

    private ShopStockRollService rollService;
    private bool initialized;

    internal ItemDatabaseSO ItemDatabase => itemDatabase;
    internal int InitialStockCount => initialStockCount;
    internal ShopRarityChance[] RarityChances => rarityChances;

    private void Awake()
    {
        if (shopController == null)
        {
            shopController = GetComponent<ShopController>();
        }

        rollService = new ShopStockRollService();
    }

    private void Start()
    {
        InitializeInitialStock();
    }

    private void InitializeInitialStock()
    {
        if (initialized)
            return;

        if (!ValidateConfiguration())
            return;

        initialized = true;

        GenerateStock();
    }

    [ContextMenu("TEST/Reroll Shop Stock")]
    public void RerollStock()
    {
        TryRerollStock();
    }

    public bool TryRerollStock()
    {
        if (!ValidateConfiguration())
            return false;

        initialized = true;

        // 새 상품을 먼저 모두 만든 뒤 한 번에 교체한다. 실패하면 기존 재고가 그대로 남는다.
        if (!TryRollItems(out List<InventoryItem> items))
            return false;

        if (!shopController.TryReplaceGeneratedStock(items))
        {
            Debug.LogWarning(
                "[ShopStockInitializer] 새 상품을 모두 배치하지 못해 기존 재고를 유지했습니다.");
            return false;
        }

        return true;
    }

    private bool TryRollItems(out List<InventoryItem> items)
    {
        items = new List<InventoryItem>(initialStockCount);

        for (int i = 0; i < initialStockCount; i++)
        {
            if (!rollService.TryRoll(
                    itemDatabase,
                    rarityChances,
                    out ItemDefinitionSO selectedDefinition))
            {
                Debug.LogError(
                    "[ShopStockInitializer] 판매 상품 추첨에 실패했습니다.");
                return false;
            }

            ItemInstance itemData =
                ItemDataCreator.CreateItemData(selectedDefinition);

            if (itemData == null)
            {
                Debug.LogError(
                    $"[ShopStockInitializer] " +
                    $"{selectedDefinition.itemName} 인스턴스 생성에 실패했습니다.");
                return false;
            }

            items.Add(new InventoryItem(itemData));
        }

        return true;
    }

    private int GenerateStock()
    {
        int generatedCount = 0;

        for (int i = 0; i < initialStockCount; i++)
        {
            if (!rollService.TryRoll(
                    itemDatabase,
                    rarityChances,
                    out ItemDefinitionSO selectedDefinition))
            {
                Debug.LogError(
                    "[ShopStockInitializer] 판매 상품 추첨에 실패했습니다.");
                break;
            }

            ItemInstance itemData =
                ItemDataCreator.CreateItemData(selectedDefinition);

            if (itemData == null)
            {
                Debug.LogError(
                    $"[ShopStockInitializer] " +
                    $"{selectedDefinition.itemName} 인스턴스 생성에 실패했습니다.");
                break;
            }

            InventoryItem item =
                new InventoryItem(itemData);

            if (shopController.TryAddGeneratedStock(item))
            {
                generatedCount++;
                continue;
            }

            Debug.LogWarning(
                $"[ShopStockInitializer] " +
                $"{selectedDefinition.itemName}을 상점에 추가하지 못했습니다.");

            // 상점 Grid 공간 부족 등 다음 상품도 실패할 가능성이 높다.
            break;
        }

        return generatedCount;
    }

    private bool ValidateConfiguration()
    {
        if (shopController == null)
        {
            Debug.LogError(
                "[ShopStockInitializer] ShopController가 없습니다.");
            return false;
        }

        if (itemDatabase == null)
        {
            Debug.LogError(
                "[ShopStockInitializer] ItemDatabase가 연결되지 않았습니다.");
            return false;
        }

        if (rarityChances == null || rarityChances.Length == 0)
        {
            Debug.LogError(
                "[ShopStockInitializer] 등급 확률표가 비어 있습니다.");
            return false;
        }

        if (initialStockCount <= 0)
        {
            Debug.LogError(
                "[ShopStockInitializer] 초기 재고 개수가 올바르지 않습니다.");
            return false;
        }

        rollService ??= new ShopStockRollService();
        return true;
    }
}
