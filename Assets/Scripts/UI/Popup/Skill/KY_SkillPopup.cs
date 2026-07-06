public class KY_SkillPopup : KY_PopupBase
{
    private KY_SlideAnimator slideAnimator;

    void Awake()
    {
        slideAnimator = GetComponent<KY_SlideAnimator>();
    }

    public override void Open()
    {
        gameObject.SetActive(true);
        slideAnimator.SlideIn();
    }

    public override void Close()
    {
        slideAnimator.SlideOut(() => gameObject.SetActive(false));
    }
}