using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 프레임 장식의 알파를 은은하게 올리고 내리는 반복형 미세연출입니다.
public class KY_BorderPulseEffect : MonoBehaviour
{
    [SerializeField] private Graphic target;
    [SerializeField, Range(0f, 1f)] private float lowAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float highAlpha = 0.85f;
    [SerializeField] private float pulseDuration = 2.2f;
    [SerializeField] private Ease pulseEase = Ease.InOutSine;
    [SerializeField] private bool playOnEnable = true;

    private Color baseColor;
    private Tween pulseTween;

    private void Awake()
    {
        target ??= GetComponent<Graphic>();
        if (target != null)
            baseColor = target.color;
    }

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    public void Play()
    {
        if (target == null)
            return;

        pulseTween?.Kill();
        Color color = baseColor;
        color.a = lowAlpha;
        target.color = color;

        pulseTween = target.DOFade(highAlpha, pulseDuration)
            .SetEase(pulseEase)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    public void Stop()
    {
        pulseTween?.Kill();
        pulseTween = null;

        if (target != null)
            target.color = baseColor;
    }

    private void OnDisable() => Stop();
}
