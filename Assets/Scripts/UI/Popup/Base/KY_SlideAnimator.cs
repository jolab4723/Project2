using DG.Tweening;
using UnityEngine;

// 옆에서 나오는 방식의 팝업의 애니메이션 코드입니다.
public class KY_SlideAnimator : MonoBehaviour
{
    [Header("수치 조정")]
    public float duration = 0.35f;      // 지연
    public Ease inEase = Ease.OutBack;  // 나가는 속도
    public Ease outEase = Ease.InBack;  // 들어오는 속도
    public float hiddenOffsetX = 0f;    // 들어가는 위치

    [Header("일시정지 영향 여부")]
    public bool ignoreTimeScale = false; 

    private RectTransform rectTransform;
    private Vector2 originalPosition;       // 원래 위치
    private Vector2 hiddenPosition;         // 숨는 위치

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
        hiddenPosition = new Vector2(
        originalPosition.x + rectTransform.rect.width + hiddenOffsetX, originalPosition.y);
        rectTransform.anchoredPosition = hiddenPosition;
    }

    // 팝업이 들어올 때 호출
    public Tween SlideIn()
    {
        return rectTransform.DOAnchorPos(originalPosition, duration)
            .SetEase(inEase, 0)
            .SetUpdate(ignoreTimeScale);
    }

    // 팝업이 나갈 때 호출
    public Tween SlideOut(System.Action onComplete)
    {
        return rectTransform.DOAnchorPos(hiddenPosition, duration)
            .SetEase(outEase, 0)
            .SetUpdate(ignoreTimeScale)
            .OnComplete(() => onComplete?.Invoke());
    }
}