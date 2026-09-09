using UnityEngine;

// 한 화면에서 여러 UI 연출 컴포넌트의 실행 순서를 조율하는 로컬 관리자입니다.
// 이동·페이드·글리치의 실제 구현은 각 효과 컴포넌트가 담당합니다.
public class KY_UIAnimationManager : MonoBehaviour
{
    [Header("필수 패널 연출")]
    [SerializeField] private KY_SlideAnimator panelSlide;
    [SerializeField] private KY_FadeEffect panelFade;

    [Header("선택적 강조 효과")]
    [SerializeField] private KY_GlitchEffect panelGlitch;
    [SerializeField] private KY_ColorPulseEffect panelColorPulse;

    private void Awake()
    {
        if (panelSlide == null)
            Debug.LogWarning("Panel Slide가 연결되지 않았습니다.", this);

        if (panelFade == null)
            Debug.LogWarning("Panel Fade가 연결되지 않았습니다.", this);
    }

    // 패널 등장 연출을 실행합니다.
    public void PlayPanelOpen()
    {
        if (panelSlide != null)
            panelSlide.ReplayIn();

        if (panelFade != null)
            panelFade.FadeIn();
    }

    // 패널 퇴장 연출을 실행합니다.
    public void PlayPanelClose()
    {
        if (panelFade != null)
            panelFade.FadeOut();

        if (panelSlide != null)
            panelSlide.SlideOut();
    }

    // 선택 변경이나 버튼 강조에 사용할 보조 효과를 실행합니다.
    public void PlaySelectionFeedback()
    {
        if (panelGlitch != null)
            panelGlitch.Play();

        if (panelColorPulse != null)
            panelColorPulse.Play();
    }
}
