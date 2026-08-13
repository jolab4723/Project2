using System;
using ItemSystem;
using UnityEngine;

/// <summary>
/// WJ 원본 <c>PotionUseManager</c>의 PlayerContext 전환 검증용 복제본이다.
/// <para>원본: <c>Assets/WJ_TestPlace/Script/Player/PotionUseManager.cs</c></para>
/// <para><c>Instance</c>, 중복 오브젝트 삭제와 내부 로컬 플레이어 판정을 제거하고 Equipment·Health·Buff를 같은 플레이어 참조로 받는다.</para>
/// <para>충전량은 컴포넌트 인스턴스마다 따로 보관하며, UI가 직접 조회하지 않아도 되도록 <c>ChargesChanged</c>를 발행한다.</para>
/// <para>로컬 입력 허용 여부는 <c>MirrorSpawnedPlayerBinder</c>가 입력 컴포넌트만 제어하고, 원격 플레이어도 독립된 포션 상태는 유지한다.</para>
/// </summary>
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

    internal void ApplyAuthoritativeCharges(int currentCharges)
    {
        int clamped = Mathf.Clamp(currentCharges, 0, MaxCharges);
        if (CurrentCharges == clamped)
            return;

        CurrentCharges = clamped;
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
