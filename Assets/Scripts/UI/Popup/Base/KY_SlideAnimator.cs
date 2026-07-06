using UnityEngine;
using System.Collections;

public class KY_SlideAnimator : MonoBehaviour
{
    public float slideSpeed = 10f;
    public float hiddenOffsetX = 0f;

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
            rectTransform.anchoredPosition = Vector2.Lerp(
                rectTransform.anchoredPosition,
                originalPosition,
                Time.deltaTime * slideSpeed
            );
            yield return null;
        }
        rectTransform.anchoredPosition = originalPosition;
    }

    IEnumerator DoSlideOut(System.Action onComplete)
    {
        while (Vector2.Distance(rectTransform.anchoredPosition, hiddenPosition) > 0.1f)
        {
            rectTransform.anchoredPosition = Vector2.Lerp(
                rectTransform.anchoredPosition,
                hiddenPosition,
                Time.deltaTime * slideSpeed
            );
            yield return null;
        }
        rectTransform.anchoredPosition = hiddenPosition;
        onComplete?.Invoke();
    }
}