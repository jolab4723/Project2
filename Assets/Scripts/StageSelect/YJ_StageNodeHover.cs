using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class YJ_StageNodeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Background")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RectTransform backgroundRect;

    [Header("Icon")]
    [SerializeField] private RectTransform iconRect;
    [SerializeField] private Image iconImage;

    [Header("Hover Effect")]
    [SerializeField, Range(0f, 1f)] private float normalAlpha = 0.1f;
    [SerializeField, Range(0f, 1f)] private float hoverAlpha = 0.5f;
    [SerializeField, Range(0f, 1f)] private float selectedAlpha = 1f;
    [SerializeField, Min(1f)] private float hoverScale = 1.1f;
    [SerializeField, Min(0.01f)] private float transitionDuration = 0.2f;

    [Header("Hover Float")]
    [SerializeField, Min(0f)] private float hoverFloatDistance = 10f;
    [SerializeField, Min(0.01f)] private float hoverFloatFrequency = 0.75f;
    [SerializeField, Min(0.01f)] private float hoverFloatReturnDuration = 0.15f;

    [Header("Click Effect")]
    [SerializeField, Range(0.1f, 1f)] private float pressedIconScale = 0.9f;
    [SerializeField, Min(0.01f)] private float shrinkDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float expandDuration = 0.12f;

    [Header("Selection")]
    [SerializeField] private YJ_StageSelectManager stageSelectManager;
    [SerializeField] private YJ_StageNodeData nodeData;
    [SerializeField] private bool isInteractable = true;
    [SerializeField] private bool isSelected;

    [Header("Locked State")]
    [SerializeField, Range(0f, 1f)] private float lockedAlpha = 0.02f;

    [Header("Cleared Floor")]
    [SerializeField] private Color clearedTint = new Color(0.45f, 0.45f, 0.45f, 1f);

    private Vector3 normalScale;
    private Vector3 normalIconScale;
    private Vector2 normalIconPosition;
    private Color originalBackgroundColor;
    private Color originalIconColor;
    private bool isPointerInside;
    private bool isClearedFloor;
    private bool isPathBlocked;
    private Coroutine transitionRoutine;
    private Coroutine clickRoutine;
    private Coroutine hoverFloatRoutine;

    public bool IsSelected => isSelected;
    public bool IsInteractable => isInteractable;
    public YJ_StageNodeData NodeData => nodeData;

    private void Reset()
    {
        FindReferences();
    }

    private void Awake()
    {
        FindReferences();

        if (backgroundRect == null || backgroundImage == null)
        {
            enabled = false;
            return;
        }

        normalScale = backgroundRect.localScale;
        originalBackgroundColor = backgroundImage.color;

        if (iconRect != null)
        {
            normalIconScale = iconRect.localScale;
            normalIconPosition = iconRect.anchoredPosition;
        }

        if (iconImage != null)
            originalIconColor = iconImage.color;

        isSelected = false;
        SetBackgroundVisual(normalAlpha, normalScale);
    }

    private void OnDisable()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (clickRoutine != null)
        {
            StopCoroutine(clickRoutine);
            clickRoutine = null;
        }

        if (hoverFloatRoutine != null)
        {
            StopCoroutine(hoverFloatRoutine);
            hoverFloatRoutine = null;
        }

        if (stageSelectManager != null)
            stageSelectManager.NotifyNodeDisabled(this);

        isPointerInside = false;
        isSelected = false;
        isClearedFloor = false;
        isPathBlocked = false;
        ApplyDisabledTint();

        if (backgroundRect != null && backgroundImage != null)
        {
            backgroundRect.gameObject.SetActive(true);
            SetBackgroundVisual(normalAlpha, normalScale);
        }

        if (iconRect != null)
        {
            iconRect.localScale = normalIconScale;
            iconRect.anchoredPosition = normalIconPosition;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isInteractable)
            return;

        isPointerInside = true;
        RefreshBackgroundVisual();
        StartHoverFloat();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        RefreshBackgroundVisual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isInteractable || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (stageSelectManager == null)
            stageSelectManager = YJ_StageSelectManager.Instance;

        bool selected = stageSelectManager != null
            ? stageSelectManager.SelectNode(this)
            : SetSelectedWithoutManager();

        if (selected)
            PlayClickAnimation();
    }

    public void Initialize(YJ_StageNodeData data, YJ_StageSelectManager manager)
    {
        nodeData = data;
        stageSelectManager = manager;
        isSelected = false;
    }

    public void SetInteractable(bool interactable)
    {
        ApplyState(isClearedFloor, isPathBlocked, interactable);
    }

    public void SetFloorCleared(bool cleared)
    {
        ApplyState(cleared, isPathBlocked, isInteractable);
    }

    public void SetPathBlocked(bool blocked)
    {
        ApplyState(isClearedFloor, blocked, isInteractable);
    }

    public void ApplyState(bool floorCleared, bool pathBlocked, bool interactable)
    {
        bool disabledStateChanged = isClearedFloor != floorCleared ||
                                    isPathBlocked != pathBlocked;
        bool interactionChanged = isInteractable != interactable;

        if (!disabledStateChanged && !interactionChanged)
            return;

        isClearedFloor = floorCleared;
        isPathBlocked = pathBlocked;
        isInteractable = interactable;

        if (!isInteractable)
        {
            isPointerInside = false;
            isSelected = false;
        }

        if (disabledStateChanged)
            RefreshDisabledState();
        else
            RefreshBackgroundVisual();
    }

    private void RefreshDisabledState()
    {
        ApplyDisabledTint();

        if (backgroundRect != null)
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            backgroundRect.localScale = normalScale;
            backgroundRect.gameObject.SetActive(!IsVisuallyDisabled());
        }

        RefreshBackgroundVisual();
    }

    public void SetSelected(bool selected)
    {
        if (isSelected == selected)
            return;

        isSelected = selected;
        RefreshBackgroundVisual();
    }

    private void RefreshBackgroundVisual()
    {
        if (IsVisuallyDisabled() || backgroundRect == null || !backgroundRect.gameObject.activeSelf)
            return;

        float targetAlpha = !isInteractable
            ? lockedAlpha
            : isSelected
                ? selectedAlpha
                : isPointerInside ? hoverAlpha : normalAlpha;

        Vector3 targetScale = isInteractable && isPointerInside
            ? normalScale * hoverScale
            : normalScale;

        StartTransition(targetAlpha, targetScale);
    }

    private void StartTransition(float targetAlpha, Vector3 targetScale)
    {
        if (!isActiveAndEnabled)
            return;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(TransitionRoutine(targetAlpha, targetScale));
    }

    private IEnumerator TransitionRoutine(float targetAlpha, Vector3 targetScale)
    {
        float startAlpha = backgroundImage.color.a;
        Vector3 startScale = backgroundRect.localScale;
        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / transitionDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            float alpha = Mathf.Lerp(startAlpha, targetAlpha, easedT);
            Vector3 scale = Vector3.Lerp(startScale, targetScale, easedT);
            SetBackgroundVisual(alpha, scale);

            yield return null;
        }

        SetBackgroundVisual(targetAlpha, targetScale);
        transitionRoutine = null;
    }

    private void PlayClickAnimation()
    {
        if (iconRect == null)
            return;

        if (clickRoutine != null)
            StopCoroutine(clickRoutine);

        clickRoutine = StartCoroutine(ClickRoutine());
    }

    private void StartHoverFloat()
    {
        if (iconRect == null)
            return;

        if (hoverFloatRoutine != null)
            StopCoroutine(hoverFloatRoutine);

        hoverFloatRoutine = StartCoroutine(HoverFloatRoutine());
    }

    private IEnumerator HoverFloatRoutine()
    {
        float elapsedTime = 0f;

        while (isPointerInside && isInteractable)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float phase = elapsedTime * hoverFloatFrequency * Mathf.PI * 2f;
            float yOffset = Mathf.Sin(phase) * hoverFloatDistance;
            iconRect.anchoredPosition = normalIconPosition + Vector2.up * yOffset;
            yield return null;
        }

        Vector2 returnStartPosition = iconRect.anchoredPosition;
        elapsedTime = 0f;

        while (elapsedTime < hoverFloatReturnDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / hoverFloatReturnDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            iconRect.anchoredPosition = Vector2.Lerp(
                returnStartPosition,
                normalIconPosition,
                easedT);
            yield return null;
        }

        iconRect.anchoredPosition = normalIconPosition;
        hoverFloatRoutine = null;
    }

    private IEnumerator ClickRoutine()
    {
        Vector3 pressedScale = normalIconScale * pressedIconScale;

        yield return ScaleIcon(iconRect.localScale, pressedScale, shrinkDuration);
        yield return ScaleIcon(iconRect.localScale, normalIconScale, expandDuration);

        iconRect.localScale = normalIconScale;
        clickRoutine = null;
    }

    private IEnumerator ScaleIcon(Vector3 startScale, Vector3 targetScale, float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            iconRect.localScale = Vector3.Lerp(startScale, targetScale, easedT);

            yield return null;
        }

        iconRect.localScale = targetScale;
    }

    private void SetBackgroundVisual(float alpha, Vector3 scale)
    {
        Color color = backgroundImage.color;
        color.a = alpha;
        backgroundImage.color = color;
        backgroundRect.localScale = scale;
    }

    private void FindReferences()
    {
        if (backgroundRect == null)
        {
            Transform background = transform.Find("Background");
            if (background != null)
                backgroundRect = background as RectTransform;
        }

        if (backgroundImage == null && backgroundRect != null)
            backgroundImage = backgroundRect.GetComponent<Image>();

        if (iconRect == null)
        {
            Transform icon = transform.Find("Icon");
            if (icon != null)
                iconRect = icon as RectTransform;
        }

        if (iconImage == null && iconRect != null)
            iconImage = iconRect.GetComponent<Image>();

        if (stageSelectManager == null)
            stageSelectManager = YJ_StageSelectManager.Instance;

        if (nodeData == null)
            nodeData = GetComponent<YJ_StageNodeData>();
    }

    private bool SetSelectedWithoutManager()
    {
        SetSelected(true);
        return true;
    }

    private bool IsVisuallyDisabled()
    {
        return isClearedFloor || isPathBlocked;
    }

    private void ApplyDisabledTint()
    {
        bool useDisabledTint = IsVisuallyDisabled();

        if (backgroundImage != null)
        {
            Color color = useDisabledTint ? clearedTint : originalBackgroundColor;
            color.a = backgroundImage.color.a;
            backgroundImage.color = color;
        }

        if (iconImage != null)
        {
            Color color = useDisabledTint ? clearedTint : originalIconColor;
            color.a = originalIconColor.a;
            iconImage.color = color;
        }

    }
}
