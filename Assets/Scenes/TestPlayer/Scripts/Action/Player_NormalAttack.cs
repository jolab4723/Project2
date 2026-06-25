using UnityEngine;
using UnityEngine.AI;

public class Player_NormalAttack : Player_ClickToMove
{
    private Animator animator;
    [SerializeField] private bool attacking = false;

    [Header("공격 설정")]
    public float attackDuration = 1.0f;
    private float lastAttackTime = -99f;

    [Header("이펙트 설정")]
    [Tooltip("여기에 칼에 달아둔 이펙트(ParticleSystem)를 끌어다 넣으세요")]
    public ParticleSystem swordEffect; // TrailRenderer를 쓰신다면 TrailRenderer로 적으셔도 됩니다.

    private bool isWaitingForDamageEvent = false;
    private GameObject currentTarget = null;

    // ★ 추가됨: 현재 적을 쫓아가고 있는 중인지(추적 모드) 확인하는 변수
    [SerializeField] private bool isChasing = false;

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
    }

    protected override void Update()
    {
        attacking = (Time.time - lastAttackTime) < attackDuration;

        // ★ 새로 추가됨: 추적 로직 (Update에서 매 프레임 감시합니다)
        if (isChasing)
        {
            // 만약 쫓아가던 중에 적이 죽어서 사라졌다면 추적 중지
            if (currentTarget == null)
            {
                isChasing = false;
                if (agent != null) agent.ResetPath();
            }
            // 쫓아가는 도중 공격이 시작되었다면 혹시 모르니 추적 플래그 끄기
            else if (attacking)
            {
                isChasing = false;
            }
            else
            {
                float distance = Vector3.Distance(transform.position, currentTarget.transform.position);

                // 1. 쫓아가다가 드디어 사거리 안으로 들어왔다면?!
                if (distance <= status.AttackRange)
                {
                    isChasing = false; // 추적 끝!

                    if (agent != null)
                    {
                        agent.isStopped = true;
                        agent.ResetPath();
                    }

                    animator.SetTrigger("Attack");
                    lastAttackTime = Time.time;
                    isWaitingForDamageEvent = true;

                    // 적을 바라보게 회전
                    Vector3 lookDirection = currentTarget.transform.position - transform.position;
                    lookDirection.y = 0;
                    transform.rotation = Quaternion.LookRotation(lookDirection);
                }
                // 2. 아직 사거리 밖이라면?
                else
                {
                    if (agent != null)
                    {
                        // 적이 도망갈 수도 있으므로, 내비메쉬의 목적지를 계속 적의 실시간 위치로 갱신해 줍니다!
                        agent.isStopped = false;
                        agent.SetDestination(currentTarget.transform.position);
                    }
                }
            }
        }

        base.Update();
    }

    protected override void OnLeftClickTarget(GameObject target, Vector3 clickPosition)
    {
        if (attacking) return;

        if (target.CompareTag("Enemy"))
        {
            currentTarget = target; // 누굴 때릴 건지 타겟을 등록합니다.

            float distance = Vector3.Distance(transform.position, target.transform.position);
            bool InAttackRange = (distance <= status.AttackRange);

            if (InAttackRange)
            {
                // 이미 사거리 안이면 쫓아갈 필요 없이 즉시 공격!
                isChasing = false;

                if (agent != null)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }

                animator.SetTrigger("Attack");
                lastAttackTime = Time.time;
                isWaitingForDamageEvent = true;

                Vector3 lookDirection = target.transform.position - transform.position;
                lookDirection.y = 0;
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
            else
            {
                // ★ 거리가 멀면 바로 공격하지 않고 "추적 모드"만 켭니다! (나머지는 Update가 알아서 함)
                isChasing = true;
            }
        }
    }

    public void EnableEffect()
    {
        if (swordEffect != null)
        {
            swordEffect.Play(); // 파티클 재생 (TrailRenderer면 swordEffect.emitting = true;)
        }
    }

    public void DisableEffect()
    {
        if (swordEffect != null)
        {
            // 기존의 Stop() 대신 아래 코드를 사용합니다.
            // 뿜어내는 것을 멈추는 동시에(StopEmitting), 이미 화면에 남아있는 파티클 입자들도 즉시 삭제(Clear)합니다!
            swordEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    protected override void OnRightClickTarget(GameObject target, Vector3 clickPosition)
    {
        isChasing = false;
        isWaitingForDamageEvent = false;
        // ★ 우클릭 캔슬을 할 때, 혹시 켜져 있던 이펙트가 있다면 강제로 꺼줍니다! (잔상 방지)
        DisableEffect();
        base.OnRightClickTarget(target, clickPosition);
    }

    public void Damage()
    {
        if (isWaitingForDamageEvent && currentTarget != null)
        {
            Debug.Log($"[{currentTarget.name}] 에게 진짜로 데미지가 들어갔습니다!");
            isWaitingForDamageEvent = false;
        }
        else
        {
            Debug.Log("이동으로 인해 공격이 캔슬되어 허공을 갈랐습니다.");
        }
    }
}