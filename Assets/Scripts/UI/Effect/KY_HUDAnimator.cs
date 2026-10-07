// HUD 애니메이션을 위한 코드
using UnityEngine;
using System.Collections;


//  HUD 이동 방향
public enum SlideDirection { Up, Down, Left, Right }

public class KY_HUDAnimator : MonoBehaviour
{
    public SlideDirection slideInDirection = SlideDirection.Up;
    public float slideDistance = 200f;
    public float slideSpeed = 10f;
    public float delay = 0f;
    public bool reactToSidePopup = false;

    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Vector2 hiddenPosition;
    private Coroutine currentCoroutine;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
        hiddenPosition = GetHiddenPosition();
        rectTransform.anchoredPosition = hiddenPosition;
    }

    void OnEnable()
    {
        // 재활성화될 때마다 숨김 위치에서 등장 연출을 다시 시작한다.
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = hiddenPosition;
            currentCoroutine = StartCoroutine(DelayedSlideIn());
        }

        if (reactToSidePopup)
        {
            KY_GameEvents.OnSidePopupOpened += SlideOut;
            KY_GameEvents.OnSidePopupClosed += SlideIn;
        }
    }

    void OnDisable()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }

        if (reactToSidePopup)
        {
            KY_GameEvents.OnSidePopupOpened -= SlideOut;
            KY_GameEvents.OnSidePopupClosed -= SlideIn;
        }
    }

    Vector2 GetHiddenPosition()
    {
        switch (slideInDirection)
        {
            case SlideDirection.Up: return originalPosition + Vector2.down * slideDistance;
            case SlideDirection.Down: return originalPosition + Vector2.up * slideDistance;
            case SlideDirection.Left: return originalPosition + Vector2.right * slideDistance;
            case SlideDirection.Right: return originalPosition + Vector2.left * slideDistance;
            default: return originalPosition;
        }
    }

    IEnumerator DelayedSlideIn()
    {
        yield return new WaitForSeconds(delay);
        SlideIn();
    }

    public void SlideIn()
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(DoSlide(originalPosition));
    }

    public void SlideOut()
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(DoSlide(hiddenPosition));
    }

    IEnumerator DoSlide(Vector2 target)
    {
        while (Vector2.Distance(rectTransform.anchoredPosition, target) > 0.1f)
        {
            rectTransform.anchoredPosition = Vector2.Lerp(
                rectTransform.anchoredPosition,
                target,
                Time.deltaTime * slideSpeed
            );
            yield return null;
        }
        rectTransform.anchoredPosition = target;
    }
}
