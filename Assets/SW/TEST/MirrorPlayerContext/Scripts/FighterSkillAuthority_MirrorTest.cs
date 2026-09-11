using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public enum MirrorSkillRequestResult : byte
{
    None, Accepted, Hit, NoTarget, Dead, InvalidSlot, InvalidAim, DuplicateRequest,
    SkillOnCooldown, SkillAlreadyPending, AnimationImpactMissing, UnsupportedCharacter,
    InsufficientMana, Interrupted, InvalidSelection,
}

/// <summary>
/// 소유자의 스킬 입력을 서버의 원본 Fighter/Gunner 컨트롤러에 전달한다.
/// 마나·쿨다운·스택·차징·피해는 원본이 처리하고, UI와 애니메이션은 확정된 상태를 표시한다.
/// 기존 프리팹 참조를 유지하기 위해 클래스 이름은 그대로 사용한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerContext), typeof(PlayerActionInputHandler_MirrorTest))]
public sealed class FighterSkillAuthority_MirrorTest : NetworkBehaviour, ISkillController
{
    public struct SkillState
    {
        public SkillEvolutionId evolution;
        public SkillEnhancementId enhancement;
        public double readyAt;
        public float cooldown;
        public int stacks;
        public int maxStacks;
    }

    private const int SkillSlotCount = 3;
    private const float MaxAimDistance = 1000f;
    private static readonly SkillEffectPart[] EffectParts = (SkillEffectPart[])Enum.GetValues(typeof(SkillEffectPart));

    [SerializeField] private PlayerContext context;
    [SerializeField] private PlayerActionInputHandler_MirrorTest inputHandler;
    [SerializeField] private T_PlayerCombat combat;
    [SerializeField] private T_PlayerController controller;
    [SerializeField] private WBH_PlayerStateMachine stateMachine;
    [SerializeField] private WBH_PlayerStatus status;
    [SerializeField] private WBH_PlayerAnimation_MirrorTest animationView;
    [SerializeField] private WBH_PlayerEffect playerEffect;
    [SerializeField] private FighterSkillController fighterSkills;
    [SerializeField] private GunnerSkillController gunnerSkills;
    [SerializeField] private SkillDefinitionSO[] skills = new SkillDefinitionSO[SkillSlotCount];
    [SerializeField] private SkillEvolutionId[] activeEvolutions = new SkillEvolutionId[SkillSlotCount];

    private readonly SyncList<SkillState> skillStates = new();
    [SyncVar] private MirrorSkillRequestResult lastResult;
    [SyncVar] private byte lastSkillIndex = byte.MaxValue;
    [SyncVar] private int lastHitCount;
    [SyncVar] private int lastStatusEffectCount;
    [SyncVar] private uint acceptedSkillCount;
    [SyncVar] private uint rejectedSkillCount;

    private MirrorSpawnedPlayerBinder binder;
    private PlayerNetworkTransform_MirrorTest networkTransform;
    private NetworkAnimator networkAnimator;
    private bool savedAnimatorAuthority;
    private bool savedAnimatorEnabled;
    private bool originalEventsBound;
    private bool localRequestPending;
    private bool ownerInputBlocked;
    private uint nextLocalRequestId;
    [SyncVar] private uint lastServerRequestId;
    private uint pendingServerRequestId;
    private int pendingServerSlot = -1;
    private bool serverCharging;
    private bool serverMotionLocked;
    private uint motionAckRequestId;
    private float serverExpiresAt;
    private float nextSnapshotAt;
    private float nextMotionAt;
    private readonly HashSet<(int clip, float time, string function)> executedEvents = new();
    private int presentationSkillIndex = -1;
    private SkillEvolutionId presentationEvolution;
    private Vector3[] presentationScales;

    public event Action SkillStateChanged;
    public int SkillCount => Mathf.Min(SkillSlotCount, skills?.Length ?? 0);
    public MirrorSkillRequestResult LastResult => lastResult;
    public int LastSkillIndex => lastSkillIndex == byte.MaxValue ? -1 : lastSkillIndex;
    public int LastHitCount => lastHitCount;
    public int LastStatusEffectCount => lastStatusEffectCount;
    public uint AcceptedSkillCount => acceptedSkillCount;
    public uint RejectedSkillCount => rejectedSkillCount;
    public bool ServerMotionLocked => isServer && serverMotionLocked;
    private ISkillController Original => fighterSkills != null ? fighterSkills : gunnerSkills;
    private bool IsUnavailable => status == null || status.IsDead || context?.RuntimeState?.IsDead == true ||
        binder?.IsTemporarilyAbsent == true ||
        (NetworkManager.singleton as MirrorTestNetworkManager)?.CurrentSessionRoute is not (MirrorSessionRoute.Combat or MirrorSessionRoute.Camp);

