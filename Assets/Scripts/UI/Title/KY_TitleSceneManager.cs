using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// 타이틀 씬 관리 매니저입니다.
public class KY_TitleSceneManager : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] Button singlePlayButton;
    [SerializeField] Button multiPlayButton;
    [SerializeField] Button passiveSkillButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button quitButton;

    [Header("정보 표시")]
    [SerializeField] TextMeshProUGUI userInfoText;
    [SerializeField] TextMeshProUGUI versionText;

    [Header("팝업 매니저")]
    [SerializeField] KY_PopupManager popupManager;

    void Start()
    {
        singlePlayButton.onClick.AddListener(OnSinglePlayClicked);
        multiPlayButton.onClick.AddListener(OnMultiPlayClicked);
        passiveSkillButton.onClick.AddListener(OnPassiveSkillClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

        RefreshUserInfo();
        versionText.text = "Version " + Application.version;
    }

    void OnSettingsClicked()
    {
        popupManager.Show(PopupType.Settings);
    }

    void OnQuitClicked()
    {
        Application.Quit();
    }

    void RefreshUserInfo()
    {
        userInfoText.text = "유저 이름";
    }

    void OnSinglePlayClicked()
    {
        Core.SceneLoader loader = Core.SceneLoader.Instance;

        if (loader == null || loader.IsLoading)
            return;

        loader.LoadScene("SinglePlayerLobbyScene");
    }

    void OnMultiPlayClicked()
    {
        Core.SceneLoader loader = Core.SceneLoader.Instance;

        if (loader == null || loader.IsLoading)
            return;

        loader.LoadScene("MultiplayerLobbyScene");
    }

    void OnPassiveSkillClicked() { popupManager.Show(PopupType.PassiveSkill); }
}
