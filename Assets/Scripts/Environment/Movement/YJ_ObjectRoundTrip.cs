using UnityEngine;

public class YJ_ObjectRoundTrip : MonoBehaviour
{
    [SerializeField] private float angle = 25f;
    [SerializeField] private float speed = 1.5f;
    [SerializeField] private Vector3 swingAxis = Vector3.forward;
    [SerializeField] private bool inverse = false;

    private Quaternion startRotation;
    private float direction;

    private void Start()
    {
        startRotation = transform.localRotation;
        direction = inverse ? -1f : 1f;
    }

    private void Update()
    {
        float currentAngle = Mathf.Sin(Time.time * speed) * angle * direction;
        transform.localRotation = startRotation * Quaternion.AngleAxis(currentAngle, swingAxis);
    }
}

