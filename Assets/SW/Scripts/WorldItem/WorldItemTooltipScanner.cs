using ItemSystem;
using UnityEngine;

public sealed class WorldItemTooltipScanner : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private WorldItemTooltipView tooltipView;
    [SerializeField] private LayerMask worldItemLayer;
    [SerializeField] private float detectionRadius = 2.5f;
    [SerializeField] private float scanInterval = 0.1f;

    private readonly Collider[] overlapBuffer = new Collider[32];

    private ItemDataStorage currentTarget;
    private float nextScanTime;

    private void Awake()
    {
        if (player == null)
            player = transform;
    }

    private void Update()
    {
        if (Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + scanInterval;
        ScanNearestItem();
    }

    public bool TryGetCurrentTarget(out ItemDataStorage target)
    {
        target = currentTarget;

        return target != null && target.Item?.definition != null;
    }
    private void ScanNearestItem()
    {
        if (player == null || tooltipView == null)
            return;

        int count = Physics.OverlapSphereNonAlloc(
            player.position,
            detectionRadius,
            overlapBuffer,
            worldItemLayer,
            QueryTriggerInteraction.Collide);

        ItemDataStorage nearest = null;
        float nearestSqrDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapBuffer[i];
            overlapBuffer[i] = null;

            if (hit == null)
                continue;

            ItemDataStorage storage =
                hit.GetComponentInParent<ItemDataStorage>();

            if (storage == null || storage.Item?.definition == null)
                continue;

            float sqrDistance =
                (storage.transform.position - player.position).sqrMagnitude;

            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = storage;
            }
        }

        if (nearest == currentTarget)
            return;

        currentTarget = nearest;

        if (currentTarget == null)
        {
            tooltipView.Hide();
            return;
        }

        tooltipView.Show(
            currentTarget.Item,
            currentTarget.transform);
    }

    public bool CanInteract(ItemDataStorage target)
    {
        return target != null &&
               target == currentTarget &&
               target.Item?.definition != null;
    }

    private void OnDisable()
    {
        currentTarget = null;

        if (tooltipView != null)
            tooltipView.Hide();
    }
}