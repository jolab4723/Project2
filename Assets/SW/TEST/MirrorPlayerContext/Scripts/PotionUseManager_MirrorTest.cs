using System;
using ItemSystem;
using UnityEngine;

/// <summary>
/// 공통 PotionUseState 규칙을 서버에서 실행하고 충전량 변경을 동기화에 알립니다.
/// <para>원본: <c>Assets/WJ_TestPlace/Script/Player/PotionUseManager.cs</c></para>
/// <para><c>Instance</c>, 중복 오브젝트 삭제와 내부 로컬 플레이어 판정을 제거하고 Equipment·Health·Buff를 같은 플레이어 참조로 받는다.</para>
/// <para>충전량은 컴포넌트 인스턴스마다 따로 보관하며, UI가 직접 조회하지 않아도 되도록 <c>ChargesChanged</c>를 발행한다.</para>
/// <para>로컬 입력 허용 여부는 <c>MirrorSpawnedPlayerBinder</c>가 입력 컴포넌트만 제어하고, 원격 플레이어도 독립된 포션 상태는 유지한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class PotionUseManager_MirrorTest : MonoBehaviour
{
    [SerializeField, Min(0)] private int basePotionCharges = 3;
    [SerializeField, Min(0f)] private float useCooldownSeconds = 1f;
    [SerializeField] private EquipmentSystem equipmentSystem;
    [SerializeField] private PlayerHealthManager health;
    [SerializeField] private PlayerBuffManager buffs;

    public event Action<int, int> ChargesChanged;

    private readonly PotionUseState useState = new PotionUseState();

    public int CurrentCharges => useState.CurrentCharges;
    public int MaxCharges => basePotionCharges;
    public float UseCooldownSeconds => useCooldownSeconds;
    public float RemainingCooldown => useState.RemainingCooldown;

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

    /// <summary>서버만 공통 사용 규칙을 실행합니다. 클라이언트는 요청과 결과 표시만 합니다.</summary>
    public bool TryUsePotion()
    {
        if (!Mirror.NetworkServer.active || !TryGetEquippedPotion(out ItemInstance potion) ||
            !useState.TryUse(potion.definition, health, buffs, useCooldownSeconds))
            return false;
        ChargesChanged?.Invoke(CurrentCharges, MaxCharges);
        return true;
    }

    public void RechargeAllPotions()
    {
        useState.Recharge(MaxCharges);
        ChargesChanged?.Invoke(CurrentCharges, MaxCharges);
    }

    internal void ApplyAuthoritativeCharges(int currentCharges)
    {
        int clamped = Mathf.Clamp(currentCharges, 0, MaxCharges);
        if (CurrentCharges == clamped)
            return;

        useState.ApplyCharges(clamped, MaxCharges);
        ChargesChanged?.Invoke(CurrentCharges, MaxCharges);
    }

    public bool TryGetEquippedPotion(out ItemInstance potion)
    {
        return PotionUseState.TryGetEquippedPotion(equipmentSystem, out potion);
    }

    private void ResolveReferences()
    {
        equipmentSystem ??= GetComponentInChildren<EquipmentSystem>(true);
        health ??= GetComponent<PlayerHealthManager>();
        buffs ??= GetComponent<PlayerBuffManager>();
    }
}
