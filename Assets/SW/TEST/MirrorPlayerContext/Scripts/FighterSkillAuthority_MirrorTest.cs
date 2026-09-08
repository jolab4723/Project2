using System.Collections;
using System.Collections.Generic;
using ItemSystem;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

public enum MirrorSkillRequestResult : byte
{
    None = 0,
    Accepted = 1,
    Hit = 2,
    NoTarget = 3,
    Dead = 4,
    InvalidSlot = 5,
    InvalidAim = 6,
    DuplicateRequest = 7,
    SkillOnCooldown = 8,
    SkillAlreadyPending = 9,
    AnimationImpactMissing = 10,
}

/// <summary>
/// WJ <see cref="FighterSkillController"/>의 현재 Fighter 스킬을 Mirror 테스트 플레이어에 연결한다.
/// 입력과 이동 피드백은 로컬 소유자가 처리하고, 쿨타임·범위 판정·피해·상태이상은 서버가 확정한다.
/// WJ 원본과 데이터 SO는 수정하지 않는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerContext), typeof(PlayerActionInputHandler_MirrorTest))]
public sealed class FighterSkillAuthority_MirrorTest : NetworkBehaviour
{
    private const float MaxAimDistance = 1000f;
    private const int SkillSlotCount = 3;
    private const float AnimationImpactTimeoutSeconds = 4f;

    [SerializeField] private PlayerContext context;
    [SerializeField] private PlayerActionInputHandler_MirrorTest inputHandler;
    [SerializeField] private T_PlayerCombat combat;
    [SerializeField] private T_PlayerController controller;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;
    [SerializeField] private WBH_PlayerStatus status;
    [SerializeField] private PlayerBuffManager buffManager;
    [SerializeField] private WBH_PlayerAnimation_MirrorTest animationView;
    [SerializeField] private WBH_PlayerEffect playerEffect;
    [SerializeField] private LayerMask enemyLayer = 1 << 10;

    [Tooltip("WJ SkillDefinitionSO. 인덱스 0~2 = A/S/D")]
    [SerializeField] private SkillDefinitionSO[] skills = new SkillDefinitionSO[SkillSlotCount];

    [Tooltip("Mirror 테스트 기본값은 넉백 검증용 Evolution1, 에어본 검증용 Evolution2, 기본 대시다.")]
    [SerializeField] private SkillEvolutionId[] activeEvolutions =
    {
        SkillEvolutionId.Evolution1,
        SkillEvolutionId.Evolution2,
        SkillEvolutionId.None,
    };

    [Header("범위 표시")]
    [SerializeField] private Color sectorVisualColor = new(1f, 0.5f, 0.1f, 0.35f);
    [SerializeField] private Color lineVisualColor = new(1f, 0.15f, 0.1f, 0.35f);
    [SerializeField] private Color dashVisualColor = new(0.2f, 0.7f, 1f, 0.35f);

    [SyncVar] private double skill0ReadyAt;
    [SyncVar] private double skill1ReadyAt;
    [SyncVar] private double skill2ReadyAt;
    [SyncVar] private MirrorSkillRequestResult lastResult;
    [SyncVar] private byte lastSkillIndex = byte.MaxValue;
    [SyncVar] private int lastHitCount;
    [SyncVar] private int lastStatusEffectCount;
    [SyncVar] private uint acceptedSkillCount;
    [SyncVar] private uint rejectedSkillCount;

    private readonly double[] predictedReadyAt = new double[SkillSlotCount];
    private readonly HashSet<WBH_ICombat> resolvedTargets = new();
    private uint nextLocalRequestId;
    private uint activeLocalRequestId;
    private byte activeLocalSlot = byte.MaxValue;
    private Vector3 activeLocalAim;
    private double localAnimationExpiresAt;
    private uint lastServerRequestId;
    private uint pendingServerRequestId;
    private byte pendingServerSlot = byte.MaxValue;
    private Vector3 pendingServerAim;
    private double serverAnimationExpiresAt;
    private Coroutine localSkillRoutine;
    private int presentationSkillIndex = -1;
    private SkillEvolutionId presentationEvolution;

