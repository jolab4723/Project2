using UnityEngine;
using UnityEngine.AI;

public class WBH_PlayerAnimation : MonoBehaviour
{
    [Header("EffectRoot")]
    [SerializeField] private Transform fighterEffectRoot;
    [SerializeField] private Transform gunnerEffectRoot;


    [Header("Attack")]
    [SerializeField] private WBH_EffectData Eff_fighterAtk;
    [SerializeField] private WBH_EffectData Eff_gunnerShotgunAtk;

    [SerializeField] private WBH_EffectSpawner effectSpawner;

    private Animator animator;
    private WBH_PlayerStateMachine stateMachine;
    private T_PlayerCombat combat;
    private T_PlayerController controller;
    private NavMeshAgent agent;
    private WBH_PlayerEffect effect;
    private WBH_PlayerStatus status;
    

    void Awake()
    {
        animator = GetComponent<Animator>();
        stateMachine = GetComponent<WBH_PlayerStateMachine>();
        agent = GetComponent<NavMeshAgent>();
        combat = GetComponent<T_PlayerCombat>();
        controller = GetComponent<T_PlayerController>();
        effect = GetComponent<WBH_PlayerEffect>();
        status = GetComponent<WBH_PlayerStatus>();
    }

    private void OnEnable()
    {
        stateMachine.OnEnterState += HandleEnterState;
        status.OnAtkSpeedChanged += SetAtkAnimationSpeed;
    }
    private void OnDisable()
    {
        stateMachine.OnEnterState -= HandleEnterState;
        status.OnAtkSpeedChanged -= SetAtkAnimationSpeed;
    }

    private void Start()
    {
        SetAtkAnimationSpeed(1);
    }

    void Update()
    {
        UpdateMoveAnimation();
    }

    private void HandleEnterState(PlayerState now)
    {
        switch(now)
        {
            case PlayerState.Attack:
                animator.SetTrigger("Attack");
                break;

            case PlayerState.Skill:
                PlaySkillAnimation();
                break;

            case PlayerState.Dodge:
                animator.SetTrigger("Dodge");
                break;

            case PlayerState.Hit:
                animator.SetTrigger("Hit");
                break;

            case PlayerState.Dead:
                animator.SetTrigger("Dead");
                break;
        }
    }

    private void SetAtkAnimationSpeed(float attackSpeed)
    {
        animator.SetFloat("AttackSpeed", status.AttackSpeed);
    }

    private void UpdateMoveAnimation()
    {
        switch(stateMachine.CurrentState)
        {
            case PlayerState.Dodge:
            case PlayerState.Dead:
                return;
        }

        float speed = agent.velocity.magnitude / agent.speed;

        if (speed < 0.05f)
            speed = 0;

        animator.SetFloat("MoveSpeed", speed);
    }

    private void PlaySkillAnimation()
    {

    }


    // --- 애니메이션 클립 이벤트 (상태 및 인게임에 영향)
    public void AniEvent_ExecuteAttack()
    {
        combat.ExecuteAttack();
    }
    public void AniEvent_EndAttack()
    {
        Debug.Log($"EndAttack 호출 / 현재 상태 : {stateMachine.CurrentState}");
        stateMachine.ChangeState(PlayerState.Idle);
    }
    public void AniEvent_HitEnd()
    {
        stateMachine.ChangeState(PlayerState.Idle);
    }


    //--- (이펙트)
    public void AniEvent_FighterAttackEvent()
    {
        effectSpawner.SpawnEffect(Eff_fighterAtk, fighterEffectRoot);
    }

    public void AniEvent_GunnerAttackEvent()
    {
        if (combat.currentWeapon == GunnerWeaponType.Shotgun)
            effectSpawner.SpawnEffect(Eff_gunnerShotgunAtk, gunnerEffectRoot);
    }
}
