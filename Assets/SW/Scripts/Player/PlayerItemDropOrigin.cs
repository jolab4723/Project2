using UnityEngine;

public sealed class PlayerItemDropOrigin : MonoBehaviour
{
    [SerializeField] private Transform dropPoint;
    [SerializeField] private float heightOffset = 0.5f;


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
        position.y = dropPoint.position.y + heightOffset;
        rotation = dropPoint.rotation;
        return true;
    }
}