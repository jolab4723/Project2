using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class T_PlayerController : MonoBehaviour, WBH_ICombat
{

    [Header("Move")]
    [SerializeField] public NavMeshAgent agent;

    [SerializeField] private WBH_PlayerStateMachine stateMachine;

    private Camera mainCamera;
    private Animator animator;
    private WBH_PlayerIndicator indicator;
    private WBH_PlayerStatus status;
    private WBH_PlayerStatusEffectController statusEffectController;
    private Vector3 dodgeDir;
    public int reviveCount = 3;

    public Vector3 lookDir { get; private set; }
    public float currentDodgeCooltime { get; private set; }
    public bool IsControlEnabled { get; private set; } = true;
    public bool IsInvincible { get; private set; } = false; // 무적여부

    public WBH_ICombatStatus Status => status;
    public bool canDodge => currentDodgeCooltime <= 0f;
    private bool CanUseAgent => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;


    private void Awake()
    {
        mainCamera = Camera.main;
        animator = GetComponent<Animator>();
        stateMachine = GetComponent<WBH_PlayerStateMachine>();
        indicator = GetComponentInChildren<WBH_PlayerIndicator>();
        status = GetComponent<WBH_PlayerStatus>();
        statusEffectController = GetComponent<WBH_PlayerStatusEffectController>();

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
        status.OnDead += Die;
    }
    private void OnDisable()
    {
        stateMachine.OnEnterState -= HandleEnterState;
        stateMachine.OnExitState -= HandleExitState;
        status.OnDead -= Die;
    }

    private void Update()
    {
        // 회피 쿨타임 체크
        CheckDodge();
        // 이동 종료 시, Idle 상태로 변환
        UpdateMoveState();
        Revive();
    }

    // 상태 진입 행동
    private void HandleEnterState(PlayerState state)
    {
        if(state == PlayerState.Dodge)
        {
            this.gameObject.transform.LookAt(dodgeDir);
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
    private IEnumerator Dodge(Vector3 dir)
    {
        if(!CanUseAgent || !TryGetDodgeEnd(dir, out Vector3 endPos))
        {
            stateMachine.ChangeState(PlayerState.Idle);
            yield break;
        }

        agent.ResetPath();
        agent.isStopped = true;
        IsInvincible = true;
        animator.SetTrigger("Dodge");

        float elapsed = 0f;

        while (elapsed < status.DodgeDuration)
        {
            elapsed += Time.deltaTime;

            Vector3 remaining = endPos - transform.position;
            Vector3 step = remaining * (Time.deltaTime / Mathf.Max(0.001f, status.DodgeDuration - elapsed + Time.deltaTime));

            agent.Move(step);
            yield return null;
        }

        agent.Warp(endPos);
        agent.isStopped = false;
        IsInvincible = false;
        stateMachine.ChangeState(PlayerState.Idle);
    }

    public void MoveCommand(Vector3 destination)
    {
        if (stateMachine.IsAnyState(PlayerState.Dodge, PlayerState.Dead))
            return;

        if (!IsControlEnabled)
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
        if (!IsControlEnabled || !canDodge)
            return;

        dodgeDir = GetMouseDirection();

        if (dodgeDir == Vector3.zero)
            return;

        currentDodgeCooltime = status.DodgeCooltime;

        stateMachine.ChangeState(PlayerState.Dodge);
    }

    private void UpdateMoveState()
    {
        if (!CanUseAgent || !stateMachine.Is( PlayerState.Move))
            return;

        stateMachine.ChangeState(PlayerState.Idle);
    }

    public void Die() //!@ 사망처리. 이벤트 구독으로 리팩토링.
    {
        stateMachine.ChangeState(PlayerState.Dead);
        SetControlEnable(false);
    }

    public void ResetStoppingDistance()
    {
        agent.stoppingDistance = 0f;
    }

    public void SetMoveSpeed(float moveSpeed)
    {
        Debug.Log($"Agent Speed Before : {agent.speed}");
        agent.speed = moveSpeed;
        Debug.Log($"Agent Speed After : {agent.speed}");
    }

    public void TakeDamage(WBH_DamageResult result)
    {
        if (IsInvincible || stateMachine.Is(PlayerState.Dead))
            return;

        status.TakeDamage(result);

        // 최대 hp 대비 큰 피해(10%) 입으면 피격 애니메이션
        if(result.FinalDamage >= status.MaxHealth *0.1f )
        {
            stateMachine.ChangeState(PlayerState.Hit);
        }
    }

    // 현재 조작가능한 상태인지 판단
    public void SetControlEnable(bool enabled)
    {
        if (!CanUseAgent)
            return;

        IsControlEnabled = enabled;

        if (!enabled)
        {
            stateMachine.ChangeState(PlayerState.Idle);

            agent.ResetPath();
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        else
            agent.isStopped = false;
    }

    // 상태이상 추가
    public void AddStatusEffect (WBH_StatusEffectData data)
    {
        if (IsInvincible || stateMachine.Is(PlayerState.Dead))
            return;

        statusEffectController.AddStatusEffect(data);
    }

    private bool TryGetDodgeEnd(Vector3 direction, out Vector3 dodgeEnd)
    {
        Vector3 start = transform.position;
        Vector3 desiredEnd = start + direction * status.DodgeDistance;

        if(NavMesh.Raycast(start, desiredEnd, out NavMeshHit hit, agent.areaMask))
        {
            float safeDistance = Mathf.Max(0f, Vector3.Distance(start, hit.position) - agent.radius);
            dodgeEnd = start + direction * safeDistance;
        }
        else
        {
            dodgeEnd = desiredEnd;
        }
        return Vector3.Distance(start, dodgeEnd) > 0.01f;
    }

    // --- 테스트용 메서드
    // 부활
    public void Revive()
    {
        if(status.IsDead && reviveCount > 0)
        {
            reviveCount--;
            animator.SetTrigger("Revive");
            stateMachine.ChangeState(PlayerState.Idle);
            SetControlEnable(true);
            status.Heal(status.MaxHealth * 1f);
            StartCoroutine( BeInvincible(10));
        }
    }
    // 무적 코루틴. duration 동안 IsInvincible 이며 TakeDamage 의 영향을 받지 않음.
    private IEnumerator BeInvincible(float duration)
    {
        IsInvincible = true;
        yield return new WaitForSeconds(duration);
        IsInvincible = false;
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

    // --- combat.cs 에서 활용할 이동처리. 현재는 추적 기능을 사용하지 않아 미사용 상태
    //public void MoveToTarget(Vector3 position, float attackRange)
    //{
    //    if (!CanUseAgent || stateMachine.Is(PlayerState.Dodge))
    //        return;

    //    agent.stoppingDistance = attackRange;

    //    agent.SetDestination(position);
    //}
}
