using UnityEngine;
using UnityEngine.AI;

public class WBH_EnemyHiddenPattern : WBH_IEnemyPattern
{
    private WBH_EnemyPattern owner;
    private WBH_EnemyPattern.HiddenSettings settings;

    private readonly NavMeshPath fleePath = new NavMeshPath();

    private float despawnTime;
    private float nextRepathTime;
    private float speedBoostEndTime;

    private bool droppedNarmal;
    private bool droppedAdvanced;
    private bool droppedElite;

    public void Initialize(WBH_EnemyPattern owner)
    {
        this.owner = owner;
        settings = owner.HiddenConfig;

        despawnTime = Time.time + settings.lifeTime;

        nextRepathTime = 0f;
        speedBoostEndTime = 0f;

        droppedNarmal = false;
        droppedAdvanced = false;
        droppedElite = false;

        owner.Status.OnDamaged += HandleDamaged;
        owner.Status.OnHpChanged += HandleHpChanged;

        owner.Movement.SetMoveSpeed(owner.CurrentMoveSpeed);
    }

    public void Tick(float deltaTime)
    {
        if(Time.time >= despawnTime)
        {
            //owner.Despawn(); !@
            return;
        }

        UpdateMoveSpeed();

        if (Time.time < nextRepathTime)
            return;

        nextRepathTime = Time.time + settings.repathInterval;

        //owner.TryS(); !@

        //TryS(); !@
    }

    private void HandleDamaged(WBH_DamageResult result)
    {
        speedBoostEndTime = Time.time + settings.damagedSpeedDuration;
    }

    private void UpdateMoveSpeed()
    {
        bool isBoosted = Time.time < speedBoostEndTime;

        float multiplier = isBoosted ? settings.damagedSpeedMultiplier : 1f;

        owner.Movement.SetMoveSpeed(owner.CurrentMoveSpeed * multiplier);
    }

    private void HandleHpChanged(float currentHp, float maxHp)
    {
        if (maxHp <= 0f)
            return;

        float ratio = currentHp / maxHp;
        Vector3 dropPos = owner.transform.position;

        if(!droppedNarmal && ratio <= 0.6f)
        {
            droppedNarmal = true;
            DropItem(EnemyGrade.Normal, dropPos);
        }

        if (!droppedAdvanced && ratio <= 0.3f)
        {
            droppedAdvanced = true;
            DropItem(EnemyGrade.Advanced, dropPos);
        }

        if (!droppedElite && ratio <= 0f)
        {
            droppedElite = true;
            DropItem(EnemyGrade.Elite, dropPos);
        }
    }

    private void DropItem(EnemyGrade grade, Vector3 position)
    {
        if(Core.ItemManager.Instance == null)
        {
            Log.Warning($"[{nameof(WBH_EnemyHiddenPattern)}]" + "ItemManager 가 없어 아이템을 드롭하지 못했습니다.");
            return;
        }

        Core.ItemManager.Instance.DropRandomItem(grade, position);
    }

    private void TrySelectFleeDestination()
    {
        Transform target = owner.Target;

        if (target == null)
            return;

        Vector3 origin = owner.transform.position;
        Vector3 awayDirection = origin - target.position;
        awayDirection.y = 0f;

        if(awayDirection.sqrMagnitude < 0.001f)
        {
            awayDirection = owner.transform.forward;
        }

        awayDirection.Normalize();

        int candidateCount = Mathf.Max(1, settings.candidateCount);
    }
}
