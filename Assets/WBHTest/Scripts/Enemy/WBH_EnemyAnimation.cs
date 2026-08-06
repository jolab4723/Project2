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

    public const int DashSkillId = 1;
    public const int ShootBurstSkillId = 2;

    private Action attackEvent;

    private readonly int MoveHash = Animator.StringToHash("MoveSpeed");
    private readonly int AttackHash = Animator.StringToHash("Attack");
    private readonly int HitHash = Animator.StringToHash("Hit");
    private readonly int DieHash = Animator.StringToHash("Die");
    private readonly int SkillHash = Animator.StringToHash("Skill");
    private readonly int SkillIdHash = Animator.StringToHash("SkillID");


    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<WBH_EnemyMovement>();
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


    public void PlayAttack(Action callback)
    {
        attackEvent = callback;

        if (!useAttackAni || animator == null)
        {
            attackEvent?.Invoke();
            attackEvent = null;
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

    public void PlayDash()
    {
        animator.SetTrigger(SkillHash);
        animator.SetFloat(SkillIdHash, DashSkillId);
    }

    public void PlayShootBurst()
    {
        animator.SetTrigger(SkillHash);
        animator.SetFloat(SkillIdHash, ShootBurstSkillId);
    }


    // 공격 애니메이션 이벤트
    public void OnAttackEvent()
    {
        attackEvent?.Invoke();
        attackEvent = null;
    }
}
