using UnityEngine;

public class ShopItemBadgeView : MonoBehaviour
{
    [SerializeField] private GameObject playerSoldBadge;

    private void Awake()
    {
        Hide();
    }

    public void Apply(ShopItemSource source)
    {
        if (playerSoldBadge == null)
            return;

        bool shouldShow = source == ShopItemSource.PlayerSold;

        playerSoldBadge.SetActive(shouldShow);
    }

    public void Hide()
    {
        if (playerSoldBadge == null)
            return;

        playerSoldBadge.SetActive(false);
    }
}