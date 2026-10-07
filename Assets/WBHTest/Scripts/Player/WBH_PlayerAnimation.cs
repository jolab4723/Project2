using UnityEngine;
using UnityEngine.AI;

public class WBH_PlayerAnimation : MonoBehaviour
{
    /// <summary>SW 수정: 클립에서 발생한 게임 상태 이벤트를 외부 권한 처리기로 구분해 전달합니다.</summary>
    public enum GameplayEvent { Attack, EndAttack, EndHit, Skill, Backstep, EndSkill, EndDead, EndRevive }

    /// <summary>SW 수정: 로컬 실행 대신 연결된 권한 처리기를 사용하는지 나타냅니다.</summary>
    [SerializeField] private bool externalAuthority;
    private System.Func<bool> readsLocalState;
    private System.Func<bool> readsStats;
    private System.Func<bool> presentsEffects;
    private System.Action<string> requestTrigger;
    private System.Action<GameplayEvent, AnimationEvent> requestGameplay;
    private System.Action<int> requestSkillEffect;
    private System.Action<AnimationEvent, Animator> requestSkillSfx;

    /// <summary>SW 수정: 원본의 표시와 클립 이벤트는 유지하고 실행 권한만 같은 객체의 Authority에 위임한다.</summary>
    public void BindAuthority(System.Func<bool> localState, System.Func<bool> stats,
        System.Func<bool> presentation, System.Action<string> trigger,
        System.Action<GameplayEvent, AnimationEvent> gameplay,
        System.Action<int> skillEffect, System.Action<AnimationEvent, Animator> skillSfx)
    {
        externalAuthority = true;
        readsLocalState = localState;
        readsStats = stats;
        presentsEffects = presentation;
        requestTrigger = trigger;
        requestGameplay = gameplay;
        requestSkillEffect = skillEffect;
        requestSkillSfx = skillSfx;
    }

    /// <summary>SW 수정: 외부 권한이 연결되면 해당 처리기가 허용한 경우에만 이펙트와 사운드를 표시합니다.</summary>
    private bool CanPresent => !externalAuthority || (presentsEffects != null && presentsEffects());
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

