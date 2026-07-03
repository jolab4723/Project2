using UnityEngine;

public class WBH_TestFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target;

    private Vector3 offset;

    void Start()
    {
        offset = transform.position - target.position;
    }

    void LateUpdate()
    {
        transform.position = target.position + offset;
    }
}
