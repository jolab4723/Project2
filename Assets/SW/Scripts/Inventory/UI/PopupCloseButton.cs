using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class PopupCloseButton : MonoBehaviour
{
    [SerializeField] private bool useSidePopupManager;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ClosePopup);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(ClosePopup);
    }

    private void ClosePopup()
    {
        if (useSidePopupManager && KY_PopupManager.Instance != null)
        {
            KY_PopupManager.Instance.HideSidePopup();
            return;
        }

        KY_PopupBase popup = GetComponentInParent<KY_PopupBase>(true);
        popup?.Close();
    }
}
