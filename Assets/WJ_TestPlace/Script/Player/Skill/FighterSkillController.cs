using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 파이터 액티브 스킬(Skill1~3, 키는 A/S/D) 실행을 담당한다.
/// PlayerActionInputHandler.OnSkillKeyPressed(이미 있었지만 아무도 구독하지 않던 이벤트)를
/// 구독해서 쿨타임을 확인하고, 스킬 형태(부채꼴/직선/대시)에 맞는 판정과 효과를 적용한다.
///
/// 전용 스킬 애니메이션이 아직 없어서, 기존 공격처럼 애니메이션 이벤트로 타이밍을 맞추는 대신
/// 코루틴으로 "스킬 진행 시간"만큼 PlayerState.Skill을 유지했다가 Idle로 돌아간다 - 기능은
/// 정상 동작하지만 시각적으로는 애니메이션 없이 즉시 판정된다.
///
/// 피해 적용은 T_PlayerCombat.CreateDamageRequest + WBH_CombatManager.ProcessDamage를 그대로
/// 재사용한다 - Attacker가 T_PlayerController여야 OnDamageDealt/OnCrit 아이템 트리거가 제대로
/// 발동하기 때문에(이번 세션에서 이미 확인한 부분) 직접 WBH_DamageRequest를 새로 만들지 않는다.
/// </summary>
public class FighterSkillController : MonoBehaviour
{
    [SerializeField] private PlayerActionInputHandler inputHandler;
    [SerializeField] private T_PlayerCombat combat;
    [SerializeField] private T_PlayerController controller;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;
    [SerializeField] private WBH_PlayerStatus status;
    [SerializeField] private LayerMask enemyLayer;

    [Tooltip("인덱스 0~2 = Skill1~3(A/S/D)")]
    [SerializeField] private SkillDefinitionSO[] skills = new SkillDefinitionSO[3];

    [Header("범위 표시(피드백용, 판정과 무관)")]
    [SerializeField] private Color sectorVisualColor = new Color(1f, 0.5f, 0.1f, 0.35f);
    [SerializeField] private Color lineVisualColor = new Color(1f, 0.15f, 0.1f, 0.35f);
    [SerializeField] private Color dashVisualColor = new Color(0.2f, 0.7f, 1f, 0.35f);

    private readonly float[] cooldownRemaining = new float[3];

    // T_PlayerCombat.CanAttack과 같은 조건 - 그 필드는 private라 직접 재사용할 수 없어 그대로 옮겨왔다.
    private bool CanUseSkill => !stateMachine.IsAnyState(PlayerState.Hit, PlayerState.Attack,
        PlayerState.Skill, PlayerState.Dodge, PlayerState.Dead);

    private void OnEnable()
    {
        if (inputHandler != null)
            inputHandler.OnSkillKeyPressed += HandleSkillKeyPressed;
    }

    private void OnDisable()
    {
        if (inputHandler != null)
            inputHandler.OnSkillKeyPressed -= HandleSkillKeyPressed;
    }

    private void Update()
    {
        for (int i = 0; i < cooldownRemaining.Length; i++)
        {
            if (cooldownRemaining[i] > 0f)
                cooldownRemaining[i] -= Time.deltaTime;
        }
    }

    /// <summary>남은 쿨타임(초). 준비됐으면 0.</summary>
    public float GetRemainingCooldown(int index) =>
        index >= 0 && index < cooldownRemaining.Length ? Mathf.Max(0f, cooldownRemaining[index]) : 0f;

    private void HandleSkillKeyPressed(int index) => TryUseSkill(index);

    /// <summary>인덱스(0~2 = Skill1~3)에 해당하는 스킬을 사용한다. 쿨타임 중이거나 행동 불가 상태면 조용히 실패.</summary>
    public bool TryUseSkill(int index)
    {
        if (index < 0 || index >= skills.Length)
            return false;

        SkillDefinitionSO def = skills[index];
        if (def == null || cooldownRemaining[index] > 0f || !CanUseSkill)
            return false;

        cooldownRemaining[index] = def.cooldownSeconds;
        FaceCursor();
        combat.CancelChase();
        stateMachine.ChangeState(PlayerState.Skill);

        switch (def.shapeType)
        {
            case SkillShapeType.SectorSlash:
                ExecuteSectorSlash(def);
                StartCoroutine(ReturnToIdleAfter(0.3f));
                break;

            case SkillShapeType.LineSlam:
                ExecuteLineSlam(def);
                StartCoroutine(ReturnToIdleAfter(0.35f));
                break;

            case SkillShapeType.Dash:
                StartCoroutine(ExecuteDash(def));
                break;
        }

        return true;
    }

