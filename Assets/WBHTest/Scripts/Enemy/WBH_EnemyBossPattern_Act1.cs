using UnityEngine;

public class WBH_EnemyBossPattern_Act1 : WBH_IEnemyPattern
{
    private float Phase2HpRatio = 0.3f;
    private float phase1TargetChangeInterval = 10f;

    private float barrageCooldown = 15f;    // 전방 탄막 패턴 쿨타임
    private float missileCooldown = 20f;    // 미사일 발사 패턴 쿨타임
    private float burstCooldown = 3f;       // 연사 패턴 쿨타임

    private float dashCooldown = 15f;       // 2페) 돌진 쿨타임
    private float jumpCooldown = 20f;       // 2페) 낙하공격 쿨타임

    private float dashTargetRange = 30f;    // 2페) 돌진 타겟 설정 사거리
    private float dashDistance = 30f;       // 2페) 최대 돌진 거리
    private float dashDuration = 0.8f;      // 2페) 돌진 시간
    private float dashReadyDuration = 1f;  // 2페) 돌진 대기 시간
    private float jumpTargetRange = 20f;    // 2페) 낙하공격 타겟 설정 최대 거리(최대 낙하공격 사거리)

    private WBH_EnemyPattern owner;
    private WBH_EnemyBossPhaseView_Act1 phaseView;

    private bool isPhaseTwo;
    private bool isPhaseTransition;

    private int transitionMissilePerRingCount = 8; // 페이즈 전환 시 발사할 미사일 수 (내외부 각 8발)
    private float transitionInnerRadius = 7f;     // 페이즈 전환 시 안쪽 원 반지름
    private float transitionOuterRaidus = 13f;     // 페이즈 전환 시 바깥쪽 원 반지름
    private float transitionExplosionRadius = 2f;  // 페이즈 전환 미사일 폭발 반경
    private float transitionWarningDuration = 2f; // 페이즈 전환 미사일 떨어지는 시간(인디케이터 표시 시간)
    private float transitionRecoveryDuration = 2f; // 페이즈 전환 후딜레이

    private float phase1TargetTimer;
    private float barrageTimer;
    private float missileTimer;
    private float burstTimer;
    private float dashTimer;
    private float jumpTimer;

    private int barrageBulletCount = 15;            // 탄막 총알 개수
    private float barrageSpreadAngle = 180f;        // 탄막 각도
    private float barrageRange = 35f;               // 탄막 패턴 사거리
    private float barrageRecoverDuration = 0.5f;    // 탄막 패턴 인디케이터 표시 기간

    private int missileCount = 3;                   // 미사일 개수
    private float missileExplsionRadius = 3f;       // 미사일 폭발 범위
    private float missileWarningDuration = 1.5f;    // 미사일 인디케이터 표시 기간
    private float missileRecoveryDuration = 0.4f;   // 패턴 후딜레이
    private float missileTargetSpreadRadius = 4f;   // 타겟 주위 미사일 번짐 반경

    private float jumpDamageRadius = 5f;            // 낙하공격 데미지 적용반경
    private float jumpDuration = 1.5f;              // 체공 시간
    private float jumpRecoveryDuration = 1f;      // 패턴 후딜레이

    public void Initialize(WBH_EnemyPattern owner)
    {
        this.owner = owner;

        phaseView = owner.GetComponent<WBH_EnemyBossPhaseView_Act1>();
        phaseView?.SetPhaseOne();

        phase1TargetTimer = phase1TargetChangeInterval;
    }

    public void Tick(float deltaTime)
    {
        DecreaseTimers(deltaTime);

        if(!isPhaseTwo && !isPhaseTransition && owner.HealthRatio <= Phase2HpRatio)
        {
            EnterPhaseTwo();
        }

        if (isPhaseTransition || owner.Combat.IsActionInProgress)
            return;

        if(isPhaseTwo)
        {
            TickPhaseTwo();
        }
        else
        {
            TickPhaseOne();
        }
    }

