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

    private Animator animator;

    private bool isMoving;

    private Action attackEvent;

    private readonly int MoveHash = Animator.StringToHash("Move");
    private readonly int AttackHash = Animator.StringToHash("Attack");
    private readonly int HitHash = Animator.StringToHash("Hit");
    private readonly int DieHash = Animator.StringToHash("Die");


    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    // controller, combat 참조 필요할 경우 추가. !@
    // spawn 시점에 animator.SetBool(DieHash, false); 추가 - 메서드 만들고 spawner 에서 호출
    public void Initialize()
    {

    }

    public void SetMove(bool isMove)
    {
        if (!useMoveAni || animator == null || isMoving == isMove)
            return;

        isMoving = isMove;
        animator.SetBool(MoveHash, isMove);
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
        animator.SetBool(DieHash, true);
    }


    // 공격 애니메이션 이벤트
    public void OnAttackEvent()
    {
        attackEvent?.Invoke();
        attackEvent = null;
    }
}
