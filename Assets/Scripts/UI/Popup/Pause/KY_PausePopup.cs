using UnityEngine;

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
}
