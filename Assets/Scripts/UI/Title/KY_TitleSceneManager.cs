using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Core;

// 타이틀 씬 관리 매니저입니다.
public class KY_TitleSceneManager : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] Button singlePlayButton;
    [SerializeField] Button multiPlayButton;
    [SerializeField] Button continueButton;
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
        // 중단 저장 기능이 아직 없으므로 타이틀 진입 시에는 항상 비활성으로 시작한다.
        // 저장 검사 기능이 추가되면 SetContinueAvailable 결과만 넘겨 활성화하면 된다.
        SetContinueAvailable(false);

        singlePlayButton.onClick.AddListener(OnSinglePlayClicked);
        multiPlayButton.onClick.AddListener(OnMultiPlayClicked);
        passiveSkillButton.onClick.AddListener(OnPassiveSkillClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

        RefreshUserInfo();
        versionText.text = "Version " + Application.version;

        YJ_BgmPlayer.Instance.Play(YJ_BgmPlayer.YJ_BgmType.TitleBgm);
    }

    /// <summary>유효한 중단 저장 여부에 맞춰 이어하기 버튼의 입력 가능 상태를 갱신한다.</summary>
    public void SetContinueAvailable(bool isAvailable)
    {
        if (continueButton == null)
            return;

        continueButton.interactable = isAvailable;

        if (continueButton.TryGetComponent(out CanvasGroup canvasGroup))
        {
            canvasGroup.interactable = isAvailable;
            canvasGroup.blocksRaycasts = isAvailable;
        }
    }

    void OnSettingsClicked()
    {
        popupManager.Show(PopupType.Settings);
    }

    void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void RefreshUserInfo()
    {
        if (userInfoText == null)
            return;

        string playerName = null;
        DataManager dataManager = DataManager.Instance;
        if (dataManager != null)
        {
            var slot = dataManager.LoadSinglePlayerSlot();
            playerName = slot != null && slot.profile != null ? slot.profile.playerName : null;
        }

        userInfoText.text = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName;
    }

    void OnSinglePlayClicked()
    {
        Core.SceneLoader loader = Core.SceneLoader.Instance;

        if (loader == null || loader.IsLoading)
            return;

        loader.LoadScene("SinglePlayerLobbyScene");
        YJ_BgmPlayer.Instance.Stop();
    }

    void OnMultiPlayClicked()
    {
        Core.SceneLoader loader = Core.SceneLoader.Instance;

        if (loader == null || loader.IsLoading)
            return;

        loader.LoadScene("MultiplayerLobbyScene");
        YJ_BgmPlayer.Instance.Stop();
    }

    void OnPassiveSkillClicked() { popupManager.Show(PopupType.PassiveSkill); }
}
