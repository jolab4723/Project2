using DG.Tweening;
using UnityEngine;

/// <summary>
/// 지정한 UI RectTransform을 원래 위치로 복구하며 짧게 흔드는 범용 연출이다.
/// </summary>
public sealed class KY_UIShakeEffect : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField] private float duration = 0.22f;
    [SerializeField] private float strength = 8f;
    [SerializeField] private int vibrato = 14;

    private Tween shakeTween;
    private Vector2 originalAnchoredPosition;
    private bool isShaking;

    private void Awake()
    {
        target ??= transform as RectTransform;
    }

    /// <summary>현재 레이아웃 위치를 기준으로 흔든 뒤 원래 위치로 복구한다.</summary>
    public void Play()
    {
        if (target == null)
            return;

        if (isShaking)
            return;

        originalAnchoredPosition = target.anchoredPosition;
        isShaking = true;
        shakeTween = target.DOShakeAnchorPos(duration, strength, vibrato, 90f)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(RestorePosition)
            .OnKill(RestorePosition);
    }

    private void OnDisable()
    {
        shakeTween?.Kill();
        RestorePosition();
    }

    private void RestorePosition()
    {
        if (!isShaking)
            return;

        if (target != null)
            target.anchoredPosition = originalAnchoredPosition;

        shakeTween = null;
        isShaking = false;
    }
}
