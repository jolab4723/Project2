using System;
using ItemSystem;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PotionUseManager_MirrorTest : MonoBehaviour
{
    [SerializeField, Min(0)] private int basePotionCharges = 3;
    [SerializeField] private EquipmentSystem equipmentSystem;
    [SerializeField] private PlayerHealthManager health;
    [SerializeField] private PlayerBuffManager buffs;

    public event Action<int, int> ChargesChanged;

    public int CurrentCharges { get; private set; }
    public int MaxCharges => basePotionCharges;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        RechargeAllPotions();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif

    public bool TryUsePotion()
    {
        if (CurrentCharges <= 0 || !TryGetEquippedPotion(out ItemInstance potion))
            return false;

        switch (potion.definition.potionEffectType)
        {
            case PotionEffectType.Heal:
                health.Heal(potion.definition.potionEffectValue);
                break;

            case PotionEffectType.StatBoost:
                if (potion.definition.potionBuff != null)
                    buffs.ApplyBuff(potion.definition.potionBuff);
                break;
        }

        CurrentCharges--;
        ChargesChanged?.Invoke(CurrentCharges, MaxCharges);
        return true;
    }

    public void RechargeAllPotions()
    {
        CurrentCharges = MaxCharges;
        ChargesChanged?.Invoke(CurrentCharges, MaxCharges);
    }

    public bool TryGetEquippedPotion(out ItemInstance potion)
    {
        potion = null;

        if (equipmentSystem == null ||
            !equipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Potion, out ItemInstance equipped) ||
            equipped?.definition == null ||
            equipped.definition.category != ItemCategory.Potion)
        {
            return false;
        }

        potion = equipped;
        return true;
    }

    private void ResolveReferences()
    {
        equipmentSystem ??= GetComponentInChildren<EquipmentSystem>(true);
        health ??= GetComponent<PlayerHealthManager>();
        buffs ??= GetComponent<PlayerBuffManager>();
    }
}
