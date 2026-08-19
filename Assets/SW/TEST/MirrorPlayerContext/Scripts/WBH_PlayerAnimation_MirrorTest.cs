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
    [SerializeField] private WBH_EffectSpawner effectSpawner;

    private Animator animator;
    private NetworkAnimator networkAnimator;
    private WBH_PlayerStateMachine stateMachine;
    private T_PlayerCombat combat;
    private PlayerCombatAuthority_MirrorTest combatAuthority;
    private NavMeshAgent agent;
    private WBH_PlayerStatus status;
    private bool localEventsBound;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        stateMachine = GetComponent<WBH_PlayerStateMachine>();
        combat = GetComponent<T_PlayerCombat>();
        combatAuthority = GetComponent<PlayerCombatAuthority_MirrorTest>();
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

    public override void OnStopLocalPlayer()
    {
        UnbindLocalEvents();
        base.OnStopLocalPlayer();
    }

    private void OnDisable()
    {
        UnbindLocalEvents();
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
        status.OnAtkSpeedChanged += HandleAttackSpeedChanged;
        localEventsBound = true;
    }

    private void UnbindLocalEvents()
    {
        if (!localEventsBound)
            return;

        stateMachine.OnEnterState -= HandleEnterState;
        status.OnAtkSpeedChanged -= HandleAttackSpeedChanged;
        localEventsBound = false;
    }

    private void HandleEnterState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Attack:
                networkAnimator.SetTrigger("Attack");
                break;

            case PlayerState.Skill:
                PlaySkillAnimation();
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
        if (stateMachine.IsAnyState(PlayerState.Dodge, PlayerState.Dead))
            return;

        float speed = agent.speed > 0f
            ? agent.velocity.magnitude / agent.speed
            : 0f;

        if (speed < 0.05f)
            speed = 0f;

        animator.SetFloat("MoveSpeed", speed);
    }

    private void PlaySkillAnimation()
    {
    }

    public void AniEvent_ExecuteAttack()
    {
        if (isLocalPlayer)
            combatAuthority?.TryConfirmLocalAttackImpactFromAnimation();
    }

    public void AniEvent_EndAttack()
    {
        if (isLocalPlayer)
            stateMachine.ChangeState(PlayerState.Idle);
    }

    public void AniEvent_HitEnd()
    {
        if (isLocalPlayer)
            stateMachine.ChangeState(PlayerState.Idle);
    }

    /// <summary>Fighter_Hit 클립에 남아 있는 기존 이벤트 이름을 테스트 복제본에서 호환한다.</summary>
    public void AniEvent_EndHit()
    {
        AniEvent_HitEnd();
    }

    public void AniEvent_FighterAttackEvent()
    {
        WBH_EffectSpawner spawner = ResolveSceneEffectSpawner();
        if (spawner != null && Eff_fighterAtk != null && fighterEffectRoot != null)
            spawner.SpawnEffect(Eff_fighterAtk, fighterEffectRoot);
    }

    public void AniEvent_GunnerAttackEvent()
    {
        WBH_EffectSpawner spawner = ResolveSceneEffectSpawner();
        if (combat.currentWeapon == GunnerWeaponType.Shotgun &&
            spawner != null &&
            Eff_gunnerShotgunAtk != null &&
            gunnerEffectRoot != null)
        {
            spawner.SpawnEffect(Eff_gunnerShotgunAtk, gunnerEffectRoot);
        }
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
        return effectSpawner;
    }
}
