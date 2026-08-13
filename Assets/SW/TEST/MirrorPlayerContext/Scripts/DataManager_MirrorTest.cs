using System;
using System.Collections.Generic;
using System.IO;
using ItemSystem;
using UnityEngine;

/// <summary>
/// WJ 원본 <c>DataManager</c>의 PlayerContext 전환 검증용 복제본이다.
/// <para>원본: <c>Assets/WJ_TestPlace/Script/Core/Manager/DataManager.cs</c></para>
/// <para>전역 Singleton과 매니저 모듈, 프로필·옵션·스테이지 슬롯 기능을 제외하고 플레이어 상태 저장/로드만 남겼다.</para>
/// <para><c>SaveGameplayData/LoadGameplayData(PlayerContext)</c>로 대상을 명시하며, 해당 Context의 Stat·Health·Mana·Wallet·Inventory·Equipment만 읽고 쓴다.</para>
/// <para>전용 파일명을 사용하므로 운영 세이브를 덮어쓰지 않는다. 네트워크 동기화나 서버 권한 저장은 이 테스트 범위에 포함하지 않는다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class DataManager_MirrorTest : MonoBehaviour
{
    [SerializeField] private ItemDatabaseSO itemDatabase;
    [SerializeField] private string fileName = "mirrortest_gamesave.json";

    public string SavePath => Path.Combine(Application.persistentDataPath, fileName);

    public bool SaveGameplayData(PlayerContext target)
    {
        if (!CanUse(target))
            return false;

        try
        {
            MirrorTestSaveData data = BuildSaveData(target);
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[DataManager_MirrorTest] Save failed: {exception.Message}", this);
            return false;
        }
    }

    public bool LoadGameplayData(PlayerContext target)
    {
        if (!CanUse(target) || !File.Exists(SavePath))
            return false;

        try
        {
            MirrorTestSaveData data = JsonUtility.FromJson<MirrorTestSaveData>(File.ReadAllText(SavePath));
            if (data == null)
                return false;

            ApplyStatus(target, data.status);
            ApplyInventory(target, data.inventory);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[DataManager_MirrorTest] Load failed: {exception.Message}", this);
            return false;
        }
    }

    private bool CanUse(PlayerContext target)
    {
        if (target != null && target.IsComplete && itemDatabase != null)
            return true;

        Debug.LogError("[DataManager_MirrorTest] Complete PlayerContext and ItemDatabaseSO are required.", this);
        return false;
    }

    private static MirrorTestSaveData BuildSaveData(PlayerContext target)
    {
        MirrorTestSaveData data = new();
        data.status.level = target.Stats.Stat.currentLevel;
        data.status.experience = target.Stats.Stat.currentExp;
        data.status.health = target.Health.CurrentHealth;
        data.status.mana = target.Mana.CurrentMana;
        data.status.gold = target.Wallet.Gold;

        foreach (InventoryItem item in target.Inventory.PlayerGrid.GetAllItems())
            data.inventory.Add(ToSaveItem(item, false, default));

        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in target.Equipment.GetEquippedItems())
            data.inventory.Add(ToSaveItem(pair.Value, true, pair.Key));

        return data;
    }

    private static MirrorTestItemData ToSaveItem(InventoryItem inventoryItem, bool equipped, EquipSlotType slot)
    {
        ItemInstance item = inventoryItem.itemData;
        return new MirrorTestItemData
        {
            instanceId = item.instanceId,
            itemId = item.definition != null ? item.definition.itemId : null,
            rolledSubStats = item.rolledSubStats,
            rolledElement = item.rolledElement,
            upgradeLevel = item.upgradeLevel,
            x = inventoryItem.x,
            y = inventoryItem.y,
            rotated = inventoryItem.isRotated,
            equipped = equipped,
            slot = slot,
        };
    }

    private static void ApplyStatus(PlayerContext target, MirrorTestStatusData status)
    {
        target.Stats.Stat.currentLevel = status.level;
        target.Stats.Stat.currentExp = status.experience;
        target.Stats.Recalculate();
        target.Health.SetCurrentHealth(status.health);
        target.Mana.SetCurrentMana(status.mana);
        target.Wallet.SetGold(status.gold);
    }

    private void ApplyInventory(PlayerContext target, List<MirrorTestItemData> savedItems)
    {
        ClearInventory(target);

        foreach (MirrorTestItemData saved in savedItems)
        {
            ItemDefinitionSO definition = itemDatabase.GetById(saved.itemId);
            if (definition == null)
                continue;

            ItemInstance instance = new()
            {
                instanceId = saved.instanceId,
                definition = definition,
                rolledSubStats = saved.rolledSubStats ?? new List<RolledSubStat>(),
                rolledElement = saved.rolledElement,
                upgradeLevel = saved.upgradeLevel,
            };

            InventoryItem inventoryItem = new(instance) { isRotated = saved.rotated };

            if (saved.equipped)
            {
                inventoryItem.isRotated = false;
                if (target.Equipment.TryEquipState(inventoryItem, saved.slot) == EquipResult.Success)
                {
                    target.Inventory.NotifyItemOwnershipGained(inventoryItem);
                    continue;
                }
            }

            InventoryAddResultData result = target.Inventory.TryAddItemAt(inventoryItem, saved.x, saved.y);
            if (result.Result != InventoryAddResult.Success)
                target.Inventory.TryAddItemData(instance);
        }

        target.Equipment.PublishChanged();
    }

    private static void ClearInventory(PlayerContext target)
    {
        List<KeyValuePair<EquipSlotType, InventoryItem>> equipped = new(target.Equipment.GetEquippedItems());
        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in equipped)
        {
            if (target.Equipment.TryUnequipState(pair.Key) == EquipResult.Success)
                target.Inventory.NotifyItemOwnershipLost(pair.Value);
        }

        List<InventoryItem> gridItems = new(target.Inventory.GetAllInventoryItems());
        foreach (InventoryItem item in gridItems)
            target.Inventory.TryRemoveInventoryItem(item);

        target.Equipment.PublishChanged();
    }

    [Serializable]
    private sealed class MirrorTestSaveData
    {
        public MirrorTestStatusData status = new();
        public List<MirrorTestItemData> inventory = new();
    }

    [Serializable]
    private sealed class MirrorTestStatusData
    {
        public int level;
        public float experience;
        public float health;
        public float mana;
        public int gold;
    }

    [Serializable]
    private sealed class MirrorTestItemData
    {
        public string instanceId;
        public string itemId;
        public List<RolledSubStat> rolledSubStats;
        public ElementType rolledElement;
        public int upgradeLevel;
        public int x;
        public int y;
        public bool rotated;
        public bool equipped;
        public EquipSlotType slot;
    }
}