    public int SkillCount => Mathf.Min(SkillSlotCount, skills?.Length ?? 0);
    public MirrorSkillRequestResult LastResult => lastResult;
    public int LastSkillIndex => lastSkillIndex == byte.MaxValue ? -1 : lastSkillIndex;
    public int LastHitCount => lastHitCount;
    public int LastStatusEffectCount => lastStatusEffectCount;
    public uint AcceptedSkillCount => acceptedSkillCount;
    public uint RejectedSkillCount => rejectedSkillCount;
    private bool IsUnavailable => GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent == true ||
        context?.RuntimeState?.IsDead == true || status == null || status.IsDead;

    /// <summary>재접속 예약으로 미완료 스킬과 로컬 대시를 취소하며 서버 쿨다운과 마나는 유지한다.</summary>
    [Server]
    public void ServerCancelForDisconnect()
    {
        ClearServerPendingSkill();
        ClearLocalPendingSkill(restoreIdle: false);
        lastServerRequestId = 0;
        resolvedTargets.Clear();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (IsUnavailable)
        {
            if (isServer) ServerCancelForDisconnect();
            else ClearLocalPendingSkill(restoreIdle: false);
            return;
        }
        if (isLocalPlayer &&
            activeLocalRequestId != 0 &&
            NetworkTime.time >= localAnimationExpiresAt)
        {
            ClearLocalPendingSkill(restoreIdle: true);
        }

        if (isServer &&
            pendingServerRequestId != 0 &&
            NetworkTime.time >= serverAnimationExpiresAt)
        {
            ClearServerPendingSkill();
            rejectedSkillCount++;
            lastResult = MirrorSkillRequestResult.AnimationImpactMissing;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();

        if (skills == null || skills.Length != SkillSlotCount)
            System.Array.Resize(ref skills, SkillSlotCount);
        if (activeEvolutions == null || activeEvolutions.Length != SkillSlotCount)
            System.Array.Resize(ref activeEvolutions, SkillSlotCount);
    }
#endif

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        ClearLocalPendingSkill(restoreIdle: false);
        System.Array.Clear(predictedReadyAt, 0, predictedReadyAt.Length);
        nextLocalRequestId = 0;
        UnbindLocalInput();
        if (inputHandler != null)
        {
            inputHandler.OnSkillKeyPressed += HandleSkillPressed;
            inputHandler.OnSkillKeyReleased += HandleSkillReleased;
        }
    }

    public override void OnStopLocalPlayer()
    {
        UnbindLocalInput();
        ClearLocalPendingSkill(restoreIdle: false);
        System.Array.Clear(predictedReadyAt, 0, predictedReadyAt.Length);
        nextLocalRequestId = 0;
        base.OnStopLocalPlayer();
    }

    private void OnDisable()
    {
        UnbindLocalInput();

        if (isLocalPlayer)
            ClearLocalPendingSkill(restoreIdle: false);
    }

    public SkillDefinitionSO GetSkillDefinition(int index)
    {
        return index >= 0 && index < SkillCount ? skills[index] : null;
    }

    public SkillEvolutionId GetEvolution(int index)
    {
        return index >= 0 && index < (activeEvolutions?.Length ?? 0)
            ? activeEvolutions[index]
            : SkillEvolutionId.None;
    }

    public float GetRemainingCooldown(int index)
    {
        if (index < 0 || index >= SkillSlotCount)
            return 0f;

        double readyAt = System.Math.Max(GetServerReadyAt(index), predictedReadyAt[index]);
        return Mathf.Max(0f, (float)(readyAt - NetworkTime.time));
    }

