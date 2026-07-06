using System;
using UnityEngine;

public static class KY_GameEvents
{
    // 기존
    public static event Action<float, float> OnHealthChanged;
    public static event Action<float, float> OnManaChanged;
    public static event Action<float, float> OnExpChanged;
    public static event Action<int, int> OnLocationChanged;

    public static void HealthChanged(float current, float max)
    {
        OnHealthChanged?.Invoke(current, max);
    }

    public static void ManaChanged(float current, float max)
    {
        OnManaChanged?.Invoke(current, max);
    }

    public static void ExpChanged(float current, float max)
    {
        OnExpChanged?.Invoke(current, max);
    }

    // 스킬 추가
    public static event Action<int, Sprite> OnSkillEquipped;
    public static event Action<int> OnSkillUnequipped;

    public static void SkillEquipped(int index, Sprite icon)
    {
        OnSkillEquipped?.Invoke(index, icon);
    }

    public static void SkillUnequipped(int index)
    {
        OnSkillUnequipped?.Invoke(index);
    }

    public static void LocationChanged(int chapter, int zone)
    {
        OnLocationChanged?.Invoke(chapter, zone);
    }


    // 메뉴 입력
    public static event Action OnEscPressed;
    public static event Action OnInventoryRequested;
    public static event Action OnSkillRequested;
    public static event Action OnStatusRequested;
    public static event Action OnQuestRequested;

    public static void EscPressed()
    {
        OnEscPressed?.Invoke();
    }

    public static void InventoryRequested()
    {
        OnInventoryRequested?.Invoke();
    }

    public static void SkillRequested()
    {
        OnSkillRequested?.Invoke();
    }

    public static void StatusRequested()
    {
        OnStatusRequested?.Invoke();
    }

    public static void QuestRequested()
    {
        OnQuestRequested?.Invoke();
    }

    // 스텟용
    public static event Action<KY_StatData> OnStatusDataProvided;

    public static void ProvideStatusData(KY_StatData data)
    {
        OnStatusDataProvided?.Invoke(data);
    }
}