    /// <summary>SW 수정: 외부 권한이 스킬 애니메이션을 제어할 때 로컬 스킬 이벤트의 중복 구독을 막습니다.</summary>
    private void OnEnable()
    {
        stateMachine.OnEnterState += HandleEnterState;
        status.OnAtkSpeedChanged += SetAtkAnimationSpeed;

        if(!externalAuthority && fighterSkillController != null)
        {
            fighterSkillController.OnSkillAniRequested += PlayFighterSkillAnimation;
            fighterSkillController.OnChargeAniChanged += SetChargingAnimation;
        }

        if(!externalAuthority && gunnerSkillController != null)
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
    /// <summary>SW 수정: 로컬 상태를 읽을 권한이 있을 때 상태별 애니메이션 요청을 전달합니다.</summary>
    private void HandleEnterState(PlayerState now)
    {
        if (externalAuthority && (readsLocalState == null || !readsLocalState())) return;
        switch(now)
        {
            case PlayerState.Attack:
                SetAnimationTrigger("Attack");
                break;

            case PlayerState.Dodge:
                SetAnimationTrigger("Dodge");
                if (CanPresent) effect?.PlaySfx(WBH_PlayerEffectCue.Dodge);
                break;

            case PlayerState.Hit:
                SetAnimationTrigger("Hit");
                break;

            case PlayerState.Dead:
                SetAnimationTrigger("Dead");
                break;

            case PlayerState.Revive:
                if (!externalAuthority) SetAnimationTrigger("Revive");
                break;
        }
    }

    /// <summary>SW 수정: 스탯을 읽을 권한이 있을 때 실제 공격 속도를 애니메이터에 반영합니다.</summary>
    private void SetAtkAnimationSpeed(float attackSpeed)
    {
        if (externalAuthority && (readsStats == null || !readsStats())) return;
        animator.SetFloat("AttackSpeed", status.AttackSpeed);
    }

    /// <summary>SW 수정: 외부 권한이 연결되면 트리거 요청을 위임하고, 로컬에서는 애니메이터에 직접 적용합니다.</summary>
    private void SetAnimationTrigger(string triggerName)
    {
        if (externalAuthority) requestTrigger?.Invoke(triggerName);
        else animator.SetTrigger(triggerName);
    }

    /// <summary>SW 수정: 현재 스탯의 공격 속도를 권한 확인을 거쳐 애니메이션에 다시 반영합니다.</summary>
    public void RefreshAnimation() => SetAtkAnimationSpeed(1f);

    /// <summary>SW 수정: 로컬 상태를 읽을 권한이 있을 때 이동 상태와 실제 속도로 애니메이션을 갱신합니다.</summary>
    private void UpdateMoveAnimation()
    {
        if (externalAuthority && (readsLocalState == null || !readsLocalState())) return;
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
    /// <summary>SW 수정: 공격 실행 이벤트를 외부 권한에 위임하거나 기존 로컬 전투 흐름으로 실행합니다.</summary>
    public void AniEvent_ExecuteAttack()
    {
        if (externalAuthority)
        {
            requestGameplay?.Invoke(GameplayEvent.Attack, null);
            return;
        }
        combat.ExecuteAttack();
    }
    /// <summary>SW 수정: 현재 공격 상태일 때만 공격 종료 이벤트를 적용합니다. 외부 권한이 연결되면 종료 판단을 위임합니다.</summary>
    public void AniEvent_EndAttack()
    {
        if (externalAuthority)
        {
            requestGameplay?.Invoke(GameplayEvent.EndAttack, null);
            return;
        }
        // SW 수정: 전이 중 남은 클립 이벤트가 사망·부활 상태를 덮어쓰지 않게 합니다.
        if (stateMachine.Is(PlayerState.Attack))
            stateMachine.ChangeState(PlayerState.Idle);
    }
    /// <summary>SW 수정: 현재 피격 상태일 때만 피격 종료 이벤트를 적용합니다. 외부 권한이 연결되면 종료 판단을 위임합니다.</summary>
    public void AniEvent_HitEnd()
    {
        if (externalAuthority)
        {
            requestGameplay?.Invoke(GameplayEvent.EndHit, null);
            return;
        }
        if (stateMachine.Is(PlayerState.Hit))
            stateMachine.ChangeState(PlayerState.Idle);
    }
    /// <summary>SW 수정: 스킬 실행 클립 이벤트를 외부 권한에 전달하거나 기존 대기 중인 로컬 스킬을 실행합니다.</summary>
    public void AniEvent_ExecuteSkill(AnimationEvent animationEvent)
    {
        if (externalAuthority)
        {
            requestGameplay?.Invoke(GameplayEvent.Skill, animationEvent);
            return;
        }
        fighterSkillController?.ExecutePendingSkill();
        gunnerSkillController?.ExecutePendingSkill();
    }
    /// <summary>SW 수정: 백스텝 클립 이벤트를 외부 권한에 전달하거나 기존 거너의 대기 중인 이동을 실행합니다.</summary>
    public void AniEvent_ExecuteBackstepMove(AnimationEvent animationEvent) // 거너 스킬 중 사격 후 백스텝의 동작 분리를 위해 예외적으로 별도 메서드 작성.
    {
        if (externalAuthority)
        {
            requestGameplay?.Invoke(GameplayEvent.Backstep, animationEvent);
            return;
        }
        gunnerSkillController?.ExecutePendingBackstepMove();
    }
    /// <summary>SW 수정: 스킬 종료를 외부 권한에 위임하거나 기존 로컬 스킬 애니메이션을 종료합니다.</summary>
    public void AniEvent_EndSkill()
    {
        if (externalAuthority)
        {
            requestGameplay?.Invoke(GameplayEvent.EndSkill, null);
            return;
        }
        fighterSkillController?.EndPendingSkillAni();
        gunnerSkillController?.EndPendingSkillAni();
    }
    /// <summary>SW 수정: 사망 애니메이션 종료 후의 부활 판단을 외부 권한 또는 기존 로컬 컨트롤러에 전달합니다.</summary>
    public void AniEvent_EndDead()
    {
        if (externalAuthority)
        {
            requestGameplay?.Invoke(GameplayEvent.EndDead, null);
            return;
        }
        controller?.TryRevive();
    }
    /// <summary>SW 수정: 부활 애니메이션 종료를 외부 권한에 위임하거나 기존 로컬 컨트롤러에 반영합니다.</summary>
    public void AniEvent_EndRevive()
    {
        if (externalAuthority)
        {
            requestGameplay?.Invoke(GameplayEvent.EndRevive, null);
            return;
        }
        controller?.CompleteRevive();
    }



    //--- 애니메이션 클립 이벤트 (이펙트)

    // 일반 공격용.
    /// <summary>SW 수정: 표시 권한과 이펙트 생성기를 확인한 뒤 일반 공격 이펙트를 재생합니다.</summary>
    public void AniEvent_PlayEffect(int cueValue) 
    {
        if (!CanPresent || !EnsureEffectSpawner()) return;
        WBH_PlayerEffectCue cue = (WBH_PlayerEffectCue)cueValue;

        effect?.PlayEffect(cue, Vector3.one);
    }

    // 스킬용. SkillEffectPart enum 의 파트별로 분기 재생이 가능하다.
    /// <summary>SW 수정: 표시 권한과 이펙트 생성기를 확인하고 스킬 이펙트를 외부 권한 또는 기존 로컬 스킬에 전달합니다.</summary>
    public void AniEvent_PlaySkillEffect(int partValue)
    {
        if (!CanPresent || !EnsureEffectSpawner()) return;
        if (externalAuthority)
        {
            requestSkillEffect?.Invoke(partValue);
            return;
        }
        fighterSkillController?.PlayPendingSkillEffect(partValue);
        gunnerSkillController?.PlayPendingSkillEffect(partValue);
    }

    /// <summary>SW 수정: 표시 권한과 이펙트 생성기를 확인한 뒤 파이터 차징 이펙트를 재생합니다.</summary>
    public void AniEvent_PlayFighterChargeEffect()
    {
        if (!CanPresent || !EnsureEffectSpawner()) return;
        effect?.PlayFighterChargeEffect();
    }
    /// <summary>SW 수정: 표시 권한이 있을 때 파이터 차징 이펙트를 종료합니다.</summary>
    public void AniEvent_StopFighterChargeEffect()
    {
        if (!CanPresent) return;
        effect?.StopFighterChargeEffect();
    }


    //public void AniEvent_FighterAttackEvent()
    //{
    //    effectSpawner.SpawnEffect(Eff_fighterAtk, fighterEffectRoot);
    //}

    /// <summary>SW 수정: 네트워크 발사 연출의 중복 재생을 막고, 로컬 공격에서만 샷건 이펙트를 생성합니다.</summary>
    public void AniEvent_GunnerAttackEvent()
    {
        // 네트워크 총구는 타격 확정과 발사 RPC가 한 번만 재생한다.
        if (externalAuthority || !CanPresent || !EnsureEffectSpawner()) return;
        if (combat.currentWeapon == GunnerWeaponType.Shotgun)
            effectSpawner.SpawnEffect(Eff_gunnerShotgunAtk, gunnerEffectRoot);
    }

    /// <summary>SW 수정: 표시 권한을 확인하고 스킬 사운드 이벤트를 외부 권한 또는 기존 로컬 스킬에 전달합니다.</summary>
    public void AniEvent_PlaySkillSfx(AnimationEvent animationEvent)
    {
        if (!CanPresent) return;
        if (externalAuthority)
        {
            requestSkillSfx?.Invoke(animationEvent, animator);
            return;
        }
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

    /// <summary>SW 수정: 표시 권한이 있을 때 장착 무기에 맞는 파이터 공격 사운드를 예약합니다.</summary>
    public void AniEvent_PlayFighterAttackSfx(AnimationEvent animationEvent)
    {
        if (!CanPresent) return;
        if (TryGetFighterAttackCue(out WBH_PlayerEffectCue cue))
            effect?.ScheduleSfx(cue, animator, animationEvent);
    }

    /// <summary>SW 수정: 표시 권한이 있을 때 실제 장착 무기에 맞는 거너 공격 사운드를 예약합니다.</summary>
    public void AniEvent_PlayGunnerAttackSfx(AnimationEvent animationEvent)
    {
        if (!CanPresent) return;
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

    // 기존 Fighter_Hit 클립의 이벤트 이름도 같은 수신점으로 연결한다.
    /// <summary>SW 수정: 기존 클립의 피격 종료 이벤트를 공통 피격 종료 처리로 전달합니다.</summary>
    public void AniEvent_EndHit() => AniEvent_HitEnd();

    /// <summary>SW 수정: 표시 권한과 필요한 참조가 준비되면 파이터 공격 이펙트를 생성합니다.</summary>
    public void AniEvent_FighterAttackEvent()
    {
        if (CanPresent && EnsureEffectSpawner() && Eff_fighterAtk != null && fighterEffectRoot != null)
            effectSpawner.SpawnEffect(Eff_fighterAtk, fighterEffectRoot);
    }

    /// <summary>SW 수정: 현재 이펙트 풀이 준비되면 생성기를 찾아 기존 플레이어 이펙트에 연결합니다.</summary>
    private bool EnsureEffectSpawner()
    {
        if (effectSpawner == null)
        {
            WBH_EffectPoolManager pool = FindFirstObjectByType<WBH_EffectPoolManager>(FindObjectsInactive.Exclude);
            effectSpawner = pool != null ? pool.GetComponent<WBH_EffectSpawner>() : null;
            if (effectSpawner != null) effect?.Initialize(effectSpawner);
        }
        return effectSpawner != null;
    }

    /// <summary>SW 수정: 대기 중인 스킬 연출을 취소하고 사망·피격·부활 상태 외에는 이동 애니메이션으로 돌아갑니다.</summary>
    public void CancelSkillAnimation()
    {
        effect?.CancelPendingSfx();
        effect?.StopFighterChargeEffect();
        animator.ResetTrigger(SkillHash);
        animator.SetBool(IsChargingHash, false);
        if (!stateMachine.IsAnyState(PlayerState.Dead, PlayerState.Hit, PlayerState.Revive))
            animator.Play("Base Layer.Locomotion", 0, 0f);
    }

    /// <summary>SW 수정: 권한 처리기가 확정한 사망 상태를 다른 전이 트리거를 정리한 뒤 즉시 표시합니다.</summary>
    public void ApplyAuthoritativeDeath()
    {
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Dodge");
        animator.ResetTrigger("Hit");
        animator.ResetTrigger("Revive");
        animator.ResetTrigger("Dead");
        animator.SetFloat("MoveSpeed", 0f);
        animator.SetTrigger("Dead");
        animator.Update(0f);
    }

    /// <summary>SW 수정: 권한 처리기가 확정한 일반 부활 상태를 이동 애니메이션으로 즉시 표시합니다.</summary>
    public void ApplyAuthoritativeRevive()
    {
        animator.ResetTrigger("Dead");
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Dodge");
        animator.ResetTrigger("Hit");
        animator.ResetTrigger("Revive");
        animator.SetFloat("MoveSpeed", 0f);
        animator.Play("Base Layer.Locomotion", 0, 0f);
        animator.Update(0f);
    }

    /// <summary>SW 수정: 권한 처리기가 확정한 패시브 부활 애니메이션을 중복 시작하지 않도록 표시합니다.</summary>
    public void ApplyAuthoritativePassiveRevive()
    {
        animator.ResetTrigger("Dead");
        animator.ResetTrigger("Revive");
        animator.SetFloat("MoveSpeed", 0f);
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("revival01") ||
            (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("revival01"))) return;
        animator.SetTrigger("Revive");
    }
}