    public bool TryUseLocalSkill(int index)
    {
        if (!isLocalPlayer || !NetworkClient.active || !NetworkClient.ready ||
            index < 0 || index >= SkillCount || skills[index] == null ||
            IsUnavailable || context?.RuntimeState?.HasSnapshot != true || !CanUseLocalSkill() ||
            GetRemainingCooldown(index) > 0f)
        {
            return false;
        }

        Vector3 aimDirection = GetCursorDirection();
        if (!IsFinite(aimDirection) || aimDirection.sqrMagnitude < 0.001f)
            return false;

        SkillDefinitionSO definition = skills[index];
        predictedReadyAt[index] = NetworkTime.time + Mathf.Max(0f, definition.cooldownSeconds);
        nextLocalRequestId++;
        if (nextLocalRequestId == 0)
            nextLocalRequestId++;

        transform.forward = aimDirection;
        combat?.CancelChase();
        stateMachine?.ChangeState(PlayerState.Skill);

        activeLocalRequestId = nextLocalRequestId;
        activeLocalSlot = (byte)index;
        activeLocalAim = aimDirection;
        localAnimationExpiresAt = NetworkTime.time + AnimationImpactTimeoutSeconds;

        CmdRequestSkill(nextLocalRequestId, (byte)index, aimDirection);
        return true;
    }

    private void HandleSkillPressed(int index)
    {
        if (index < SkillSlotCount)
            TryUseLocalSkill(index);
    }

    private void HandleSkillReleased(int index)
    {
        // Mirror 테스트 기본 프리셋에는 차징 진화가 없다. 릴리즈 입력 경계는 WJ와 호환되도록 유지한다.
    }

    [Command]
    private void CmdRequestSkill(uint requestId, byte slotIndex, Vector3 aimDirection)
    {
        if (IsUnavailable)
        {
            Reject(requestId, slotIndex, MirrorSkillRequestResult.Dead);
            return;
        }

        if (requestId == 0 || requestId <= lastServerRequestId)
        {
            Reject(requestId, slotIndex, MirrorSkillRequestResult.DuplicateRequest);
            return;
        }

        lastServerRequestId = requestId;

        if (slotIndex >= SkillCount || skills[slotIndex] == null)
        {
            Reject(requestId, slotIndex, MirrorSkillRequestResult.InvalidSlot);
            return;
        }

        aimDirection.y = 0f;
        if (!IsFinite(aimDirection) || aimDirection.sqrMagnitude < 0.001f ||
            aimDirection.sqrMagnitude > MaxAimDistance * MaxAimDistance)
        {
            Reject(requestId, slotIndex, MirrorSkillRequestResult.InvalidAim);
            return;
        }

        double now = NetworkTime.time;
        if (pendingServerRequestId != 0)
        {
            Reject(requestId, slotIndex, MirrorSkillRequestResult.SkillAlreadyPending);
            return;
        }

        if (now < GetServerReadyAt(slotIndex))
        {
            Reject(requestId, slotIndex, MirrorSkillRequestResult.SkillOnCooldown);
            return;
        }

        SkillDefinitionSO definition = skills[slotIndex];
        aimDirection.Normalize();
        transform.forward = aimDirection;
        SetServerReadyAt(slotIndex, now + Mathf.Max(0f, definition.cooldownSeconds));
        lastSkillIndex = slotIndex;
        lastResult = MirrorSkillRequestResult.Accepted;
        lastHitCount = 0;
        lastStatusEffectCount = 0;
        acceptedSkillCount++;

        pendingServerRequestId = requestId;
        pendingServerSlot = slotIndex;
        pendingServerAim = aimDirection;
        serverAnimationExpiresAt = now + AnimationImpactTimeoutSeconds;
        RpcBeginSkillPresentation(
            slotIndex,
            GetEvolution(slotIndex),
            definition.shapeType == SkillShapeType.Dash
                ? Mathf.Max(0.01f, definition.dashDuration)
                : 0f);
    }

