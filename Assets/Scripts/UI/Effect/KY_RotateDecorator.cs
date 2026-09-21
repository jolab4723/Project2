using UnityEngine;

/// <summary>UI 장식 오브젝트를 시간 배율과 무관하게 일정 속도로 회전시킨다.</summary>
public class KY_RotateDecorator : MonoBehaviour
{
    public float rotateSpeed = 30f; // 인스펙터에서 조절

    void Update()
    {
        transform.Rotate(0f, 0f, rotateSpeed * Time.unscaledDeltaTime);
    }
}