    private void Awake() => ResolveReferences();

    public override void OnStartServer()
    {
        base.OnStartServer();
        lastServerRequestId = 0;
        pendingServerRequestId = 0;
        motionAckRequestId = 0;
        UnlockServerMotion();
        ResolveReferences();
        if (Original == null)
        {
            Debug.LogError("[MirrorSkill] 원본 스킬 컨트롤러가 연결되지 않았습니다.", this);
            return;
        }
        if (!originalEventsBound)
        {
            if (fighterSkills != null)
            {
                fighterSkills.OnSkillAniRequested += HandleFighterAnimation;
                fighterSkills.OnChargeAniChanged += HandleChargeAnimation;
            }
            if (gunnerSkills != null) gunnerSkills.OnSkillAniRequested += HandleGunnerAnimation;
            originalEventsBound = true;
        }
        if (fighterSkills != null) fighterSkills.enabled = true;
        if (gunnerSkills != null) gunnerSkills.enabled = true;
        PublishSkillStates();
    }

    public override void OnStopServer()
    {
        ServerCancelForDisconnect();
        UnbindOriginalEvents();
        if (fighterSkills != null) fighterSkills.enabled = false;
        if (gunnerSkills != null) gunnerSkills.enabled = false;
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isServer)
        {
            if (fighterSkills != null) fighterSkills.enabled = false;
            if (gunnerSkills != null) gunnerSkills.enabled = false;
        }
        skillStates.OnChange += OnSkillStateChanged;
        SkillStateChanged?.Invoke();
    }

    public override void OnStopClient()
    {
        skillStates.OnChange -= OnSkillStateChanged;
        base.OnStopClient();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        localRequestPending = false;
        if (nextLocalRequestId <= lastServerRequestId)
            nextLocalRequestId = lastServerRequestId;
        UnbindLocalInput();
        inputHandler.OnSkillKeyPressed += HandleSkillPressed;
        inputHandler.OnSkillKeyReleased += HandleSkillReleased;
    }

    public override void OnStopLocalPlayer()
    {
        UnbindLocalInput();
        SetOwnerInputBlocked(false);
        localRequestPending = false;
        base.OnStopLocalPlayer();
    }

    private void OnDisable()
    {
        UnbindLocalInput();
        if (isServer && NetworkServer.active) ServerCancelForDisconnect();
        SetOwnerInputBlocked(false);
    }

    private void OnDestroy() => UnbindOriginalEvents();

    private void LateUpdate()
    {
        if (!isServer || Original == null) return;
        if (pendingServerRequestId != 0)
        {
            if (IsUnavailable || !stateMachine.Is(PlayerState.Skill))
                FinishServerSkill(IsUnavailable || stateMachine.IsAnyState(PlayerState.Hit, PlayerState.Dead, PlayerState.Revive) || executedEvents.Count == 0);
            else if (Time.time >= serverExpiresAt)
            {
                lastResult = MirrorSkillRequestResult.AnimationImpactMissing;
                FinishServerSkill(true);
            }
            else if (connectionToClient != null && Time.unscaledTime >= nextMotionAt)
            {
                nextMotionAt = Time.unscaledTime + 0.05f;
                TargetApplySkillPose(connectionToClient, pendingServerRequestId, transform.position, transform.rotation);
            }
        }
        if (Time.unscaledTime >= nextSnapshotAt)
        {
            nextSnapshotAt = Time.unscaledTime + 0.2f;
            PublishSkillStates();
        }
    }

    public SkillDefinitionSO GetSkillDefinition(int index) => index >= 0 && index < SkillCount ? skills[index] : null;
    public SkillEvolutionId GetEvolution(int index) => isServer && Original != null ? Original.GetEvolution(index) :
        index >= 0 && index < skillStates.Count ? skillStates[index].evolution :
        index >= 0 && index < (activeEvolutions?.Length ?? 0) ? activeEvolutions[index] : SkillEvolutionId.None;
    public SkillEnhancementId GetEnhancement(int index) => isServer && Original != null ? Original.GetEnhancement(index) :
        index >= 0 && index < skillStates.Count ? skillStates[index].enhancement : SkillEnhancementId.None;
    public float GetRemainingCooldown(int index) => isServer && Original != null ? Original.GetRemainingCooldown(index) :
        index >= 0 && index < skillStates.Count ? Mathf.Max(0f, (float)(skillStates[index].readyAt - NetworkTime.time)) : 0f;
    public float GetEffectiveCooldown(int index) => isServer && Original != null ? Original.GetEffectiveCooldown(index) :
        index >= 0 && index < skillStates.Count ? skillStates[index].cooldown : 0f;

    public bool TryGetStackInfo(int index, out int current, out int max)
    {
        if (isServer && Original != null) return Original.TryGetStackInfo(index, out current, out max);
        current = max = 0;
        if (index < 0 || index >= skillStates.Count || skillStates[index].maxStacks <= 0) return false;
        current = skillStates[index].stacks;
        max = skillStates[index].maxStacks;
        return true;
    }

    public void SetEvolution(int index, SkillEvolutionId evolution)
    {
        if (CanSendLocalRequest() && index >= 0 && index < SkillCount && Enum.IsDefined(typeof(SkillEvolutionId), evolution))
            CmdSetSelection((byte)index, evolution, GetEnhancement(index));
    }

    public void SetEnhancement(int index, SkillEnhancementId enhancement)
    {
        if (CanSendLocalRequest() && index >= 0 && index < SkillCount && Enum.IsDefined(typeof(SkillEnhancementId), enhancement))
            CmdSetSelection((byte)index, GetEvolution(index), enhancement);
    }

    [Command]
    private void CmdSetSelection(byte index, SkillEvolutionId evolution, SkillEnhancementId enhancement)
    {
        if (Original == null || IsUnavailable || index >= SkillCount || pendingServerRequestId != 0 || serverMotionLocked ||
            !CanBeginSkillState() || !Enum.IsDefined(typeof(SkillEvolutionId), evolution) ||
            !Enum.IsDefined(typeof(SkillEnhancementId), enhancement))
        {
            lastResult = MirrorSkillRequestResult.InvalidSelection;
            rejectedSkillCount++;
            return;
        }
        Original.SetEvolution(index, evolution);
        Original.SetEnhancement(index, enhancement);
        PublishSkillStates();
    }

    public bool TryUseLocalSkill(int index)
    {
        Vector3 target = GetCursorPosition();
        return TryUseLocalSkill(index, target - transform.position, target);
    }

    /// <summary>명시적 조준도 실제 소유자의 Command 경로를 거친다.</summary>
    public bool TryUseLocalSkill(int index, Vector3 aimDirection) =>
        TryUseLocalSkill(index, aimDirection, transform.position + aimDirection);

    public bool TryUseLocalSkill(int index, Vector3 aimDirection, Vector3 targetPosition)
    {
        if (!CanSendLocalRequest() || localRequestPending || ownerInputBlocked || SkillPopupController.IsOpen ||
            !CanBeginSkillState() || index < 0 || index >= SkillCount || skills[index] == null ||
            context?.RuntimeState?.HasSnapshot != true || !TryValidateAim(ref aimDirection, targetPosition)) return false;
        // 충전 타이머가 남아 있어도 잔여 스택으로 사용할 수 있다. 최종 연사 제한은 원본 서버가 검사한다.
        if (TryGetStackInfo(index, out int stacks, out _)) { if (stacks <= 0) return false; }
        else if (GetRemainingCooldown(index) > 0f) return false;
        localRequestPending = true;
        if (nextLocalRequestId <= lastServerRequestId)
            nextLocalRequestId = lastServerRequestId;
        nextLocalRequestId++;
        if (nextLocalRequestId == 0) nextLocalRequestId++;
        CmdRequestSkill(nextLocalRequestId, (byte)index, aimDirection, targetPosition);
        return true;
    }

    /// <summary>소유자의 차징 슬롯을 해제한다. 최대 차징 도달 시 자동 해제는 원본 서버가 처리한다.</summary>
    public void ReleaseLocalSkill(int index)
    {
        if (isLocalPlayer && NetworkClient.active && NetworkClient.ready && index >= 0 && index < SkillCount)
            CmdReleaseSkill(nextLocalRequestId, (byte)index);
    }

    private void HandleSkillPressed(int index) => TryUseLocalSkill(index);
    private void HandleSkillReleased(int index) => ReleaseLocalSkill(index);

    [Command]
    private void CmdRequestSkill(uint requestId, byte index, Vector3 aimDirection, Vector3 targetPosition)
    {
        if (requestId == 0 || requestId <= lastServerRequestId) { Reject(requestId, MirrorSkillRequestResult.DuplicateRequest); return; }
        lastServerRequestId = requestId;
        if (Original == null) { Reject(requestId, MirrorSkillRequestResult.UnsupportedCharacter); return; }
        if (IsUnavailable) { Reject(requestId, MirrorSkillRequestResult.Dead); return; }
        if (index >= SkillCount || skills[index] == null) { Reject(requestId, MirrorSkillRequestResult.InvalidSlot); return; }
        if (!TryValidateAim(ref aimDirection, targetPosition)) { Reject(requestId, MirrorSkillRequestResult.InvalidAim); return; }
        if (pendingServerRequestId != 0 || serverMotionLocked || context.CombatAuthority?.ServerAttackPending == true || !CanBeginSkillState())
        { Reject(requestId, MirrorSkillRequestResult.SkillAlreadyPending); return; }

        pendingServerRequestId = requestId;
        pendingServerSlot = index;
        savedAnimatorAuthority = networkAnimator.clientAuthority;
        savedAnimatorEnabled = networkAnimator.enabled;
        // 기본 이동·공격은 기존 소유자 Animator 경로를 쓰되, 스킬 중에는 서버 클립을 덮어쓰지 못하게 한다.
        networkAnimator.clientAuthority = false;
        networkAnimator.enabled = false;
        serverMotionLocked = true;
        executedEvents.Clear();
        lastHitCount = lastStatusEffectCount = 0;
        serverCharging = fighterSkills != null && skills[index].shapeType == SkillShapeType.SectorSlash &&
            Original.GetEvolution(index) == SkillEvolutionId.Evolution3;
        serverExpiresAt = Time.time + 10f + 8f / Mathf.Max(0.1f, status.AttackSpeed) +
            (serverCharging ? Mathf.Max(0f, skills[index].evoChargeMaxSeconds) : 0f);
        bool accepted = fighterSkills != null
            ? serverCharging ? fighterSkills.TryStartCharge(index, aimDirection, context.Stats)
                : fighterSkills.TryUseSkill(index, aimDirection, context.Stats)
            : gunnerSkills.TryUseSkill(index, aimDirection, targetPosition, context.Stats);
        if (!accepted)
        {
            pendingServerRequestId = 0;
            pendingServerSlot = -1;
            serverCharging = false;
            UnlockServerMotion();
            Reject(requestId, status.CurrentMp < skills[index].GetManaCost(Original.GetEvolution(index))
                ? MirrorSkillRequestResult.InsufficientMana : MirrorSkillRequestResult.SkillOnCooldown);
            return;
        }
        lastSkillIndex = index;
        lastResult = MirrorSkillRequestResult.Accepted;
        acceptedSkillCount++;
        PublishSkillStates();
    }

    [Command]
    private void CmdReleaseSkill(uint requestId, byte index)
    {
        if (IsUnavailable || fighterSkills == null || !serverCharging || requestId != pendingServerRequestId ||
            index != pendingServerSlot) return;
        if (!fighterSkills.TryReleaseCharge(index)) FinishServerSkill(true);
        PublishSkillStates();
    }

    private void HandleFighterAnimation(int skillId, bool charging, float duration) => BeginServerPresentation(skillId, charging, duration);
    private void HandleGunnerAnimation(int skillId, int evolution, float duration) => BeginServerPresentation(skillId, false, duration);

    private void BeginServerPresentation(int skillId, bool charging, float duration)
    {
        if (!isServer || pendingServerRequestId == 0) return;
        var scales = new Vector3[10];
        foreach (SkillEffectPart part in EffectParts)
            scales[(int)part] = fighterSkills != null ? fighterSkills.GetPendingSkillEffectScale((int)part) : gunnerSkills.GetPendingSkillEffectScale((int)part);
        var evolution = Original.GetEvolution(skillId - 1);
        BeginPresentation(skillId - 1, evolution, charging, duration, transform.forward, scales);
        RpcBeginPresentation(pendingServerRequestId, (byte)(skillId - 1), evolution, charging, duration, transform.forward, scales);
    }

    [ClientRpc]
    private void RpcBeginPresentation(uint requestId, byte index, SkillEvolutionId evolution, bool charging, float duration, Vector3 aim, Vector3[] scales)
    {
        if (isServer) return;
        if (isLocalPlayer && requestId == nextLocalRequestId) localRequestPending = false;
        BeginPresentation(index, evolution, charging, duration, aim, scales);
    }

    private void BeginPresentation(int index, SkillEvolutionId evolution, bool charging, float duration, Vector3 aim, Vector3[] scales)
    {
        presentationSkillIndex = index;
        presentationEvolution = evolution;
        presentationScales = scales;
        if (isLocalPlayer)
        {
            localRequestPending = false;
            SetOwnerInputBlocked(true);
            stateMachine.ChangeState(PlayerState.Skill);
        }
        transform.forward = aim;
        if (fighterSkills != null)
        {
            if (charging && scales?.Length > 0) playerEffect?.SetChargeEnhancementScale(scales[0]);
            animationView.PlaySkillAnimation(index + 1, charging, duration);
        }
        else animationView.PlayGunnerSkillAnimation(index + 1, (int)evolution, duration);
    }

    private void HandleChargeAnimation(bool charging)
    {
        if (!isServer || pendingServerRequestId == 0) return;
        serverCharging = charging;
        animationView.SetChargingAnimation(charging);
        RpcChargeAnimation(charging);
        PublishSkillStates();
    }

    [ClientRpc]
    private void RpcChargeAnimation(bool charging)
    {
        if (!isServer) animationView.SetChargingAnimation(charging);
    }

    /// <summary>서버 Animator가 재생한 클립의 각 이벤트를 한 번씩 실행하고 서로 다른 다단히트는 보존한다.</summary>
    [Server]
    public void ServerExecuteSkillFromAnimation(AnimationEvent animationEvent)
    {
        if (!ClaimAnimationEvent(animationEvent)) return;
        if (fighterSkills != null) fighterSkills.ExecutePendingSkill();
        else gunnerSkills.ExecutePendingSkill();
        if (lastHitCount == 0) lastResult = MirrorSkillRequestResult.NoTarget;
    }

    [Server]
    public void ServerExecuteBackstepFromAnimation(AnimationEvent animationEvent)
    {
        if (gunnerSkills == null || GetSkillDefinition(pendingServerSlot)?.shapeType != SkillShapeType.BackstepShot ||
            !ClaimAnimationEvent(animationEvent)) return;
        gunnerSkills.ExecutePendingBackstepMove();
    }

    private bool ClaimAnimationEvent(AnimationEvent animationEvent)
    {
        if (pendingServerRequestId == 0 || serverCharging || IsUnavailable || !stateMachine.Is(PlayerState.Skill) ||
            animationEvent == null || animationEvent.animatorClipInfo.clip == null) return false;
        return executedEvents.Add((animationEvent.animatorClipInfo.clip.GetInstanceID(), animationEvent.time, animationEvent.functionName));
    }

    /// <summary>클라이언트 AnimationEvent는 피해를 승인하지 않는다.</summary>
    public bool TryConfirmLocalSkillImpactFromAnimation() => false;

    [Server]
    public void EndPendingSkillAnimation()
    {
        if (pendingServerRequestId == 0 || serverCharging) return;
        if (fighterSkills != null) fighterSkills.EndPendingSkillAni();
        else gunnerSkills?.EndPendingSkillAni();
        if (!stateMachine.Is(PlayerState.Skill)) FinishServerSkill(false);
    }

    /// <summary>원본에서 계산한 배율로 캐릭터에 붙는 스킬 이펙트를 표시한다.</summary>
    public void PlayPendingSkillEffect(int partValue)
    {
        if (!isClient || IsUnavailable || presentationSkillIndex < 0 || playerEffect == null ||
            !Enum.IsDefined(typeof(SkillEffectPart), partValue) || presentationScales == null || partValue >= presentationScales.Length) return;
        var cue = fighterSkills != null
            ? PlayerEffectCueUtility.CreateFighterSkillCue(presentationSkillIndex + 1, presentationEvolution, (SkillEffectPart)partValue)
            : PlayerEffectCueUtility.CreateGunnerSkillCue(presentationSkillIndex + 1, presentationEvolution, (SkillEffectPart)partValue);
        playerEffect.PlayEffect(cue, presentationScales[partValue]);
    }

    [Server]
    public void ServerRecordSkillHit(WBH_DamageResult result)
    {
        if (pendingServerRequestId == 0 || serverCharging) return;
        lastHitCount++;
        if (result.StatusEffect.HasValue) lastStatusEffectCount++;
        lastResult = MirrorSkillRequestResult.Hit;
    }

    [Server]
    private void FinishServerSkill(bool cancelled)
    {
        uint requestId = pendingServerRequestId;
        if (requestId == 0) return;
        pendingServerRequestId = 0;
        pendingServerSlot = -1;
        serverCharging = false;
        if (cancelled)
        {
            if (fighterSkills != null) fighterSkills.CancelActiveSkill();
            else gunnerSkills?.CancelActiveSkill();
            if (lastResult != MirrorSkillRequestResult.AnimationImpactMissing) lastResult = MirrorSkillRequestResult.Interrupted;
        }
        motionAckRequestId = !IsUnavailable && connectionToClient != null ? requestId : 0;
        if (motionAckRequestId == 0) UnlockServerMotion();
        if (isClient) FinishPresentation(requestId, cancelled, transform.position, transform.rotation);
        else if (cancelled) animationView.CancelSkillAnimation();
        RpcFinishPresentation(requestId, cancelled, transform.position, transform.rotation);
        PublishSkillStates();
    }

    [ClientRpc]
    private void RpcFinishPresentation(uint requestId, bool cancelled, Vector3 position, Quaternion rotation)
    {
        if (!isServer) FinishPresentation(requestId, cancelled, position, rotation);
    }

    private void FinishPresentation(uint requestId, bool cancelled, Vector3 position, Quaternion rotation)
    {
        if (isLocalPlayer)
        {
            ApplyOwnerPose(position, rotation);
            SetOwnerInputBlocked(false);
            localRequestPending = false;
            if (stateMachine.Is(PlayerState.Skill)) stateMachine.ChangeState(PlayerState.Idle);
            CmdAcknowledgeSkillPose(requestId);
        }
        if (cancelled) animationView.CancelSkillAnimation();
        presentationSkillIndex = -1;
        presentationScales = null;
    }

    [TargetRpc]
    private void TargetApplySkillPose(NetworkConnectionToClient target, uint requestId, Vector3 position, Quaternion rotation)
    {
        if (requestId == nextLocalRequestId && ownerInputBlocked) ApplyOwnerPose(position, rotation);
    }

    private void ApplyOwnerPose(Vector3 position, Quaternion rotation)
    {
        if (isServer) return;
        var agent = controller.agent;
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(position);
        else transform.position = position;
        transform.rotation = rotation;
    }

    [Command]
    private void CmdAcknowledgeSkillPose(uint requestId)
    {
        if (motionAckRequestId != 0 && motionAckRequestId == requestId && pendingServerRequestId == 0)
        {
            motionAckRequestId = 0;
            UnlockServerMotion();
        }
    }

    private void UnlockServerMotion()
    {
        networkTransform?.serverSnapshots.Clear();
        if (serverMotionLocked && networkAnimator != null)
        {
            networkAnimator.clientAuthority = savedAnimatorAuthority;
            networkAnimator.enabled = savedAnimatorEnabled;
        }
        serverMotionLocked = false;
    }

    /// <summary>재접속 예약이나 씬 이동에서 미완료 행동을 취소하고 이미 소모한 자원은 보존한다.</summary>
    [Server]
    public void ServerCancelForDisconnect()
    {
        if (pendingServerRequestId != 0) FinishServerSkill(true);
        motionAckRequestId = 0;
        UnlockServerMotion();
        lastServerRequestId = 0;
        if (fighterSkills != null) fighterSkills.CancelActiveSkill();
        else gunnerSkills?.CancelActiveSkill();
    }

    [Server]
    private void Reject(uint requestId, MirrorSkillRequestResult result)
    {
        rejectedSkillCount++;
        lastResult = result;
        if (connectionToClient != null) TargetRejectSkill(connectionToClient, requestId);
        PublishSkillStates();
    }

    [TargetRpc]
    private void TargetRejectSkill(NetworkConnectionToClient target, uint requestId)
    {
        if (requestId == nextLocalRequestId) localRequestPending = false;
    }

    [Server]
    private void PublishSkillStates()
    {
        if (Original == null) return;
        for (int i = 0; i < SkillCount; i++)
        {
            Original.TryGetStackInfo(i, out int current, out int max);
            float remaining = Original.GetRemainingCooldown(i);
            var snapshot = new SkillState
            {
                evolution = Original.GetEvolution(i), enhancement = Original.GetEnhancement(i),
                readyAt = remaining > 0f ? NetworkTime.time + remaining : 0d,
                cooldown = Original.GetEffectiveCooldown(i), stacks = current, maxStacks = max,
            };
            if (skillStates.Count <= i) skillStates.Add(snapshot);
            else
            {
                var old = skillStates[i];
                if (old.evolution != snapshot.evolution || old.enhancement != snapshot.enhancement ||
                    old.stacks != current || old.maxStacks != max || !Mathf.Approximately(old.cooldown, snapshot.cooldown) ||
                    Math.Abs(old.readyAt - snapshot.readyAt) > 0.05d) skillStates[i] = snapshot;
            }
        }
    }

    private void OnSkillStateChanged(SyncList<SkillState>.Operation operation, int index, SkillState value) => SkillStateChanged?.Invoke();
    private bool CanSendLocalRequest() => isLocalPlayer && NetworkClient.active && NetworkClient.ready && !IsUnavailable;
    private bool CanBeginSkillState() => stateMachine != null && !stateMachine.IsAnyState(
        PlayerState.Hit, PlayerState.Attack, PlayerState.Skill, PlayerState.Dodge, PlayerState.Dead, PlayerState.Revive);

    private bool TryValidateAim(ref Vector3 aim, Vector3 target)
    {
        if (!IsFinite(aim) || !IsFinite(target) || !float.IsFinite(aim.sqrMagnitude) ||
            aim.sqrMagnitude > MaxAimDistance * MaxAimDistance || !float.IsFinite((target - transform.position).sqrMagnitude) ||
            (target - transform.position).sqrMagnitude > MaxAimDistance * MaxAimDistance) return false;
        aim.y = 0f;
        if (aim.sqrMagnitude < 0.0001f) return false;
        aim.Normalize();
        return true;
    }

    private Vector3 GetCursorPosition()
    {
        if (Camera.main == null) return transform.position + transform.forward;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        return new Plane(Vector3.up, transform.position).Raycast(ray, out float distance)
            ? ray.GetPoint(distance) : transform.position + transform.forward;
    }

    private void SetOwnerInputBlocked(bool blocked)
    {
        if (!isLocalPlayer || ownerInputBlocked == blocked || controller == null) return;
        ownerInputBlocked = blocked;
        controller.SetCutSceneControlBlock(blocked);
    }

    private void UnbindLocalInput()
    {
        if (inputHandler == null) return;
        inputHandler.OnSkillKeyPressed -= HandleSkillPressed;
        inputHandler.OnSkillKeyReleased -= HandleSkillReleased;
    }

    private void UnbindOriginalEvents()
    {
        if (!originalEventsBound) return;
        if (fighterSkills != null)
        {
            fighterSkills.OnSkillAniRequested -= HandleFighterAnimation;
            fighterSkills.OnChargeAniChanged -= HandleChargeAnimation;
        }
        if (gunnerSkills != null) gunnerSkills.OnSkillAniRequested -= HandleGunnerAnimation;
        originalEventsBound = false;
    }

    private void ResolveReferences()
    {
        context ??= GetComponent<PlayerContext>();
        inputHandler ??= GetComponent<PlayerActionInputHandler_MirrorTest>();
        combat ??= GetComponent<T_PlayerCombat>();
        controller ??= GetComponent<T_PlayerController>();
        stateMachine ??= GetComponent<WBH_PlayerStateMachine>();
        status ??= GetComponent<WBH_PlayerStatus>();
        animationView ??= GetComponent<WBH_PlayerAnimation_MirrorTest>();
        playerEffect ??= GetComponent<WBH_PlayerEffect>();
        fighterSkills ??= GetComponent<FighterSkillController>();
        gunnerSkills ??= GetComponent<GunnerSkillController>();
        binder ??= GetComponent<MirrorSpawnedPlayerBinder>();
        networkTransform ??= GetComponent<PlayerNetworkTransform_MirrorTest>();
        networkAnimator ??= GetComponent<NetworkAnimator>();
    }

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
}
