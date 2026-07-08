using UnityEngine;

public class YJ_ObjectRotation : MonoBehaviour
{
    public Direction direction = Direction.Y;
    private Vector3 rotationAxis;

    [SerializeField] private float rotateSpeed = 30f;

    private void Start()
    {
        switch (direction)
        {
            case Direction.X:
            rotationAxis = Vector3.right;
            break;

            case Direction.Y:
            rotationAxis = Vector3.up;
            break;

            case Direction.Z:
            rotationAxis = Vector3.forward;
            break;
        }
    }

    void Update()
    {
        transform.Rotate(rotationAxis * rotateSpeed * Time.deltaTime);
    }
}
