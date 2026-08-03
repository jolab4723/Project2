using System;
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
public class WBH_EnemyController : MonoBehaviour, WBH_ICombat
{
    private WBH_EnemyMovement movement;
    private WBH_EnemyCombat combat;
    private WBH_EnemyAnimation enemyAnimation;
    private WBH_EnemyStatus status;
    private WBH_EnemyPattern pattern;
    private WBH_EnemyStatusEffectController statusEffectController;
    private WBH_EnemyPoolManager poolManager; 

    private WBH_EnemyInfo info;

    public static event Action OnEnemyDead; // 사망 시, 현재 남은 적 숫자를 WBH_EnemySpawnManager 에 반영

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

        Debug.Log(info.enemyName);
    }

    public void TakeDamage(WBH_DamageResult result)
    {
        status.TakeDamage(result);

        // 애니메이션 피격 !@

        
    }


    private void Dead()
    {
        OnEnemyDead?.Invoke();

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
}
