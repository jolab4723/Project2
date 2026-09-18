using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 로그인과 계정 생성 팝업의 표시 전환을 담당한다.
/// </summary>
public sealed class KY_LoginSceneManager : MonoBehaviour
{
    [SerializeField] private KY_LoginPopup loginPopup;
    [SerializeField] private KY_CreateAccountPopup createAccountPopup;

    [Header("팝업 전환 연출")]
    [SerializeField] private KY_FadeEffect loginContentFade;
    [SerializeField] private KY_FadeEffect createAccountContentFade;

    [Header("공용 안내")]
    [SerializeField] private TMP_Text logText;

    private Tween transitionTween;
    private MonoBehaviour nextPopup;
    private KY_FadeEffect nextFade;
    private bool focusLoginAfterTransition;
    private bool isInitialized;
    private bool isTransitioning;

    private void OnEnable()
    {
        if (loginPopup != null)
            loginPopup.CreateAccountPopupRequested += ShowCreateAccountPopup;

        if (createAccountPopup != null)
            createAccountPopup.LoginPopupRequested += ShowLoginPopup;
    }

    private void OnDisable()
    {
        transitionTween?.Kill();
        isTransitioning = false;

        if (loginPopup != null)
            loginPopup.CreateAccountPopupRequested -= ShowCreateAccountPopup;

        if (createAccountPopup != null)
            createAccountPopup.LoginPopupRequested -= ShowLoginPopup;
    }

    /// <summary>씬에 처음 들어오면 로그인 팝업을 표시한다.</summary>
    private void Start()
    {
        SetPopupState(loginPopup, true);
        SetPopupState(createAccountPopup, false);
        ClearLogText();
        loginPopup?.FocusAccountIdInput();
        isInitialized = true;
    }

    /// <summary>계정 생성 팝업에서 로그인 팝업으로 전환한다.</summary>
    public void ShowLoginPopup()
    {
        if (!isInitialized)
        {
            SetPopupState(loginPopup, true);
            SetPopupState(createAccountPopup, false);
            loginPopup?.FocusAccountIdInput();
            return;
        }

        BeginTransition(createAccountPopup, createAccountContentFade, loginPopup, loginContentFade, true);
    }

    /// <summary>로그인 팝업에서 계정 생성 팝업으로 전환한다.</summary>
    public void ShowCreateAccountPopup()
    {
        if (!isInitialized)
        {
            SetPopupState(loginPopup, false);
            SetPopupState(createAccountPopup, true);
            return;
        }

        BeginTransition(loginPopup, loginContentFade, createAccountPopup, createAccountContentFade, false);
    }

    /// <summary>현재 내용을 페이드아웃한 뒤 다음 팝업 내용을 페이드인한다.</summary>
    private void BeginTransition(MonoBehaviour currentPopup, KY_FadeEffect currentFade, MonoBehaviour targetPopup, KY_FadeEffect targetFade, bool focusLogin)
    {
        if (isTransitioning)
            return;

        isTransitioning = true;
        ClearLogText();
        nextPopup = targetPopup;
        nextFade = targetFade;
        focusLoginAfterTransition = focusLogin;

        if (currentFade == null)
        {
            ShowNextPopup();
            return;
        }

        transitionTween?.Kill();
        transitionTween = currentFade.FadeOut().OnComplete(ShowNextPopup);
    }

    /// <summary>다음 팝업을 표시하고 내용 페이드인을 시작한다.</summary>
    private void ShowNextPopup()
    {
        SetPopupState(loginPopup, nextPopup == loginPopup);
        SetPopupState(createAccountPopup, nextPopup == createAccountPopup);

        if (nextFade == null)
        {
            FinishTransition();
            return;
        }

        transitionTween = nextFade.FadeIn().OnComplete(FinishTransition);
    }

    /// <summary>전환을 마치고 필요하면 로그인 입력칸에 포커스를 둔다.</summary>
    private void FinishTransition()
    {
        if (focusLoginAfterTransition)
            loginPopup?.FocusAccountIdInput();

        isTransitioning = false;
        nextPopup = null;
        nextFade = null;
    }

    /// <summary>팝업 전환 시 이전 안내 문구를 지운다.</summary>
    private void ClearLogText()
    {
        if (logText != null)
            logText.text = string.Empty;
    }

    /// <summary>지정한 팝업의 활성 상태를 바꾼다.</summary>
    private static void SetPopupState(MonoBehaviour popup, bool isVisible)
    {
        if (popup != null)
            popup.gameObject.SetActive(isVisible);
    }
}
