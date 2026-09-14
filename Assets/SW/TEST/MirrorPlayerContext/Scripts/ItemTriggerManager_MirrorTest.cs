using System.Collections.Generic;
using ItemSystem;
using Mirror;
using UnityEngine;

/// <summary>
/// WJ 원본 <c>ItemTriggerManager</c>의 PlayerContext 전환 검증용 복제본이다.
/// <para>원본: <c>Assets/WJ_TestPlace/Script/Player/ItemTriggerManager.cs</c></para>
/// <para><c>Instance</c>와 로컬 NetworkIdentity 판정을 제거하고, 같은 플레이어의 Inventory·Health·Buff·StateMachine을 직접 참조한다.</para>
/// <para>피격·회피·처치 호출은 이 컴포넌트가 소유한 플레이어의 장비와 유물만 검사한다.</para>
/// <para>고유 효과 SO에 있던 공유 쿨타임 대신 플레이어 컴포넌트의 딕셔너리에 아이템별 실행 시간을 보관한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemTriggerManager_MirrorTest : NetworkBehaviour
{
    [SerializeField] private InventoryController inventory;
    [SerializeField] private PlayerHealthManager health;
    [SerializeField] private PlayerBuffManager buffs;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;

    private readonly SyncDictionary<string, double> cooldownEndTimes = new();

    public int ActiveCooldownCount
    {
        get
        {
            int count = 0;
            foreach (KeyValuePair<string, double> pair in cooldownEndTimes)
            {
                if (pair.Value > NetworkTime.time)
                    count++;
            }

            return count;
        }
    }

    private void Awake()
    {
        inventory ??= GetComponentInChildren<InventoryController>(true);
        health ??= GetComponent<PlayerHealthManager>();
        buffs ??= GetComponent<PlayerBuffManager>();
        stateMachine ??= GetComponent<WBH_PlayerStateMachine>();
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDamageTaken += HandleHitTaken;

        if (stateMachine != null)
            stateMachine.OnEnterState += HandleStateEntered;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDamageTaken -= HandleHitTaken;

        if (stateMachine != null)
            stateMachine.OnEnterState -= HandleStateEntered;
    }

    public void Fire(TriggerCondition condition)
    {
        if (!isServer || inventory == null || buffs == null)
            return;

        if (inventory.EquipmentSystem != null)
        {
            foreach (KeyValuePair<EquipSlotType, InventoryItem> pair in inventory.EquipmentSystem.GetEquippedItems())
                FireIfReady(pair.Value?.itemData, condition);
        }

        if (inventory.PlayerGrid == null)
            return;

        foreach (InventoryItem item in inventory.PlayerGrid.GetAllItems())
        {
            if (item?.itemData?.definition?.category == ItemCategory.Relic)
                FireIfReady(item.itemData, condition);
        }
    }

    public float GetRemainingCooldown(ItemInstance item)
    {
        if (item?.definition?.uniqueEffect is not TriggeredBuffUniqueEffectSO effect || effect.cooldownSeconds <= 0f)
            return 0f;

        string key = GetCooldownKey(effect, item);
        return cooldownEndTimes.TryGetValue(key, out double cooldownEnd)
            ? Mathf.Max(0f, (float)(cooldownEnd - NetworkTime.time))
            : 0f;
    }

    private void HandleHitTaken(float amount)
    {
        Fire(TriggerCondition.OnHitTaken);
    }

    private void HandleStateEntered(PlayerState state)
    {
        if (state == PlayerState.Dodge)
            Fire(TriggerCondition.OnDodge);
    }

    private void FireIfReady(ItemInstance item, TriggerCondition condition)
    {
        if (item?.definition?.uniqueEffect is not TriggeredBuffUniqueEffectSO effect ||
            effect.triggerCondition != condition)
        {
            return;
        }

        string key = GetCooldownKey(effect, item);
        double now = NetworkTime.time;
        if (cooldownEndTimes.TryGetValue(key, out double cooldownEnd) && now < cooldownEnd)
        {
            return;
        }

        cooldownEndTimes[key] = now + Mathf.Max(0f, effect.cooldownSeconds);
        if (effect.persistStackOnItem)
        {
            int next = (int)System.Math.Min((long)Mathf.Max(0, item.persistedStackCount) + 1, int.MaxValue);
            item.persistedStackCount = effect.buffSpec.maxStack > 0 ? Mathf.Min(next, effect.buffSpec.maxStack) : next;
            buffs.SetBuffStack(effect, item.persistedStackCount);
            GetComponent<PlayerInventorySync_MirrorTest>()?.ServerSyncPersistedStack(item);
        }
        else buffs.ApplyBuff(effect);
    }

    private static string GetCooldownKey(TriggeredBuffUniqueEffectSO effect, ItemInstance item)
    {
        string itemKey = effect.duplicatePolicy == DuplicateTriggerPolicy.PerItem
            ? item?.instanceId
            : null;

        return effect.name + ":" + (itemKey ?? "shared");
    }
}
