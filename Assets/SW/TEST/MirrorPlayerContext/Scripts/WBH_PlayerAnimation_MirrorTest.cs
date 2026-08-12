using Mirror;
using UnityEngine;
using UnityEngine.AI;

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
    private NavMeshAgent agent;
    private WBH_PlayerStatus status;
    private bool localEventsBound;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        stateMachine = GetComponent<WBH_PlayerStateMachine>();
        combat = GetComponent<T_PlayerCombat>();
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
            combat.ExecuteAttack();
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

    public void AniEvent_FighterAttackEvent()
    {
        if (effectSpawner != null && Eff_fighterAtk != null && fighterEffectRoot != null)
            effectSpawner.SpawnEffect(Eff_fighterAtk, fighterEffectRoot);
    }

    public void AniEvent_GunnerAttackEvent()
    {
        if (combat.currentWeapon == GunnerWeaponType.Shotgun &&
            effectSpawner != null &&
            Eff_gunnerShotgunAtk != null &&
            gunnerEffectRoot != null)
        {
            effectSpawner.SpawnEffect(Eff_gunnerShotgunAtk, gunnerEffectRoot);
        }
    }
}
