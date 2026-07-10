using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class WBH_EnemyMovement : MonoBehaviour
{
    public bool IsMoving => agent.hasPath && agent.velocity.sqrMagnitude > 0.01f;
    public bool IsArrived => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;

    private WBH_EnemyStatus status;
    private NavMeshAgent agent;
    private Vector3 lastDestination;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        status = GetComponent<WBH_EnemyStatus>();
    }

    // NavMeshAgent 초기화
    public void Initialize(WBH_EnemyInfo info)
    {
        agent.speed = info.moveSpeed;
    }

    // 목적지 이동
    public void Move(Vector3 destination)
    {
        if ((destination - lastDestination).sqrMagnitude < 0.01f)
            return;

        lastDestination = destination;
        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    // 정지
    public void Stop()
    {
        agent.isStopped = true;
    }

    // 속도 변경
    public void SetMoveSpeed(float moveSpeed)
    {
        agent.speed = moveSpeed;
    }

    // 순간이동 (몬스터 생성시 위치지정, 순간이동 패턴 구현 시 사용.)
    public void Warp(Vector3 position)
    {
        agent.Warp(position);
    }
}
