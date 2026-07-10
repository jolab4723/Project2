using UnityEngine;

[RequireComponent(typeof(WBH_EnemyMovement))]
[RequireComponent(typeof(WBH_EnemyCombat))]
[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyAnimation))]
[RequireComponent(typeof(WBH_EnemyPattern))]
public class WBH_EnemyController : MonoBehaviour, T_IDamageable
{
    private WBH_EnemyMovement movement;
    private WBH_EnemyCombat combat;
    private WBH_EnemyAnimation enemyAnimation;
    private WBH_EnemyStatus status;
    private WBH_EnemyPattern pattern;

    private WBH_EnemyInfo info;

    public WBH_EnemyInfo Info => info;

    private void Awake()
    {
        movement = GetComponent<WBH_EnemyMovement>();
        combat = GetComponent<WBH_EnemyCombat>();
        enemyAnimation = GetComponent<WBH_EnemyAnimation>();
        status = GetComponent<WBH_EnemyStatus>();
        pattern = GetComponent<WBH_EnemyPattern>();
    }

    public void Initialize(WBH_EnemyInfo info)
    {
        this.info = info;
        
        status.Initialize(info);

        movement.Initialize(info);
        combat.Initialize(info);
        enemyAnimation.Initialize();

        pattern.Initialize(this);

        Debug.Log(info.enemyName);
    }

    public void TakeDamage(float damage)
    {
        status.ApplyDamage(damage);

        // 애니메이션 피격

        // 사망 처리
        if(status.IsDead)
        {

        }
    }

    public void SetTarget(Transform target)
    {
        pattern.SetTarget(target);
    }
}
