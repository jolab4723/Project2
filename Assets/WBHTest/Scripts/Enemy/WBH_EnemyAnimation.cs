using System;
using UnityEngine;

[RequireComponent(typeof(WBH_EnemyController))]
public class WBH_EnemyAnimation : MonoBehaviour
{
    [Header("Animation Option")]
    [SerializeField] private bool useMoveAni = true;
    [SerializeField] private bool useAttackAni = true;
    [SerializeField] private bool useHitAni = true;
    [SerializeField] private bool useDieAni = true;
    [SerializeField] private bool useSkillAni = true;

    private Animator animator;
    WBH_EnemyMovement movement;
    WBH_EnemyCombat combat;
    WBH_EnemyEffect effect;

    public const int DashSkillId = 1;
    public const int ShootBurstSkillId = 2;
    public const int BarrageSkillId = 3;
    public const int MissileSkillId = 4;
    public const int TransitionPhaseSkillId = 5;
    public const int JumpAtkSkillId = 6;
    public const int DashWaitSkillId = 7;

    private Action attackEvent;
    private Action attackEndEvent;

    private readonly int MoveHash = Animator.StringToHash("MoveSpeed");
    private readonly int AttackHash = Animator.StringToHash("Attack");
    private readonly int HitHash = Animator.StringToHash("Hit");
    private readonly int DieHash = Animator.StringToHash("Die");
    private readonly int SkillHash = Animator.StringToHash("Skill");
    private readonly int SkillIdHash = Animator.StringToHash("SkillID");
    private readonly int IsGroggyHash = Animator.StringToHash("IsGroggy");
    private readonly int IsPhaseTransitionHash = Animator.StringToHash("IsPhaseTransition");

    public bool IsSkillAniPlaying { get; private set; }
    public bool UseDieAni => useDieAni;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<WBH_EnemyMovement>();
        combat = GetComponent<WBH_EnemyCombat>();
        effect = GetComponent<WBH_EnemyEffect>();
    }

    private void Update()
    {
        if (animator == null || !useMoveAni)
            return;

        animator.SetFloat(MoveHash, movement.CurrentSpeed);
    }

    // controller, combat 참조 필요할 경우 추가. !@
    // spawn 시점에 animator.SetBool(DieHash, false); 추가 - 메서드 만들고 spawner 에서 호출
    public void Initialize()
    {

    }


    public void PlayAttack(Action onHit, Action onFinished)
    {
        attackEvent = onHit;
        attackEndEvent = onFinished; 

        if (!useAttackAni || animator == null)
        {
            attackEvent?.Invoke();
            attackEndEvent?.Invoke();
            attackEvent = null;
            attackEndEvent = null;
            return;
        }
        animator.SetTrigger(AttackHash);
    }

    public void PlayHit()
    {
        if (!useHitAni || animator == null)
            return;
        animator.SetTrigger(HitHash);
    }

    public void PlayDie()
    {
        if (!useDieAni || animator == null)
            return;
        animator.SetTrigger(DieHash);
    }

    public void PlaySkill(int skillId)
    {
        if (!useSkillAni || animator == null)
            return;

        IsSkillAniPlaying = true;

        animator.SetInteger(SkillIdHash, skillId);
        animator.SetTrigger(SkillHash);
    }

    public void SetGroggy(bool isGroggy)
    {
        if (animator == null)
            return;

        animator.SetBool(IsGroggyHash, isGroggy);
    }

    public void SetPhaseTransition(bool active)
    {
        if (animator == null)
            return;

        animator.SetBool(IsPhaseTransitionHash, active);
    }

    // 스킬 진행 강제 초기화
    public void ResetSkillAniState()
    {
        IsSkillAniPlaying = false;
    }

    private bool TryGetEffectCue(int cueValue, out WBH_EnemyEffectCue cue)
    {
        cue = WBH_EnemyEffectCue.None;

        if (!Enum.IsDefined(typeof(WBH_EnemyEffectCue), cueValue))
        {
            Log.Warning($"{name} : 정의되지 않은 EnemyEffectCue 입니다. value = {cueValue}");
            return false;
        }
        cue = (WBH_EnemyEffectCue)cueValue;

        if(cue == WBH_EnemyEffectCue.None)
            return false;

        return true;
    }

    // 애니메이션 이벤트
    public void OnAttackEvent()
    {
        attackEvent?.Invoke();
        attackEvent = null;
    }
    public void OnAttackEndEvent()
    {
        attackEndEvent?.Invoke();
        attackEndEvent = null;
    }

    public void OnSkillAniEnd()
    {
        IsSkillAniPlaying = false;
    }

    public void OnPlayEffect(int cueValue)
    {
        if (!TryGetEffectCue(cueValue, out WBH_EnemyEffectCue cue))
            return;

        effect?.PlayEffect(cue, Vector3.one);
    }

    public void OnPlaySfx(AnimationEvent animationEvent)
    {
        if (animationEvent == null)
            return;

        if (!TryGetEffectCue(animationEvent.intParameter, out WBH_EnemyEffectCue cue))
            return;

        effect?.ScheduleSfx(cue, animator, animationEvent);
    }
}
