using UnityEngine;
using UnityEngine.AI;

public class Player_ClickToMove : Player_ClickToAction
{
    protected NavMeshAgent agent;
    protected PlayerStatus status;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        status = GetComponent<PlayerStatus>();
    }

    protected virtual void Start()
    {
        agent.acceleration = 999;
        agent.autoBraking = false;
    }

    protected override void OnRightClickTarget(GameObject target, Vector3 clickPosition)
    {
        if (agent != null)
        {
            // 정지 상태 풀기
            agent.isStopped = false;

            // 1. Status 스크립트에 있는 내 이동 속도를 에이전트에게 전달
            if (status != null)
            {
                agent.speed = status.moveSpeed;
            }

            // 2. 클릭한 방향(목표 좌표)을 향해 즉시 회전 (휙 도는 효과)
            Vector3 lookDirection = clickPosition - transform.position;
            lookDirection.y = 0; // 캐릭터가 바닥을 향해 꼬라박거나 들리는 현상(y축 기울기) 방지

            // 내 위치와 클릭한 위치가 완전히 똑같지 않다면 방향을 바라봄
            if (lookDirection != Vector3.zero)
            {
                // Slerp(부드러운 회전)를 쓰지 않고 즉시 꽂아버립니다.
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }

            // 3. 최종적으로 내비메쉬 에이전트에게 목적지로 걸어가라고 명령
            agent.SetDestination(clickPosition);
        }
    }
}
