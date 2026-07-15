using UnityEngine;

public sealed class PlayerItemDropOrigin : MonoBehaviour
{
    [SerializeField] private Transform dropPoint;

    public bool TryGetSpawnPose(
        out Vector3 position,
        out Quaternion rotation)
    {
        if (dropPoint == null)
        {
            position = default;
            rotation = Quaternion.identity;
            return false;
        }

        position = dropPoint.position;
        rotation = dropPoint.rotation;
        return true;
    }
}