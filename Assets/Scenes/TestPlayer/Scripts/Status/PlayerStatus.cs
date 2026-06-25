using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
    public enum State
    {
        Idle,
        Move,
        Chase,
        Attack,
        Die
    }

    [SerializeField] private State playerState = State.Idle;

    [Header("Move")]
    public float moveSpeed = 7.0f;
    public float MoveSpeed => moveSpeed;

    [Header("Attack")]
    private float attackRange = 1.75f;
    public float AttackRange => attackRange;

    public void SetState(State state)
    {
        playerState = state;
    }
}
