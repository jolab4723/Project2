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

    [SerializeField] private AnimationClip fighterDash;
    [SerializeField] private AnimationClip gunnerBackstepMove;

    [SerializeField] private WBH_EffectSpawner effectSpawner;

    private Animator animator;
    private WBH_PlayerStateMachine stateMachine;
    private T_PlayerCombat combat;
    private T_PlayerController controller;
    private NavMeshAgent agent;
    private WBH_PlayerEffect effect;
    private WBH_PlayerStatus status;
    private FighterSkillController fighterSkillController;
    private GunnerSkillController gunnerSkillController;

    private readonly int SkillHash = Animator.StringToHash("Skill");
    private readonly int SkillIdHash = Animator.StringToHash("SkillID");
    private readonly int IsChargingHash = Animator.StringToHash("IsCharging");
    private readonly int SkillSpeedHash = Animator.StringToHash("SkillSpeed");
    private readonly int BackstepMoveSpeedHash = Animator.StringToHash("BackstepMoveSpeed");
    private readonly int EvoNumberHash = Animator.StringToHash("EvoNumber"); // 진화 번호 파라미터, 현재(9/1)는 거너 애니메이션 컨트롤러에서만 활용중
    

    void Awake()
    {
        animator = GetComponent<Animator>();
        stateMachine = GetComponent<WBH_PlayerStateMachine>();
        agent = GetComponent<NavMeshAgent>();
        combat = GetComponent<T_PlayerCombat>();
        controller = GetComponent<T_PlayerController>();
        effect = GetComponent<WBH_PlayerEffect>();
        status = GetComponent<WBH_PlayerStatus>();
        fighterSkillController = GetComponent<FighterSkillController>();
        gunnerSkillController = GetComponent<GunnerSkillController>();
    }

    private void OnEnable()
    {
        stateMachine.OnEnterState += HandleEnterState;
        status.OnAtkSpeedChanged += SetAtkAnimationSpeed;

        if(fighterSkillController != null)
        {
            fighterSkillController.OnSkillAniRequested += PlayFighterSkillAnimation;
            fighterSkillController.OnChargeAniChanged += SetChargingAnimation;
        }

        if(gunnerSkillController != null)
        {
            gunnerSkillController.OnSkillAniRequested += PlayGunnerSkillAni;
        }
    }
    private void OnDisable()
    {
        stateMachine.OnEnterState -= HandleEnterState;
        status.OnAtkSpeedChanged -= SetAtkAnimationSpeed;

        if (fighterSkillController != null)
        {
            fighterSkillController.OnSkillAniRequested -= PlayFighterSkillAnimation;
            fighterSkillController.OnChargeAniChanged -= SetChargingAnimation;
        }

        if(animator != null)
        {
            animator.SetBool(IsChargingHash, false);
        }

        if (gunnerSkillController != null)
        {
            gunnerSkillController.OnSkillAniRequested -= PlayGunnerSkillAni;
        }
    }

    private void Start()
    {
        SetAtkAnimationSpeed(1);
    }

    void Update()
    {
        UpdateMoveAnimation();
    }

    // 스킬은 별도 이벤트로 제어.
    private void HandleEnterState(PlayerState now)
    {
        switch(now)
        {
            case PlayerState.Attack:
                animator.SetTrigger("Attack");
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

            case PlayerState.Revive:
                animator.SetTrigger("Revive");
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
            case PlayerState.Revive:
                return;
        }

        float speed = agent.velocity.magnitude / agent.speed;

        if (speed < 0.05f)
            speed = 0;

        animator.SetFloat("MoveSpeed", speed);
    }

    public void PlayFighterSkillAnimation(int skillId, bool isCharging, float targetDuration)
    {
        float skillSpeed = 1f;
        const int DashSkillId = 3;

        if(skillId == DashSkillId && fighterDash != null && targetDuration > 0f)
        {
            skillSpeed = fighterDash.length / targetDuration;
        }

        animator.SetFloat(SkillSpeedHash, skillSpeed);
        animator.SetInteger(SkillIdHash, skillId);
        animator.SetBool(IsChargingHash, isCharging);

        animator.ResetTrigger(SkillHash);
        animator.SetTrigger(SkillHash);
    }

    public void SetChargingAnimation(bool isCharging)
    {
        animator.SetBool(IsChargingHash, isCharging);
    }

    public void PlayGunnerSkillAni(int skillId, int evoNumber, float backstepDuration)
    {
        animator.SetFloat(SkillSpeedHash, 1f);

        float backstepMoveSpeed = 1f;

        if (gunnerBackstepMove != null && backstepDuration > 0f)
        {
            backstepMoveSpeed = gunnerBackstepMove.length / backstepDuration;
        }

        animator.SetFloat(BackstepMoveSpeedHash, backstepMoveSpeed);

        animator.SetInteger(SkillIdHash, skillId);
        animator.SetInteger(EvoNumberHash, evoNumber);

        animator.ResetTrigger(SkillHash);
        animator.SetTrigger(SkillHash);
    }

    // --- 애니메이션 클립 이벤트 (상태 및 인게임에 영향)
    public void AniEvent_ExecuteAttack()
    {
        combat.ExecuteAttack();
    }
    public void AniEvent_EndAttack()
    {
        stateMachine.ChangeState(PlayerState.Idle);
    }
    public void AniEvent_HitEnd()
    {
        stateMachine.ChangeState(PlayerState.Idle);
    }
    public void AniEvent_ExecuteSkill()
    {
        fighterSkillController?.ExecutePendingSkill();
        gunnerSkillController?.ExecutePendingSkill();
    }
    public void AniEvent_ExecuteBackstepMove() // 거너 스킬 중 사격 후 백스텝의 동작 분리를 위해 예외적으로 별도 메서드 작성.
    {
        gunnerSkillController?.ExecutePendingBackstepMove();
    }
    public void AniEvent_EndSkill()
    {
        fighterSkillController?.EndPendingSkillAni();
        gunnerSkillController?.EndPendingSkillAni();
    }
    public void AniEvent_EndDead()
    {
        controller?.TryRevive();
    }
    public void AniEvent_EndRevive()
    {
        controller?.CompleteRevive();
    }



    //--- 애니메이션 클립 이벤트 (이펙트)

    // 일반 공격용.
    public void AniEvent_PlayEffect(int cueValue) 
    {
        WBH_PlayerEffectCue cue = (WBH_PlayerEffectCue)cueValue;

        effect?.PlayEffect(cue, Vector3.one);
    }

    // 스킬용. SkillEffectPart enum 의 파트별로 분기 재생이 가능하다.
    public void AniEvent_PlaySkillEffect(int partValue)
    {
        fighterSkillController?.PlayPendingSkillEffect(partValue);
    }

    public void AniEvent_PlayFighterChargeEffect()
    {
        effect?.PlayFighterChargeEffect();
    }
    public void AniEvent_StopFighterChargeEffect()
    {
        effect?.StopFighterChargeEffect();
    }


    //public void AniEvent_FighterAttackEvent()
    //{
    //    effectSpawner.SpawnEffect(Eff_fighterAtk, fighterEffectRoot);
    //}

    public void AniEvent_GunnerAttackEvent()
    {
        if (combat.currentWeapon == GunnerWeaponType.Shotgun)
            effectSpawner.SpawnEffect(Eff_gunnerShotgunAtk, gunnerEffectRoot);
    }
}
