using UnityEngine;

/// <summary>게임 이벤트를 구독해 체력·마나·경험치 HUD 표시를 갱신한다.</summary>
public class KY_HUDManager : MonoBehaviour
{
    public KY_StatusView statusView;
    public KY_SkillView skillView;
    public KY_LocationView locationView;

    void OnEnable()
    {
        KY_GameEvents.OnHealthChanged += OnHealthChanged;
        KY_GameEvents.OnManaChanged += OnManaChanged;
        KY_GameEvents.OnExpChanged += OnExpChanged;
    }

    void OnDisable()
    {
        KY_GameEvents.OnHealthChanged -= OnHealthChanged;
        KY_GameEvents.OnManaChanged -= OnManaChanged;
        KY_GameEvents.OnExpChanged -= OnExpChanged;
    }

    void OnHealthChanged(float current, float max)
    {
        statusView.UpdateHealth(current, max);
    }

    void OnManaChanged(float current, float max)
    {
        statusView.UpdateMana(current, max);
    }

    void OnExpChanged(float current, float max)
    {
        statusView.UpdateExp(current, max);
    }
}
