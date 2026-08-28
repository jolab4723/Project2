using Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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

    [Header("리바인드 오버레이 (예전 KY_RebindManager에서 이관)")]
    public GameObject rebindOverlay;
    public TextMeshProUGUI rebindText;

    private InputActionRebindingExtensions.RebindingOperation rebindOperation;

    [Header("게임 플레이")]
    public TMP_Dropdown languageDropdown;

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
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new System.Collections.Generic.List<string>
    {
        "한국어",
        "English ",
        "日本語",
        "中國語"
    });

        //inputActions = new GameInputActions();
        //inputActions.Enable();
    }

    void InitRebindSlots()
    {
        var actions = KeyBindingService.InputActions;
        skill1Slot.Init(actions.Player.Skill1, this);
        skill2Slot.Init(actions.Player.Skill2, this);
        skill3Slot.Init(actions.Player.Skill3, this);
        skill4Slot.Init(actions.Player.Skill4, this);
        potionSlot.Init(actions.Player.Potion, this);
        dodgeSlot.Init(actions.Player.Dodge, this);
    }

    /// <summary>키 하나를 새로 리바인드한다(예전 KY_RebindManager.StartRebind 이관). 오버레이를 띄우고
    /// 완료/취소 시 정리한다. 완료 시 슬롯 텍스트 갱신 + 저장 + KeyBindingChanged 이벤트 발행까지 처리.</summary>
    public void StartRebind(InputAction action, KY_RebindSlot slot)
    {
        rebindOverlay.SetActive(true);
        rebindText.text = "변경할 키를 입력해주세요";

        action.Disable();

        rebindOperation = action.PerformInteractiveRebinding()
            .WithControlsExcluding("Mouse")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(operation =>
            {
                action.Enable();
                rebindOverlay.SetActive(false);
                slot.RefreshKeyText();
                KeyBindingService.Save();
                KY_GameEvents.KeyBindingChanged();
                rebindOperation.Dispose();
            })
            .OnCancel(operation =>
            {
                action.Enable();
                rebindOverlay.SetActive(false);
                rebindOperation.Dispose();
            })
            .Start();
    }

    public override void Open()
    {
        base.Open();
        LoadCurrentSettings();
    }

    void LoadCurrentSettings()
    {
        tempData = SettingManager.Instance.GetData();

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
        SettingManager.Instance.Apply(new KY_SettingsData());
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

        SettingManager.Instance.Apply(tempData);
    }
}