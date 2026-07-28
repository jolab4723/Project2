using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class PopupCloseButton : MonoBehaviour
{
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
        KY_PopupBase popup = GetComponentInParent<KY_PopupBase>(true);
        popup?.Close();
    }
}
