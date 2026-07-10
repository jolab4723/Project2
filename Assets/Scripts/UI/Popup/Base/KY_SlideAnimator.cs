using UnityEngine;
using System.Collections;

public enum SlideType
{
    Lerp,
    MoveTowards
}

public class KY_SlideAnimator : MonoBehaviour
{
    public float slideSpeed = 10f;
    public float hiddenOffsetX = 0f;
    public SlideType inslideType = SlideType.Lerp;
    public SlideType outslideType = SlideType.Lerp;

    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Vector2 hiddenPosition;

    private Coroutine currentCoroutine;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
        hiddenPosition = new Vector2(
            originalPosition.x + rectTransform.rect.width + hiddenOffsetX,
            originalPosition.y
        );
        rectTransform.anchoredPosition = hiddenPosition;
    }

    public void SlideIn()
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(DoSlideIn());
    }

    public void SlideOut(System.Action onComplete)
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(DoSlideOut(onComplete));
    }

    IEnumerator DoSlideIn()
    {
        while (Vector2.Distance(rectTransform.anchoredPosition, originalPosition) > 0.1f)
        {
            if (inslideType == SlideType.Lerp)
            {
                rectTransform.anchoredPosition = Vector2.Lerp(
                    rectTransform.anchoredPosition,
                    originalPosition,
                    Time.unscaledDeltaTime * slideSpeed
                );
            }
            else
            {
                rectTransform.anchoredPosition = Vector2.MoveTowards(
                    rectTransform.anchoredPosition,
                    originalPosition,
                    slideSpeed * 300 * Time.unscaledDeltaTime
                );
            }
            yield return null;
        }
        rectTransform.anchoredPosition = originalPosition;
    }

    IEnumerator DoSlideOut(System.Action onComplete)
    {
        while (Vector2.Distance(rectTransform.anchoredPosition, hiddenPosition) > 0.1f)
        {
            if (outslideType == SlideType.Lerp)
            {
                rectTransform.anchoredPosition = Vector2.Lerp(
                    rectTransform.anchoredPosition,
                    hiddenPosition,
                    Time.unscaledDeltaTime * slideSpeed
                );
            }
            else
            {
                rectTransform.anchoredPosition = Vector2.MoveTowards(
                    rectTransform.anchoredPosition,
                    hiddenPosition,
                    slideSpeed * 300 * Time.unscaledDeltaTime
                );
            }
            yield return null;
        }
        rectTransform.anchoredPosition = hiddenPosition;
        onComplete?.Invoke();
    }
}