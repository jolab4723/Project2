using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class WBH_EnemyMovement : MonoBehaviour
{
    public bool IsMoving => agent.hasPath && agent.velocity.sqrMagnitude > 0.01f;
    public bool IsArrived => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;
    public float CurrentSpeed => agent.velocity.magnitude / agent.speed;//!@

    private WBH_EnemyStatus status;
    private NavMeshAgent agent;
    private Vector3 lastDestination;
    private bool canControl = true;
    private bool isStatusEffectControlBlocked; // 상태이상으로 인한 움직임 불가처리
    private bool isCutSceneControlBlocked;
    private float jumpHeight = 3f;
    private float landingNavSearchRadius = 2f;
    
    private Coroutine jumpCoroutine;
    private Coroutine dashCoroutine;
    public event Action OnDashUpdate;

    public bool CanControl => canControl && !isStatusEffectControlBlocked && ! isCutSceneControlBlocked;
    private bool CanUseAgent => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    public int AreaMask => agent.areaMask;


    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        status = GetComponent<WBH_EnemyStatus>();
    }

    private void OnEnable()
    {
        RefreshControlState();
    }

    // NavMeshAgent 초기화
    public void Initialize(WBH_EnemyInfo info)
    {
        agent ??= GetComponent<NavMeshAgent>();

        canControl = true;
        isStatusEffectControlBlocked = false;

        agent.speed = info.moveSpeed;

        lastDestination = Vector3.zero;
    }

    // 목적지 이동
    public void Move(Vector3 destination)
    {
        if (!CanControl)
            return;

        if ((destination - lastDestination).sqrMagnitude < 0.01f)
            return;

        lastDestination = destination;
        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    // 정지
    public void Stop()
    {
        if (!CanUseAgent)
            return;

        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    // 속도 변경
    public void SetMoveSpeed(float moveSpeed)
    {
        agent.speed = moveSpeed;
    }

    // 순간이동 (navmesh 끈 상태에서 이동 후 다시 nav적용 시킬 때 필요) (몬스터 생성시 위치지정, 순간이동 패턴 구현 시 사용)
    public void Warp(Vector3 position)
    {
        agent.Warp(position);
    }

    // 움직임 여부 조작
    public void SetControlEnable(bool enable)
    {
        canControl = enable;
        RefreshControlState();
    }

    // 상태이상으로 인한 움직임 불가
    public void SetStatusEffectControlBlock(bool block)
    {
        isStatusEffectControlBlocked = block;
        RefreshControlState();
    }

    private void RefreshControlState()
    {
        if (!CanUseAgent)
            return;

        if(!CanControl)
        {
            Stop();
            return;
        }
        agent.isStopped = false;
    }

    // 돌진 (데미지 X)
    public void Dash(Vector3 direction, float distance, float duration, Action onCompleted = null)
    {
        CancelForcedMovement();

        dashCoroutine = StartCoroutine(CoDash(direction, distance, duration, onCompleted));
    }

    private IEnumerator CoDash(Vector3 direction, float distance, float duration, Action onCompleted)
    {
        SetControlEnable(false);

        Vector3 start = transform.position;

        Vector3 end = start + direction * distance;

        float time = 0f;

        while (time < duration) 
        {
            time += Time.deltaTime;

            transform.position = Vector3.Lerp(start, end, time / duration);
            OnDashUpdate?.Invoke();

            yield return null;
        }

        Warp(transform.position);
        dashCoroutine = null;

        SetControlEnable(true);

        onCompleted?.Invoke();
    }

    // 점프 (데미지 X)
    public bool JumpTo(Vector3 destination, float duration, System.Action onCompleted = null)
    {
        if(!NavMesh.SamplePosition(destination, out NavMeshHit navMeshHit, landingNavSearchRadius, agent.areaMask))
        {
            return false;
        }

        if (jumpCoroutine != null)
        {
            StopCoroutine(jumpCoroutine);
        }

        jumpCoroutine = StartCoroutine(CoJumpTo(navMeshHit.position, duration, onCompleted));

        return true;
    }
    // 점프할 목적지가 navMesh 가능한지 탐색
    public bool CanJumpTo(Vector3 destination)
    {
        return NavMesh.SamplePosition(destination, out _, landingNavSearchRadius, agent.areaMask);
    }

    private IEnumerator CoJumpTo(Vector3 landingPos, float duration, System.Action onCompleted)
    {
        SetControlEnable(false);

        Vector3 startPos = transform.position;
        float elapsed = 0f;

        agent.updatePosition = false;

        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 position = Vector3.Lerp(startPos, landingPos, t);

            position.y += jumpHeight * 4f * t * (1f - t);

            transform.position = position;

            yield return null;
        }

        transform.position = landingPos;

        agent.Warp(landingPos);
        agent.updatePosition = true;

        jumpCoroutine = null;
        SetControlEnable(true);

        onCompleted?.Invoke();
    }

    // 돌진, 점프 코루틴 중단 (적 사망, 적 기절과 같이 패턴 중단 시에 사용)
    public void CancelForcedMovement()
    {
        if(dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }
        if(jumpCoroutine != null)
        {
            StopCoroutine (jumpCoroutine);
            jumpCoroutine = null;
        }

        if (agent == null)
            return;

        agent.updatePosition = true;

        if(agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.Warp(transform.position);
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    // destination 까지 navMesh 경로가 있는지 체크
    public bool TryCalculatePath(Vector3 destination, NavMeshPath path)
    {
        if(!agent.enabled || !agent.isOnNavMesh)
            return false;

        if (!agent.CalculatePath(destination, path))
            return false;

        return path.status == NavMeshPathStatus.PathComplete;
    }

    // 컷씬 동안 행동불가
    public void SetCutSceneControlBlock(bool block)
    {
        isCutSceneControlBlocked = block;
        RefreshControlState();
    }
}
