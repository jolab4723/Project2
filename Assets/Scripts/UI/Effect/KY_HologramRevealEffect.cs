using DG.Tweening;
using UnityEngine;

// 기존 슬라이드·페이드·글리치 컴포넌트를 하나의 홀로그램 등장 연출로 묶습니다.
public class KY_HologramRevealEffect : MonoBehaviour
{
    [SerializeField] private KY_SlideAnimator slide;
    [SerializeField] private KY_FadeEffect fade;
    [SerializeField] private KY_GlitchEffect glitch;
    [SerializeField] private KY_ColorPulseEffect colorPulse;
    [SerializeField] private float glitchDelay = 0.05f;

    private Sequence sequence;

    public void PlayReveal()
    {
        sequence?.Kill();
        sequence = DOTween.Sequence().SetLink(gameObject);
        if (slide != null) sequence.Join(slide.ReplayIn());
        if (fade != null) sequence.Join(fade.FadeIn());
        if (glitch != null) sequence.InsertCallback(glitchDelay, glitch.Play);
        if (colorPulse != null) sequence.InsertCallback(glitchDelay, colorPulse.Play);
    }

    public void PlayHide()
    {
        sequence?.Kill();
        if (fade != null) fade.FadeOut();
        if (slide != null) slide.SlideOut();
    }

    private void OnDisable() => sequence?.Kill();
}
