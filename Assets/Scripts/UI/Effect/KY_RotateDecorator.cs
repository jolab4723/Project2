using UnityEngine;

public class KY_RotateDecorator : MonoBehaviour
{
    public float rotateSpeed = 30f; // 인스펙터에서 조절

    void Update()
    {
        transform.Rotate(0f, 0f, rotateSpeed * Time.unscaledDeltaTime);
    }
}