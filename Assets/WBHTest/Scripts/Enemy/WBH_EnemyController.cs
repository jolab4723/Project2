using UnityEngine;

[RequireComponent(typeof(WBH_EnemyMovement))]
[RequireComponent(typeof(WBH_EnemyCombat))]
[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyAnimation))]
[RequireComponent(typeof(WBH_EnemyPattern))]
public class WBH_EnemyController : MonoBehaviour, WBH_ICombat
{
    private WBH_EnemyMovement movement;
    private WBH_EnemyCombat combat;
    private WBH_EnemyAnimation enemyAnimation;
    private WBH_EnemyStatus status;
    private WBH_EnemyPattern pattern;
    private WBH_EnemyPoolManager poolManager; 

    private WBH_EnemyInfo info;

    public WBH_EnemyInfo Info => info;
    public WBH_ICombatStatus Status => status;

    private void Awake()
    {
        movement = GetComponent<WBH_EnemyMovement>();
        combat = GetComponent<WBH_EnemyCombat>();
        enemyAnimation = GetComponent<WBH_EnemyAnimation>();
        status = GetComponent<WBH_EnemyStatus>();
        pattern = GetComponent<WBH_EnemyPattern>();
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
        poolManager.Return(this);
    }

    public void SetTarget(Transform target)
    {
        pattern.SetTarget(target);
    }
}
