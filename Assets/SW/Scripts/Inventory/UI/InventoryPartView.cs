using UnityEngine;

public class InventoryPartView : MonoBehaviour
{
    [SerializeField] private KY_PopupBase inventory;
    [SerializeField] private KY_PopupBase shop;
    [SerializeField] private KY_PopupBase upgrade;

    private void OnEnable()
    {
        KY_GameEvents.OnEscPressed += HandleEscape;
    }

    private void OnDisable()
    {
        KY_GameEvents.OnEscPressed -= HandleEscape;
    }
    private void HandleEscape()
    {
        bool anyWindowOpen =
            inventory.gameObject.activeSelf ||
            shop.gameObject.activeSelf ||
            upgrade.gameObject.activeSelf;

        if (!anyWindowOpen)
            return;

        CloseAll();
    }
    public void OpenShop()
    {
        upgrade.Close();
        inventory.Open();
        shop.Open();
    }

    public void OpenUpgrade()
    {
        shop.Close();
        inventory.Open();
        upgrade.Open();
    }

    public void CloseAll()
    {
        shop.Close();
        upgrade.Close();
        inventory.Close();
    }
}
