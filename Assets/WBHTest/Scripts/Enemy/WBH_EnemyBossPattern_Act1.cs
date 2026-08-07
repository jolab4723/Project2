using UnityEngine;

public class WBH_EnemyBossPattern_Act1 : WBH_IEnemyPattern
{
    private float Phase2HpRatio = 0.3f;
    private float phase1TargetChangeInterval = 10f;

    private float barrageCooldown = 15f;    // 전방 탄막 패턴 쿨타임
    private float missileCooldown = 20f;    // 미사일 발사 패턴 쿨타임
    private float BurstCooldown = 3f;       // 연사 패턴 쿨타임

    private float DashCooldown = 15f;       // 2페) 돌진 쿨타임
    private float JumpCooldown = 20f;       // 2페) 낙하공격 쿨타임

    private float DashTargetRange = 30f;    // 2페) 돌진 타겟 설정 최대 거리(최대 돌진 사거리)
    private float JumpTargetRange = 20f;    // 2페) 낙하공격 타겟 설정 최대 거리(최대 낙하공격 사거리)

    private WBH_EnemyPattern owner;

    private bool isPhaseTwo;

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
    private float missileRecoveryDuration = 0.4f;   
    private float missileTargetSpreadRadius = 4f;   // 타겟 주위 미사일 번짐 반경

    private float jumpDamageRadius = 5f;            // 낙하공격 데미지 적용반경
    private float jumpDuration = 0.8f;              // 체공 시간
    private float jumpRecoveryDuration = 0.4f;      // 인디케이터 표시 기간

    public void Initialize(WBH_EnemyPattern owner)
    {
        this.owner = owner;
        phase1TargetTimer = phase1TargetChangeInterval;
    }

    public void Tick(float deltaTime)
    {
        DecreaseTimers(deltaTime);

        if(!isPhaseTwo && owner.HealthRatio <= Phase2HpRatio)
        {
            EnterPhaseTwo();
        }

        if (owner.Combat.IsActionInProgress)
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
            burstTimer = BurstCooldown;
        }
    }

    private void TickPhaseTwo()
    {
        if(jumpTimer <= 0f && owner.TrySelectFarTarget(JumpTargetRange))
        {
            Vector3 landingPos = owner.Target.position;

            if(owner.Combat.TryJumpAttack(landingPos, jumpDamageRadius,jumpDuration, jumpRecoveryDuration))
            {
                jumpTimer = JumpCooldown;
                return;
            }
        }

        if(dashTimer <= 0f 
           && owner.TrySelectFarTarget(DashTargetRange) 
           && owner.Combat.TryDashAttack(DashTargetRange,0.8f, owner.Indicator))
        {
            dashTimer = DashCooldown;
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

    private void EnterPhaseTwo()
    {
        isPhaseTwo = true;

        dashTimer = 0f;
        jumpTimer = 0f;
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

    private Vector3[] CreateMissilePoints()
    {
        Vector3 center = owner.Target.position;
        return new[]
        {
            center, center + new Vector3(missileTargetSpreadRadius, 0f, 0f), center + new Vector3(-missileTargetSpreadRadius, 0f, 0f)
        };
    }
}
