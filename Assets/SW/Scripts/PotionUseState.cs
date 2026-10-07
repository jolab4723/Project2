using ItemSystem;
using UnityEngine;

/// <summary>싱글과 서버가 함께 사용하는 플레이어별 포션 충전·재사용·효과 규칙입니다.</summary>
public sealed class PotionUseState
{
    /// <summary>다음에 포션을 쓸 수 있게 되는 시각.</summary>
    private float nextUsableTime;

    public int CurrentCharges { get; private set; }
    public float RemainingCooldown => Mathf.Max(0f, nextUsableTime - Time.time);
    public bool IsCooldownReady => Time.time >= nextUsableTime;

    /// <summary>충전을 최대치로 채웁니다. 남아 있는 재사용 대기시간은 유지합니다.</summary>
    public void Recharge(int maxCharges)
    {
        CurrentCharges = Mathf.Max(0, maxCharges);
    }

    /// <summary>서버에서 받은 충전량만 반영하며 포션 효과는 실행하지 않습니다.</summary>
    public void ApplyCharges(int charges, int maxCharges)
    {
        CurrentCharges = Mathf.Clamp(charges, 0, Mathf.Max(0, maxCharges));
    }

    /// <summary>현재 장착 슬롯의 유효한 포션을 찾습니다.</summary>
    public static bool TryGetEquippedPotion(EquipmentSystem equipment, out ItemInstance potion)
    {
        potion = null;
        if (equipment == null ||
            !equipment.TryGetEquippedItemInstance(EquipSlotType.Potion, out ItemInstance equipped) ||
            equipped?.definition == null || equipped.definition.category != ItemCategory.Potion)
            return false;

        potion = equipped;
        return true;
    }

    /// <summary>살아 있는 소유자에게 효과를 적용하고 성공할 때만 충전과 재사용 시간을 차감합니다.</summary>
    public bool TryUse(ItemDefinitionSO definition, PlayerHealthManager health, PlayerBuffManager buffs, float cooldownSeconds)
    {
        if (definition == null || definition.category != ItemCategory.Potion ||
            health == null || health.CurrentHealth <= 0f || CurrentCharges <= 0 || !IsCooldownReady)
            return false;

        // WJ 원본 설명: 포션 하나의 효과를 실제로 적용한다 - 회복 또는 능력치 증가.
        switch (definition.potionEffectType)
        {
            case PotionEffectType.Heal:
                health.Heal(definition.potionEffectValue);
                break;
            case PotionEffectType.StatBoost:
                if (buffs == null || definition.potionBuff == null)
                    return false;
                buffs.ApplyBuff(definition.potionBuff);
                break;
            default:
                return false;
        }

        CurrentCharges--;
        nextUsableTime = Time.time + Mathf.Max(0f, cooldownSeconds);
        return true;
    }
}
