using UnityEngine;

public class YJ_SlashEffectRotation : MonoBehaviour
{
    [Tooltip("활성화할 때 적용할 로컬 X 회전의 최소/최대 각도(도). 기존 X에 더하지 않고 지정합니다.")]
    [SerializeField] private Vector2 rotationXRange = new Vector2(-30f, 30f);

    private Vector3 initialEulerAngles;

    private void Awake()
    {
        initialEulerAngles = transform.localEulerAngles;
    }

    private void OnEnable()
    {
        float min = Mathf.Min(rotationXRange.x, rotationXRange.y);
        float max = Mathf.Max(rotationXRange.x, rotationXRange.y);
        Vector3 angles = initialEulerAngles;
        angles.x = Random.Range(min, max);
        transform.localEulerAngles = angles;
    }
}
