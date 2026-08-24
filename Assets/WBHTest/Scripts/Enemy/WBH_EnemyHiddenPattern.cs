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

    private bool droppedNormal;
    private bool droppedAdvanced;
    private bool droppedElite;

    public void Initialize(WBH_EnemyPattern owner)
    {
        this.owner = owner;
        settings = owner.HiddenConfig;

        despawnTime = Time.time + settings.lifeTime;

        nextRepathTime = 0f;
        speedBoostEndTime = 0f;

        droppedNormal = false;
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
            owner.Despawn();
            return;
        }

        UpdateMoveSpeed();

        if (Time.time < nextRepathTime)
            return;

        nextRepathTime = Time.time + settings.repathInterval;

        owner.TrySelectNearTarget();

        TrySelectFleeDestination();
    }
    
    // 피격 시 이속증가
    private void UpdateMoveSpeed()
    {
        bool isBoosted = Time.time < speedBoostEndTime;

        float multiplier = isBoosted ? settings.damagedSpeedMultiplier : 1f;

        owner.Movement.SetMoveSpeed(owner.CurrentMoveSpeed * multiplier);
    }

    // 이속증가 중 피격 시 효과 연장
    private void HandleDamaged(WBH_DamageResult result)
    {
        speedBoostEndTime = Time.time + settings.damagedSpeedDuration;
    }

    // Hp 비율에 따라 아이템 드랍. 추후 골드 추가 예정. !@
    private void HandleHpChanged(float currentHp, float maxHp)
    {
        if (maxHp <= 0f)
            return;

        float ratio = currentHp / maxHp;
        Vector3 dropPos = owner.transform.position;

        if(!droppedNormal && ratio <= 0.6f)
        {
            droppedNormal = true;
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

    // 타겟 회피 방식.
    // 가장 가까운 플레이어 선택 > 반대방향 위주로 부채꼴 후보 지점 생성 > NavMesh 가능여부 판단 > 플레이어로부터 멀고 첫 이동방향이 플레이어 방향이 아닌 후보 지점으로 이동 > repathInterval 마다 경로 재탐색
    private void TrySelectFleeDestination()
    {
        Transform target = owner.Target;

        if (target == null)
            return;

        Vector3 origin = owner.transform.position;
        Vector3 awayDir = origin - target.position;
        awayDir.y = 0f;

        if(awayDir.sqrMagnitude < 0.001f)
        {
            awayDir = owner.transform.forward;
        }

        awayDir.Normalize();

        int candidateCount = Mathf.Max(1, settings.candidateCount);

        float bestScore = float.NegativeInfinity;

        Vector3 bestDestination = default;
        bool foundDestination = false;

        for(int i = 0; i < candidateCount; i++)
        {
            float ratio = candidateCount == 1 ? 0.5f : i / (candidateCount - 1f);

            float angle = Mathf.Lerp(-settings.maxFleeAngle, settings.maxFleeAngle, ratio);

            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * awayDir;

            Vector3 candidate = origin + dir * settings.fleeDistance;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, settings.navMeshSampleRadius, owner.Movement.AreaMask))
                continue;
            if (!owner.Movement.TryCalculatePath(hit.position, fleePath))
                continue;

            float score = CalculateCandidateScore(origin,target.position, awayDir, hit.position,fleePath);

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestDestination = hit.position;
            foundDestination = true;
        }

        if(foundDestination)
        {
            owner.Movement.Move(bestDestination);
        }
    }

    // 후보경로가 타겟과 멀더라도 첫번째 방향전환이 타겟쪽으로 꺾이는 후보경로가 있을 수 있기에 가중치 줘서 점수를 낮춤
    private float CalculateCandidateScore(Vector3 origin, Vector3 targetPos, Vector3 awayDir, Vector3 candidate, NavMeshPath path)
    {
        float targetDistanceScore = (candidate - targetPos).sqrMagnitude;

        float firstDirScore = 0f;

        if(path.corners.Length >= 2)
        {
            Vector3 firstDir = path.corners[1] - origin; // 첫 번째 코너지점 - 현재 위치

            firstDir.y = 0f;

            if(firstDir.sqrMagnitude > 0.001f)
            {
                firstDir.Normalize();

                firstDirScore = Vector3.Dot(firstDir, awayDir) * settings.initialDirectionWeight;
            }
        }

        return targetDistanceScore + firstDirScore;
    }

    public void Cleanup()
    {
        if (owner == null)
            return;

        if(owner.Status != null)
        {
            owner.Status.OnDamaged -= HandleDamaged;
            owner.Status.OnHpChanged -= HandleHpChanged;
        }

        owner.Movement.SetMoveSpeed(owner.CurrentMoveSpeed);
    }
}
