using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 한 줄 안내를 단계별로 보여 주는 튜토리얼 오버레이의 표시와 다음/건너뛰기를 담당한다.
/// 저장 데이터와 싱글 플레이 진입 판정은 별도 컨트롤러에서 연결한다.
/// </summary>
public sealed class KY_TutorialOverlay : MonoBehaviour
{
    [Serializable]
    public sealed class TutorialStep
    {
        [TextArea(1, 2)] public string message;
        public bool showDim = true;
        public GameObject illustration;
        public RectTransform highlightTarget;
    }

    [Header("레이어")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject dimLayer;
    [SerializeField] private GameObject highlightLayer;
    [SerializeField] private RectTransform circleHighlight;
    [SerializeField] private Material spotlightMaterial;
    [Tooltip("대상 크기와 무관한 고정 원 지름입니다. Canvas 기준 단위입니다.")]
    [SerializeField, Min(1f)] private float circleDiameter = 300f;
    [SerializeField, Min(0f)] private float edgeSoftness = 2f;

    private Image dimImage;
    private Material originalDimMaterial;
    private Material runtimeSpotlight;

    [Header("안내 패널")]
    [SerializeField] private TMP_Text guideText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button skipButton;

    [Header("단계")]
    [SerializeField] private List<TutorialStep> steps = new();
    [SerializeField] private bool pauseGameWhileOpen = true;

    [Header("테스트")]
    [Tooltip("현재 씬에서 튜토리얼 화면을 바로 확인하기 위한 옵션입니다.")]
    [SerializeField] private bool openOnStartForPreview;
    [Tooltip("테스트 자동 표시를 시작하기 전 대기 시간입니다.")]
    [SerializeField, Min(0f)] private float previewOpenDelay = 2f;

    private int currentStepIndex = -1;
    private float previousTimeScale = 1f;
    private bool isOpen;
    private bool isTimeScaleOverridden;

    public event Action Completed;
    public event Action Skipped;

    private void Awake()
    {
        canvasGroup ??= GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        InitializeSpotlight();
        if (nextButton != null)
            nextButton.onClick.AddListener(ShowNextStep);
        if (skipButton != null)
            skipButton.onClick.AddListener(Skip);

        SetVisible(false);
    }

    private void Start()
    {
        if (openOnStartForPreview)
        {
            StartCoroutine(OpenPreviewAfterDelay());
        }
    }

    private IEnumerator OpenPreviewAfterDelay()
    {
        if (previewOpenDelay > 0f)
            yield return new WaitForSecondsRealtime(previewOpenDelay);

        Open();
    }

    private void OnDestroy()
    {
        if (nextButton != null)
            nextButton.onClick.RemoveListener(ShowNextStep);
        if (skipButton != null)
            skipButton.onClick.RemoveListener(Skip);

        RestoreTimeScale();
        if (runtimeSpotlight != null)
        {
            if (dimImage != null) dimImage.material = originalDimMaterial;
            Destroy(runtimeSpotlight);
        }
    }

    private void OnDisable()
    {
        Close();
    }

    private void InitializeSpotlight()
    {
        if (runtimeSpotlight != null || spotlightMaterial == null || dimLayer == null)
            return;
        dimImage = dimLayer.GetComponent<Image>();
        if (dimImage == null) return;
        originalDimMaterial = dimImage.material;
        runtimeSpotlight = new Material(spotlightMaterial);
        runtimeSpotlight.name = "Tutorial Spotlight (Instance)";
        dimImage.material = runtimeSpotlight;
    }

    private void LateUpdate()
    {
        if (isOpen && currentStepIndex >= 0 && currentStepIndex < steps.Count)
            UpdateSpotlight(steps[currentStepIndex].highlightTarget);
    }

    private void UpdateSpotlight(RectTransform target)
    {
        if (runtimeSpotlight == null) return;
        Rect dimRect = dimImage.rectTransform.rect;
        runtimeSpotlight.SetVector("_DimRect", new Vector4(dimRect.xMin, dimRect.yMin, dimRect.width, dimRect.height));
        bool visible = target != null && target.gameObject.activeInHierarchy;
        runtimeSpotlight.SetFloat("_HoleEnabled", visible ? 1f : 0f);
        if (!visible) return;

        // Convert via screen space so different Canvas scales/cameras remain aligned.
        Canvas sourceCanvas = target.GetComponentInParent<Canvas>();
        Canvas dimCanvas = dimImage.canvas;
        Camera sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? sourceCanvas.worldCamera : null;
        Camera dimCamera = dimCanvas != null && dimCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? dimCanvas.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, target.TransformPoint(target.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(dimImage.rectTransform, screen, dimCamera, out Vector2 center);
        runtimeSpotlight.SetVector("_HoleCircle", new Vector4(center.x, center.y, Mathf.Max(1f, circleDiameter) * 0.5f, 0f));
        runtimeSpotlight.SetFloat("_Softness", edgeSoftness);
    }

    /// <summary>첫 안내부터 오버레이를 표시한다.</summary>
    public void Open()
    {
        if (steps.Count == 0)
        {
            Debug.LogWarning("[KY_TutorialOverlay] 표시할 튜토리얼 단계가 없습니다.", this);
            return;
        }

        if (pauseGameWhileOpen && !isOpen)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            isTimeScaleOverridden = true;
        }

        isOpen = true;
        SetVisible(true);
        ShowStep(0);
    }

    /// <summary>다음 안내를 표시하고 마지막 단계라면 완료한다.</summary>
    public void ShowNextStep()
    {
        if (!isOpen)
            return;

        int nextIndex = currentStepIndex + 1;
        if (nextIndex >= steps.Count)
        {
            Close();
            Completed?.Invoke();
            return;
        }

        ShowStep(nextIndex);
    }

    /// <summary>튜토리얼을 즉시 닫는다.</summary>
    public void Skip()
    {
        if (!isOpen)
            return;

        Close();
        Skipped?.Invoke();
    }

    /// <summary>현재 표시를 닫고 게임 시간을 원래 값으로 돌린다.</summary>
    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;
        currentStepIndex = -1;
        SetVisible(false);
        RestoreTimeScale();
    }

    private void ShowStep(int stepIndex)
    {
        currentStepIndex = stepIndex;
        TutorialStep step = steps[stepIndex];

        if (guideText != null)
            guideText.text = step.message;

        if (dimLayer != null)
            dimLayer.SetActive(step.showDim);

        bool showHighlight = runtimeSpotlight == null && step.highlightTarget != null && circleHighlight != null;
        if (highlightLayer != null)
            highlightLayer.SetActive(showHighlight);
        if (showHighlight)
            PositionCircleHighlight(step.highlightTarget);
        UpdateSpotlight(step.highlightTarget);

        foreach (TutorialStep tutorialStep in steps)
        {
            if (tutorialStep.illustration != null)
                tutorialStep.illustration.SetActive(tutorialStep == step);
        }
    }

    private void PositionCircleHighlight(RectTransform target)
    {
        RectTransform overlayRect = transform as RectTransform;
        if (overlayRect == null || target == null)
            return;

        circleHighlight.position = target.TransformPoint(target.rect.center);
        circleHighlight.sizeDelta = Vector2.one * Mathf.Max(1f, circleDiameter);
    }

    private void SetVisible(bool visible)
    {
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void RestoreTimeScale()
    {
        if (isTimeScaleOverridden)
        {
            Time.timeScale = previousTimeScale;
            isTimeScaleOverridden = false;
        }
    }
}
