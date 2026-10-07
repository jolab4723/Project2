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
    private T_PlayerCombat combat;
    private WBH_PlayerEffect playerEffect;
    private WBH_PlayerStatusEffectController statusEffectController;
    private WBH_EffectSpawner effectSpawner;
    private WBH_ProjectileSpawner projectileSpawner;
    private YJ_MeshTrailTut meshTrailTut; // 2026.08.31 조용준 추가
    private Vector3 dodgeDir;
    private Coroutine invincibilityRoutine;

    public int reviveCount; // Start()에서 패시브 스킬(Revive) 해금 여부로 초기화된다.
    private bool canControl = true;
    private bool isStatusEffectControlBlocked;

    // 잡기 관련 변수
    private bool isGrabbed;
    private bool grabPreviousUpdatePos;
    private bool grabPreviousUpdateRot;

    // 컷씬 관련 변수
    private bool isCutSceneControlBlocked;
    private bool isCutSceneDamageBlocked;
    private bool isCutSceneTransformControlled;
    private bool previousAgentUpdatePosition;
    private bool previousAgentUpdateRotation;

    public Vector3 lookDir { get; private set; }
    public float currentDodgeCooltime { get; private set; }
    // SW 수정: 회피 무적과 시간제(부활·스킬) 무적을 따로 두어 한쪽 종료가 다른 쪽 무적을 끄지 않게 한다.
    public bool IsInvincible => isDodgeInvincible || isTimedInvincible; // 무적여부
    private bool isDodgeInvincible;
    private bool isTimedInvincible;
    // SW 수정: 비활성화·상태 이탈 때 진행 중인 회피 이동을 중지하기 위한 핸들
    private Coroutine dodgeRoutine;

    public WBH_ICombatStatus Status => status;
    public bool IsGrabbed => isGrabbed;
    public bool CanDodge => currentDodgeCooltime <= 0f;
    private bool CanUseAgent => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    public bool IsControlEnabled => canControl && !isStatusEffectControlBlocked && !isCutSceneControlBlocked && !isGrabbed;



    private void Awake()
    {
        mainCamera = Camera.main;
        animator = GetComponent<Animator>();
        combat = GetComponent<T_PlayerCombat>();
        stateMachine = GetComponent<WBH_PlayerStateMachine>();
        indicator = GetComponentInChildren<WBH_PlayerIndicator>();
        status = GetComponent<WBH_PlayerStatus>();
        playerEffect = GetComponent<WBH_PlayerEffect>();
        statusEffectController = GetComponent<WBH_PlayerStatusEffectController>();

        meshTrailTut = GetComponentInChildren<YJ_MeshTrailTut>(); // 2026.08.31 조용준 추가

        effectSpawner = FindFirstObjectByType<WBH_EffectSpawner>();
        projectileSpawner = FindFirstObjectByType<WBH_ProjectileSpawner>();

        agent.autoBraking = false;
        agent.updateRotation = false;
        agent.speed *= status.MoveSpeed;
    }

    private void Start()
    {
        status.Initialize(this);
        combat.Initialize(projectileSpawner);
        statusEffectController.Initialize(effectSpawner);
        playerEffect.Initialize(effectSpawner);

        // 부활 패시브(Revive)를 해금한 경우에만 1회 부활 가능
        // SW 수정: 네트워크 플레이어는 서버가 보유한 개별 프로필과 소비 상태를 사용합니다.
        if (GetComponent<Mirror.NetworkIdentity>() == null)
            reviveCount = PassiveSkillManager.Instance != null && PassiveSkillManager.Instance.HasRevive ? 1 : 0;
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

        // SW 수정: 컴포넌트 비활성화만으로는 코루틴이 멈추지 않으므로 회피를 직접 중지하고 회피 상태에 갇히지 않게 한다.
        if (dodgeRoutine != null)
        {
            CancelDodge();
            if (stateMachine.Is(PlayerState.Dodge))
                stateMachine.ChangeState(PlayerState.Idle);
        }

        bool wasGrabbed = isGrabbed;
        isGrabbed = false;

        if(wasGrabbed && agent != null)
        {
            agent.updatePosition = grabPreviousUpdatePos;
            agent.updateRotation = grabPreviousUpdateRot;
        }
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
            transform.forward = dodgeDir;
            lookDir = dodgeDir;
            CancelDodge();
            dodgeRoutine = StartCoroutine(Dodge(dodgeDir));
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

    // 회피 쿨타임 판단
    private void CheckDodge()
    {
        if (CanDodge)
            return;

        currentDodgeCooltime -= Time.deltaTime;

        if (currentDodgeCooltime <= 0f)
        {
            currentDodgeCooltime = 0f;
        }
    }

    // 마우스 커서 방향 반환 메서드
    /// <summary>SW 수정: 입력 카메라를 다시 연결하고, 레이가 충돌 지점을 찾지 못하면 플레이어 높이의 평면에서 조준 방향을 구합니다.</summary>
    private Vector3 GetMouseDirection()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return transform.forward;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if(Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 dir = hit.point - transform.position;
            dir.y = 0;

            return dir.normalized;
        }
        // 바닥 밖과 고층 맵에서도 플레이어 높이의 수평 평면을 조준한다.
        Plane playerHeightPlane = new(Vector3.up, transform.position);
        if (playerHeightPlane.Raycast(ray, out float rayDistance))
            return Vector3.ProjectOnPlane(ray.GetPoint(rayDistance) - transform.position, Vector3.up).normalized;
        return transform.forward;
    }

    // 회피 코루틴
    /// <summary>SW 수정: 회피 모션은 공통 상태 전이에서 재생하고, 트레일 컴포넌트가 있을 때만 잔상을 실행합니다.</summary>
    private IEnumerator Dodge(Vector3 dir)
    {
        if(!CanUseAgent || !TryGetDodgeEnd(dir, out Vector3 endPos))
        {
            dodgeRoutine = null;
            stateMachine.ChangeState(PlayerState.Idle);
            yield break;
        }

        agent.ResetPath();
        agent.isStopped = true;
        isDodgeInvincible = true;
        // 회피 모션은 공통 PlayerAnimation이 상태 전이에서 한 번 재생한다.
        meshTrailTut?.Trail(); // 2026.08.31 조용준 추가

        float elapsed = 0f;

        while (elapsed < status.DodgeDuration)
        {
            elapsed += Time.deltaTime;

            Vector3 remaining = endPos - transform.position;
            Vector3 step = remaining * (Time.deltaTime / Mathf.Max(0.001f, status.DodgeDuration - elapsed + Time.deltaTime));

            agent.Move(step);
            yield return null;

            // SW 수정: Agent 해제나 다른 상태 전이(사망·잡기 등)가 생기면 이전 목적지로 이동·Warp·Idle 덮어쓰기를 하지 않는다.
            if (!CanUseAgent || !stateMachine.Is(PlayerState.Dodge))
            {
                dodgeRoutine = null;
                isDodgeInvincible = false;
                yield break;
            }
        }

        dodgeRoutine = null;
        agent.Warp(endPos);
        agent.isStopped = false;
        isDodgeInvincible = false;
        stateMachine.ChangeState(PlayerState.Idle);
    }

    /// <summary>SW 수정: 진행 중인 회피 이동을 멈추고 회피 무적만 해제한다. 시작 때 소비한 쿨다운은 환불하지 않으며 여러 번 호출해도 안전하다.</summary>
    private void CancelDodge()
    {
        if (dodgeRoutine != null)
            StopCoroutine(dodgeRoutine);
        dodgeRoutine = null;
        isDodgeInvincible = false;
    }

    // 이동명령
    public void MoveCommand(Vector3 destination)
    {
        if (!CanUseAgent || !IsControlEnabled || stateMachine.IsAnyState(PlayerState.Dodge, PlayerState.Skill,PlayerState.Dead))
            return;

        agent.isStopped = false;
        agent.stoppingDistance = 0f;

        if (!agent.SetDestination(destination))
            return;

        stateMachine.ChangeState(PlayerState.Move);

        Vector3 dir = destination - transform.position;
        dir.y = 0;

        if(dir.sqrMagnitude > 0.001f)
        {
            transform.forward = dir;
        }

        lookDir = transform.forward;
    }

    // 회피
    /// <summary>SW 수정: 기존 회피 명령을 입력 카메라 기반의 공통 회피 검사에 전달합니다.</summary>
    public void TryDodge()
    {
        TryDodgeFromInput();
    }

    /// <summary>SW 수정: 같은 플레이어를 조준하는 카메라를 명시적으로 연결한다. 씬 교체로 파괴되면 다시 찾는다.</summary>
    public void BindInputCamera(Camera camera) => mainCamera = camera;

    /// <summary>SW 수정: 현재 입력 카메라의 조준 방향으로 회피를 요청하고 수락 여부를 반환한다.</summary>
    public bool TryDodgeFromInput() => TryDodge(GetMouseDirection());

    /// <summary>SW 수정: 회피 방향과 도착 지점을 먼저 검사하고, 수락할 때만 쿨다운과 상태를 한 번 변경한다.</summary>
    public bool TryDodge(Vector3 direction)
    {
        if (!isActiveAndEnabled || !CanUseAgent || !IsControlEnabled || !CanDodge || status == null ||
            status.IsDead || stateMachine.IsAnyState(PlayerState.Dodge, PlayerState.Skill, PlayerState.Dead, PlayerState.Revive) ||
            !float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z))
            return false;
        direction.y = 0f;
        if (!float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < 0.0001f)
            return false;
        direction.Normalize();
        if (!TryGetDodgeEnd(direction, out _))
            return false;
        dodgeDir = direction;
        currentDodgeCooltime = status.DodgeCooltime;
        stateMachine.ChangeState(PlayerState.Dodge);
        return true;
    }

    // 추적 명령
    public bool ChaseCommand(Vector3 destination, float stoppingDistance)
    {
        if (!CanUseAgent || !IsControlEnabled || stateMachine.IsAnyState(PlayerState.Dodge, PlayerState.Dead))
            return false;

        agent.isStopped = false;
        agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);

        if (!agent.SetDestination(destination))
            return false;

        stateMachine.ChangeState(PlayerState.Chase);

        Vector3 dir = destination - transform.position;
        dir.y = 0;

        if(dir.sqrMagnitude > 0.001f)
        {
            transform.forward = dir.normalized;
        }

        lookDir = transform.forward;

        return true;
    }

    // 이동취소
    public void StopMovement()
    {
        if(CanUseAgent)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.stoppingDistance = 0f;
        }

        if(stateMachine.IsAnyState(PlayerState.Move,PlayerState.Chase))
        {
            stateMachine.ChangeState(PlayerState.Idle);
        }
    }

    // 목적지 도달 시 자동 Idle 상태 진입
    private void UpdateMoveState()
    {
        if (!CanUseAgent || !stateMachine.Is( PlayerState.Move))
            return;

        if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.05f)
            return;

        if (agent.hasPath && agent.velocity.sqrMagnitude > 0.01f)
            return;

        agent.ResetPath();
        stateMachine.ChangeState(PlayerState.Idle);
    }

    public void Die() // 사망처리. 
    {
        SetControlEnable(false);
        stateMachine.ChangeState(PlayerState.Dead);
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
        if (IsInvincible || isCutSceneDamageBlocked ||stateMachine.Is(PlayerState.Dead))
            return;

        status.TakeDamage(result);

        // 최대 hp 대비 큰 피해(10%) 입으면 피격 애니메이션
        // SW 수정: 치명적인 피해의 OnDead가 확정한 사망 상태를 피격 상태로 덮어쓰지 않습니다.
        if(!status.IsDead && result.FinalDamage >= status.MaxHealth *0.1f )
        {
            stateMachine.ChangeState(PlayerState.Hit);
        }
    }

    // 현재 조작가능한 상태인지 판단
    public void SetControlEnable(bool enabled)
    {
        canControl = enabled;
        RefreshControlState();
    }
    // 상태이상이 조작을 막고 있는지 판단
    public void SetStatusEffectControlBlock(bool block)
    {
        isStatusEffectControlBlocked = block;
        RefreshControlState();
    }
    // 조작 상태 갱신
    private void RefreshControlState()
    {
        if (!CanUseAgent)
            return;

        if (!IsControlEnabled)
        {
            if (!stateMachine.IsAnyState(PlayerState.Dead, PlayerState.Revive))
            {
                stateMachine.ChangeState(PlayerState.Idle);
            }

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

    // 회피지점 검사 (벽뚫 방지)
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

    // 부활
    private void BeginRevive(float healthRatio, float invincibleDuration)
    {
        float clampRatio = Mathf.Clamp01(healthRatio);

        float healAmount = status.MaxHealth * clampRatio;

        status.Heal(healAmount);

        ApplyInvincibility(invincibleDuration);

        stateMachine.ChangeState(PlayerState.Revive);
    }

    public void TryRevive()
    {
        if (!stateMachine.Is(PlayerState.Dead) || !status.IsDead || reviveCount <= 0)
            return;

        reviveCount--;

        float healthRatio = PassiveSkillManager.Instance != null ? PassiveSkillManager.Instance.ReviveHealthPercent / 100f : 1f;
        if (!TryBeginPassiveRevive(healthRatio))
            reviveCount++;
    }

    /// <summary>SW 수정: 횟수 소비는 호출자가 소유하고 정상 패시브의 회복·무적·상태 전이는 공유합니다.</summary>
    public bool TryBeginPassiveRevive(float healthRatio)
    {
        var identity = GetComponent<Mirror.NetworkIdentity>();
        if ((identity != null && !identity.isServer) || !float.IsFinite(healthRatio) || healthRatio <= 0f ||
            !stateMachine.Is(PlayerState.Dead) || !status.IsDead)
            return false;
        SetControlEnable(false);
        BeginRevive(healthRatio, 10f);
        return true;
    }

    public void CompleteRevive()
    {
        if (!stateMachine.Is(PlayerState.Revive))
            return;

        stateMachine.ChangeState(PlayerState.Idle);
        SetControlEnable(true);
    }

    public void ReviveForStageClear()
    {
        if (!status.IsDead)
            return;

        BeginRevive(0.1f, 3f);
    }


    /// <summary>외부(스킬 등)에서 일정 시간 무적을 걸 때 사용. 이미 무적이 진행 중이면 새 지속시간으로 갱신한다.</summary>
    public void ApplyInvincibility(float duration)
    {
        if (invincibilityRoutine != null)
            StopCoroutine(invincibilityRoutine);

        invincibilityRoutine = StartCoroutine(BeInvincible(duration));
    }

    // 무적 코루틴. duration 동안 IsInvincible 이며 TakeDamage 의 영향을 받지 않음.
    private IEnumerator BeInvincible(float duration)
    {
        isTimedInvincible = true;
        yield return new WaitForSeconds(duration);
        isTimedInvincible = false;
        invincibilityRoutine = null;
    }

    // 컷씬 동안 행동불가
    public void SetCutSceneControlBlock(bool block)
    {
        isCutSceneControlBlocked = block;
        RefreshControlState();
    }
    public void SetCutSceneDamageBlock(bool block)
    {
        isCutSceneDamageBlocked = block;
    }

    public bool TryBeginGrab()
    {
        if (isGrabbed || !isActiveAndEnabled || stateMachine.IsAnyState(PlayerState.Dead, PlayerState.Revive, PlayerState.Dodge))
            return false;

        if (IsInvincible)
            return false;

        isGrabbed = true;

        StopMovement();
        RefreshControlState();

        if(agent != null && agent.isActiveAndEnabled)
        {
            grabPreviousUpdatePos = agent.updatePosition;
            grabPreviousUpdateRot = agent.updateRotation;

            agent.updatePosition = false;
            agent.updateRotation = false;
        }
        return true;
    }

    public void SetGrabPosition(Vector3 worldPos)
    {
        if (!isGrabbed)
            return;
        transform.position = worldPos;
    }

    public void EndGrab(Vector3 releasePos)
    {
        if (!isGrabbed)
            return;

        isGrabbed = false;

        if(agent != null && agent.isActiveAndEnabled)
        {
            int areaMask = agent.areaMask;

            if(NavMesh.SamplePosition(releasePos, out NavMeshHit hit, 2f, areaMask))
            {
                transform.position = hit.position;
                agent.Warp(hit.position);
            }
            else
            {
                transform.position = releasePos;
            }

            agent.updatePosition = grabPreviousUpdatePos;
            agent.updateRotation = grabPreviousUpdateRot;
        }
        else
        {
            transform.position = releasePos;
        }
        RefreshControlState();
    }

    // --- 테스트용 메서드

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