    /// <summary>
    /// 최신 Fighter 애니메이션 클립의 <c>AniEvent_ExecuteSkill</c>이 실제로 도착한 요청만
    /// 서버 판정으로 확정한다. 입력만으로는 피해가 발생하지 않는다.
    /// </summary>
    public bool TryConfirmLocalSkillImpactFromAnimation()
    {
        if (IsUnavailable || !isLocalPlayer ||
            !NetworkClient.active ||
            !NetworkClient.ready ||
            activeLocalRequestId == 0 ||
            activeLocalSlot >= SkillCount)
        {
            return false;
        }

        SkillDefinitionSO definition = skills[activeLocalSlot];
        if (definition == null)
            return false;

        uint requestId = activeLocalRequestId;
        byte slotIndex = activeLocalSlot;
        Vector3 aimDirection = activeLocalAim;

        activeLocalRequestId = 0;
        activeLocalSlot = byte.MaxValue;
        activeLocalAim = Vector3.zero;
        localAnimationExpiresAt = 0d;

        if (definition.shapeType == SkillShapeType.Dash)
        {
            if (localSkillRoutine != null)
                StopCoroutine(localSkillRoutine);

            localSkillRoutine = StartCoroutine(ExecuteLocalDash(definition, aimDirection));
        }

        CmdConfirmSkillAnimationImpact(requestId);
        return true;
    }

    [Command]
    private void CmdConfirmSkillAnimationImpact(uint requestId)
    {
        if (IsUnavailable || requestId == 0 ||
            requestId != pendingServerRequestId ||
            pendingServerSlot >= SkillCount ||
            NetworkTime.time > serverAnimationExpiresAt)
        {
            return;
        }

        byte slotIndex = pendingServerSlot;
        Vector3 aimDirection = pendingServerAim;
        SkillDefinitionSO definition = skills[slotIndex];
        ClearServerPendingSkill();

        if (definition == null)
            return;

        ResolveServerSkill(slotIndex, definition, aimDirection);
        RpcPresentSkillImpact(slotIndex, transform.position, aimDirection);
    }

    [Server]
    private void ResolveServerSkill(int index, SkillDefinitionSO definition, Vector3 aimDirection)
    {
        if (IsUnavailable) return;
        resolvedTargets.Clear();

        switch (definition.shapeType)
        {
            case SkillShapeType.SectorSlash:
                ResolveSectorSkill(index, definition, aimDirection);
                break;

            case SkillShapeType.LineSlam:
                ResolveLineSkill(index, definition, aimDirection);
                break;

            case SkillShapeType.Dash:
                ApplyServerDashBuff(index, definition);
                break;
        }

        if (definition.shapeType != SkillShapeType.Dash)
        {
            lastResult = lastHitCount > 0
                ? MirrorSkillRequestResult.Hit
                : MirrorSkillRequestResult.NoTarget;
        }
    }

