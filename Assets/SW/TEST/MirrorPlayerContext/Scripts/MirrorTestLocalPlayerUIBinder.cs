using UnityEngine;

[DisallowMultipleComponent]
public sealed class MirrorTestLocalPlayerUIBinder : MonoBehaviour
{
    [SerializeField] private MirrorTestNetworkManager networkManager;
    [SerializeField] private InventoryView inventoryView;
    [SerializeField] private MirrorTestPlayerHud playerHud;

    private void OnEnable()
    {
        if (networkManager == null)
            networkManager = FindFirstObjectByType<MirrorTestNetworkManager>();

        if (networkManager == null || inventoryView == null)
        {
            Debug.LogError(
                "[MirrorTestLocalPlayerUIBinder] NetworkManager 또는 InventoryView 참조가 비어 있습니다.",
                this);
            return;
        }

        networkManager.LocalPlayerContextChanged += HandleLocalPlayerChanged;
        HandleLocalPlayerChanged(networkManager.LocalPlayerContext);
    }

    private void OnDisable()
    {
        if (networkManager != null)
            networkManager.LocalPlayerContextChanged -= HandleLocalPlayerChanged;

        inventoryView?.Unbind();
        playerHud?.Unbind();
    }

    private void HandleLocalPlayerChanged(PlayerContext context)
    {
        inventoryView.Unbind();
        playerHud?.Unbind();

        if (context != null && !inventoryView.Bind(context))
        {
            Debug.LogError(
                "[MirrorTestLocalPlayerUIBinder] 로컬 PlayerContext UI Bind에 실패했습니다.",
                this);
        }

        if (context != null)
            playerHud?.Bind(context);
    }
}
