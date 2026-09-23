using System.Collections.Generic;
using ItemSystem;
using Mirror;
using UnityEngine;

/// <summary>
/// 싱글·서버가 공유하는 플레이어별 유물 실행기다.
/// <para>원본: <c>Assets/SW/Scripts/Player/PlayerRelicEffectProvider.cs</c></para>
/// <para>유물 SO의 전역 <c>OnEquip/OnUnequip</c> 호출 대신 같은 플레이어의 Inventory와 Buff를 직접 참조해 효과를 적용한다.</para>
/// <para>패시브 중첩 수, 오라 GameObject, 조건부 버프 Runner를 이 플레이어 컴포넌트가 소유하여 다른 플레이어와 런타임 상태를 공유하지 않는다.</para>
/// <para>비활성화 시 이벤트 구독, 버프와 생성한 런타임 오브젝트를 모두 정리한다.</para>
/// <para>WJ 적 디버프 병합 차이: 오라의 판정용 Collider·Rigidbody·BuffFieldZone_MirrorTest는 서버에서만 만들고,
/// <c>FieldAuraUniqueEffectSO.targetEnemies</c>를 그대로 전달한다. 클라이언트는 판정 없이 자기 오라의 시각 표시만 만든다.</para>
/// </summary>
[DisallowMultipleComponent]
public class PlayerRelicEffectRuntime : MonoBehaviour
{
    [SerializeField] private InventoryController inventory;
    [SerializeField] private PlayerBuffManager buffs;

    private readonly Dictionary<PassiveBuffUniqueEffectSO, int> passiveCounts = new();
    private readonly Dictionary<ItemInstance, GameObject> runtimeObjects = new();
    private readonly HashSet<ItemInstance> ownedItems = new();
    private PlayerContext owner;
    private bool reconcileQueued = true;
    private bool wasServer;
    private bool wasAlive;

    protected virtual void Awake()
    {
        inventory ??= GetComponentInChildren<InventoryController>(true);
        buffs ??= GetComponent<PlayerBuffManager>();
        owner ??= GetComponent<PlayerContext>();
    }

    protected virtual void OnEnable()
    {
        owner ??= GetComponent<PlayerContext>();
        inventory ??= owner?.Inventory;
        if (inventory == null)
            return;

        inventory.OnItemOwnershipGained -= HandleOwnershipGained;
        inventory.OnItemOwnershipLost -= HandleOwnershipLost;
        inventory.OnItemOwnershipGained += HandleOwnershipGained;
        inventory.OnItemOwnershipLost += HandleOwnershipLost;
        reconcileQueued = true;
    }

    protected virtual void Start() => OnEnable();

    /// <summary>
    /// 거래와 실패 복구가 끝난 가방을 기준으로 유물 효과를 맞춥니다.
    /// 이동·회전·중복 알림은 기존 실행 객체와 쿨다운을 다시 만들지 않습니다.
    /// </summary>
    protected virtual void LateUpdate()
    {
        if (owner == null || owner.Inventory != inventory || owner.Buffs != buffs)
        {
            ClearEffects();
            reconcileQueued = true;
            return;
        }
        bool server = owner != null && owner.Effects.CanExecute;
        bool alive = owner != null && (server
            ? owner.Health != null && owner.Health.CurrentHealth > 0f
            : owner.RuntimeState != null && !owner.RuntimeState.IsDead);
        if (wasServer != server)
        {
            ClearEffects();
            reconcileQueued = true;
        }
        if (wasAlive != alive)
            reconcileQueued = true;
        wasServer = server;
        wasAlive = alive;
        if (!reconcileQueued || inventory?.PlayerGrid == null)
            return;

        reconcileQueued = false;
        var nextItems = new HashSet<ItemInstance>();
        if (alive)
        {
            foreach (InventoryItem item in inventory.PlayerGrid.GetAllItems())
                if (IsRelic(item)) nextItems.Add(item.itemData);
        }
        foreach (ItemInstance item in new List<ItemInstance>(ownedItems))
            if (!nextItems.Contains(item)) RemoveEffect(item);
        foreach (ItemInstance item in nextItems)
            if (ownedItems.Add(item)) AddEffect(item);
    }

