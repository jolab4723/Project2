using UnityEngine;

public class YJ_ObjectRotation : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 30f;

    void Update()
    {
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }
}
