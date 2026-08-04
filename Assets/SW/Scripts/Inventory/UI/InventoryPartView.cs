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

    public void ToggleInventory()
    {
        if (IsOpen(inventory))
        {
            CloseAll();
            return;
        }

        OpenInventory();
    }

    public void OpenInventory()
    {
        bool hadOpenWindow = HasOpenWindow;
        OpenIfClosed(inventory);
        NotifyOpened(hadOpenWindow);
    }

    public void OpenShop()
    {
        bool hadOpenWindow = HasOpenWindow;
        CloseIfOpen(upgrade);
        OpenIfClosed(inventory);
        OpenIfClosed(shop);
        NotifyOpened(hadOpenWindow);
    }

    public void OpenUpgrade()
    {
        bool hadOpenWindow = HasOpenWindow;
        CloseIfOpen(shop);
        OpenIfClosed(inventory);
        OpenIfClosed(upgrade);
        NotifyOpened(hadOpenWindow);
    }

    public void CloseAll()
    {
        if (!HasOpenWindow)
            return;

        CloseIfOpen(shop);
        CloseIfOpen(upgrade);
        CloseIfOpen(inventory);
        KY_GameEvents.SidePopupClosed();
    }

    private void NotifyOpened(bool hadOpenWindow)
    {
        if (!hadOpenWindow && HasOpenWindow)
            KY_GameEvents.SidePopupOpened();
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
