using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>
/// P3-B 방어구 고유 효과의 PlayerContext 전환용 활성 어댑터.
/// <para>
/// 유물은 <see cref="PlayerRelicEffectProvider_MirrorTest"/>가 인벤토리 소유권을
/// 책임지므로 이 컴포넌트는 장비 슬롯에 실제로 장착된 Armor만 관찰한다.
/// </para>
/// <para>
/// EquipmentSystem의 단일 변경 이벤트를 기준으로 장착·해제·교체를 reconcile하고,
/// threshold/aura의 런타임 객체와 passive 중복 수명을 이 PlayerContext 안에 둔다.
/// Triggered 효과의 발동 판정은 ItemTriggerManager_MirrorTest가 실제 전투/상태 이벤트에서
/// 수행하므로 여기서는 장착 시 복원과 해제 시 정리만 한다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerArmorEffectProvider_MirrorTest : MonoBehaviour
{
    [SerializeField] private EquipmentSystem equipment;
    [SerializeField] private PlayerStatManager stats;
    [SerializeField] private PlayerHealthManager health;
    [SerializeField] private PlayerManaManager mana;
    [SerializeField] private PlayerBuffManager buffs;

    private readonly HashSet<ItemInstance> activeArmorItems = new();
    private readonly Dictionary<PassiveBuffUniqueEffectSO, int> passiveCounts = new();
    private readonly Dictionary<ItemInstance, GameObject> runtimeObjects = new();

    private void Awake()
    {
        equipment ??= GetComponentInChildren<EquipmentSystem>(true);
        stats ??= GetComponent<PlayerStatManager>();
        health ??= GetComponent<PlayerHealthManager>();
        mana ??= GetComponent<PlayerManaManager>();
        buffs ??= GetComponent<PlayerBuffManager>();
    }

    private void OnEnable()
    {
        if (equipment != null)
            equipment.OnEquipmentChanged += HandleEquipmentChanged;
    }

    private void Start()
    {
        ReconcileEquipment();
    }

    private void OnDisable()
    {
        if (equipment != null)
            equipment.OnEquipmentChanged -= HandleEquipmentChanged;

        foreach (ItemInstance item in activeArmorItems)
            Deactivate(item);

        activeArmorItems.Clear();

        foreach (PassiveBuffUniqueEffectSO passive in passiveCounts.Keys)
            buffs?.RemoveBuff(passive);

        passiveCounts.Clear();
    }

    private void HandleEquipmentChanged(EquippedItemInfo[] _)
    {
        ReconcileEquipment();
    }

    private void ReconcileEquipment()
    {
        if (equipment == null || buffs == null)
            return;

        var current = new HashSet<ItemInstance>();
        foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in equipment.GetEquippedItems())
        {
            InventoryItem item = pair.Value;
            if (!IsArmor(item))
                continue;

            ItemInstance instance = item.itemData;
            if (instance?.definition?.uniqueEffect == null)
                continue;

            current.Add(instance);
            if (!activeArmorItems.Contains(instance))
                Activate(instance);
        }

        foreach (ItemInstance previous in activeArmorItems)
        {
            if (!current.Contains(previous))
                Deactivate(previous);
        }

        activeArmorItems.Clear();
        activeArmorItems.UnionWith(current);
    }

    private void Activate(ItemInstance ownerItem)
    {
        UniqueEffectSO effect = ownerItem?.definition?.uniqueEffect;
        if (effect == null)
            return;

        switch (effect)
        {
            case TriggeredBuffUniqueEffectSO triggered:
                if (triggered.persistStackOnItem && ownerItem.persistedStackCount > 0)
                    buffs.SetBuffStack(triggered, ownerItem.persistedStackCount);
                break;

            case PassiveBuffUniqueEffectSO passive:
                int count = passiveCounts.TryGetValue(passive, out int current) ? current + 1 : 1;
                passiveCounts[passive] = count;
                if (count == 1)
                    buffs.ApplyBuff(passive);
                break;

            case FieldAuraUniqueEffectSO aura:
                CreateAura(ownerItem, aura);
                break;

            case StatThresholdBuffUniqueEffectSO threshold:
                CreateThresholdRunner(ownerItem, threshold);
                break;
        }
    }

    private void Deactivate(ItemInstance ownerItem)
    {
        UniqueEffectSO effect = ownerItem?.definition?.uniqueEffect;
        if (effect == null)
            return;

        if (effect is TriggeredBuffUniqueEffectSO triggered)
        {
            buffs.RemoveBuff(triggered);
        }
        else if (effect is PassiveBuffUniqueEffectSO passive &&
                 passiveCounts.TryGetValue(passive, out int count))
        {
            if (count <= 1)
            {
                passiveCounts.Remove(passive);
                buffs.RemoveBuff(passive);
            }
            else
            {
                passiveCounts[passive] = count - 1;
            }
        }

        // Destroy는 Play Mode에서 프레임 끝에 실행되므로 threshold runner의 OnDestroy만
        // 기다리면 장비 해제 직후 한 프레임 동안 효과가 남을 수 있다. 소유권 경계에서
        // 먼저 제거하고 runner는 이벤트 구독만 정리하게 한다.
        if (effect is StatThresholdBuffUniqueEffectSO threshold)
            buffs.RemoveBuff(threshold);

        if (runtimeObjects.Remove(ownerItem, out GameObject runtimeObject) && runtimeObject != null)
            Destroy(runtimeObject);
    }

    private void CreateAura(ItemInstance ownerItem, FieldAuraUniqueEffectSO aura)
    {
        if (runtimeObjects.ContainsKey(ownerItem))
            return;

        GameObject zoneObject = new($"[MirrorTest Armor Aura] {aura.name}");
        zoneObject.layer = 2;
        zoneObject.transform.position = transform.position;

        FollowTransform follow = zoneObject.AddComponent<FollowTransform>();
        follow.SetTarget(transform);

        if (Mirror.NetworkServer.active)
        {
            SphereCollider collider = zoneObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = aura.radius;

            Rigidbody rigidbody = zoneObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            BuffFieldZone_MirrorTest zone = zoneObject.AddComponent<BuffFieldZone_MirrorTest>();
            zone.ConfigureRuntime(
                aura,
                targetEnemies: aura.targetEnemies,
                removeOnExit: true,
                removeWhenZoneDisabled: true);
        }

        if (aura.showAreaVisual && Mirror.NetworkClient.active)
        {
            AreaRingVisual ring = zoneObject.AddComponent<AreaRingVisual>();
            ring.SetColor(aura.areaVisualColor);
            ring.SetRadius(aura.radius);
        }

        runtimeObjects.Add(ownerItem, zoneObject);
    }

    private void CreateThresholdRunner(ItemInstance ownerItem, StatThresholdBuffUniqueEffectSO effect)
    {
        if (runtimeObjects.ContainsKey(ownerItem))
            return;

        GameObject runnerObject = new($"[MirrorTest Armor Threshold] {effect.name}");
        runnerObject.transform.SetParent(transform, false);
        runnerObject.AddComponent<StatThresholdRunner_MirrorTest>().Bind(
            stats,
            health,
            mana,
            buffs,
            effect);

        runtimeObjects.Add(ownerItem, runnerObject);
    }

    private static bool IsArmor(InventoryItem item)
    {
        return item?.itemData?.definition != null &&
               item.itemData.definition.category == ItemCategory.Armor;
    }
}
