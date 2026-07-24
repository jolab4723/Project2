using DG.Tweening;
using UnityEngine;

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

    public Tween FadeIn()
    {
        fadeTween?.Kill();
        canvasGroup.alpha = 0f;
        fadeTween = canvasGroup.DOFade(1f, duration*10)
            .SetEase(fadeEase)
            .SetUpdate(ignoreTimeScale)
            .SetLink(gameObject);
        return fadeTween;
    }

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