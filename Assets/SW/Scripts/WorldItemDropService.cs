using ItemSystem;
using UnityEngine;

public sealed class WorldItemDropService : MonoBehaviour
{
    [SerializeField] private float minDropRadius = 0.9f;

    [SerializeField] private float maxDropRadius = 2.2f;

    [SerializeField] private float clearanceRadius = 0.6f;

    [SerializeField] private int maxPlacementAttempts = 20;

    [SerializeField] private LayerMask blockedDropLayers;

    [SerializeField] private ItemDataStorage pickupPrefab;
    [SerializeField] private PlayerItemDropOrigin dropOrigin;

    public void Bind(PlayerItemDropOrigin origin)
    {
        dropOrigin = origin;
    }

    public WorldItemDropResult TryDrop(ItemInstance item)
    {
        return TryDrop(item, out _);
    }

    public WorldItemDropResult TryDrop(
        ItemInstance item,
        out ItemDataStorage spawnedPickup)
    {
        spawnedPickup = null;

        if (item == null || item.definition == null)
            return WorldItemDropResult.InvalidItem;

        if (dropOrigin == null ||
            !dropOrigin.TryGetSpawnPose(
                out Vector3 center,
                out Quaternion rotation))
        {
            return WorldItemDropResult.DropOriginUnavailable;
        }

        if (pickupPrefab == null)
            return WorldItemDropResult.PickupPrefabUnavailable;

        if (!TryFindAvailablePosition(center,out Vector3 spawnPosition))
        {
            return WorldItemDropResult.NoAvailablePosition;
        }

        spawnedPickup = Instantiate(pickupPrefab, spawnPosition, rotation);

        if (spawnedPickup == null)
            return WorldItemDropResult.SpawnFailed;

        spawnedPickup.Init(item);

        if (spawnedPickup.TryGetComponent(
                out WorldItemRarityColorView rarityColorView))
        {
            rarityColorView.Apply(item.definition.rarity);
        }

        return WorldItemDropResult.Success;
    }

    private bool TryFindAvailablePosition(Vector3 center, out Vector3 availablePosition)
    {
        float minRadius = Mathf.Max(0f, Mathf.Min(minDropRadius, maxDropRadius));

        float maxRadius = Mathf.Max(minRadius, Mathf.Max(minDropRadius, maxDropRadius));

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);

            float distance = Mathf.Sqrt(Random.Range(minRadius * minRadius, maxRadius * maxRadius));

            Vector3 candidate =center + new Vector3(
                    Mathf.Cos(angle) * distance, 0f,
                    Mathf.Sin(angle) * distance);

            bool isBlocked = Physics.CheckSphere(
                candidate,
                clearanceRadius,
                blockedDropLayers,
                QueryTriggerInteraction.Ignore);

            if (isBlocked)
                continue;

            availablePosition = candidate;
            return true;
        }

        availablePosition = default;
        return false;
    }
}
