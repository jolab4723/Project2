using UnityEngine;
using UnityEngine.AI;

public class Enemy_Animator : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>(); // 애니메이터 가져오기
    }

    void Update()
    {
        // agent.velocity.magnitude 는 방향에 상관없이 "현재 이동하는 총 속력"을 숫자로 반환합니다.
        float speed = agent.velocity.magnitude;
        // 애니메이터의 "Speed"라는 파라미터에 값을 넘겨줍니다.
        animator.SetFloat("Speed", speed);
    }
}
