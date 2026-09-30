using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Core;

/// <summary>일시정지 메뉴의 시간 정지, 설정, 포기 및 저장 후 종료 흐름을 관리한다.</summary>
public class KY_PausePopup : KY_PopupBase
{
    private const string ResultSceneName = "ClearResultScene";

    [SerializeField] private bool pauseGameTime = true; // SW 수정
    private KY_SlideAnimator slideAnimator;
    private bool ownsTimePause;
    private float previousTimeScale;
    private KY_DialogData? externalGiveUp;
    private KY_DialogData? externalSaveAndExit;

    /// <summary>SW 수정: Inspector에 연결한 종료 버튼과 문구를 사용해 이름 검색을 대신한다.</summary>
    [Header("종료 버튼")]
    [SerializeField] private Button giveUpButton;
    [SerializeField] private Button saveAndExitButton;
    // 정산 버튼(ButtonGroup 아래 "Adjustment")은 조건을 만족할 때만 표시한다.
    [SerializeField] private Button settleButton;
    [SerializeField] private TMP_Text giveUpLabel;
    [SerializeField] private TMP_Text saveAndExitLabel;

    private bool externalPresentationBound;
    private bool originalPauseGameTime;
    private bool originalGiveUpVisible;
    private bool originalSaveVisible;
    private string originalGiveUpText;
    private string originalSaveText;
    private TMP_FontAsset originalGiveUpFont;
    private TMP_FontAsset originalSaveFont;
    private UILabelText giveUpFixedLabel;
    private UILabelText saveFixedLabel;
    private bool originalGiveUpLabelEnabled;
    private bool originalSaveLabelEnabled;
    private string externalGiveUpText;
    private string externalSaveText;
    private TMP_FontAsset externalExitFont;

    // 확인 팝업 문구 다국어 테이블. 비워두면 Resources의 공용 DB를 자동으로 찾아 쓴다.
    private const string UILabelResourcePath = "DataFiles/UIData/3. GeneratedAssets/UILabelDatabase";
    [SerializeField] private UILabelDatabaseSO uiLabels;

    /// <summary>SW 수정: 세션 종료 확인과 실행을 외부에 맡긴다. null로 해제하면 기존 싱글 동작을 사용한다.</summary>
    public void BindExitActions(KY_DialogData? giveUp, KY_DialogData? saveAndExit)
    {
        externalGiveUp = giveUp;
        externalSaveAndExit = saveAndExit;
    }

    /// <summary>SW 수정: 세션 종료 버튼의 표시와 문구를 연결하고, 메뉴가 서버 시간을 멈추지 않게 한다.</summary>
    public void BindExitPresentation(
        bool giveUpVisible,
        bool saveVisible,
        string giveUpText,
        string saveText,
        TMP_FontAsset font)
    {
        if (!externalPresentationBound)
        {
            originalPauseGameTime = pauseGameTime;
            originalGiveUpVisible = giveUpButton != null && giveUpButton.gameObject.activeSelf;
            originalSaveVisible = saveAndExitButton != null && saveAndExitButton.gameObject.activeSelf;
            if (giveUpLabel != null)
            {
                originalGiveUpText = giveUpLabel.text;
                originalGiveUpFont = giveUpLabel.font;
                giveUpLabel.TryGetComponent(out giveUpFixedLabel);
                if (giveUpFixedLabel != null)
                {
                    originalGiveUpLabelEnabled = giveUpFixedLabel.enabled;
                    giveUpFixedLabel.enabled = false;
                }
            }
            if (saveAndExitLabel != null)
            {
                originalSaveText = saveAndExitLabel.text;
                originalSaveFont = saveAndExitLabel.font;
                saveAndExitLabel.TryGetComponent(out saveFixedLabel);
                if (saveFixedLabel != null)
                {
                    originalSaveLabelEnabled = saveFixedLabel.enabled;
                    saveFixedLabel.enabled = false;
                }
            }
            externalPresentationBound = true;
        }
        else
        {
            CaptureFixedLabelChanges();
        }

        externalGiveUpText = giveUpText;
        externalSaveText = saveText;
        externalExitFont = font;
        PauseGameTime = false;
        if (giveUpButton != null) giveUpButton.gameObject.SetActive(giveUpVisible);
        if (saveAndExitButton != null) saveAndExitButton.gameObject.SetActive(saveVisible);
        RefreshSettleButton();
        ApplyExitPresentation();
    }

