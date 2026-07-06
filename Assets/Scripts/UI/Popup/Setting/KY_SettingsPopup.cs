using UnityEngine;

public class KY_SettingsPopup : KY_PopupBase
{
    public void OnClickBack()
    {
        KY_PopupManager.Instance.Hide();
        // PausePopup 다시 열기는 다음 단계에서
    }
}