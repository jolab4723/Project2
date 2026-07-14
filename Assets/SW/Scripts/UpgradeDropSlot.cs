using UnityEngine;

public class UpgradeDropSlot : MonoBehaviour
{
    [SerializeField] private UpgradeController upgradeController;

    private void Awake()
    {
        if (upgradeController == null)
            upgradeController = GetComponentInParent<UpgradeController>();
    }

    public bool TrySelectItem(ItemUI itemUI)
    {
        if (upgradeController == null ||
            itemUI?.Item?.itemData == null)
        {
            return false;
        }

        return upgradeController.TrySetItem(itemUI.Item.itemData);
    }
}
