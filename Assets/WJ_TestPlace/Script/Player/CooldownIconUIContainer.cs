using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>
/// 쿨타임이 진행 중인 발동형 고유효과(TriggeredBuffUniqueEffectSO) 아이템만 아이콘으로 나열해서
/// 보여주는 HUD UI. BuffIconUIContainer의 파생이지만, 쿨타임은 PlayerBuffManager 같은 중앙 리스트가
/// 없고 각 TriggeredBuffUniqueEffectSO 에셋이 개별적으로 쿨타임을 들고 있다(ItemTriggerManager.Fire()와
/// 같은 구조). 그래서 변경 이벤트를 구독하는 대신, 장착 아이템 + 보유 유물을 매 프레임 직접 순회해서
/// 지금 쿨타임 중인 것만 골라낸다 - ItemTriggerManager.Fire()/FireRelics()와 같은 순회 범위를 쓴다.
/// </summary>
public class CooldownIconUIContainer : MonoBehaviour
{
    private struct CooldownEntry
    {
        public ItemInstance item;
        public TriggeredBuffUniqueEffectSO effect;
    }

    [SerializeField] private CooldownIconSlot iconSlotPrefab;

    [Tooltip("슬롯을 실제로 배치할 부모(GridLayoutGroup이 붙은 곳). 비워두면 이 오브젝트 자신을 쓴다.")]
    [SerializeField] private Transform slotParent;

    private readonly List<CooldownIconSlot> pool = new List<CooldownIconSlot>();
    private readonly List<CooldownEntry> onCooldown = new List<CooldownEntry>();

    private void Awake()
    {
        if (slotParent == null)
            slotParent = transform;
    }

    private void Update()
    {
        if (iconSlotPrefab == null)
            return;

        CollectOnCooldownItems();

        while (pool.Count < onCooldown.Count)
            pool.Add(Instantiate(iconSlotPrefab, slotParent));

        for (int i = 0; i < pool.Count; i++)
        {
            bool inUse = i < onCooldown.Count;
            pool[i].gameObject.SetActive(inUse);
            if (inUse)
                pool[i].Bind(onCooldown[i].item, onCooldown[i].effect);
        }
    }

    /// <summary>
    /// 장착 아이템 + 보유 유물 중 발동형 고유효과를 가졌고 지금 쿨타임 진행 중인 것만 모은다.
    /// !! 유물은 장착 슬롯이 아니라 인벤토리 보유 개념이라 EquipmentSystem.GetEquippedItems()에
    ///    잡히지 않는다 - ItemTriggerManager.FireRelics()와 같은 이유로 PlayerGrid를 별도로 훑는다.
    /// </summary>
    private void CollectOnCooldownItems()
    {
        onCooldown.Clear();

        if (InventoryController.Instance == null)
            return;

        if (InventoryController.Instance.EquipmentSystem != null)
        {
            foreach (var pair in InventoryController.Instance.EquipmentSystem.GetEquippedItems())
                TryCollect(pair.Value != null ? pair.Value.itemData : null);
        }

        InventoryGrid playerGrid = InventoryController.Instance.PlayerGrid;
        if (playerGrid == null)
            return;

        foreach (InventoryItem inventoryItem in playerGrid.GetAllItems())
        {
            ItemInstance itemData = inventoryItem?.itemData;
            if (itemData?.definition != null && itemData.definition.category == ItemCategory.Relic)
                TryCollect(itemData);
        }
    }

    private void TryCollect(ItemInstance itemData)
    {
        var uniqueEffect = itemData?.definition != null ? itemData.definition.uniqueEffect : null;
        if (uniqueEffect is TriggeredBuffUniqueEffectSO triggered && triggered.GetRemainingCooldown(itemData) > 0f)
            onCooldown.Add(new CooldownEntry { item = itemData, effect = triggered });
    }
}