    [Server]
    private void ResolveSectorSkill(int index, SkillDefinitionSO definition, Vector3 aimDirection)
    {
        SkillEvolutionId evolution = GetEvolution(index);
        float angle = evolution == SkillEvolutionId.Evolution3 ? 360f : definition.sectorAngle;
        Collider[] hits = Physics.OverlapSphere(transform.position, definition.sectorRange, enemyLayer);

        foreach (Collider hit in hits)
        {
            WBH_ICombat target = FindCombatTarget(hit);
            if (target == null || !resolvedTargets.Add(target))
                continue;

            Vector3 toTarget = GetTargetPosition(target) - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.001f || Vector3.Angle(aimDirection, toTarget.normalized) > angle * 0.5f)
                continue;

            if (!TryDamageTarget(target, definition.damageMultiplier))
                continue;

            if (evolution == SkillEvolutionId.Evolution1)
            {
                Vector3 knockbackDirection = toTarget.normalized;
                RecordStatus(target, new WBH_StatusEffectData(
                    WBH_StatusEffectType.KnockBack,
                    duration: definition.evoKnockbackDuration,
                    direction: knockbackDirection,
                    force: definition.evoKnockbackForce));
                RecordStatus(target, WBH_StatusEffectPresets.Stun1);
            }
        }
    }

    [Server]
    private void ResolveLineSkill(int index, SkillDefinitionSO definition, Vector3 aimDirection)
    {
        SkillEvolutionId evolution = GetEvolution(index);
        float length = evolution == SkillEvolutionId.Evolution2
            ? definition.evoWideLineLength
            : evolution == SkillEvolutionId.Evolution3
                ? definition.evoNarrowLineLength
                : definition.lineLength;
        float width = evolution == SkillEvolutionId.Evolution2
            ? definition.evoWideLineWidth
            : evolution == SkillEvolutionId.Evolution3
                ? definition.evoNarrowLineWidth
                : definition.lineWidth;
        float damageMultiplier = evolution == SkillEvolutionId.Evolution3
            ? definition.evoNarrowDamageMultiplier
            : definition.damageMultiplier;

        Vector3 center = transform.position + aimDirection * (length * 0.5f);
        Quaternion rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
        Collider[] hits = Physics.OverlapBox(
            center,
            new Vector3(width * 0.5f, 1.5f, length * 0.5f),
            rotation,
            enemyLayer);

        foreach (Collider hit in hits)
        {
            WBH_ICombat target = FindCombatTarget(hit);
            if (target == null || !resolvedTargets.Add(target) || !TryDamageTarget(target, damageMultiplier))
                continue;

            if (evolution == SkillEvolutionId.Evolution1)
            {
                RecordStatus(target, new WBH_StatusEffectData(
                    WBH_StatusEffectType.DefenseDown,
                    duration: definition.evoDefenseDownDuration,
                    value: definition.evoDefenseDownMultiplier));
                RecordStatus(target, WBH_StatusEffectPresets.Stun1);
            }
            else if (evolution == SkillEvolutionId.Evolution2)
            {
                RecordStatus(target, new WBH_StatusEffectData(
                    WBH_StatusEffectType.Airborne,
                    duration: definition.evoAirborneDuration,
                    height: definition.evoAirborneHeight));
            }
        }
    }

    [Server]
    private bool TryDamageTarget(WBH_ICombat target, float damageMultiplier)
    {
        if (IsUnavailable) return false;
        if (!WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                context,
                target,
                status.CurrentElement,
                damageMultiplier,
                null,
                out _))
        {
            return false;
        }

        lastHitCount++;
        return true;
    }

    [Server]
    private void RecordStatus(WBH_ICombat target, WBH_StatusEffectData data)
    {
        if (target?.Status == null || target.Status.IsDead)
            return;

        if (target is Component component &&
            component.GetComponentInParent<NetworkEnemyAuthority_MirrorTest>() is NetworkEnemyAuthority_MirrorTest authority)
        {
            if (authority.ServerTryApplyStatusEffect(data))
                lastStatusEffectCount++;
        }
        else
        {
            target.AddStatusEffect(data);
            lastStatusEffectCount++;
        }
    }

    [Server]
    private void ApplyServerDashBuff(int index, SkillDefinitionSO definition)
    {
        if (GetEvolution(index) == SkillEvolutionId.Evolution3 &&
            definition.evoDashDamageBuff != null && buffManager != null)
        {
            buffManager.ApplyBuff(definition.evoDashDamageBuff);
        }
    }

    [ClientRpc]
    private void RpcBeginSkillPresentation(
        byte slotIndex,
        SkillEvolutionId evolution,
        float dashDuration)
    {
        if (IsUnavailable || slotIndex >= SkillCount || skills[slotIndex] == null)
            return;

        presentationSkillIndex = slotIndex;
        presentationEvolution = evolution;
        animationView?.PlaySkillAnimation(slotIndex + 1, false, dashDuration);
    }

    [ClientRpc]
    private void RpcPresentSkillImpact(byte slotIndex, Vector3 origin, Vector3 aimDirection)
    {
        if (IsUnavailable || slotIndex >= SkillCount || skills[slotIndex] == null)
            return;

        SkillDefinitionSO definition = skills[slotIndex];
        SkillEvolutionId evolution = GetEvolution(slotIndex);

        switch (definition.shapeType)
        {
            case SkillShapeType.SectorSlash:
                float angle = evolution == SkillEvolutionId.Evolution3 ? 360f : definition.sectorAngle;
                SkillRangeVisual.ShowSector(origin, aimDirection, definition.sectorRange, angle, sectorVisualColor);
                break;

            case SkillShapeType.LineSlam:
                float length = evolution == SkillEvolutionId.Evolution2
                    ? definition.evoWideLineLength
                    : evolution == SkillEvolutionId.Evolution3
                        ? definition.evoNarrowLineLength
                        : definition.lineLength;
                float width = evolution == SkillEvolutionId.Evolution2
                    ? definition.evoWideLineWidth
                    : evolution == SkillEvolutionId.Evolution3
                        ? definition.evoNarrowLineWidth
                        : definition.lineWidth;
                SkillRangeVisual.ShowLine(origin, aimDirection, length, width, lineVisualColor);
                break;

            case SkillShapeType.Dash:
                SkillRangeVisual.ShowLine(origin, aimDirection, definition.dashDistance, 0.6f, dashVisualColor);
                break;
        }
    }

    public void EndPendingSkillAnimation()
    {
        if (presentationSkillIndex < 0)
            return;

        SkillDefinitionSO definition = GetSkillDefinition(presentationSkillIndex);
        presentationSkillIndex = -1;
        presentationEvolution = SkillEvolutionId.None;

        if (IsUnavailable || !isLocalPlayer || definition?.shapeType == SkillShapeType.Dash)
            return;

        if (stateMachine != null && stateMachine.Is(PlayerState.Skill))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    public void PlayPendingSkillEffect(int partValue)
    {
        if (IsUnavailable || presentationSkillIndex < 0 ||
            presentationSkillIndex >= SkillCount ||
            !System.Enum.IsDefined(typeof(SkillEffectPart), partValue))
        {
            return;
        }

        SkillDefinitionSO definition = skills[presentationSkillIndex];
        if (definition == null || playerEffect == null)
            return;

        WBH_PlayerEffectCue cue = PlayerEffectCueUtility.CreateFighterSkillCue(
            presentationSkillIndex + 1,
            presentationEvolution,
            (SkillEffectPart)partValue);
        playerEffect.PlayEffect(cue, CalculatePendingEffectScale(definition));
    }

    private Vector3 CalculatePendingEffectScale(SkillDefinitionSO definition)
    {
        float baseRange = definition.shapeType switch
        {
            SkillShapeType.SectorSlash => definition.sectorRange,
            SkillShapeType.LineSlam => presentationEvolution switch
            {
                SkillEvolutionId.Evolution2 => definition.evoWideLineLength,
                SkillEvolutionId.Evolution3 => definition.evoNarrowLineLength,
                _ => definition.lineLength,
            },
            SkillShapeType.Dash => definition.dashDistance,
            _ => 0f,
        };

        if (baseRange <= Mathf.Epsilon)
            return Vector3.one;

        float flatBonus = 0f;
        float percentBonus = 0f;
        if (context?.Stats != null)
            context.Stats.GetSkillRangeBonus(out flatBonus, out percentBonus);

        float rangeScale = (baseRange + flatBonus) * (1f + percentBonus / 100f) / baseRange;
        return Vector3.one * rangeScale;
    }

    [TargetRpc]
    private void TargetRejectSkill(NetworkConnectionToClient target, uint requestId, byte slotIndex)
    {
        if (slotIndex < predictedReadyAt.Length && requestId == nextLocalRequestId)
            predictedReadyAt[slotIndex] = 0d;

        if (requestId == activeLocalRequestId)
            ClearLocalPendingSkill(restoreIdle: true);
    }

    [Server]
    private void Reject(uint requestId, byte slotIndex, MirrorSkillRequestResult result)
    {
        rejectedSkillCount++;
        lastResult = result;
        TargetRejectSkill(connectionToClient, requestId, slotIndex);
    }

    private IEnumerator ExecuteLocalDash(SkillDefinitionSO definition, Vector3 direction)
    {
        if (IsUnavailable || !isLocalPlayer) yield break;
        NavMeshAgent agent = controller != null ? controller.agent : null;
        if (agent == null || !agent.enabled)
        {
            stateMachine?.ChangeState(PlayerState.Idle);
            presentationSkillIndex = -1;
            presentationEvolution = SkillEvolutionId.None;
            yield break;
        }

        Vector3 start = transform.position;
        Vector3 target = start + direction * definition.dashDistance;
        if (agent.isOnNavMesh && NavMesh.Raycast(start, target, out NavMeshHit hit, agent.areaMask))
            target = hit.position;

        float duration = Mathf.Max(0.01f, definition.dashDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsUnavailable || !isLocalPlayer || !agent.enabled || !agent.isOnNavMesh)
            {
                localSkillRoutine = null;
                yield break;
            }
            elapsed += Time.deltaTime;
            Vector3 next = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            agent.Move(next - transform.position);
            yield return null;
        }

        if (IsUnavailable || !isLocalPlayer)
        {
            localSkillRoutine = null;
            yield break;
        }
        if (agent.isOnNavMesh)
            agent.Warp(target);

        stateMachine?.ChangeState(PlayerState.Idle);
        presentationSkillIndex = -1;
        presentationEvolution = SkillEvolutionId.None;
        localSkillRoutine = null;
    }

    private void ClearLocalPendingSkill(bool restoreIdle)
    {
        activeLocalRequestId = 0;
        activeLocalSlot = byte.MaxValue;
        activeLocalAim = Vector3.zero;
        localAnimationExpiresAt = 0d;
        presentationSkillIndex = -1;
        presentationEvolution = SkillEvolutionId.None;

        if (localSkillRoutine != null)
        {
            StopCoroutine(localSkillRoutine);
            localSkillRoutine = null;
        }

        if (restoreIdle && stateMachine != null && stateMachine.Is(PlayerState.Skill))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    [Server]
    private void ClearServerPendingSkill()
    {
        pendingServerRequestId = 0;
        pendingServerSlot = byte.MaxValue;
        pendingServerAim = Vector3.zero;
        serverAnimationExpiresAt = 0d;
    }

    private bool CanUseLocalSkill()
    {
        return stateMachine != null && !stateMachine.IsAnyState(
            PlayerState.Hit,
            PlayerState.Attack,
            PlayerState.Skill,
            PlayerState.Dodge,
            PlayerState.Dead,
            PlayerState.Revive);
    }

    private Vector3 GetCursorDirection()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return transform.forward;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new(Vector3.up, transform.position);
        if (!groundPlane.Raycast(ray, out float distance))
            return transform.forward;

        Vector3 direction = ray.GetPoint(distance) - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
    }

    private static WBH_ICombat FindCombatTarget(Collider hit)
    {
        if (hit == null)
            return null;

        MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is WBH_ICombat target)
                return target;
        }

        return null;
    }

    private static Vector3 GetTargetPosition(WBH_ICombat target)
    {
        return target is Component component ? component.transform.position : Vector3.zero;
    }

    private double GetServerReadyAt(int index)
    {
        return index switch
        {
            0 => skill0ReadyAt,
            1 => skill1ReadyAt,
            2 => skill2ReadyAt,
            _ => 0d,
        };
    }

    [Server]
    private void SetServerReadyAt(int index, double value)
    {
        switch (index)
        {
            case 0:
                skill0ReadyAt = value;
                break;
            case 1:
                skill1ReadyAt = value;
                break;
            case 2:
                skill2ReadyAt = value;
                break;
        }
    }

    private void UnbindLocalInput()
    {
        if (inputHandler == null)
            return;

        inputHandler.OnSkillKeyPressed -= HandleSkillPressed;
        inputHandler.OnSkillKeyReleased -= HandleSkillReleased;
    }

    private void ResolveReferences()
    {
        context ??= GetComponent<PlayerContext>();
        inputHandler ??= GetComponent<PlayerActionInputHandler_MirrorTest>();
        combat ??= GetComponent<T_PlayerCombat>();
        controller ??= GetComponent<T_PlayerController>();
        stateMachine ??= GetComponent<WBH_PlayerStateMachine>();
        status ??= GetComponent<WBH_PlayerStatus>();
        buffManager ??= GetComponent<PlayerBuffManager>();
        animationView ??= GetComponent<WBH_PlayerAnimation_MirrorTest>();
        playerEffect ??= GetComponent<WBH_PlayerEffect>();
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
