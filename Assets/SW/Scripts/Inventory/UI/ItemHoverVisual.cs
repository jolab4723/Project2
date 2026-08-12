using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class ItemHoverVisual : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IBeginDragHandler,
    IEndDragHandler
{
    [SerializeField] private CanvasGroup hoverGlow;
    [SerializeField] private ItemUI itemUI;
    [SerializeField] private ItemDragHandler dragHandler;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.1f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.15f;

    private RectTransform rect;
    private Coroutine fadeRoutine;
    private Coroutine dragEndRoutine;
    private bool pointerInside;

    private void Awake()
    {
        EnsureReferences();
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        EnsureReferences();
        pointerInside = false;
        SetAlpha(0f);
    }

    private void OnDisable()
    {
        StopAnimations();
        pointerInside = false;
        SetAlpha(0f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;

        if (itemUI == null || itemUI.Item == null ||
            (dragHandler != null && dragHandler.IsDragging))
        {
            return;
        }

        FadeTo(1f, fadeInDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        FadeTo(0f, fadeOutDuration);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        pointerInside = false;
        FadeTo(0f, fadeOutDuration);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (rect == null || eventData == null)
            return;

        pointerInside = RectTransformUtility.RectangleContainsScreenPoint(
            rect,
            eventData.position,
            eventData.pressEventCamera);

        if (!pointerInside)
            return;

        if (dragEndRoutine != null)
            StopCoroutine(dragEndRoutine);

        dragEndRoutine = StartCoroutine(RefreshAfterDrag());
    }

    private IEnumerator RefreshAfterDrag()
    {
        yield return null;
        dragEndRoutine = null;

        if (pointerInside &&
            itemUI != null && itemUI.Item != null &&
            (dragHandler == null || !dragHandler.IsDragging))
        {
            FadeTo(1f, fadeInDuration);
        }
    }

    private void EnsureReferences()
    {
        if (rect == null)
            rect = GetComponent<RectTransform>();

        if (itemUI == null)
            itemUI = GetComponent<ItemUI>();

        if (dragHandler == null)
            dragHandler = GetComponent<ItemDragHandler>();

        if (hoverGlow == null)
            hoverGlow = transform.Find("HoverInnerGlow")?.GetComponent<CanvasGroup>();
    }

    private void FadeTo(float targetAlpha, float duration)
    {
        if (hoverGlow == null)
            return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        if (duration <= 0f)
        {
            fadeRoutine = null;
            SetAlpha(targetAlpha);
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        float speed = 1f / duration;

        while (!Mathf.Approximately(hoverGlow.alpha, targetAlpha))
        {
            hoverGlow.alpha = Mathf.MoveTowards(
                hoverGlow.alpha,
                targetAlpha,
                speed * Time.unscaledDeltaTime);
            yield return null;
        }

        hoverGlow.alpha = targetAlpha;
        fadeRoutine = null;
    }

    private void StopAnimations()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (dragEndRoutine != null)
        {
            StopCoroutine(dragEndRoutine);
            dragEndRoutine = null;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (hoverGlow != null)
            hoverGlow.alpha = alpha;
    }
}
