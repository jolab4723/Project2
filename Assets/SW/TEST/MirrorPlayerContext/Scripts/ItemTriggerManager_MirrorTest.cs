using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ItemTriggerManager_MirrorTest : MonoBehaviour
{
    [SerializeField] private InventoryController inventory;
    [SerializeField] private PlayerHealthManager health;
    [SerializeField] private PlayerBuffManager buffs;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;

    private readonly Dictionary<string, float> lastTriggerTimes = new();

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
        if (inventory == null || buffs == null)
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
        return lastTriggerTimes.TryGetValue(key, out float lastTime)
            ? Mathf.Max(0f, lastTime + effect.cooldownSeconds - Time.time)
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
        if (lastTriggerTimes.TryGetValue(key, out float lastTime) &&
            Time.time < lastTime + effect.cooldownSeconds)
        {
            return;
        }

        lastTriggerTimes[key] = Time.time;
        buffs.ApplyBuff(effect);
    }

    private static string GetCooldownKey(TriggeredBuffUniqueEffectSO effect, ItemInstance item)
    {
        string itemKey = effect.duplicatePolicy == DuplicateTriggerPolicy.PerItem
            ? item?.instanceId
            : null;

        return effect.GetInstanceID() + ":" + (itemKey ?? "shared");
    }
}
