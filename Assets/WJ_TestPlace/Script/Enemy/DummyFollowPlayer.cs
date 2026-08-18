using UnityEngine;

/// <summary>
/// 허수아비가 플레이어를 따라다니게 한다 - 스킬 테스트할 때마다 허수아비 위치로 되돌아가지 않고
/// 플레이어 근처에서 바로 테스트할 수 있게 하기 위한 편의 기능. TrainingDummyStatus는 원래
/// "이동/공격 없이 맞기만 하면 되는" 독립 컴포넌트로 설계돼 있어서(주석 참고), 이동 로직은
/// NavMeshAgent 없이 별도의 가벼운 컴포넌트로 분리했다 - 벽 회피는 하지 않는 단순 직선 추적이다.
/// </summary>
[RequireComponent(typeof(TrainingDummyStatus))]
public class DummyFollowPlayer : MonoBehaviour
{
    [Tooltip("따라가는 속도(유닛/초).")]
    [SerializeField] private float followSpeed = 4f;

    [Tooltip("플레이어와 이 거리 이하로는 다가가지 않는다.")]
    [SerializeField] private float stopDistance = 2f;

    [Tooltip("플레이어와 이 거리 이상 벌어지면(예: 순간이동, 빠른 이동) 순간이동으로 따라붙는다.")]
    [SerializeField] private float teleportDistance = 15f;

    private Transform player;
    private TrainingDummyStatus status;

    private void Awake()
    {
        status = GetComponent<TrainingDummyStatus>();
        ResolvePlayer();
    }

    private void Update()
    {
        if (player == null && !ResolvePlayer())
            return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        if (distance <= stopDistance)
            return;

        Vector3 dir = toPlayer / distance;

        if (distance > teleportDistance)
        {
            transform.position = player.position - dir * stopDistance;
            return;
        }

        // 이동속도 감소 버프(예: 중력장 발생)를 실제로 체감할 수 있도록 배율을 반영한다.
        float speed = followSpeed * (status != null ? status.MoveSpeedMultiplier : 1f);
        transform.position += dir * speed * Time.deltaTime;
        transform.forward = dir;
    }

    private bool ResolvePlayer()
    {
        player = PlayerStatManager.Instance != null ? PlayerStatManager.Instance.transform : null;
        return player != null;
    }
}