    /// <summary>SW 수정: 종료 요청과 버튼 문구·표시·시간 정지 설정을 기존 싱글 상태로 되돌린다.</summary>
    public void UnbindExitPresentation()
    {
        if (!externalPresentationBound)
            return;

        CaptureFixedLabelChanges();
        externalPresentationBound = false;
        BindExitActions(null, null);
        PauseGameTime = originalPauseGameTime;
        if (giveUpButton != null) giveUpButton.gameObject.SetActive(originalGiveUpVisible);
        if (saveAndExitButton != null) saveAndExitButton.gameObject.SetActive(originalSaveVisible);
        if (giveUpFixedLabel != null) giveUpFixedLabel.enabled = originalGiveUpLabelEnabled;
        if (saveFixedLabel != null) saveFixedLabel.enabled = originalSaveLabelEnabled;
        if (giveUpLabel != null)
        {
            giveUpLabel.text = originalGiveUpText;
            giveUpLabel.font = originalGiveUpFont;
        }
        if (saveAndExitLabel != null)
        {
            saveAndExitLabel.text = originalSaveText;
            saveAndExitLabel.font = originalSaveFont;
        }
        externalGiveUpText = externalSaveText = null;
        externalExitFont = null;
        RefreshSettleButton();
    }

    /// <summary>SW 수정: 싱글에서는 게임 시간을 멈춘다. 멀티플레이 메뉴는 false로 설정해 화면만 연다.</summary>
    public bool PauseGameTime
    {
        get => pauseGameTime;
        set
        {
            pauseGameTime = value;
            if (!value) RestoreGameTime();
        }
    }

    /// <summary>SW 수정: 슬라이드 애니메이터를 준비하고 버튼 연결은 팝업 활성화 시점에 처리한다.</summary>
    void Awake()
    {
        slideAnimator = GetComponentInChildren<KY_SlideAnimator>(true);
        if (slideAnimator != null) slideAnimator.ignoreTimeScale = true;
    }

    /// <summary>SW 수정: 팝업이 열릴 때 버튼을 연결하고 현재 세션의 종료 표시를 적용한다.</summary>
    private void OnEnable()
    {
        // 씬에 저장된 UnityEvent가 비어 있어도 포기 버튼이 동작하도록 자동 연결한다.
        // Inspector의 기존 클릭 연결이 없는 버튼만 자동 연결한다.
        BindPauseButton(giveUpButton, OnClickGiveUp, nameof(OnClickGiveUp));
        BindPauseButton(saveAndExitButton, OnClickSaveAndExit, nameof(OnClickSaveAndExit));
        BindPauseButton(settleButton, OnClickSettle, nameof(OnClickSettle));
        RefreshSettleButton();
        if (externalPresentationBound)
        {
            CaptureFixedLabelChanges();
            ApplyExitPresentation();
        }
    }

    /// <summary>다국어 DB의 현재 언어 문구를 반환한다. DB를 찾지 못하면 기존 한국어 문구를 쓴다.</summary>
    private string GetUILabel(string key, string fallback)
    {
        if (uiLabels == null)
            uiLabels = Resources.Load<UILabelDatabaseSO>(UILabelResourcePath);

        return uiLabels != null ? uiLabels.GetLabel(key) : fallback;
    }

