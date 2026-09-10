using UnityEngine;
using UnityEngine.UI;

public class KY_InventoryPopup : KY_PopupBase
{
    private KY_SlideAnimator slideAnimator;
    private GraphicRaycaster raycaster;

    void Awake()
    {
        slideAnimator = GetComponent<KY_SlideAnimator>();
        raycaster = GetComponent<GraphicRaycaster>();
    }

    public override void Open()
    {
        gameObject.SetActive(true);
        if (raycaster != null)
            raycaster.enabled = true;

        slideAnimator?.SlideIn();
    }

    public override void Close()
    {
        if (raycaster != null)
            raycaster.enabled = false;

        if (slideAnimator != null)
            slideAnimator.SlideOut(() => gameObject.SetActive(false));
        else
            gameObject.SetActive(false);
    }
}
