using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// BH 원본 <c>WBH_PlayerAnimation</c>의 Mirror 검증용 복제본이다.
/// <para>원본: <c>Assets/WBHTest/Scripts/Player/WBH_PlayerAnimation.cs</c></para>
/// <para><c>MonoBehaviour</c> 대신 <c>NetworkBehaviour</c>를 사용하고, 로컬 플레이어만 상태·공격속도 이벤트와 NavMeshAgent 이동값을 읽는다.</para>
/// <para>Attack·Dodge·Hit·Dead Trigger는 <c>NetworkAnimator</c>로 전달하여 원격 화면에서도 같은 애니메이션을 재생한다.</para>
/// <para>4-A 차이: 공격 AnimationEvent는 로컬 데미지를 실행하지 않고 같은 공격 요청 번호가 실제 클립의
/// 타격 지점에 도달했다는 사실만 <c>PlayerCombatAuthority_MirrorTest</c>에 전달한다. 서버가 이를 확인한 뒤
/// Physics 판정과 피해를 한 번만 처리하므로 공격 모션이 재생되지 않은 클릭은 피해를 만들 수 없다.</para>
/// <para>5-A 보완: 씬 전역 EffectPool에 연결된 Spawner를 클라이언트에서 지연 탐색해 공격 연출만 재생한다.
/// 서버가 확정한 부활 번호를 받으면 사망 클립의 남은 시간과 관계없이 Locomotion으로 복구한다.</para>
/// <para>6-B 보완: 재접속 직후 사망 스냅샷이 상태 이벤트보다 먼저 적용돼도 각 화면의 Animator에
/// Dead Trigger를 한 번 직접 보장해 상태는 Dead인데 Idle로 서 있는 불일치를 막는다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(NetworkIdentity), typeof(NetworkAnimator))]
public sealed class WBH_PlayerAnimation_MirrorTest : NetworkBehaviour
{
    [Header("EffectRoot")]
    [SerializeField] private Transform fighterEffectRoot;
    [SerializeField] private Transform gunnerEffectRoot;

    [Header("Attack")]
    [SerializeField] private WBH_EffectData Eff_fighterAtk;
    [SerializeField] private WBH_EffectData Eff_gunnerShotgunAtk;
    [SerializeField] private AnimationClip fighterDash;
    [SerializeField] private AnimationClip gunnerBackstepMove;
    [SerializeField] private WBH_EffectSpawner effectSpawner;

    private Animator animator;
    private NetworkAnimator networkAnimator;
    private WBH_PlayerStateMachine stateMachine;
    private T_PlayerCombat combat;
    private PlayerCombatAuthority_MirrorTest combatAuthority;
    private FighterSkillAuthority_MirrorTest skillAuthority;
    private WBH_PlayerEffect playerEffect;
    private NavMeshAgent agent;
    private WBH_PlayerStatus status;
    private bool localEventsBound;
    private bool statEventsBound;

