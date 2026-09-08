using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Graphic의 색상을 짧게 강조한 뒤 원래 색상으로 되돌리는 컴포넌트입니다.
public class KY_ColorPulseEffect : MonoBehaviour
{
    [SerializeField] private Graphic target;
    [SerializeField] private Color pulseColor = Color.cyan;
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private Ease ease = Ease.OutQuad;

    private Color originalColor;
    private Tween tween;

    private void Awake()
    {
        target ??= GetComponent<Graphic>();
        if (target != null) originalColor = target.color;
    }

    public void Play()
    {
        if (target == null) return;
        tween?.Kill();
        target.color = originalColor;
        tween = target.DOColor(pulseColor, duration * 0.5f)
            .SetEase(ease)
            .SetLink(gameObject)
            .OnComplete(() => target.DOColor(originalColor, duration * 0.5f).SetEase(ease).SetLink(gameObject));
    }

    public void Stop()
    {
        tween?.Kill();
        if (target != null) target.color = originalColor;
    }

    private void OnDisable() => tween?.Kill();
}
