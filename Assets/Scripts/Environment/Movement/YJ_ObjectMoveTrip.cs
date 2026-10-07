using UnityEngine;

public class YJ_ObjectMoveTrip : MonoBehaviour
{
    [SerializeField] private float speed = 1f;
    [SerializeField] private float distance = 1f;
    [SerializeField] private Vector3 swingAxis = Vector3.forward;
    [SerializeField] private bool inverse = false;

    private Vector3 startPosition;
    private float elapsedTime;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        float direction = inverse ? -1f : 1f;
        float offset = Mathf.Sin(elapsedTime * speed) * distance * direction;

        transform.position = startPosition + swingAxis.normalized * offset;
    }
}
