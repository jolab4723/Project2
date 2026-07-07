using UnityEngine;

public enum Direction
{
    Up,
    Forward,
    Right
}

public class YJ_ObjectRotation : MonoBehaviour
{
    public Direction direction = Direction.Up;
    private Vector3 rotationAxis;

    [SerializeField] private float rotateSpeed = 30f;

    private void Start()
    {
        switch (direction)
        {
            case Direction.Up:
            rotationAxis = Vector3.up;
            break;

            case Direction.Forward:
            rotationAxis = Vector3.forward;
            break;

            case Direction.Right:
            rotationAxis = Vector3.right;
            break;
        }
    }

    void Update()
    {
        transform.Rotate(rotationAxis * rotateSpeed * Time.deltaTime);
    }
}