    private readonly int skillHash = Animator.StringToHash("Skill");
    private readonly int skillIdHash = Animator.StringToHash("SkillID");
    private readonly int isChargingHash = Animator.StringToHash("IsCharging");
    private readonly int skillSpeedHash = Animator.StringToHash("SkillSpeed");
    private readonly int evolutionHash = Animator.StringToHash("EvoNumber");
    private readonly int backstepSpeedHash = Animator.StringToHash("BackstepMoveSpeed");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        stateMachine = GetComponent<WBH_PlayerStateMachine>();
        combat = GetComponent<T_PlayerCombat>();
        combatAuthority = GetComponent<PlayerCombatAuthority_MirrorTest>();
        skillAuthority = GetComponent<FighterSkillAuthority_MirrorTest>();
        playerEffect = GetComponent<WBH_PlayerEffect>();
        agent = GetComponent<NavMeshAgent>();
        status = GetComponent<WBH_PlayerStatus>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        BindLocalEvents();
        SetAttackAnimationSpeed();
        UpdateMoveAnimation();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // 전용 서버도 원본 클립의 모든 타격 시점을 실행한다.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        BindStatEvents();
        SetAttackAnimationSpeed();
    }

    public override void OnStopServer()
    {
        if (!isLocalPlayer) UnbindStatEvents();
        base.OnStopServer();
    }

    public override void OnStopLocalPlayer()
    {
        UnbindLocalEvents();
        base.OnStopLocalPlayer();
    }

    private void OnDisable()
    {
        UnbindLocalEvents();
        UnbindStatEvents();
    }

    private void Update()
    {
        if (!isLocalPlayer)
            return;

        UpdateMoveAnimation();
    }

    private void BindLocalEvents()
    {
        if (localEventsBound)
            return;

        stateMachine.OnEnterState += HandleEnterState;
        BindStatEvents();
        localEventsBound = true;
    }

    private void UnbindLocalEvents()
    {
        if (!localEventsBound)
            return;

        stateMachine.OnEnterState -= HandleEnterState;
        if (!isServer) UnbindStatEvents();
        localEventsBound = false;
    }

    private void BindStatEvents()
    {
        if (statEventsBound || status == null) return;
        status.OnAtkSpeedChanged += HandleAttackSpeedChanged;
        statEventsBound = true;
    }

    private void UnbindStatEvents()
    {
        if (!statEventsBound) return;
        if (status != null) status.OnAtkSpeedChanged -= HandleAttackSpeedChanged;
        statEventsBound = false;
    }

    private void HandleEnterState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Attack:
                networkAnimator.SetTrigger("Attack");
                break;

            case PlayerState.Dodge:
                networkAnimator.SetTrigger("Dodge");
                break;

            case PlayerState.Hit:
                networkAnimator.SetTrigger("Hit");
                break;

            case PlayerState.Dead:
                networkAnimator.SetTrigger("Dead");
                break;

            // 정상 부활은 서버 스냅샷의 ApplyAuthoritativePassiveRevive에서 재생한다.
            // 재접속 때 NetworkAnimator가 복원한 재생 위치를 로컬 Trigger로 덮지 않는다.
        }
    }

    private void HandleAttackSpeedChanged(float _)
    {
        SetAttackAnimationSpeed();
    }

    private void SetAttackAnimationSpeed()
    {
        animator.SetFloat("AttackSpeed", status.AttackSpeed);
    }

    private void UpdateMoveAnimation()
    {
        if (stateMachine.IsAnyState(PlayerState.Dodge, PlayerState.Dead, PlayerState.Revive))
            return;

        float speed = agent.speed > 0f
            ? agent.velocity.magnitude / agent.speed
            : 0f;

        if (speed < 0.05f)
            speed = 0f;

        animator.SetFloat("MoveSpeed", speed);
    }

    public void PlaySkillAnimation(int skillId, bool isCharging, float targetDuration)
    {
        ResolveSceneEffectSpawner();
        playerEffect?.CancelPendingSfx();
        float skillSpeed = 1f;
        const int DashSkillId = 3;

        if (skillId == DashSkillId && fighterDash != null && targetDuration > 0f)
            skillSpeed = fighterDash.length / targetDuration;

        animator.SetFloat(skillSpeedHash, skillSpeed);
        animator.SetInteger(skillIdHash, skillId);
        animator.SetBool(isChargingHash, isCharging);
        animator.ResetTrigger(skillHash);
        animator.SetTrigger(skillHash);
    }

    public void SetChargingAnimation(bool isCharging)
    {
        animator.SetBool(isChargingHash, isCharging);
    }

    /// <summary>서버가 확정한 거너 스킬과 진화에 맞춰 기존 모션을 재생한다.</summary>
    public void PlayGunnerSkillAnimation(int skillId, int evolution, float duration)
    {
        ResolveSceneEffectSpawner();
        playerEffect?.CancelPendingSfx();
        animator.SetFloat(skillSpeedHash, 1f);
        animator.SetFloat(backstepSpeedHash, gunnerBackstepMove != null && duration > 0f
            ? gunnerBackstepMove.length / duration : 1f);
        animator.SetInteger(skillIdHash, skillId);
        animator.SetInteger(evolutionHash, evolution);
        animator.ResetTrigger(skillHash);
        animator.SetTrigger(skillHash);
    }

    /// <summary>취소된 스킬의 남은 이벤트와 표현을 정리한다. 사망·피격 모션은 유지한다.</summary>
    public void CancelSkillAnimation()
    {
        playerEffect?.CancelPendingSfx();
        playerEffect?.StopFighterChargeEffect();
        animator.ResetTrigger(skillHash);
        animator.SetBool(isChargingHash, false);
        if (stateMachine == null || !stateMachine.IsAnyState(PlayerState.Dead, PlayerState.Hit, PlayerState.Revive))
            animator.Play("Base Layer.Locomotion", 0, 0f);
    }

    public void AniEvent_ExecuteAttack()
    {
        if (isLocalPlayer)
            combatAuthority?.TryConfirmLocalAttackImpactFromAnimation();
    }

    public void AniEvent_EndAttack()
    {
        if (isLocalPlayer && stateMachine.Is(PlayerState.Attack))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    public void AniEvent_HitEnd()
    {
        if (isLocalPlayer && stateMachine.Is(PlayerState.Hit))
            stateMachine.ChangeState(PlayerState.Idle);
    }

    public void AniEvent_ExecuteSkill(AnimationEvent animationEvent)
    {
        if (isServer)
            skillAuthority?.ServerExecuteSkillFromAnimation(animationEvent);
    }

    public void AniEvent_ExecuteBackstepMove(AnimationEvent animationEvent)
    {
        if (isServer)
            skillAuthority?.ServerExecuteBackstepFromAnimation(animationEvent);
    }

    public void AniEvent_EndSkill()
    {
        if (isServer) skillAuthority?.EndPendingSkillAnimation();
    }

    public void AniEvent_EndDead()
    {
        // Mirror 사망/부활은 PlayerRuntimeStateSync_MirrorTest의 서버 스냅샷만 확정한다.
        if (isServer) GetComponent<PlayerRuntimeStateSync_MirrorTest>()?.ServerTryPassiveRevive();
    }

    public void AniEvent_EndRevive()
    {
        // 서버 부활 스냅샷이 입력 복구까지 함께 처리한다.
        if (isServer) GetComponent<PlayerRuntimeStateSync_MirrorTest>()?.ServerCompletePassiveRevive();
    }

    /// <summary>Fighter_Hit 클립에 남아 있는 기존 이벤트 이름을 테스트 복제본에서 호환한다.</summary>
    public void AniEvent_EndHit()
    {
        AniEvent_HitEnd();
    }

    public void AniEvent_PlayEffect(int cueValue)
    {
        if (ResolveSceneEffectSpawner() == null) return;
        WBH_PlayerEffectCue cue = (WBH_PlayerEffectCue)cueValue;

        // 원본의 최신 바인딩·재생 속도·앵커를 그대로 사용한다.
        playerEffect?.PlayEffect(cue, Vector3.one);
    }

    public void AniEvent_PlaySkillEffect(int partValue)
    {
        if (ResolveSceneEffectSpawner() == null) return;
        skillAuthority?.PlayPendingSkillEffect(partValue);
    }

    public void AniEvent_PlaySkillSfx(AnimationEvent animationEvent)
    {
        skillAuthority?.PlayPendingSkillSfx(animationEvent, animator);
    }

    public void AniEvent_PlayFighterChargeEffect()
    {
        if (ResolveSceneEffectSpawner() == null) return;
        playerEffect?.PlayFighterChargeEffect();
    }

    public void AniEvent_StopFighterChargeEffect()
    {
        playerEffect?.StopFighterChargeEffect();
    }

    public void AniEvent_FighterAttackEvent()
    {
        WBH_EffectSpawner spawner = ResolveSceneEffectSpawner();
        if (spawner != null && Eff_fighterAtk != null && fighterEffectRoot != null)
            spawner.SpawnEffect(Eff_fighterAtk, fighterEffectRoot);
    }

    public void AniEvent_GunnerAttackEvent()
    {
        // 총구 VFX는 소유자의 타격 이벤트와 원격 발사 RPC가 각 화면에서 한 번만 재생한다.
        if (combatAuthority != null) return;
        WBH_EffectSpawner spawner = ResolveSceneEffectSpawner();
        if (combat.currentWeapon == GunnerWeaponType.Shotgun &&
            spawner != null &&
            Eff_gunnerShotgunAtk != null &&
            gunnerEffectRoot != null)
        {
            spawner.SpawnEffect(Eff_gunnerShotgunAtk, gunnerEffectRoot);
        }
    }

    public void AniEvent_PlayFighterAttackSfx(AnimationEvent animationEvent)
    {
        if (!isClient || playerEffect == null) return;
        PlayerStatManager stats = GetComponent<PlayerStatManager>();
        ItemSystem.WeaponType weaponType = stats != null && stats.TryGetEquippedWeaponInfo(out var weapon)
            ? weapon.weaponType : ItemSystem.WeaponType.Greatsword;
        WBH_PlayerEffectCue cue = weaponType switch
        {
            ItemSystem.WeaponType.Axe => WBH_PlayerEffectCue.F_normal0_evo0_etc1,
            ItemSystem.WeaponType.Blunt => WBH_PlayerEffectCue.F_normal0_evo0_etc2,
            _ => WBH_PlayerEffectCue.F_normal0_evo0_etc0
        };
        playerEffect.ScheduleSfx(cue, animator, animationEvent);
    }

    public void AniEvent_PlayGunnerAttackSfx(AnimationEvent animationEvent)
    {
        if (!isClient || combat == null || playerEffect == null) return;
        GunnerWeaponVfxBinding binding = combat.GetComponentInChildren<GunnerWeaponVfxBinding>();
        GunnerWeaponType weaponType = binding != null ? binding.WeaponType : combat.currentWeapon;
        WBH_PlayerEffectCue cue = weaponType switch
        {
            GunnerWeaponType.Rifle => WBH_PlayerEffectCue.G_normal0_evo0_etc0,
            GunnerWeaponType.Shotgun => WBH_PlayerEffectCue.G_normal0_evo0_etc1,
            GunnerWeaponType.GrenadeLauncher => WBH_PlayerEffectCue.G_normal0_evo0_etc2,
            _ => WBH_PlayerEffectCue.None
        };
        if (cue != WBH_PlayerEffectCue.None)
            playerEffect.ScheduleSfx(cue, animator, animationEvent);
    }

    /// <summary>
    /// 서버가 처음 확정한 사망 스냅샷을 현재 화면의 Animator에 즉시 반영한다.
    /// 상태 이벤트가 이미 지나간 재접속 복제본도 사망 모션을 놓치지 않게 한다.
    /// </summary>
    public void ApplyAuthoritativeDeath()
    {
        if (animator == null)
            return;

        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Dodge");
        animator.ResetTrigger("Hit");
        animator.ResetTrigger("Revive");
        animator.ResetTrigger("Dead");
        animator.SetFloat("MoveSpeed", 0f);
        animator.SetTrigger("Dead");
        animator.Update(0f);
    }

    /// <summary>
    /// 서버가 확정한 수동 부활을 각 화면의 Animator에 즉시 반영한다.
    /// Dead 전이의 Exit Time을 기다리지 않아 누운 채 이동하거나 이동 포즈에 고정되는 현상을 막는다.
    /// </summary>
    public void ApplyAuthoritativeRevive()
    {
        if (animator == null)
            return;

        animator.ResetTrigger("Dead");
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Dodge");
        animator.ResetTrigger("Hit");
        animator.ResetTrigger("Revive");
        animator.SetFloat("MoveSpeed", 0f);
        animator.Play("Base Layer.Locomotion", 0, 0f);
        animator.Update(0f);
    }

    public void ApplyAuthoritativePassiveRevive()
    {
        if (animator == null) return;
        animator.ResetTrigger("Dead");
        animator.ResetTrigger("Revive");
        animator.SetFloat("MoveSpeed", 0f);
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("revival01") ||
            (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("revival01")))
            return;
        animator.SetTrigger("Revive");
    }

    private WBH_EffectSpawner ResolveSceneEffectSpawner()
    {
        if (!isClient)
            return null;

        if (effectSpawner != null)
            return effectSpawner;

        WBH_EffectPoolManager poolManager =
            FindFirstObjectByType<WBH_EffectPoolManager>(FindObjectsInactive.Exclude);
        effectSpawner = poolManager != null
            ? poolManager.GetComponent<WBH_EffectSpawner>()
            : null;
        // 씬마다 새 스포너를 원본의 공개 초기화 경계로 연결한다.
        // 스킬을 기본 공격보다 먼저 사용해도 같은 경로를 통과한다.
        if (effectSpawner != null) playerEffect?.Initialize(effectSpawner);
        return effectSpawner;
    }
}
