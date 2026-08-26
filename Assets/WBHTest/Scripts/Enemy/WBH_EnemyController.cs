using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(WBH_EnemyMovement))]
[RequireComponent(typeof(WBH_EnemyCombat))]
[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyAnimation))]
[RequireComponent(typeof(WBH_EnemyPattern))]
[RequireComponent(typeof(WBH_EnemyStatusEffectController))]
[RequireComponent(typeof(EnemyKillReward))]
public class WBH_EnemyController : MonoBehaviour, WBH_ICombat
{
    private WBH_EnemyMovement movement;
    private WBH_EnemyCombat combat;
    private WBH_EnemyAnimation enemyAnimation;
    private WBH_EnemyStatus status;
    private WBH_EnemyPattern pattern;
    private WBH_EnemyStatusEffectController statusEffectController;
    private WBH_EnemyPoolManager poolManager; 
    private WBHEnemyDestructionAdapter destructionAdapter;
    private WBH_EnemyBossDeathView bossDeathView;
    private WBH_EnemyGradeVisual gradeVisual;

    private WBH_EnemyInfo info;

    public static event Action OnEnemyDead; // 사망 시, 현재 남은 적 숫자를 WBH_EnemySpawnManager 에 반영
    private bool isDying;

    public WBH_EnemyInfo Info => info;
    public WBH_ICombatStatus Status => status;

    private void Awake()
    {
        movement = GetComponent<WBH_EnemyMovement>();
        combat = GetComponent<WBH_EnemyCombat>();
        enemyAnimation = GetComponent<WBH_EnemyAnimation>();
        status = GetComponent<WBH_EnemyStatus>();
        pattern = GetComponent<WBH_EnemyPattern>();
        statusEffectController = GetComponent<WBH_EnemyStatusEffectController>();
        destructionAdapter = GetComponent<WBHEnemyDestructionAdapter>();
        bossDeathView = GetComponent<WBH_EnemyBossDeathView>();
        gradeVisual = GetComponent<WBH_EnemyGradeVisual>();
    }

    private void OnEnable()
    {
        status.OnDead += Dead; 
    }

    private void OnDisable()
    {
        status.OnDead -= Dead;
    }

    public void Initialize(WBH_EnemyInfo info, WBH_EnemyPoolManager poolManager)
    {
        this.info = info;
        this.poolManager = poolManager;

        status ??= GetComponent<WBH_EnemyStatus>();
        movement ??= GetComponent<WBH_EnemyMovement>();
        combat ??= GetComponent<WBH_EnemyCombat>();
        enemyAnimation ??= GetComponent<WBH_EnemyAnimation>();
        pattern ??= GetComponent<WBH_EnemyPattern>();
        statusEffectController ??= GetComponent<WBH_EnemyStatusEffectController>();

        status.Initialize(info);
        movement.Initialize(info);
        combat.Initialize(info);
        enemyAnimation.Initialize();
        pattern.Initialize(this);
        gradeVisual?.ApplyGrade(info.enemyGrade);
        
        bossDeathView?.ResetVisual();

        Log.Print(info.enemyName);
        isDying = false;
    }

    public void TakeDamage(WBH_DamageResult result)
    {
        status.TakeDamage(result);

        // 애니메이션 피격 !@

    }

    private void Dead()
    {
        if (isDying)
            return;

        isDying = true;

        OnEnemyDead?.Invoke(); // 웨이브 카운트 감소 등 사망처리

        movement.Stop();
        movement.SetControlEnable(false);

        enemyAnimation.PlayDie();

        if(!enemyAnimation.UseDieAni)
        {
            poolManager.Return(this);
        }
    }

    // 스스로 데미지를 입는 특수한 사망 처리. (ex.자폭드론)
    public void KillSelf()
    {
        if (isDying || status.IsDead)
            return;

        destructionAdapter?.RecordHit(transform.position, Vector3.zero);

        float lethalDamage = Mathf.Max(1f, status.CurrentHp);

        WBH_DamageResult result = new WBH_DamageResult(this, lethalDamage, isCritical: false, ItemSystem.ElementType.None);

        status.TakeDamage(result);
    }

    // 데미지를 입어 사망하지 않고 체력이 남은 상태로 역소환. ex) 히든 등급 적.
    public void Despawn()
    {
        if (isDying)
            return;

        isDying = true;

        OnEnemyDead?.Invoke();

        movement.Stop();
        movement.SetControlEnable(false);

        poolManager.Return(this);
    }

    public void SetTarget(Transform target)
    {
        pattern.SetTarget(target);
    }

    public void AddStatusEffect(WBH_StatusEffectData data)
    {
        statusEffectController.AddStatusEffect(data);
    }

    // 보스 전용 사망연출(애니메이션 이벤트). 사망 후 n초 뒤에 디졸브 걸고 사라짐.
    public void OnDeathAnimationEnd()
    {
        if (!isDying)
            return;

        if(bossDeathView != null)
        {
            bossDeathView.PlayDeathEffect(ReturnAfterDeath);
        }
    }

    private void ReturnAfterDeath()
    {
        poolManager.Return(this);
    }
}
