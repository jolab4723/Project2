using DG.Tweening;
using UnityEngine;

// 투명도 조절을 통해 오브젝트를 등장/퇴장시키는 1회성 페이드 효과 코드입니다.
public class KY_FadeEffect : MonoBehaviour
{
    public float duration = 0.1f;
    public Ease fadeEase = Ease.Linear;
    public bool ignoreTimeScale = false;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    // 오브젝트를 alpha 0에서 시작해 1로 등장시킨다.
    public Tween FadeIn()
    {
        fadeTween?.Kill();
        canvasGroup.alpha = 0f;
        fadeTween = canvasGroup.DOFade(1f, duration)
            .SetEase(fadeEase)
            .SetUpdate(ignoreTimeScale)
            .SetLink(gameObject);
        return fadeTween;
    }

    // 오브젝트를 현재 alpha에서 0으로 퇴장시킨다.
    public Tween FadeOut()
    {
        fadeTween?.Kill();
        fadeTween = canvasGroup.DOFade(0f, duration)
            .SetEase(fadeEase)
            .SetUpdate(ignoreTimeScale)
            .SetLink(gameObject);
        return fadeTween;
    }
}