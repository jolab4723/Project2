using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class InventoryPopupCloseButton : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(CloseInventory);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(CloseInventory);
    }

    private void CloseInventory()
    {
        InventoryPartView campView = GetComponentInParent<InventoryPartView>(true);
        if (campView != null)
        {
            campView.CloseAll();
            return;
        }

        if (KY_PopupManager.Instance != null)
        {
            KY_PopupManager.Instance.HideSidePopup();
            return;
        }

        KY_PopupBase popup = GetComponentInParent<KY_PopupBase>(true);
        popup?.Close();
    }
}
