using System;
using System.Collections;
using UnityEngine;

/// <summary>게임 이벤트를 구독해 체력·마나·경험치 HUD 표시를 갱신한다.</summary>
public class KY_HUDManager : MonoBehaviour
{
    public KY_StatusView statusView;
    public KY_SkillView skillView;
    public KY_LocationView locationView;

    /// <summary>HUD 자식 뷰의 Start와 첫 레이아웃 반영이 끝난 뒤 한 번 발생한다.</summary>
    public event Action<KY_HUDManager> Ready;
    public bool IsReady { get; private set; }

    private IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        IsReady = true;
        Ready?.Invoke(this);
    }

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
