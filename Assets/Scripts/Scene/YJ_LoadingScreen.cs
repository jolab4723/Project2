using System.Collections;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class YJ_LoadingScreen : MonoBehaviour
{
    private static YJ_LoadingScreen instance;

    [Header("References")]
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressText;

    [Header("Presentation")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.25f;
    [SerializeField, Min(0f)] private float progressSmoothSpeed = 2f;
    [SerializeField] private bool showOnStart = true;
    [SerializeField] private string defaultMessage = "Loading";

    private SceneLoader subscribedLoader;
    private Coroutine fadeRoutine;
    private float displayedProgress;
    private float targetProgress;

    public bool IsVisible =>
        contentRoot != null &&
        contentRoot.activeSelf &&
        canvasGroup != null &&
        canvasGroup.alpha > 0f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveReferences();

        if (showOnStart)
            ShowImmediate(defaultMessage, 0f);
        else
            HideImmediate();
    }

    private void OnEnable()
    {
        SubscribeToSceneLoader();
    }

    private void Start()
    {
        // 실행 순서상 SceneLoader가 OnEnable 이후 준비되는 경우를 보완합니다.
        SubscribeToSceneLoader();
    }

    private void Update()
    {
        if (!IsVisible || Mathf.Approximately(displayedProgress, targetProgress))
            return;

        displayedProgress = Mathf.MoveTowards(
            displayedProgress,
            targetProgress,
            progressSmoothSpeed * Time.unscaledDeltaTime);
        ApplyProgress(displayedProgress);
    }

    private void OnDisable()
    {
        UnsubscribeFromSceneLoader();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>로딩 화면을 즉시 표시하고 메시지와 진행률을 초기화합니다.</summary>
    public void ShowImmediate(string message, float progress = 0f)
    {
        ResolveReferences();
        StopFadeRoutine();

        if (contentRoot != null)
            contentRoot.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;
        }

        SetMessage(message);
        SetProgressImmediate(progress);
    }

    /// <summary>현재 로딩 단계에 표시할 메시지를 변경합니다.</summary>
    public void SetMessage(string message)
    {
        if (progressText == null)
            return;

        string resolvedMessage =
            string.IsNullOrWhiteSpace(message) ? defaultMessage : message;
        progressText.text =
            $"{resolvedMessage}  {Mathf.RoundToInt(displayedProgress * 100f)}%";
    }

    /// <summary>목표 진행률을 지정하고 화면에서는 부드럽게 따라가도록 합니다.</summary>
    public void SetProgress(float progress)
    {
        targetProgress = Mathf.Clamp01(progress);
    }

    private void SetProgressImmediate(float progress)
    {
        displayedProgress = Mathf.Clamp01(progress);
        targetProgress = displayedProgress;
        ApplyProgress(displayedProgress);
    }

    private void ApplyProgress(float progress)
    {
        if (progressFill != null)
            progressFill.fillAmount = progress;

        if (progressText == null)
            return;

        string currentText = progressText.text;
        int separatorIndex = currentText.LastIndexOf("  ");
        string message = separatorIndex >= 0
            ? currentText.Substring(0, separatorIndex)
            : defaultMessage;
        progressText.text =
            $"{message}  {Mathf.RoundToInt(progress * 100f)}%";
    }

    private void SubscribeToSceneLoader()
    {
        SceneLoader loader = SceneLoader.Instance;
        if (loader == null || subscribedLoader == loader)
            return;

        UnsubscribeFromSceneLoader();
        subscribedLoader = loader;
        subscribedLoader.OnLoadProgress += HandleLoadProgress;
        subscribedLoader.OnSceneLoaded += HandleSceneLoaded;
    }

    private void UnsubscribeFromSceneLoader()
    {
        if (subscribedLoader == null)
            return;

        subscribedLoader.OnLoadProgress -= HandleLoadProgress;
        subscribedLoader.OnSceneLoaded -= HandleSceneLoaded;
        subscribedLoader = null;
    }

    private void HandleLoadProgress(float progress)
    {
        if (!IsVisible || fadeRoutine != null)
            ShowImmediate("Loading Scene", 0f);

        SetProgress(progress);
    }

    private void HandleSceneLoaded(string sceneName)
    {
        SetProgressImmediate(1f);
        StopFadeRoutine();
        fadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        // 새 씬의 Awake/OnEnable 이후 첫 프레임까지 로딩 화면을 유지합니다.
        yield return null;

        if (canvasGroup == null || fadeOutDuration <= 0f)
        {
            ApplyHiddenState();
            fadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            yield return null;
        }

        ApplyHiddenState();
        fadeRoutine = null;
    }

    private void HideImmediate()
    {
        StopFadeRoutine();
        ApplyHiddenState();
    }

    private void ApplyHiddenState()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (contentRoot != null)
            contentRoot.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (contentRoot == null && transform.childCount > 0)
            contentRoot = transform.GetChild(0).gameObject;

        if (contentRoot != null && canvasGroup == null)
        {
            canvasGroup = contentRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = contentRoot.AddComponent<CanvasGroup>();
        }

        if (progressText == null && contentRoot != null)
            progressText = contentRoot.GetComponentInChildren<TMP_Text>(true);
    }

    private void StopFadeRoutine()
    {
        if (fadeRoutine == null)
            return;

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }
}
