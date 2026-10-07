using UnityEngine;

public sealed class PlayerItemDropOrigin : MonoBehaviour
{
    [SerializeField] private Transform dropPoint;
    [SerializeField] private float heightOffset = 0.5f;


    public bool TryGetSpawnPose(
        out Vector3 position,
        out Quaternion rotation)
    {
        Transform origin = dropPoint;
        if (origin == null && PlayerHealthManager.Instance != null)
            origin = PlayerHealthManager.Instance.transform;

        if (origin == null)
        {
            position = default;
            rotation = Quaternion.identity;
            return false;
        }

        position = origin.position;
        position.y += heightOffset;
        rotation = origin.rotation;
        return true;
    }
}
