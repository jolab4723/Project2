using UnityEngine;

public class KY_PausePopup : KY_PopupBase
{
    public override void Open()
    {
        base.Open();
        Time.timeScale = 0f;
    }

    public override void Close()
    {
        base.Close();
        Time.timeScale = 1f;
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