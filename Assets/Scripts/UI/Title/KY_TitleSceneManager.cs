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
    private UILabelDatabaseSO accountLabels;
    private string lastAccountMessage = string.Empty;

    private void HandleLanguageChanged(GameLanguage _) => ShowAccountStatus(lastAccountMessage);

    void Start()
    {
        // 중단 저장 기능이 아직 없으므로 타이틀 진입 시에는 항상 비활성으로 시작한다.
        // 저장 검사 기능이 추가되면 SetContinueAvailable 결과만 넘겨 활성화하면 된다.
        // WJ 이우진 수정(2026-10-06): 이어하기 구현. 실제 활성 여부는 아래 RefreshUserInfo → RefreshContinueButton이 정한다.
        SetContinueAvailable(false);
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);

        singlePlayButton.onClick.AddListener(OnSinglePlayClicked);
        multiPlayButton.onClick.AddListener(OnMultiPlayClicked);
        passiveSkillButton.onClick.AddListener(OnPassiveSkillClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutClicked);

        RefreshUserInfo();
        ShowAccountStatus(string.Empty);
        // SW 수정 : 타이틀 설정창에서 언어를 바꾸면 계정 안내도 바로 같은 언어로 다시 표시한다.
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;
        versionText.text = "Version " + Application.version;

        YJ_BgmPlayer.Instance.Play(YJ_BgmPlayer.YJ_BgmType.TitleBgm);
    }

    // SW 수정 : 씬을 나갈 때 이 컴포넌트가 등록한 버튼 연결만 제거한다.
    void OnDestroy()
    {
        if (singlePlayButton != null)
            singlePlayButton.onClick.RemoveListener(OnSinglePlayClicked);
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinueClicked);
        if (multiPlayButton != null)
            multiPlayButton.onClick.RemoveListener(OnMultiPlayClicked);
        if (passiveSkillButton != null)
            passiveSkillButton.onClick.RemoveListener(OnPassiveSkillClicked);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
        if (quitButton != null)
            quitButton.onClick.RemoveListener(OnQuitClicked);
        if (logoutButton != null)
            logoutButton.onClick.RemoveListener(OnLogoutClicked);
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= HandleLanguageChanged;
    }

    /// <summary>유효한 중단 저장 여부에 맞춰 이어하기 버튼의 입력 가능 상태를 갱신한다.</summary>
    public void SetContinueAvailable(bool isAvailable)
    {
        if (continueButton == null)
            return;

        // WJ 이우진 수정(2026-10-07): TitleScene에 Continue 오브젝트가 꺼진 채 저장돼 있어 이어하기가 보이지 않았다.
        // 버튼은 항상 보이게 켜고, 이어할 런이 없으면 아래의 입력 불가(회색) 상태로만 구분한다.
        if (!continueButton.gameObject.activeSelf)
        {
            continueButton.gameObject.SetActive(true);
            // 꺼진 채 저장돼 있던 버튼이라 호버 장식(Sidebar)이 보이는 상태로 깨어난다.
            // 다른 버튼과 같은 숨김 상태로 맞추고, 호버하면 KY_ButtonSideDecorEffect가 다시 나타나게 한다.
            foreach (KY_FadeEffect decorFade in continueButton.GetComponentsInChildren<KY_FadeEffect>(true))
                decorFade.SetAlphaImmediate(0f);
        }

        continueButton.interactable = isAvailable;

        if (continueButton.TryGetComponent(out CanvasGroup canvasGroup))
        {
            canvasGroup.interactable = isAvailable;
            canvasGroup.blocksRaycasts = isAvailable;
            // WJ 이우진 추가(2026-10-07): 버튼의 색 전환(Transition)이 None이라 비활성이어도 모양이 같아, 흐리게 표시해 구분한다.
            canvasGroup.alpha = isAvailable ? 1f : DisabledContinueAlpha;
        }
    }

    // WJ 이우진 추가(2026-10-07): 이어할 런이 없을 때 이어하기 버튼의 투명도.
    private const float DisabledContinueAlpha = 0.4f;

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

        // SW 수정 : 타이틀은 Firebase를 멀티 진입 때만 초기화하므로, 로그인 세션 대신 현재 싱글 저장이 계정 소유인지로도 판단한다.
        if (logoutButton != null)
        {
            logoutButton.gameObject.SetActive(isSignedIn || !DataManager.IsGuestProfile);
            logoutButton.interactable = !isMultiplayerEntryInProgress;
        }
        if (string.IsNullOrEmpty(lastAccountMessage))
            ShowAccountStatus(string.Empty);

        // WJ 이우진 추가(2026-10-06): 로그아웃 등으로 저장 주인이 바뀌면 이어할 런도 달라지므로 함께 갱신한다.
        RefreshContinueButton();
    }

    // WJ 이우진 추가(2026-10-06): 이어하기는 항상 스테이지 선택 화면에서 재개한다(진행 중 노드는 선택된 채로 복원).
    private const string ContinueSceneName = "StageSelect";

    /// <summary>WJ 이우진 추가(2026-10-06): 저장 주인의 게임 저장에 맵이 있는 싱글 런이 있으면 이어하기를 켠다.</summary>
    private void RefreshContinueButton()
    {
        SetContinueAvailable(!isMultiplayerEntryInProgress && HasContinuableRun());
    }

    private static bool HasContinuableRun() => DataManager.Instance != null && DataManager.Instance.HasContinuableRun();

    /// <summary>
    /// WJ 이우진 추가(2026-10-06): 저장된 결과 화면 기록으로 원정을 이어서 집계하고 스테이지 선택으로 이동한다.
    /// 플레이어 상태는 스테이지·캠프에 들어갈 때 YJ_StageManager가 게임 저장에서 복원한다.
    /// </summary>
    void OnContinueClicked()
    {
        Core.SceneLoader loader = Core.SceneLoader.Instance;
        if (isMultiplayerEntryInProgress || loader == null || loader.IsLoading || !HasContinuableRun())
            return;

        KY_RunStatsTracker.Instance?.ResumeRun(DataManager.Instance.LoadSavedRunStats());
        loader.LoadScene(ContinueSceneName);
        YJ_BgmPlayer.Instance.Stop();
    }

    void OnSinglePlayClicked()
    {
        Core.SceneLoader loader = Core.SceneLoader.Instance;

        if (isMultiplayerEntryInProgress || loader == null || loader.IsLoading)
            return;

        // WJ 이우진 추가(2026-10-06): 이어할 원정이 있으면 새 게임이 덮어쓰므로 먼저 확인한다(실제 덮어쓰기는 캐릭터 선택의 BeginNewGame).
        if (popupManager != null && HasContinuableRun())
        {
            popupManager.ShowConfirm(new KY_DialogData
            {
                message = GetAccountLabel("title_ui.new_game_confirm", "새 게임을 시작하시겠습니까?"),
                warningText = GetAccountLabel("title_ui.new_game_warning", "진행 중인 원정이 사라지고 처음부터 시작합니다."),
                onYes = LoadSinglePlayerLobby,
            });
            return;
        }

        LoadSinglePlayerLobby();
    }

    private void LoadSinglePlayerLobby()
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

    // SW 수정 : 연결 중 계정 변경을 막고, 로그아웃하면 계정 진행도는 보존한 채 싱글을 게스트 저장으로 전환한다.
    void OnLogoutClicked()
    {
        if (isMultiplayerEntryInProgress || Core.SceneLoader.Instance?.IsLoading == true)
            return;
        if (Mirror.NetworkClient.active || Mirror.NetworkServer.active)
        {
            ShowAccountStatus("멀티플레이 연결을 종료한 뒤 로그아웃해 주세요.");
            return;
        }
        if (Core.FirebaseService.Default.IsLocalTestAccount)
        {
            ShowAccountStatus("개발 테스트 계정은 실행 중 로그아웃할 수 없습니다.");
            return;
        }
        if (popupManager == null)
        {
            LogoutConfirmed();
            return;
        }

        popupManager.ShowConfirm(new KY_DialogData
        {
            message = GetAccountLabel("title_ui.logout_confirm", "로그아웃할까요?"),
            warningText = GetAccountLabel("title_ui.logout_guest_notice",
                "로그아웃하면 싱글 플레이는 게스트 저장으로 전환됩니다. 계정 진행도는 다음 로그인 때 그대로 이어집니다."),
            onYes = LogoutConfirmed,
        });
    }

    /// <summary>
    /// SW 수정 : 저장된 로그인 세션까지 끝내도록 Firebase를 준비한 뒤 로그아웃하고, 싱글 저장을 게스트로 전환한다.
    /// 세션을 끝내지 못하면 저장 소유도 바꾸지 않아 로그인 상태와 저장 소유가 어긋나지 않게 한다.
    /// </summary>
    async void LogoutConfirmed()
    {
        if (isMultiplayerEntryInProgress || Mirror.NetworkClient.active || Mirror.NetworkServer.active)
            return;
        SetMultiplayerEntryInProgress(true);
        try
        {
            Core.FirebaseService firebase = Core.FirebaseService.Default;
            Core.FirebaseInitializationResult initialization = await firebase.InitializeAsync();
            if (this == null)
                return;
            if (!initialization.IsSuccess)
            {
                ShowAccountStatus("로그아웃하지 못했습니다. 다시 시도해 주세요.");
                return;
            }
            firebase.SignOut();
            DataManager.SwitchToGuestProfile();
            ShowAccountStatus(string.Empty);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[KY_TitleSceneManager] 로그아웃 실패: {exception}");
            if (this != null)
                ShowAccountStatus("로그아웃하지 못했습니다. 다시 시도해 주세요.");
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

    private void SetMultiplayerEntryInProgress(bool inProgress)
    {
        isMultiplayerEntryInProgress = inProgress;
        if (singlePlayButton != null)
            singlePlayButton.interactable = !inProgress;
        if (multiPlayButton != null)
            multiPlayButton.interactable = !inProgress;
        if (passiveSkillButton != null)
            passiveSkillButton.interactable = !inProgress;
        if (settingsButton != null)
            settingsButton.interactable = !inProgress;
        if (quitButton != null)
            quitButton.interactable = !inProgress;
        if (logoutButton != null)
            logoutButton.interactable = !inProgress;
        RefreshContinueButton(); // WJ 이우진 추가(2026-10-06): 연결 중에는 이어하기도 막는다.
    }

    private void ShowAccountStatus(string message)
    {
        if (accountStatusText == null)
            return;
        lastAccountMessage = message;
        // SW 수정 : 계정 안내 원문은 유지하고 로그인 화면과 같은 세션 메시지 번역기로 현재 언어 문구·폰트를 적용한다.
        if (accountLabels == null)
            accountLabels = Resources.Load<UILabelDatabaseSO>(SessionUIMessageLocalizer.DatabasePath);
        // SW 수정 : 안내가 없을 때는 현재 싱글 저장의 소유를 표시한다. 게스트는 Guest, 계정은 이메일만 표시한다.
        accountStatusText.text = string.IsNullOrEmpty(message)
            ? GetSaveOwnerCaption()
            : SessionUIMessageLocalizer.GetMessage(accountLabels, message);
        TMP_FontAsset font = YJ_LanguageManager.Instance != null ? YJ_LanguageManager.Instance.GetCurrentFont() : null;
        if (font != null)
            accountStatusText.font = font;
        accountStatusText.gameObject.SetActive(true);
    }

    /// <summary>SW 수정 : 현재 싱글 저장의 소유 표시. 다른 사람이 화면을 보더라도 계정 확인만 되도록 이메일 앞부분만 보인다.</summary>
    private string GetSaveOwnerCaption()
    {
        if (DataManager.IsGuestProfile)
            return GetAccountLabel("title_ui.guest", "Guest");
        string email = DataManager.LocalProfileOwnerEmail;
        if (string.IsNullOrEmpty(email))
            return SessionUIMessageLocalizer.GetMessage(accountLabels, "로그인 중");
        int at = email.IndexOf('@');
        if (at <= 0)
            return email;
        string local = email.Substring(0, at);
        return (local.Length <= 3 ? local.Substring(0, 1) : local.Substring(0, 3)) + "***" + email.Substring(at);
    }

    private string GetAccountLabel(string key, string fallback)
    {
        if (accountLabels == null)
            accountLabels = Resources.Load<UILabelDatabaseSO>(SessionUIMessageLocalizer.DatabasePath);
        string label = accountLabels != null ? accountLabels.GetLabel(key) : null;
        return string.IsNullOrEmpty(label) || label == key ? fallback : label;
    }

    void OnPassiveSkillClicked() { popupManager.Show(PopupType.PassiveSkill); }
}
