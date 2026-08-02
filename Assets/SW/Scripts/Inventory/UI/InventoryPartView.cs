using UnityEngine;

public class InventoryPartView : MonoBehaviour
{
    [SerializeField] private KY_PopupBase inventory;
    [SerializeField] private KY_PopupBase shop;
    [SerializeField] private KY_PopupBase upgrade;

    public bool HasOpenWindow =>
        IsOpen(inventory) ||
        IsOpen(shop) ||
        IsOpen(upgrade);

    private void OnEnable()
    {
        KY_GameEvents.OnEscPressed += HandleEscape;
        KY_GameEvents.OnInventoryRequested += HandleInventoryRequested;
    }

    private void OnDisable()
    {
        KY_GameEvents.OnEscPressed -= HandleEscape;
        KY_GameEvents.OnInventoryRequested -= HandleInventoryRequested;
    }

    private void HandleInventoryRequested()
    {
        if (IsOpen(inventory))
        {
            CloseAll();
            return;
        }

        OpenInventory();
    }

    private void HandleEscape()
    {
        if (!HasOpenWindow)
            return;

        CloseAll();
    }

    public void OpenInventory()
    {
        OpenIfClosed(inventory);
    }

    public void OpenShop()
    {
        CloseIfOpen(upgrade);
        OpenIfClosed(inventory);
        OpenIfClosed(shop);
    }

    public void OpenUpgrade()
    {
        CloseIfOpen(shop);
        OpenIfClosed(inventory);
        OpenIfClosed(upgrade);
    }

    public void CloseAll()
    {
        CloseIfOpen(shop);
        CloseIfOpen(upgrade);
        CloseIfOpen(inventory);
    }

    private static bool IsOpen(KY_PopupBase popup)
    {
        return popup != null && popup.gameObject.activeSelf;
    }

    private static void OpenIfClosed(KY_PopupBase popup)
    {
        if (popup == null || popup.gameObject.activeSelf)
            return;

        popup.Open();
    }

    private static void CloseIfOpen(KY_PopupBase popup)
    {
        if (popup == null || !popup.gameObject.activeSelf)
            return;

        popup.Close();
    }
}
