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
    // SW 수정 : 로그인된 계정의 로그아웃은 타이틀에서 제공한다.
    [SerializeField] UnityEngine.UI.Button logoutButton;

    [Header("정보 표시")]
    [SerializeField] TextMeshProUGUI userInfoText;
    [SerializeField] TextMeshProUGUI versionText;
    [SerializeField] TMPro.TMP_Text accountStatusText;

    [Header("팝업 매니저")]
    [SerializeField] KY_PopupManager popupManager;

    private bool isMultiplayerEntryInProgress;

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
        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutClicked);

        RefreshUserInfo();
        ShowAccountStatus(string.Empty);
        versionText.text = "Version " + Application.version;

        YJ_BgmPlayer.Instance.Play(YJ_BgmPlayer.YJ_BgmType.TitleBgm);
    }

    // SW 수정 : 씬을 나갈 때 이 컴포넌트가 등록한 버튼 연결만 제거한다.
    void OnDestroy()
    {
        if (singlePlayButton != null) singlePlayButton.onClick.RemoveListener(OnSinglePlayClicked);
        if (multiPlayButton != null) multiPlayButton.onClick.RemoveListener(OnMultiPlayClicked);
        if (passiveSkillButton != null) passiveSkillButton.onClick.RemoveListener(OnPassiveSkillClicked);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
        if (logoutButton != null) logoutButton.onClick.RemoveListener(OnLogoutClicked);
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
        string playerName = null;
        DataManager dataManager = DataManager.Instance;
        if (dataManager != null)
        {
            var slot = dataManager.LoadSinglePlayerSlot();
            playerName = slot != null && slot.profile != null ? slot.profile.playerName : null;
        }

        // SW 수정 : 싱글 로컬 이름과 인증 상태를 함께 표시하되 Firebase 초기화는 멀티 진입 때만 한다.
        bool isSignedIn = Core.FirebaseService.Default.IsSignedIn;
        if (userInfoText != null)
        {
            string displayName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName;
            userInfoText.text = displayName;
        }
        if (logoutButton != null)
        {
            logoutButton.gameObject.SetActive(isSignedIn);
            logoutButton.interactable = !isMultiplayerEntryInProgress;
        }
    }

    void OnSinglePlayClicked()
    {
        Core.SceneLoader loader = Core.SceneLoader.Instance;

        if (isMultiplayerEntryInProgress || loader == null || loader.IsLoading)
            return;

        loader.LoadScene("SinglePlayerLobbyScene");
        YJ_BgmPlayer.Instance.Stop();
    }

    // SW 수정 : 멀티 진입에서만 인증을 준비하고 클라우드 동기화가 완료된 뒤 로비로 이동한다.
    async void OnMultiPlayClicked()
    {
        Core.SceneLoader loader = Core.SceneLoader.Instance;

        if (isMultiplayerEntryInProgress || loader == null || !loader.isActiveAndEnabled || loader.IsLoading)
            return;

        SetMultiplayerEntryInProgress(true);
        ShowAccountStatus("계정 확인 중...");
        try
        {
            Core.FirebaseService firebase = Core.FirebaseService.Default;
            Core.FirebaseInitializationResult initialization = await firebase.InitializeAsync();
            if (this == null || !isActiveAndEnabled)
                return;
            if (!initialization.IsSuccess)
            {
                ShowAccountStatus(initialization.Message);
                return;
            }

            string destinationScene = "LoginScene";
            if (firebase.IsSignedIn)
            {
                string userId = firebase.CurrentUserId;
                ShowAccountStatus("동기화 중...");
                Core.SaveDataOperationResult synchronization =
                    await Core.DataManager.SynchronizeSinglePlayerProfileWithFirebaseAsync();
                if (this == null || !isActiveAndEnabled)
                    return;
                if (!firebase.IsSignedIn || firebase.CurrentUserId != userId)
                {
                    ShowAccountStatus("로그인 계정이 변경되었습니다. 멀티플레이를 다시 선택해 주세요.");
                    return;
                }
                if (!synchronization.IsSuccess)
                {
                    ShowAccountStatus(synchronization.Message);
                    return;
                }
                if (!synchronization.IsCloudSynchronized && !firebase.IsLocalTestAccount)
                {
                    ShowAccountStatus("로컬 진행도는 보존했습니다. 인터넷 연결을 확인하고 동기화를 다시 시도해 주세요.");
                    return;
                }
                destinationScene = "MultiplayerLobbyScene";
            }

            loader = Core.SceneLoader.Instance;
            if (loader == null || !loader.isActiveAndEnabled || loader.IsLoading)
                return;
            if (!Application.CanStreamedLevelBeLoaded(destinationScene) ||
                !Application.CanStreamedLevelBeLoaded("LoadingScene"))
            {
                ShowAccountStatus(destinationScene + "과 LoadingScene의 빌드 등록을 확인해 주세요.");
                return;
            }
            loader.LoadScene(destinationScene);
            YJ_BgmPlayer.Instance.Stop();
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[KY_TitleSceneManager] 멀티플레이 진입 실패: {exception}");
            if (this != null && isActiveAndEnabled)
                ShowAccountStatus("멀티플레이 계정 준비 중 오류가 발생했습니다. 다시 시도해 주세요.");
        }
        finally
        {
            if (this != null)
            {
                SetMultiplayerEntryInProgress(false);
                RefreshUserInfo();
            }
        }
    }

    // SW 수정 : 연결 중 계정 변경을 막고, 로그아웃은 인증 세션만 종료해 로컬 진행도를 보존한다.
    void OnLogoutClicked()
    {
        if (isMultiplayerEntryInProgress || Core.SceneLoader.Instance?.IsLoading == true)
            return;
        if (Mirror.NetworkClient.active || Mirror.NetworkServer.active)
        {
            ShowAccountStatus("멀티플레이 연결을 종료한 뒤 로그아웃해 주세요.");
            return;
        }

        try
        {
            Core.FirebaseService.Default.SignOut();
            RefreshUserInfo();
            ShowAccountStatus(Core.FirebaseService.Default.IsLocalTestAccount
                ? "개발 테스트 계정은 실행 중 로그아웃할 수 없습니다."
                : string.Empty);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[KY_TitleSceneManager] 로그아웃 실패: {exception}");
            ShowAccountStatus("로그아웃하지 못했습니다. 다시 시도해 주세요.");
        }
    }

    private void SetMultiplayerEntryInProgress(bool inProgress)
    {
        isMultiplayerEntryInProgress = inProgress;
        if (singlePlayButton != null) singlePlayButton.interactable = !inProgress;
        if (multiPlayButton != null) multiPlayButton.interactable = !inProgress;
        if (passiveSkillButton != null) passiveSkillButton.interactable = !inProgress;
        if (settingsButton != null) settingsButton.interactable = !inProgress;
        if (quitButton != null) quitButton.interactable = !inProgress;
        if (logoutButton != null) logoutButton.interactable = !inProgress;
    }

    private void ShowAccountStatus(string message)
    {
        if (accountStatusText == null)
            return;
        accountStatusText.text = string.IsNullOrEmpty(message)
            ? (Core.FirebaseService.Default.IsSignedIn ? "로그인 중" : "로그인 안 됨")
            : message;
        accountStatusText.gameObject.SetActive(true);
    }

    void OnPassiveSkillClicked() { popupManager.Show(PopupType.PassiveSkill); }
}
