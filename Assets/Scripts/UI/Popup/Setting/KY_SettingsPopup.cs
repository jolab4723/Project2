using Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>화면·사운드·언어·키 바인딩 설정을 표시하고 적용하는 설정 팝업이다.</summary>
public class KY_SettingsPopup : KY_PopupBase
{
    [SerializeField] private UnityEngine.UI.Toggle alliedBuffRangesToggle;
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
    [Tooltip("Sound/Pan (3)/Slider - BGM 음량")]
    public Slider bgmVolumeSlider;
    [Tooltip("Sound/Pan (4)/Slider - SFX 음량")]
    public Slider sfxVolumeSlider;

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

    [Header("다국어")]
    [Tooltip("비워두면 Resources에서 공용 UILabelDatabase를 자동으로 찾아 쓴다.")]
    [SerializeField] private UILabelDatabaseSO labelDatabase;

    private const string LabelDatabaseResourcePath = "DataFiles/UIData/3. GeneratedAssets/UILabelDatabase";

    private GameInputActions inputActions;
    private KY_SettingsData tempData;

    /// <summary>
    /// 고정 문구 하나를 현재 언어로 가져온다. DB가 없거나 키가 없으면 한국어 원문으로 떨어진다
    /// (UILabelDatabaseSO.GetLabel은 못 찾으면 key를 그대로 돌려주므로 화면에 키가 노출되지 않게 막는다).
    /// </summary>
    private string GetUILabel(string key, string fallback)
    {
        if (labelDatabase == null)
            labelDatabase = Resources.Load<UILabelDatabaseSO>(LabelDatabaseResourcePath);

        if (labelDatabase == null)
            return fallback;

        string label = labelDatabase.GetLabel(key);
        return string.IsNullOrEmpty(label) || label == key ? fallback : label;
    }

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
        // 언어 이름은 번역하지 않는다 - 각 언어를 그 언어의 표기로 보여줘야 현재 언어를 모르는
        // 사용자도 자기 언어를 찾을 수 있다. 그래서 라벨 DB를 타지 않는 고정 목록이다.
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new System.Collections.Generic.List<string>
    {
        "한국어",
        "English",
        "日本語",
        "中文"
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
        rebindText.text = GetUILabel("settings_ui.rebind_prompt", "변경할 키를 입력해주세요");

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
        // SW 수정 : 실제 버프 판정과 독립된 기기별 표시 설정이다.
        if (alliedBuffRangesToggle != null)
            alliedBuffRangesToggle.SetIsOnWithoutNotify(tempData.showAlliedBuffRanges);

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
        masterVolumeSlider.SetValueWithoutNotify(tempData.masterVolume);
        muteToggle.SetIsOnWithoutNotify(tempData.isMuted);
        if (bgmVolumeSlider != null)
            bgmVolumeSlider.SetValueWithoutNotify(tempData.bgmVolume);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(tempData.sfxVolume);
        if (bgmVolumeSlider == null || sfxVolumeSlider == null)
            Debug.LogWarning("[KY_SettingsPopup] BGM/SFX 슬라이더 참조를 연결하세요. 미연결 항목은 저장값을 유지합니다.", this);

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
            message = GetUILabel("settings_ui.reset_confirm", "설정을 기본값으로 초기화하시겠습니까?"),
            warningText = GetUILabel("settings_ui.reset_warning", "현재 설정이 모두 기본값으로 변경됩니다."),
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
        if (alliedBuffRangesToggle != null)
            tempData.showAlliedBuffRanges = alliedBuffRangesToggle.isOn;
        tempData.resolutionIndex = resolutionDropdown.value;
        // 창모드 토글이 켜져 있으면 전체 화면 데이터는 false여야 한다.
        tempData.isFullscreen = !fullscreenToggle.isOn;
        tempData.brightness = brightnessSlider.value;
        tempData.masterVolume = masterVolumeSlider.value;
        tempData.isMuted = muteToggle.isOn;
        if (bgmVolumeSlider != null)
            tempData.bgmVolume = Mathf.Clamp01(bgmVolumeSlider.value);
        if (sfxVolumeSlider != null)
            tempData.sfxVolume = Mathf.Clamp01(sfxVolumeSlider.value);

        for (int i = 0; i < qualityToggles.Length; i++)
            if (qualityToggles[i].isOn) tempData.graphicsQuality = i;

        for (int i = 0; i < frameToggles.Length; i++)
            if (frameToggles[i].isOn) tempData.targetFrameRate = i;

        tempData.language = languageDropdown.value;

        SettingManager.Instance.Apply(tempData);
    }
}