    protected virtual void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnItemOwnershipGained -= HandleOwnershipGained;
            inventory.OnItemOwnershipLost -= HandleOwnershipLost;
        }

        ClearEffects();
    }

    private void HandleOwnershipGained(InventoryItem item) => reconcileQueued = true;
    private void HandleOwnershipLost(InventoryItem item) => reconcileQueued = true;

    /// <summary>소유 사본마다 한 번 등록하고, 게임 규칙은 서버 플레이어에서만 실행합니다.</summary>
    private void AddEffect(ItemInstance ownerItem)
    {
        if (buffs == null)
            return;
        UniqueEffectSO effect = ownerItem.definition.uniqueEffect;
        if (!wasServer && !(effect is FieldAuraUniqueEffectSO))
            return;

        switch (effect)
        {
            case TriggeredBuffUniqueEffectSO triggered:
                if (wasServer && triggered.persistStackOnItem && ownerItem.persistedStackCount > 0)
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

    /// <summary>마지막 소유 사본이 사라질 때만 같은 효과의 버프를 제거합니다.</summary>
    private void RemoveEffect(ItemInstance ownerItem)
    {
        if (!ownedItems.Remove(ownerItem))
            return;
        UniqueEffectSO effect = ownerItem.definition.uniqueEffect;

        bool sameEffectRemains = false;
        foreach (ItemInstance item in ownedItems)
            if (item.definition.uniqueEffect == effect) sameEffectRemains = true;
        if (wasServer && !sameEffectRemains && effect is TriggeredBuffUniqueEffectSO triggered)
            buffs?.RemoveBuff(triggered);

        if (effect is PassiveBuffUniqueEffectSO passive && passiveCounts.TryGetValue(passive, out int count))
        {
            if (count <= 1)
            {
                passiveCounts.Remove(passive);
                buffs?.RemoveBuff(passive);
            }
            else
            {
                passiveCounts[passive] = count - 1;
            }
        }

        if (runtimeObjects.Remove(ownerItem, out GameObject runtimeObject) && runtimeObject != null)
        {
            // 같은 효과의 마지막 사본이 사라질 때까지 실행 객체와 버프를 유지한다.
            foreach (ItemInstance remaining in ownedItems)
                if (remaining.definition.uniqueEffect == effect)
                {
                    runtimeObjects.Add(remaining, runtimeObject);
                    return;
                }
            // 다음 프레임의 Destroy를 기다리지 않고 오라와 이벤트 구독을 먼저 정리합니다.
            runtimeObject.SetActive(false);
            if (Application.isPlaying) Destroy(runtimeObject);
            else DestroyImmediate(runtimeObject);
        }
    }

    /// <summary>비활성화·서버 종료 때 이 Provider가 만든 효과만 정리합니다.</summary>
    private void ClearEffects()
    {
        foreach (ItemInstance item in new List<ItemInstance>(ownedItems))
            RemoveEffect(item);
    }

    private bool HasRuntimeForEffect(UniqueEffectSO effect)
    {
        foreach (ItemInstance item in runtimeObjects.Keys)
            if (item.definition.uniqueEffect == effect) return true;
        return false;
    }

    private void CreateAura(ItemInstance ownerItem, FieldAuraUniqueEffectSO aura)
    {
        if (HasRuntimeForEffect(aura))
            return;

        GameObject zoneObject = new($"[MirrorTest Aura] {aura.name}");
        zoneObject.layer = 2;
        zoneObject.transform.position = transform.position;

        FollowTransform follow = zoneObject.AddComponent<FollowTransform>();
        follow.SetTarget(transform);

        // 적과 오라 양쪽에 Rigidbody가 없으면 Unity Trigger 이벤트가 발생하지 않는다.
        // 판정은 서버 한 곳에서만 만들고, 클라이언트의 로컬 복제본이 적 스탯을 임의로 바꾸지 않게 한다.
        if (wasServer)
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
            zone.IncludeOwner(buffs);
        }

        // 전용 서버에는 렌더링용 링을 만들지 않는다. Host와 일반 Client에서만 표시한다.
        if (aura.showAreaVisual && (NetworkClient.active || owner.GetComponent<NetworkIdentity>() == null))
        {
            AreaRingVisual ring = zoneObject.AddComponent<AreaRingVisual>();
            ring.SetColor(aura.areaVisualColor);
            ring.SetRadius(aura.radius);
        }

        runtimeObjects.Add(ownerItem, zoneObject);
    }

    private void CreateThresholdRunner(ItemInstance ownerItem, StatThresholdBuffUniqueEffectSO effect)
    {
        if (HasRuntimeForEffect(effect))
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