    private IEnumerator ReturnToIdleAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (stateMachine.Is(PlayerState.Skill))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    /// <summary>부채꼴 범위 - T_PlayerCombat.SectorAttack과 같은 방식(구체 오버랩 + 각도 필터).</summary>
    private void ExecuteSectorSlash(SkillDefinitionSO def)
    {
        SkillRangeVisual.ShowSector(transform.position, transform.forward, def.sectorRange, def.sectorAngle, sectorVisualColor);

        Collider[] targets = Physics.OverlapSphere(transform.position, def.sectorRange, enemyLayer);
        foreach (Collider target in targets)
        {
            Vector3 dirToTarget = (target.transform.position - transform.position).normalized;
            dirToTarget.y = 0f;
            if (Vector3.Angle(transform.forward, dirToTarget) > def.sectorAngle * 0.5f)
                continue;

            ApplyHit(target, def);
        }
    }

    /// <summary>정면 직선(사각형) 범위 - lineLength만큼 전방, lineWidth 너비.</summary>
    private void ExecuteLineSlam(SkillDefinitionSO def)
    {
        SkillRangeVisual.ShowLine(transform.position, transform.forward, def.lineLength, def.lineWidth, lineVisualColor);

        Vector3 center = transform.position + transform.forward * (def.lineLength * 0.5f);
        Vector3 halfExtents = new Vector3(def.lineWidth * 0.5f, 1.5f, def.lineLength * 0.5f);
        Collider[] targets = Physics.OverlapBox(center, halfExtents, transform.rotation, enemyLayer);

        foreach (Collider target in targets)
            ApplyHit(target, def);
    }

    private void ApplyHit(Collider target, SkillDefinitionSO def)
    {
        if (!target.TryGetComponent<WBH_ICombat>(out var combatTarget))
            return;

        WBH_DamageRequest request = combat.CreateDamageRequest(combatTarget, WBH_AttackType.Skill, status.CurrentElement, def.damageMultiplier);
        WBH_CombatManager.ProcessDamage(request);
    }

    /// <summary>
    /// 커서 방향으로 짧게 대시 - 피해 없이 이동만 한다.
    /// T_PlayerController.TryDodge의 NavMeshAgent 이동 방식을 참고했다(그 메서드 자체는 회피 전용
    /// 상태/무적 처리가 섞여 있어 그대로 재사용하지 않고, 이동 부분만 같은 방식으로 새로 짰다).
    /// </summary>
    private IEnumerator ExecuteDash(SkillDefinitionSO def)
    {
        Vector3 dir = GetCursorDirection();
        SkillRangeVisual.ShowLine(transform.position, dir, def.dashDistance, 0.6f, dashVisualColor);

        NavMeshAgent agent = controller.agent;

        Vector3 targetPos = transform.position + dir * def.dashDistance;
        if (NavMesh.Raycast(transform.position, targetPos, out NavMeshHit hit, NavMesh.AllAreas))
            targetPos = hit.position;

        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < def.dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / def.dashDuration);
            Vector3 next = Vector3.Lerp(start, targetPos, t);
            agent.Move(next - transform.position);
            yield return null;
        }

        agent.Warp(targetPos);

        if (stateMachine.Is(PlayerState.Skill))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    private void FaceCursor()
    {
        Vector3 dir = GetCursorDirection();
        if (dir.sqrMagnitude > 0.001f)
            transform.forward = dir;
    }

    /// <summary>마우스 커서가 가리키는 바닥 방향(XZ 평면, 정규화). T_PlayerController.GetMouseDirection과 같은 방식.</summary>
    private Vector3 GetCursorDirection()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);

        if (plane.Raycast(ray, out float distance))
        {
            Vector3 point = ray.GetPoint(distance);
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            return dir.normalized;
        }

        return transform.forward;
    }
}