    /// <summary>SW 수정: Inspector에 같은 클릭 처리가 없을 때만 런타임 버튼 처리를 연결한다.</summary>
    private void BindPauseButton(Button button, UnityAction listener, string methodName)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(listener);
        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
        {
            if (button.onClick.GetPersistentTarget(index) == this &&
                button.onClick.GetPersistentMethodName(index) == methodName)
                return;
        }
        button.onClick.AddListener(listener);
    }

    /// <summary>SW 수정: 언어 변경 뒤에도 세션 종료 문구와 폰트를 유지한다.</summary>
    private void LateUpdate()
    {
        if (!externalPresentationBound)
            return;

        // UILabelText는 비활성 상태에서도 이미 구독한 언어 이벤트를 받으므로 표시를 마지막에 맞춘다.
        CaptureFixedLabelChanges();
        ApplyExitPresentation();
    }

    /// <summary>SW 수정: 언어 변경으로 갱신된 싱글 문구와 폰트를 기록해 연결 해제 때 복원한다.</summary>
    private void CaptureFixedLabelChanges()
    {
        // 언어가 바뀌어 고정 문구가 갱신되면 싱글로 돌아갈 때도 현재 언어의 문구와 폰트를 복원한다.
        if (giveUpFixedLabel != null && giveUpLabel != null && giveUpLabel.text != externalGiveUpText)
        {
            originalGiveUpText = giveUpLabel.text;
            originalGiveUpFont = giveUpLabel.font;
        }
        if (saveFixedLabel != null && saveAndExitLabel != null && saveAndExitLabel.text != externalSaveText)
        {
            originalSaveText = saveAndExitLabel.text;
            originalSaveFont = saveAndExitLabel.font;
        }
    }

    /// <summary>SW 수정: 연결된 세션의 종료 문구와 폰트를 버튼에 표시한다.</summary>
    private void ApplyExitPresentation()
    {
        if (giveUpLabel != null)
        {
            if (giveUpLabel.text != externalGiveUpText) giveUpLabel.text = externalGiveUpText;
            if (externalExitFont != null && giveUpLabel.font != externalExitFont) giveUpLabel.font = externalExitFont;
        }
        if (saveAndExitLabel != null)
        {
            if (saveAndExitLabel.text != externalSaveText) saveAndExitLabel.text = externalSaveText;
            if (externalExitFont != null && saveAndExitLabel.font != externalExitFont) saveAndExitLabel.font = externalExitFont;
        }
    }

    /// <summary>SW 수정: 정산 버튼은 정산 가능한 상황(싱글·액트 1개 이상 클리어·비전투)에서만 보인다.</summary>
    private void RefreshSettleButton()
    {
        if (settleButton != null)
            settleButton.gameObject.SetActive(CanSettle());
    }

    /// <summary>
    /// 정산 가능 여부. 싱글 전용이며, 액트를 하나 이상 클리어해 Act2 이상에 있고,
    /// 진행 중인 노드가 없거나(맵 선택 화면) 캠프·시작 노드일 때만 허용한다.
    /// 전투·엘리트·보스·이벤트 노드 진행 중에는 정산할 수 없다.
    /// </summary>
    private bool CanSettle()
    {
        if (externalGiveUp.HasValue || externalSaveAndExit.HasValue ||
            MirrorNetworkManager.OwnsGameplay || Mirror.NetworkClient.active || Mirror.NetworkServer.active)
            return false;

        YJ_StageSaveService saveService = FindFirstObjectByType<YJ_StageSaveService>();
        if (saveService == null)
            saveService = gameObject.AddComponent<YJ_StageSaveService>();

        if (!saveService.HasSaveFile || !saveService.TryLoadSaveData(out StageMapSaveData map) || map == null)
            return false;

        if (map.act < StageActType.Act2)
            return false;

        if (string.IsNullOrEmpty(map.pendingNodeId))
            return true;

        StageNodeSaveData pending = map.nodes?.Find(node => node != null && node.id == map.pendingNodeId);
        return pending != null && (pending.type == StageNodeType.Camp || pending.type == StageNodeType.Start);
    }


    public override void Open()
    {
        base.Open();
        RefreshSettleButton();
        slideAnimator?.SlideIn();
        if (pauseGameTime && !ownsTimePause)
        {
            previousTimeScale = Time.timeScale;
            ownsTimePause = true;
            Time.timeScale = 0f;
        }
    }

    public override void Close()
    {
        RestoreGameTime();
        if (slideAnimator != null) slideAnimator.SlideOut(() => base.Close());
        else base.Close();
    }

    /// <summary>SW 수정: 팝업을 닫으면 게임 시간을 복원하고 런타임 버튼 연결을 해제한다.</summary>
    private void OnDisable()
    {
        RestoreGameTime();
        if (giveUpButton != null) giveUpButton.onClick.RemoveListener(OnClickGiveUp);
        if (saveAndExitButton != null) saveAndExitButton.onClick.RemoveListener(OnClickSaveAndExit);
        if (settleButton != null) settleButton.onClick.RemoveListener(OnClickSettle);
    }

    /// <summary>이 팝업이 멈춘 시간만 원래 값으로 돌린다. 다른 곳에서 바꾼 배속은 덮어쓰지 않는다.</summary>
    private void RestoreGameTime()
    {
        if (!ownsTimePause) return;
        if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = previousTimeScale;
        ownsTimePause = false;
    }

    public void OnClickResume()
    {
        KY_PopupManager.Instance.Hide();
    }

    public void OnClickSettings()
    {
        KY_PopupManager.Instance.Show(PopupType.Settings);
    }

    public void OnClickGiveUp()
    {
        if (KY_PopupManager.Instance == null) return;

        KY_DialogData dialog = externalGiveUp ?? new KY_DialogData
        {
            message = GetUILabel("pause_ui.giveup_confirm", "게임을 포기하시겠습니까?"),
            warningText = GetUILabel("pause_ui.giveup_warning", "경고: 현재 게임 데이터가 사라집니다."),
        };
        // 싱글 포기는 되돌릴 수 없으므로 한 번 더 확인한다. 멀티(세션 종료·이탈)는 기존처럼 한 번만 묻는다.
        dialog.onYes = externalGiveUp.HasValue ? GiveUpGame : RequestGiveUpReconfirm;
        KY_PopupManager.Instance.ShowConfirm(dialog);
    }

    /// <summary>
    /// 첫 확인의 예 버튼에서 호출된다. 확인 팝업은 하나를 재사용하고, 예 콜백 뒤에 스스로 닫히므로
    /// 콜백 안에서 바로 띄우면 매니저가 거절한다. 닫힌 다음 프레임에 재확인 팝업을 연다.
    /// (yield return null은 timeScale 0에서도 진행된다.)
    /// </summary>
    private void RequestGiveUpReconfirm()
    {
        if (isActiveAndEnabled)
            StartCoroutine(ShowGiveUpReconfirmNextFrame());
    }

    private System.Collections.IEnumerator ShowGiveUpReconfirmNextFrame()
    {
        yield return null;
        if (KY_PopupManager.Instance == null) yield break;

        KY_DialogData dialog = new KY_DialogData
        {
            message = GetUILabel("pause_ui.giveup_reconfirm", "정말 포기하시겠습니까?"),
            warningText = GetUILabel("pause_ui.giveup_reconfirm_warning", "진행 중인 원정이 초기화되며 크레딧을 받을 수 없습니다."),
            onYes = GiveUpGame,
        };
        KY_PopupManager.Instance.ShowConfirm(dialog);
    }

    public void OnClickSaveAndExit()
    {
        if (KY_PopupManager.Instance == null) return;

        KY_DialogData dialog = externalSaveAndExit ?? new KY_DialogData
        {
            message = GetUILabel("pause_ui.save_and_quit_confirm", "게임을 저장하고 종료하시겠습니까?"),
        };
        dialog.onYes = SaveAndExitGame;
        KY_PopupManager.Instance.ShowConfirm(dialog);
    }

    public void OnClickSettle()
    {
        if (KY_PopupManager.Instance == null || !CanSettle()) return;

        KY_DialogData dialog = new KY_DialogData
        {
            message = GetUILabel("pause_ui.settle_confirm", "정산 후 게임을 종료하시겠습니까?"),
            warningText = GetUILabel("pause_ui.settle_warning", "보유 크레딧과 아이템 원가의 50%를 계정 크레딧으로 받고, 현재 원정은 초기화됩니다."),
        };
        dialog.onYes = SettleGame;
        KY_PopupManager.Instance.ShowConfirm(dialog);
    }

    /// <summary>
    /// 정산 종료. 클리어와 같은 크레딧(보유 크레딧 + 아이템 원가 50%)을 계정에 옮기고,
    /// 런(인벤토리·스테이터스·스테이지 맵)만 초기화한 뒤 클리어 결과 화면으로 이동한다.
    /// 계정 프로필·패시브 트리는 유지해야 하므로 ResetAllData가 아니라 ResetGameplayData를 쓴다.
    /// </summary>
    private void SettleGame()
    {
        RestoreGameTime();

        SceneLoader loader = SceneLoader.Instance;
        DataManager dataManager = DataManager.Instance;
        if (loader == null || loader.IsLoading || dataManager == null)
        {
            Debug.LogError("[KY_PausePopup] SceneLoader 또는 DataManager가 없어 정산할 수 없습니다.", this);
            return;
        }

        // 확인 팝업이 떠 있는 사이 상황이 바뀌었을 수 있으므로 다시 확인한다.
        if (!CanSettle())
        {
            RefreshSettleButton();
            return;
        }

        // 지갑이 비워지기 전에 결과 화면에 표시할 실제 적립액을 기록한다.
        int credits = dataManager.CalculateRunEndCredits(RunEndReason.Settle);
        var tracker = KY_RunStatsTracker.Instance;
        if (tracker == null || !tracker.FinishRun(true, credits, KY_ResultType.Settle))
        {
            Debug.LogError("[KY_PausePopup] 정산 결과 기록에 실패했습니다. KY_RunStatsTracker와 ResultPayload 연결을 확인하세요.", this);
            return;
        }

        if (!dataManager.SettleRunCredits(RunEndReason.Settle))
        {
            Debug.LogError("[KY_PausePopup] 정산 크레딧을 프로필에 저장하지 못했습니다.", this);
            return;
        }

        dataManager.ResetGameplayData();
        FindFirstObjectByType<YJ_StageSaveService>()?.DeleteSaveFile();
        loader.LoadScene(ResultSceneName);
    }

    private void SaveAndExitGame()
    {
        RestoreGameTime();
        if (externalSaveAndExit.HasValue)
        {
            externalSaveAndExit.Value.onYes?.Invoke();
            return;
        }
        DataManager.Instance?.SaveGameplayData();
        DataManager.Instance?.SavePassiveData();
        LoadSceneThroughLoader("TitleScene");
    }

    private void GiveUpGame()
    {
        RestoreGameTime();
        if (externalGiveUp.HasValue)
        {
            externalGiveUp.Value.onYes?.Invoke();
            return;
        }

        // 포기는 실패한 원정 종료다. 저장 데이터를 지우기 전에 현재 기록과 지갑을 결과 Payload에 남긴다.
        SceneLoader loader = SceneLoader.Instance;
        if (loader == null || loader.IsLoading)
        {
            Debug.LogError("[KY_PausePopup] SceneLoader가 없어 포기 결과 화면으로 전환할 수 없습니다.", this);
            return;
        }

        var tracker = KY_RunStatsTracker.Instance;
        // 포기는 크레딧을 계정으로 옮기지 않으므로 결과 화면에도 0을 표시한다.
        if (tracker == null || !tracker.FinishRun(false, 0))
        {
            Debug.LogError("[KY_PausePopup] 포기 결과 기록에 실패했습니다. KY_RunStatsTracker와 ResultPayload 연결을 확인하세요.", this);
            return;
        }

        // 포기는 런만 초기화한다. 계정 프로필(크레딧·패시브 트리)은 유지해야 하므로
        // 프로필까지 지우는 ResetAllData가 아니라 정산과 같은 런 초기화를 쓴다.
        DataManager.Instance?.ResetGameplayData();
        FindFirstObjectByType<YJ_StageSaveService>()?.DeleteSaveFile();
        loader.LoadScene(ResultSceneName);
    }

    private void LoadSceneThroughLoader(string sceneName)
    {
        SceneLoader loader = SceneLoader.Instance;
        if (loader == null || loader.IsLoading)
        {
            Debug.LogError($"[KY_PausePopup] SceneLoader가 없어 {sceneName} 씬으로 전환할 수 없습니다.", this);
            return;
        }

        loader.LoadScene(sceneName);
    }
}
