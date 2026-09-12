using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Core;

public class KY_PausePopup : KY_PopupBase
{
    [SerializeField] private bool pauseGameTime = true; // SW 수정
    private KY_SlideAnimator slideAnimator;
    private bool ownsTimePause;
    private float previousTimeScale;

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

        KY_PopupManager.Instance.ShowConfirm(new KY_DialogData
        {
            message = "게임을 포기하시겠습니까?",
            warningText = "경고: 현재 게임 데이터가 사라집니다.",
            onYes = GiveUpGame
        });
    }

public void OnClickSaveAndExit()
    {
        if (KY_PopupManager.Instance == null) return;

        KY_PopupManager.Instance.ShowConfirm(new KY_DialogData
        {
            message = "게임을 저장하고 종료하시겠습니까?",
            onYes = SaveAndExitGame
        });
    }

private void SaveAndExitGame()
    {
        RestoreGameTime();
        DataManager.Instance?.SaveGameplayData();
        DataManager.Instance?.SavePassiveData();
        SceneManager.LoadScene("TitleSeane");
    }

    private void GiveUpGame()
    {
        RestoreGameTime();
        DataManager.Instance?.ResetAllData();
        SceneManager.LoadScene("TitleSeane");
    }
}
