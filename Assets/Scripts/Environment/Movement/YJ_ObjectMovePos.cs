using UnityEngine;
using System.Collections;

public class YJ_ObjectMovePos : MonoBehaviour
{
    public Direction direction = Direction.Z;
    private Vector3 originPos;
    private Vector3 moveAxis;
    private Vector3 targetPos;
    private bool canMove;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float distance = 20f;
    [SerializeField] private float startDelay = 1f;

    void Start()
    {
        originPos = transform.position;

        switch (direction)
        {
            case Direction.X:
                moveAxis = Vector3.right;
                break;

            case Direction.Y:
                moveAxis = Vector3.up;
                break;

            case Direction.Z:
                moveAxis = Vector3.forward;
                break;
        }

        targetPos = originPos + (moveAxis * distance);
        StartCoroutine(StartDelayRoutine());
    }

    private IEnumerator StartDelayRoutine()
    {
        yield return new WaitForSeconds(startDelay);
        canMove = true;
    }

    void Update()
    {
        if ( ! canMove)
            return;

        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

        if (transform.position == targetPos)
            transform.position = originPos;
    }
}
