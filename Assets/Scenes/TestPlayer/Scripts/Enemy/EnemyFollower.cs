using UnityEngine;
using UnityEngine.AI;

public class EnemyFollower : MonoBehaviour
{
    [Header("추적 대상")]
    public Transform target;
    private NavMeshAgent agent;

    [Header("이동 설정")]
    public float stopDistance = 1.5f;

    [Header("동족 겹침 방지 (드론 등 공중 유닛 전용)")]
    [Tooltip("이 기능을 켜면 설정된 레이어의 유닛끼리 가까워질 때 서로 밀어냅니다.")]
    public bool useSeparation = false;
    public float separationRadius = 1.5f; // 감지할 반경
    public float separationForce = 3f;    // 튕겨내는 힘
    public LayerMask separationLayer;     // 서로 밀어낼 레이어 (예: AirEnemy)

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.stoppingDistance = stopDistance;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    void Update()
    {
        if (target == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        // 1. 지속 추적
        agent.SetDestination(target.position);

        // 2. 동족 겹침 방지 밀어내기 적용 (스위치가 켜져 있을 때만)
        if (useSeparation)
        {
            ApplySeparation();
        }
    }

    // 겹치지 않도록 밀어내는 기능
    private void ApplySeparation()
    {
        // 주변 separationRadius 반경 안에 있는 separationLayer 유닛들을 모두 감지
        Collider[] nearbyFriends = Physics.OverlapSphere(transform.position, separationRadius, separationLayer);
        Vector3 pushVector = Vector3.zero;

        foreach (Collider col in nearbyFriends)
        {
            if (col.gameObject != gameObject) // 나 자신은 제외
            {
                // 다른 드론에서 내 쪽으로 향하는 방향 계산
                Vector3 diff = transform.position - col.transform.position;
                diff.y = 0; // 공중 위아래로 튕기는 것을 막기 위해 수평으로만 튕겨냄

                // 너무 가까우면 크게, 멀면 작게 밀어내도록 거리 대비 계산
                float distance = diff.magnitude;
                if (distance < 0.01f) distance = 0.01f;

                pushVector += (diff.normalized / distance);
            }
        }

        // 밀어낼 힘이 누적되었다면, NavMesh 표면을 따라 살짝 강제로 밀어줌
        if (pushVector != Vector3.zero)
        {
            agent.Move(pushVector * separationForce * Time.deltaTime);
        }
    }
}