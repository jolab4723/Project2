using UnityEngine;
using UnityEngine.UI;
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

    /// <summary>세션 종료 확인과 실행을 외부에 맡긴다. null로 해제하면 기존 싱글 동작을 사용한다.</summary>
    public void BindExitActions(KY_DialogData? giveUp, KY_DialogData? saveAndExit)
    {
        externalGiveUp = giveUp;
        externalSaveAndExit = saveAndExit;
    }

    /// <summary>싱글에서는 게임 시간을 멈춘다. 멀티플레이 메뉴는 false로 설정해 화면만 연다.</summary>
    public bool PauseGameTime
    {
        get => pauseGameTime;
        set
        {
            pauseGameTime = value;
            if (!value) RestoreGameTime();
        }
    }

void Awake()
    {
        slideAnimator = GetComponentInChildren<KY_SlideAnimator>(true);
        if (slideAnimator != null) slideAnimator.ignoreTimeScale = true;

        // 씬에 저장된 UnityEvent가 비어 있어도 포기 버튼이 동작하도록 자동 연결한다.
        WirePauseButtons();
    }

private void OnEnable()
    {
        WirePauseButtons();
    }

    private void WirePauseButtons()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button == null) continue;

            if (button.gameObject.name == "Giveup")
            {
                button.onClick.RemoveListener(OnClickGiveUp);
                button.onClick.AddListener(OnClickGiveUp);
            }
            else if (button.gameObject.name == "Save")
            {
                button.onClick.RemoveListener(OnClickSaveAndExit);
                button.onClick.AddListener(OnClickSaveAndExit);
            }
        }
    }


    public override void Open()
    {
        base.Open();
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

    private void OnDisable() => RestoreGameTime();

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
            message = "게임을 포기하시겠습니까?",
            warningText = "경고: 현재 게임 데이터가 사라집니다.",
        };
        dialog.onYes = GiveUpGame;
        KY_PopupManager.Instance.ShowConfirm(dialog);
    }

public void OnClickSaveAndExit()
    {
        if (KY_PopupManager.Instance == null) return;

        KY_DialogData dialog = externalSaveAndExit ?? new KY_DialogData
        {
            message = "게임을 저장하고 종료하시겠습니까?",
        };
        dialog.onYes = SaveAndExitGame;
        KY_PopupManager.Instance.ShowConfirm(dialog);
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
        if (tracker == null || !tracker.FinishRun(false))
        {
            Debug.LogError("[KY_PausePopup] 포기 결과 기록에 실패했습니다. KY_RunStatsTracker와 ResultPayload 연결을 확인하세요.", this);
            return;
        }

        DataManager.Instance?.ResetAllData();
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
