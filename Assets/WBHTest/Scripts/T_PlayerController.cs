using UnityEngine;
using UnityEngine.AI;

public class T_PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] public NavMeshAgent agent;

    [SerializeField] private WBH_PlayerStateMachine stateMachine;

    public bool canDodge => currentDodgeCooltime <= 0f;
    public Vector3 lookDir { get; private set; }
    public float currentDodgeCooltime { get; private set; }

    private Camera mainCamera;
    private Animator animator;
    private WBH_PlayerIndicator indicator;
    private WBH_PlayerStatus status;
    private Vector3 dodgeDir;

    private void Awake()
    {
        mainCamera = Camera.main;
        animator = GetComponent<Animator>();
        stateMachine = GetComponent<WBH_PlayerStateMachine>();
        indicator = GetComponentInChildren<WBH_PlayerIndicator>();
        status = GetComponent<WBH_PlayerStatus>();

        agent.autoBraking = false;
        agent.updateRotation = false;
        agent.speed *= status.MoveSpeed;
    }

    private void Start()
    {
        status.Initialize(this);
    }

    private void OnEnable()
    {
        stateMachine.OnEnterState += HandleEnterState;
        stateMachine.OnExitState += HandleExitState;
    }
    private void OnDisable()
    {
        stateMachine.OnEnterState -= HandleEnterState;
        stateMachine.OnExitState -= HandleExitState;
    }

    private void Update()
    {
        // 회피 쿨타임 체크
        CheckDodge();
        // 이동 종료 시, Idle 상태로 변환
        UpdateMoveState();
    }

    // 상태 진입 행동
    private void HandleEnterState(PlayerState state)
    {
        if(state == PlayerState.Dodge)
        {
            StartCoroutine(Dodge(dodgeDir));
        }
        switch (state)
        {
            case PlayerState.Attack:
            case PlayerState.Skill:
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
                break;
        }
        if (state == PlayerState.Dead)
        {
            indicator.Hide();
            animator.SetTrigger("Dead");
        }
    }

    // 상태 나감 행동
    private void HandleExitState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Attack:
            case PlayerState.Skill:
                agent.isStopped = false;
                break;
        }
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
        agent.enabled = false;

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + dir * status.DodgeDistance;

        float elapsed = 0f;

        animator.SetTrigger("Dodge");

        while (elapsed < status.DodgeDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / status.DodgeDuration;

            transform.position = Vector3.Lerp(startPos, endPos, t);

            yield return null;
        }

        agent.enabled = true;

        stateMachine.ChangeState(PlayerState.Idle);
    }

    public void MoveCommand(Vector3 destination)
    {
        if (stateMachine.IsAnyState(PlayerState.Dodge, PlayerState.Dead))
            return;

        stateMachine.ChangeState(PlayerState.Move);

        agent.SetDestination(destination);

        Vector3 dir = destination - transform.position;
        dir.y = 0;

        if(dir.sqrMagnitude > 0.001f)
        {
            transform.forward = dir;
        }

        lookDir = transform.forward;
    }

    public void TryDodge()
    {
        if (!canDodge)
            return;

        dodgeDir = GetMouseDirection();

        if (dodgeDir == Vector3.zero)
            return;

        currentDodgeCooltime = status.DodgeCooltime;

        stateMachine.ChangeState(PlayerState.Dodge);
    }

    private void UpdateMoveState()
    {
        if (!stateMachine.Is(PlayerState.Move) || agent.pathPending || agent.remainingDistance > agent.stoppingDistance || agent.velocity.sqrMagnitude > 0.01f)
            return;

        stateMachine.ChangeState(PlayerState.Idle);
    }

    public void Die() //!@ 사망처리. 이벤트 구독으로 리팩토링.
    {
        stateMachine.ChangeState(PlayerState.Dead);
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

    public void SetMoveSpeed(float moveSpeed)
    {
        agent.speed = moveSpeed;
    }

    // 캐릭터가 마우스 위치를 바라보게하고 해당 방향을 반환하는 메서드
    //private void PlayerViewDir()
    //{
    //    Vector3 dir = GetMouseDirection();

    //    if (dir != Vector3.zero)
    //    {
    //        lookDir = dir;
    //        transform.forward = lookDir;
    //    }
    //}
}
