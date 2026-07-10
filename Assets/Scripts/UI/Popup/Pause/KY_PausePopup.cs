using UnityEngine;

public class KY_PausePopup : KY_PopupBase
{
    private KY_SlideAnimator slideAnimator;

    void Awake()
    {
        slideAnimator = GetComponentInChildren<KY_SlideAnimator>();
    }

    public override void Open()
    {
        base.Open();
        slideAnimator.SlideIn();
        Time.timeScale = 0f;
    }

    public override void Close()
    {
        Time.timeScale = 1f;
        slideAnimator.SlideOut(() => base.Close());
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