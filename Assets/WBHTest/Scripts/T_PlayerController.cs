using UnityEngine;
using UnityEngine.AI;

public class T_PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private NavMeshAgent agent;

    [Header("Dodge")]
    [SerializeField] private float dodgeDistance = 5f;
    [SerializeField] private float dodgeDuration = 0.5f;
    [SerializeField] private float dodgeCooltime = 6f;

    [SerializeField] private WBH_PlayerStateMachine stateMachine;

    private Camera mainCamera;

    public bool canDodge => currentDodgeCooltime <= 0f;
    public Vector3 lookDir { get; private set; }
    private Animator animator;
    public float currentDodgeCooltime { get; private set; }

    private void Awake()
    {
        mainCamera = Camera.main;
        animator = GetComponent<Animator>();
        stateMachine = GetComponent<WBH_PlayerStateMachine>();

        //agent.updateRotation = false;
        agent.autoBraking = true;
    }

    private void Update()
    {
        // 회피 쿨타임 체크
        CheckDodge();
    }

    private void CheckDodge()
    {
        if (canDodge)
            return;

        currentDodgeCooltime -= Time.deltaTime;

        if (currentDodgeCooltime <= 0f)
        {
            currentDodgeCooltime = 0f;
        }
    }

    // 캐릭터가 마우스 위치를 바라보게하고 해당 방향을 반환하는 메서드
    private void PlayerViewDir()
    {
        Vector3 dir = GetMouseDirection();

        if(dir != Vector3.zero)
        { 
            lookDir = dir;
            transform.forward = lookDir;
        }
    }

    // 마우스 커서 방향 반환 메서드
    private Vector3 GetMouseDirection()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if(Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 dir = hit.point - transform.position;
            dir.y = 0;

            return dir.normalized;
        }
        return Vector3.zero;
    }

    // 회피 코루틴
    private System.Collections.IEnumerator Dodge(Vector3 dir)
    {
        stateMachine.ChangeState(PlayerState.Dodge);

        agent.enabled = false;

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + dir * dodgeDistance;

        float elapsed = 0f;

        animator.SetTrigger("Dodge");

        while (elapsed < dodgeDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / dodgeDuration;

            transform.position = Vector3.Lerp(startPos, endPos, t);

            yield return null;
        }

        agent.enabled = true;

        stateMachine.ChangeState(PlayerState.Idle);
    }

    public void MoveCommand(Vector3 destination)
    {
        if (stateMachine.Is(PlayerState.Dodge))
            return;

        agent.SetDestination(destination);
    }

    public void TryDodge()
    {
        if (!canDodge)
            return;

        Vector3 dodgeDir = GetMouseDirection();

        if (dodgeDir == Vector3.zero)
            return;

        StartCoroutine(Dodge(dodgeDir));

        currentDodgeCooltime = dodgeCooltime;
    }

    // --- combat.cs 에서 활용할 이동처리
    public void MoveToTarget(Vector3 position, float attackRange)
    {
        if (stateMachine.Is(PlayerState.Dodge))
            return;

        agent.stoppingDistance = attackRange;

        agent.SetDestination(position);
    }

    public void ResetStoppingDistance()
    {
        agent.stoppingDistance = 0f;
    }
}
