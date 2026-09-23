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

    [Header("Run Playback Speed")]
    [Tooltip("Run 애니메이션을 1배속으로 재생할 실제 이동속도입니다.")]
    [SerializeField, Min(0.01f)] private float referenceRunMoveSpeed = 5f;
    [SerializeField, Range(0.1f, 3f)] private float minimumRunPlaybackSpeed = 0.5f;
    [SerializeField, Range(0.1f, 3f)] private float maximumRunPlaybackSpeed = 2f;

    private bool hasRunPlaybackSpeedParameter;
    private static readonly int RunPlaybackSpeedHash = Animator.StringToHash("RunPlaybackSpeed");

    private Animator animator;
    private WBH_PlayerStateMachine stateMachine;
    private T_PlayerCombat combat;
    private T_PlayerController controller;
    private NavMeshAgent agent;
    private WBH_PlayerEffect effect;
    private WBH_PlayerStatus status;
    private FighterSkillController fighterSkillController;
    private GunnerSkillController gunnerSkillController;
    private PlayerStatManager statManager;

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
        statManager = GetComponent<PlayerStatManager>();

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == RunPlaybackSpeedHash &&
                parameter.type == AnimatorControllerParameterType.Float)
            {
                hasRunPlaybackSpeedParameter = true;
                break;
            }
        }

        if (!hasRunPlaybackSpeedParameter)
        {
            Debug.LogWarning(
                "[WBH_PlayerAnimation] Animator에 Float RunPlaybackSpeed를 추가하고 " +
                "Locomotion의 Speed Multiplier에 연결하세요. 현재 Run 배속 연동은 적용되지 않습니다.", this);
        }
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
            gunnerSkillController.OnSkillAniRequested += PlayGunnerSkillAnimation;
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
            gunnerSkillController.OnSkillAniRequested -= PlayGunnerSkillAnimation;
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
                effect?.PlaySfx(WBH_PlayerEffectCue.Dodge);
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
                if (hasRunPlaybackSpeedParameter)
                    animator.SetFloat(RunPlaybackSpeedHash, 1f);
                return;
        }

        Vector3 velocity = Vector3.zero;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && !agent.isStopped)
            velocity = agent.velocity;
        velocity.y = 0f;

        float actualSpeed = velocity.magnitude;
        float speed = agent != null && agent.speed > 0.01f
            ? Mathf.Clamp01(actualSpeed / agent.speed)
            : 0f;

        if (speed < 0.05f)
            speed = 0;

        animator.SetFloat("MoveSpeed", speed);

        if (hasRunPlaybackSpeedParameter)
        {
            float lowerSpeed = Mathf.Clamp(minimumRunPlaybackSpeed, 0.1f, 3f);
            float upperSpeed = Mathf.Clamp(maximumRunPlaybackSpeed, lowerSpeed, 3f);
            // Idle은 1배속으로 유지하고 달릴 때만 실제 속도에 비례시킵니다.
            float playbackSpeed = speed > 0f
                ? Mathf.Clamp(actualSpeed / Mathf.Max(0.01f, referenceRunMoveSpeed), lowerSpeed, upperSpeed)
                : 1f;
            animator.SetFloat(RunPlaybackSpeedHash, playbackSpeed);
        }
    }

    public void PlayFighterSkillAnimation(int skillId, bool isCharging, float targetDuration)
    {
        effect?.CancelPendingSfx();

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

    public void PlayGunnerSkillAnimation(int skillId, int evoNumber, float backstepDuration)
    {
        effect?.CancelPendingSfx();

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
    /// <summary>SW 수정: 현재 공격 상태일 때만 공격 종료 이벤트를 적용합니다.</summary>
    public void AniEvent_EndAttack()
    {
        // SW 수정: 전이 중 남은 클립 이벤트가 사망·부활 상태를 덮어쓰지 않게 합니다.
        if (stateMachine.Is(PlayerState.Attack))
            stateMachine.ChangeState(PlayerState.Idle);
    }
    /// <summary>SW 수정: 현재 피격 상태일 때만 피격 종료 이벤트를 적용합니다.</summary>
    public void AniEvent_HitEnd()
    {
        if (stateMachine.Is(PlayerState.Hit))
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
        gunnerSkillController?.PlayPendingSkillEffect(partValue);
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

    public void AniEvent_PlaySkillSfx(AnimationEvent animationEvent)
    {
        fighterSkillController?.PlayPendingSkillSfx(animationEvent, animator);
        gunnerSkillController?.PlayPendingSkillSfx(animationEvent, animator);
    }
    private bool TryGetFighterAttackCue(out WBH_PlayerEffectCue cue)
    {
        cue = WBH_PlayerEffectCue.None;

        if (statManager == null || ! statManager.TryGetEquippedWeaponInfo(out EquippedWeaponInfo weapon))
        {
            cue = WBH_PlayerEffectCue.F_normal0_evo0_etc0; // 무기 타입을 못읽으면 대검으로 인식.
            return true;
        }

        switch (weapon.weaponType)
        {
            case ItemSystem.WeaponType.Greatsword:
                cue = WBH_PlayerEffectCue.F_normal0_evo0_etc0;
                return true;

            case ItemSystem.WeaponType.Axe:
                cue = WBH_PlayerEffectCue.F_normal0_evo0_etc1;
                return true;

            case ItemSystem.WeaponType.Blunt:
                cue = WBH_PlayerEffectCue.F_normal0_evo0_etc2;
                return true;

            default:
                cue = WBH_PlayerEffectCue.F_normal0_evo0_etc0; // 무기 타입을 못읽으면 대검으로 인식.
                return true;
        }
    }

    public void AniEvent_PlayFighterAttackSfx(AnimationEvent animationEvent)
    {
        if (TryGetFighterAttackCue(out WBH_PlayerEffectCue cue))
            effect?.ScheduleSfx(cue, animator, animationEvent);
    }

    public void AniEvent_PlayGunnerAttackSfx(AnimationEvent animationEvent)
    {
        if (combat == null || effect == null)
            return;

        // 실제 GunnerAttack()과 동일한 무기 판정 기준.
        GunnerWeaponVfxBinding binding = combat.GetComponentInChildren<GunnerWeaponVfxBinding>();

        GunnerWeaponType weaponType = binding != null
            ? binding.WeaponType
            : combat.currentWeapon;

        WBH_PlayerEffectCue cue = weaponType switch
        {
            GunnerWeaponType.Rifle
                => WBH_PlayerEffectCue.G_normal0_evo0_etc0,

            GunnerWeaponType.Shotgun
                => WBH_PlayerEffectCue.G_normal0_evo0_etc1,

            GunnerWeaponType.GrenadeLauncher
                => WBH_PlayerEffectCue.G_normal0_evo0_etc2,

            _ => WBH_PlayerEffectCue.None
        };

        if (cue != WBH_PlayerEffectCue.None)
            effect.ScheduleSfx(cue, animator, animationEvent);
    }
}
