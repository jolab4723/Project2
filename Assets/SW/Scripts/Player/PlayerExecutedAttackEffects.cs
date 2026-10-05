using ItemSystem;
using UnityEngine;

public sealed partial class PlayerItemEffectState
{
    private string ninjaSource;
    private double ninjaExpiresAt;
    private uint ninjaReservationId;
    private float ninjaReservationBonus, directNinjaBonus, hitNinjaBonus;

    private bool TryGetNinja(out ItemInstance boots, out NinjaDodgeAttackUniqueEffectSO effect)
    {
        boots = null;
        effect = null;
        if (inventory?.EquipmentSystem == null ||
            !inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Boots, out boots)) return false;
        effect = boots?.definition?.uniqueEffect as NinjaDodgeAttackUniqueEffectSO;
        return effect != null;
    }

    private void PrepareNinjaDodge()
    {
        if (!CanExecute || !TryGetNinja(out ItemInstance boots, out var effect)) return;
        string key = effect.name + ":ninja";
        if (IsCoolingDown(key, Now)) return;
        cooldownEndTimes[key] = Now + effect.cooldownSeconds;
        ninjaSource = boots.instanceId;
        ninjaExpiresAt = Now + effect.preparationSeconds;
    }

    /// <summary>실행된 공격만 준비를 소비한다. 탄환은 반환값을 보존하여 비행 중 새 준비와 섞지 않는다.</summary>
    public float ReserveNinjaDodgeBonus(uint attackId)
    {
        if (!CanExecute || attackId == 0) return 0f;
        if (ninjaReservationId == attackId) return ninjaReservationBonus;
        ninjaReservationId = attackId;
        ninjaReservationBonus = 0f;
        if (!TryGetNinja(out ItemInstance boots, out var effect) || boots.instanceId != ninjaSource ||
            Now >= ninjaExpiresAt || context.Health == null || context.Health.CurrentHealth <= 0f) return 0f;
        ninjaSource = null;
        ninjaExpiresAt = 0d;
        ninjaReservationBonus = Mathf.Max(0f, effect.bonusFraction);
        return ninjaReservationBonus;
    }

    private void ReconcileNinja()
    {
        if (ninjaSource == null) return;
        if (!TryGetNinja(out ItemInstance boots, out _) || boots.instanceId != ninjaSource ||
            Now >= ninjaExpiresAt || context.Health == null || context.Health.CurrentHealth <= 0f)
        {
            ninjaSource = null;
            ninjaExpiresAt = 0d;
        }
    }

    private void ClearNinja()
    {
        ninjaSource = null;
        ninjaExpiresAt = 0d;
        ninjaReservationId = 0;
        ninjaReservationBonus = directNinjaBonus = hitNinjaBonus = 0f;
    }
}
