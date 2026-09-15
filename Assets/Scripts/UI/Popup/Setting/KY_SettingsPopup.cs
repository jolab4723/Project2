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

    [Header("탭")]
    [Tooltip("Tap/Display의 Toggle - 팝업을 열 때 기본으로 활성화할 탭(화면 설정)")]
    public Toggle defaultTabToggle;

    [Header("소리")]
    [Tooltip("Sound/Pan (2)/Slider - 전체음량")]
    public Slider masterVolumeSlider;
    [Tooltip("Sound/Pan (1)/Toggle - 음소거")]
    public Toggle muteToggle;
    // bgmVolume/sfxVolume(Sound/Pan (3), Pan (4))은 아직 BGM/SFX를 구분해서 재생하는 시스템 자체가
    // 없어서(오디오 믹서 등) 이번엔 연결하지 않는다 - 실제 재생 시스템이 생기면 그때 연결.

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
        languageDropdown.onValueChanged.AddListener(OnLanguageDropdownChanged);

        //inputActions = new GameInputActions();
        //inputActions.Enable();
    }

    // 드롭다운 선택값(0~3)을 실제 언어 전환에 그대로 연결한다. YJ_LanguageManager.SetLanguageByIndex가
    // 정확히 이 용도로 만들어져 있다(주석: "Dropdown의 값 0~3을 연결할 때 사용합니다").
    private void OnLanguageDropdownChanged(int index)
    {
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.SetLanguageByIndex(index);
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
        rebindOperation?.Cancel();
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
                operation.Dispose();
                rebindOperation = null;
            })
            .OnCancel(operation =>
            {
                action.Enable();
                rebindOverlay.SetActive(false);
                operation.Dispose();
                rebindOperation = null;
            })
            .Start();
    }

    private void OnDisable()
    {
        // 닫힌 팝업의 리바인드가 게임 입력을 계속 가로채지 않도록 취소한다.
        rebindOperation?.Cancel();
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
        // 저장값이 아니라 현재 Unity 화면 모드를 기준으로 표시한다.
        // 창모드면 켜고, 전체 화면/전체 화면 창이면 끈다.
        bool isWindowed = Screen.fullScreenMode == FullScreenMode.Windowed;
        fullscreenToggle.SetIsOnWithoutNotify(isWindowed);

        // 밝기
        brightnessSlider.value = tempData.brightness;

        // 소리
        masterVolumeSlider.value = tempData.masterVolume;
        muteToggle.isOn = tempData.isMuted;

        // 품질
        for (int i = 0; i < qualityToggles.Length; i++)
            qualityToggles[i].isOn = (i == tempData.graphicsQuality);

        // 프레임
        for (int i = 0; i < frameToggles.Length; i++)
            frameToggles[i].isOn = (i == tempData.targetFrameRate);

        // 탭 - 팝업을 열 때마다 항상 "화면" 탭이 기본으로 켜지게 한다. ToggleGroup으로 묶여있어서
        // 이 토글을 켜면 다른 탭은 자동으로 꺼진다(각 탭의 OnToggleChanged가 패널 표시/숨김까지 처리).
        // 이미 화면 탭이 켜져있던 경우(값 변화 없음)를 대비해 RefreshVisual도 그대로 이어서 호출한다.
        if (defaultTabToggle != null)
            defaultTabToggle.isOn = true;

        // 색 초기화
        foreach (var tab in GetComponentsInChildren<KY_SettingsTab>())
            tab.RefreshVisual();

        // 언어 - 팝업을 열 때마다 현재 언어로 드롭다운을 맞춘다(리스너가 다시 SetLanguage를
        // 부르지 않도록 SetValueWithoutNotify 사용 - 어차피 같은 언어면 SetLanguage 쪽에서도 무시하지만
        // 불필요한 재호출을 막기 위함).
        if (YJ_LanguageManager.Instance != null)
            languageDropdown.SetValueWithoutNotify((int)YJ_LanguageManager.Instance.CurrentLanguage);

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
        if (KY_PopupManager.Instance == null)
        {
            ResetSettingsConfirmed();
            return;
        }

        KY_PopupManager.Instance.ShowConfirm(new KY_DialogData
        {
            message = "설정을 기본값으로 초기화하시겠습니까?",
            warningText = "현재 설정이 모두 기본값으로 변경됩니다.",
            onYes = ResetSettingsConfirmed
        });
    }

    private void ResetSettingsConfirmed()
    {
        SettingManager.Instance.Apply(new KY_SettingsData());
        LoadCurrentSettings();
    }

    void ApplySettings()
    {
        tempData.resolutionIndex = resolutionDropdown.value;
        // 창모드 토글이 켜져 있으면 전체 화면 데이터는 false여야 한다.
        tempData.isFullscreen = !fullscreenToggle.isOn;
        tempData.brightness = brightnessSlider.value;
        tempData.masterVolume = masterVolumeSlider.value;
        tempData.isMuted = muteToggle.isOn;

        for (int i = 0; i < qualityToggles.Length; i++)
            if (qualityToggles[i].isOn) tempData.graphicsQuality = i;

        for (int i = 0; i < frameToggles.Length; i++)
            if (frameToggles[i].isOn) tempData.targetFrameRate = i;

        tempData.language = languageDropdown.value;

        SettingManager.Instance.Apply(tempData);
    }
}
