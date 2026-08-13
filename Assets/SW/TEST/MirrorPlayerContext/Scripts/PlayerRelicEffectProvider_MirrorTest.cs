using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>
/// SW 원본 <c>PlayerRelicEffectProvider</c>의 PlayerContext 전환 검증용 복제본이다.
/// <para>원본: <c>Assets/SW/Scripts/Player/PlayerRelicEffectProvider.cs</c></para>
/// <para>유물 SO의 전역 <c>OnEquip/OnUnequip</c> 호출 대신 같은 플레이어의 Inventory와 Buff를 직접 참조해 효과를 적용한다.</para>
/// <para>패시브 중첩 수, 오라 GameObject, 조건부 버프 Runner를 이 플레이어 컴포넌트가 소유하여 다른 플레이어와 런타임 상태를 공유하지 않는다.</para>
/// <para>비활성화 시 이벤트 구독, 버프와 생성한 런타임 오브젝트를 모두 정리한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRelicEffectProvider_MirrorTest : MonoBehaviour
{
    [SerializeField] private InventoryController inventory;
    [SerializeField] private PlayerBuffManager buffs;

    private readonly Dictionary<PassiveBuffUniqueEffectSO, int> passiveCounts = new();
    private readonly Dictionary<ItemInstance, GameObject> runtimeObjects = new();

    private void Awake()
    {
        inventory ??= GetComponentInChildren<InventoryController>(true);
        buffs ??= GetComponent<PlayerBuffManager>();
    }

    private void OnEnable()
    {
        if (inventory == null)
            return;

        inventory.OnItemOwnershipGained += HandleOwnershipGained;
        inventory.OnItemOwnershipLost += HandleOwnershipLost;
    }

    private void Start()
    {
        if (inventory?.PlayerGrid == null)
            return;

        foreach (InventoryItem item in inventory.PlayerGrid.GetAllItems())
            HandleOwnershipGained(item);
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnItemOwnershipGained -= HandleOwnershipGained;
            inventory.OnItemOwnershipLost -= HandleOwnershipLost;
        }

        foreach (PassiveBuffUniqueEffectSO passive in passiveCounts.Keys)
            buffs?.RemoveBuff(passive);

        passiveCounts.Clear();

        foreach (GameObject runtimeObject in runtimeObjects.Values)
        {
            if (runtimeObject != null)
                Destroy(runtimeObject);
        }

        runtimeObjects.Clear();
    }

    private void HandleOwnershipGained(InventoryItem item)
    {
        if (!IsRelic(item) || buffs == null)
            return;

        ItemInstance ownerItem = item.itemData;
        UniqueEffectSO effect = ownerItem.definition.uniqueEffect;

        switch (effect)
        {
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

    private void HandleOwnershipLost(InventoryItem item)
    {
        if (!IsRelic(item) || buffs == null)
            return;

        ItemInstance ownerItem = item.itemData;
        UniqueEffectSO effect = ownerItem.definition.uniqueEffect;

        if (effect is PassiveBuffUniqueEffectSO passive && passiveCounts.TryGetValue(passive, out int count))
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

        if (runtimeObjects.Remove(ownerItem, out GameObject runtimeObject) && runtimeObject != null)
            Destroy(runtimeObject);
    }

    private void CreateAura(ItemInstance ownerItem, FieldAuraUniqueEffectSO aura)
    {
        if (runtimeObjects.ContainsKey(ownerItem))
            return;

        GameObject zoneObject = new($"[MirrorTest Aura] {aura.name}");
        zoneObject.layer = 2;
        zoneObject.transform.position = transform.position;

        FollowTransform follow = zoneObject.AddComponent<FollowTransform>();
        follow.SetTarget(transform);

        SphereCollider collider = zoneObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = aura.radius;

        BuffFieldZone zone = zoneObject.AddComponent<BuffFieldZone>();
        zone.ConfigureRuntime(aura, removeOnExit: true, removeWhenZoneDisabled: true);

        if (aura.showAreaVisual)
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

        GameObject runnerObject = new($"[MirrorTest Threshold] {effect.name}");
        runnerObject.transform.SetParent(transform, false);
        runnerObject.AddComponent<StatThresholdRunner_MirrorTest>().Bind(
            GetComponent<PlayerStatManager>(),
            GetComponent<PlayerHealthManager>(),
            GetComponent<PlayerManaManager>(),
            buffs,
            effect);

        runtimeObjects.Add(ownerItem, runnerObject);
    }

    private static bool IsRelic(InventoryItem item)
    {
        return item?.itemData?.definition != null &&
               item.itemData.definition.category == ItemCategory.Relic;
    }
}
