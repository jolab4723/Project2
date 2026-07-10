using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KY_SettingsPopup : KY_PopupBase
{
    [Header("화면")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Slider brightnessSlider;
    public Toggle[] qualityToggles;   // 낮음, 중간, 높음
    public Toggle[] frameToggles;     // 30, 60, 무제한

    [Header("조작")]
    public KY_RebindSlot skill1Slot;
    public KY_RebindSlot skill2Slot;
    public KY_RebindSlot skill3Slot;
    public KY_RebindSlot skill4Slot;
    public KY_RebindSlot potionSlot;
    public KY_RebindSlot dodgeSlot;

    private GameInputActions inputActions;
    private KY_SettingsData tempData;

    void Awake()
    {
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(new System.Collections.Generic.List<string>
    {
        "1280 x 720",
        "1920 x 1080",
        "2560 x 1440",
        "3840 x 2160"
    });

        //inputActions = new GameInputActions();
        //inputActions.Enable();
    }

    void InitRebindSlots()
    {
        var actions = KY_RebindManager.Instance.GetInputActions();
        skill1Slot.Init(actions.Player.Skill1);
        skill2Slot.Init(actions.Player.Skill2);
        skill3Slot.Init(actions.Player.Skill3);
        skill4Slot.Init(actions.Player.Skill4);
        potionSlot.Init(actions.Player.Potion);
        dodgeSlot.Init(actions.Player.Dodge);
    }

    public override void Open()
    {
        base.Open();
        LoadCurrentSettings();
    }

    void LoadCurrentSettings()
    {
        tempData = KY_SettingsManager.Instance.GetData();

        // 해상도
        resolutionDropdown.value = tempData.resolutionIndex;

        // 창모드
        fullscreenToggle.isOn = tempData.isFullscreen;

        // 밝기
        brightnessSlider.value = tempData.brightness;

        // 품질
        for (int i = 0; i < qualityToggles.Length; i++)
            qualityToggles[i].isOn = (i == tempData.graphicsQuality);

        // 프레임
        for (int i = 0; i < frameToggles.Length; i++)
            frameToggles[i].isOn = (i == tempData.targetFrameRate);

        // 색 초기화
        foreach (var tab in GetComponentsInChildren<KY_SettingsTab>())
            tab.RefreshVisual();

        // 키 바인딩
        InitRebindSlots();
    }

    public void OnClickApply()
    {
        ApplySettings();
    }

    public void OnClickConfirm()
    {
        ApplySettings();
        KY_PopupManager.Instance.Hide();
    }

    public void OnClickReset()
    {
        KY_SettingsManager.Instance.Apply(new KY_SettingsData());
        LoadCurrentSettings();
    }

    void ApplySettings()
    {
        tempData.resolutionIndex = resolutionDropdown.value;
        tempData.isFullscreen = fullscreenToggle.isOn;
        tempData.brightness = brightnessSlider.value;

        for (int i = 0; i < qualityToggles.Length; i++)
            if (qualityToggles[i].isOn) tempData.graphicsQuality = i;

        for (int i = 0; i < frameToggles.Length; i++)
            if (frameToggles[i].isOn) tempData.targetFrameRate = i;

        KY_SettingsManager.Instance.Apply(tempData);
    }
}