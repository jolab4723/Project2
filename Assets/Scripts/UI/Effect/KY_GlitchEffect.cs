using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 짧은 깜빡임과 위치 흔들림으로 UI 글리치 느낌을 만드는 컴포넌트입니다.
public class KY_GlitchEffect : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField] private Graphic tintTarget;
    [SerializeField] private float duration = 0.2f;
    [SerializeField] private float shakeStrength = 6f;
    [SerializeField] private int vibrato = 12;
    [SerializeField] private Color glitchColor = Color.cyan;

    private Vector2 originalPosition;
    private Color originalColor;
    private Sequence sequence;

    private void Awake()
    {
        target ??= GetComponent<RectTransform>();
        tintTarget ??= GetComponent<Graphic>();
        if (target != null) originalPosition = target.anchoredPosition;
        if (tintTarget != null) originalColor = tintTarget.color;
    }

    public void Play()
    {
        sequence?.Kill();
        if (target == null) return;

        target.anchoredPosition = originalPosition;
        sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Append(target.DOShakeAnchorPos(duration, shakeStrength, vibrato, 90f, false, true));
        if (tintTarget != null)
        {
            sequence.Join(tintTarget.DOColor(glitchColor, duration * 0.35f));
            sequence.Append(tintTarget.DOColor(originalColor, duration * 0.65f));
        }
    }

    public void Stop()
    {
        sequence?.Kill();
        if (target != null) target.anchoredPosition = originalPosition;
        if (tintTarget != null) tintTarget.color = originalColor;
    }

    private void OnDisable() => sequence?.Kill();
}