    // 1페이즈 동작
    private void TickPhaseOne()
    {
        owner.Movement.Stop();

        if(phase1TargetTimer <= 0f)
        {
            owner.TrySelectAnotherActivePlayer();
            phase1TargetTimer = phase1TargetChangeInterval;
        }

        if (missileTimer <= 0f)
        {
            Vector3[] impactPoints = CreateMissilePoints();
            if(owner.Combat.TryMissile(impactPoints, missileExplsionRadius,missileWarningDuration, missileRecoveryDuration, owner.IndicatorSpawner))
            {
                missileTimer = missileCooldown;
                return;
            }
        }

        if(barrageTimer <= 0f && owner.Combat.TryBarrage(barrageBulletCount, barrageSpreadAngle, barrageRange, barrageRecoverDuration))
        {
            barrageTimer = barrageCooldown;
            return;
        }

        if(burstTimer <= 0f && owner.Combat.TryShootBurst(5))
        {
            burstTimer = burstCooldown;
        }
    }

    // 2페이즈 동작
    private void TickPhaseTwo()
    {
        if(jumpTimer <= 0f && owner.TrySelectFarTarget(jumpTargetRange))
        {
            Vector3 landingPos = owner.Target.position;

            if(owner.Combat.TryJumpAttack(landingPos, jumpDamageRadius,jumpDuration, jumpRecoveryDuration, owner.IndicatorSpawner))
            {
                jumpTimer = jumpCooldown;
                return;
            }
        }

        if(dashTimer <= 0f 
           && owner.TrySelectFarTarget(dashTargetRange) 
           && owner.Combat.TryDashAttack(Mathf.Min(owner.Distance,dashDistance), dashDuration, owner.IndicatorSpawner, owner.DashHitRadius *2f, dashReadyDuration))
        {
            dashTimer = dashCooldown;
            return;
        }

        if(owner.Distance <= owner.AttackRange)
        {
            owner.Movement.Stop();
            owner.Combat.TryAttack();
            return;
        }
        owner.Movement.Move(owner.Target.position);
    }

    // 페이즈 전환
    private void EnterPhaseTwo()
    {
        if (isPhaseTwo || isPhaseTransition)
            return;

        isPhaseTransition = true;

        owner.Movement.Stop();

        Vector3[] impactPoints = CreatePhaseMissilePoints();

        bool started = owner.Combat.TryMissile(impactPoints, transitionExplosionRadius, transitionWarningDuration, transitionRecoveryDuration, owner.IndicatorSpawner, CompletePhaseTwoTransiton);

        if(!started) // 특이 오류로 페이즈 전환 실패 시 재시도.
        {
            isPhaseTransition = false;
        }
    }

    // 페이즈 전환 완료
    private void CompletePhaseTwoTransiton()
    {
        phaseView?.SetPhaseTwo();

        isPhaseTwo = true;
        isPhaseTransition = false;

        dashTimer = 0f;
        jumpTimer = 0f;
    }

    // 페이즈 전환 시 미사일 위치 (안쪽 8발, 바깥쪽 8발)
    private Vector3[] CreatePhaseMissilePoints()
    {
        int totalCount = transitionMissilePerRingCount * 2;
        Vector3[] points = new Vector3[totalCount];
        Vector3 center = owner.transform.position;

        float angleStep = 360f / transitionMissilePerRingCount;

        for(int i = 0; i < transitionMissilePerRingCount; i++)
        {
            float innerAngle = angleStep * i;

            Vector3 innerDir = Quaternion.Euler(0f, innerAngle, 0f) * Vector3.forward;

            points[i] = center + innerDir * transitionInnerRadius;

            float outerAngle = angleStep * i + angleStep * 0.5f;

            Vector3 outDir = Quaternion.Euler(0f, outerAngle, 0f) * Vector3.forward;

            points[i + transitionMissilePerRingCount] = center + outDir * transitionOuterRaidus;
        }
        return points;
    }

    private void DecreaseTimers(float deltaTime)
    {
        phase1TargetTimer -= deltaTime;
        barrageTimer -= deltaTime;
        missileTimer -= deltaTime;
        burstTimer -= deltaTime;
        dashTimer -= deltaTime;
        jumpTimer -= deltaTime;
    }

    // 1페이즈 미사일 발사 위치. 타겟, 타겟 좌우 
    private Vector3[] CreateMissilePoints()
    {
        Vector3 center = owner.Target.position;
        return new[]
        {
            center, center + new Vector3(missileTargetSpreadRadius, 0f, 0f), center + new Vector3(-missileTargetSpreadRadius, 0f, 0f)
        };
    }
}
