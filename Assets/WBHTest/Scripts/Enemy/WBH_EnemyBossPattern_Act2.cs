using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// act2 보스는 1,2 페이즈로 나뉘며 1페이즈에서는 BasicPattern 만, 2페이즈에서는 SpecialCooldown 마다 SpecialPattern 을 사용한다.
/// TrackingFire : 타겟 방향으로 회전하며 사격 / ShortDash : 타겟 방향으로 짧게 돌진 / ShortSectorAtk : 4 거리, 150도 부채꼴 범위 공격 / WideSectorAtk : 5 거리, 180도 부채꼴 범위 공격
/// 체력이 PhaseTwoHpRatio 비율 이하라면 페이즈를 전환하며 페이즈 전환 시에는 BeginPhaseTransition() 를 실행하여 n바퀴 회전하며 일정 각도마다 탄환을 발사한다.
/// SpecialPattern 은 항상 포효 애니메이션 후 실질적인 패턴을 수행하며 이후 그로기에 걸린다.
/// GrabAndSlam : 가장 먼 플레이어를 향해 돌진하며 돌진 경로의 타겟을 모두 잡아 내려찍는다. / SummonSelfDestruct : 보스 주위에 플레이어 수 * 3 만큼 자폭병을 소환 / FlameThrow : 타겟을 추적한 후 부채꼴 범위 틱데미지 / 
/// SlowPulse : 일정 범위 내 데미지 및 둔화 디버프 부여
/// </summary>
public class WBH_EnemyBossPattern_Act2 : WBH_IEnemyPattern
{
    private enum BasicPattern
    {
        TrackingFire,
        ShortDash,
        ShortSectorAtk,
        WideSectorAtk
    }

    private enum SpecialPattern
    {
        GrabAndSlam,
        SummonSelfDestruct,
        FlameThrow,
        SlowPulse
    }

    private enum GroggyState
    {
        None, DamageWindow, Recovery
    }

    private static readonly Color BasicRangeColor = new Color(1f, 0.15f, 0.1f, 0.35f);
    private static readonly Color CannotControlRangeColor = new Color(1f, 0.5f, 0.1f, 0.35f);
    private static readonly Color DebuffRangeColor = new Color(0.2f, 0.7f, 1f, 0.35f);

    // 페이즈 및 패턴 관련 변수
    private const float PhaseTwoHpRatio = 0.5f;
    private const float TargetChangeInterval = 10f;
    private const float SpecialCooldown = 15f;

    private const float BasicPatternDelay = 1f;
    private float actionDelayTimer;
    private bool wasActionInProgress;

    private const float PatternTurnSpeed = 180f; // 패턴 간 딜레이 중 회전속도
    private const float PatternFacingTolerance = 5f; // 패턴 실행 직전 플레이어와의 각도 보정 (순간회전)

    // 1페이즈 기본 패턴 관련 변수
    private const int MaxTrackingFireCount = 2;
    private int trackingFireCount;
    private const float FireRange = 12f;
    private const int FireBulletCount = 6;
    private const float FireInterval = 0.15f;
    private const float FireTurnSpeed = 540f;

    // 1,2 페이즈 공통 기본 패턴 관련 변수
    private const float ShortDashRange = 5f;
    private const float ShortDashDuration = 0.45f;
    private const float ShortDashReadyDuration = 1f;

    private const float ShortSectorRange = 4f;
    private const float ShortSectorAngle = 150f;
    private const float ShortSectorDamageMul = 0.8f;

    private const float WideSectorRange = 5f;
    private const float WideSectorAngle = 180f;
    private const float WideSectorDamageMul = 1f;
    private const float SectorHitDelay = 1f;

    // 페이즈 전환 패턴
    private const int TransitionRotationCount = 3;
    private const float TransitionDuration = 4f;
    private const int TransitionBulletCount = 48;
    private const float TransitionBulletRange = 15f;
    private WBH_Effect transitionEffect;

    // 2페이즈 특수 패턴 관련 변수
    private const float GrabDamageMul = 1.8f;

    private const float FlameRange = 8f;
    private const float FlameRangeOffset = 2f;
    private const float FlameCastRange = FlameRange - FlameRangeOffset;
    private const float FlameApproachSpeedMul = 1.5f;
    private const float FlameAngle = 60f;
    private const float FlameDuration = 4f;
    private const float FlameDamageInterval = 0.5f;
    private const float FlameReadyDuration = 1.8f;
    private bool isBoosted;

    private const float GrabRoarDuration = 1.8f;
    private const float GrabMaxDashDistance = 30f;
    private const float GrabDashDuration = 1.2f;
    private const float GrabSlamHitDelay = 0.7f;
    private const float GrabRecoveryDuration = 0.8f;

    private const float SlowPulseRange = 7f;
    private const float SlowPulseDamageMul = 0.3f;
    private const float SlowDuration = 5f;
    private const float SlowMul = 0.5f;
    private const float SlowPulseStartDelay = 1.9f; // roar 애니메이션 > attack3 로 전환되는 시점
    private const float SlowPulseHitDelay = 27f / 30f; // attack3 애니메이션 클립 기준 계산(30프레임)
    private const float SlowPulseRecoveryDuration = 2f - SlowPulseHitDelay;

    // 특수 패턴 이후 그로기 관련 변수
    private const float GroggyDuration = 4f;
    private const float GroggyRecoveryDuration = 1.5f;
    private bool isWaitingForSpecial;
    private GroggyState groggyState;
    private float groggyTimer;

    // 애니메이터 SkillId
    private const int TrackingFireSkillId = 1;
    private const int ShortDashSkillId = 2;
    private const int ShortSectorSkillId = 3;
    private const int WideSectorSkillId = 4;
    private const int TransitionSkillId = 5;
    private const int GrabSkillId = 6;
    private const int SummonSkillId = 7;
    private const int FlameSkillId = 8;
    private const int SlowPulseSkillId = 9;
    private const int GroggySkillId = 10;

    private readonly List<BasicPattern> basicPatterns = new(4);
    

    private WBH_EnemyPattern owner;
    private WBH_BossMinionSpawner minionSpawner;

    private bool isPhaseTwo;
    private bool isPhaseTransition;

    private SpecialPattern? pendingSpecial;

    private float targetChangeTimer;
    private float specialTimer;

    public void Initialize(WBH_EnemyPattern owner)
    {
        this.owner = owner;
        minionSpawner = owner.GetComponent<WBH_BossMinionSpawner>();

        isPhaseTwo = false;
        isPhaseTransition = false;
        pendingSpecial = null;

        targetChangeTimer = TargetChangeInterval;
        specialTimer = SpecialCooldown;

        isWaitingForSpecial = false;
        groggyState = GroggyState.None;
        groggyTimer = 0f;

        trackingFireCount = 0;
        actionDelayTimer = 0f;
        wasActionInProgress = owner.Combat.IsActionInProgress;

        owner.enemyAnimation.SetGroggy(false);
        owner.SetPatternDamageBlock(false);
    }

    public void Tick(float deltaTime)
    {
        bool isBasicPatternDelayActive = UpdateBasicPatternDelay(deltaTime);

        if (TickSpecialAftermath(deltaTime))
            return;

        targetChangeTimer -= deltaTime;

        if(isPhaseTwo)
        {
            specialTimer -= deltaTime;
        }

        if (isPhaseTransition || IsActionBusy())
            return;

        if(isBasicPatternDelayActive)
        {
            owner.Movement.Stop();
            RotateTowardsTarget(deltaTime);
            return;
        }

        if (!isPhaseTwo && !isPhaseTransition && owner.HealthRatio <= PhaseTwoHpRatio)
        {
            BeginPhaseTransition();
            return;
        }

        if(pendingSpecial.HasValue)
        {
            TickPendingSpecial(deltaTime);
            return;
        }

        if(targetChangeTimer <= 0f)
        {
            owner.TrySelectAnotherActivePlayer();
            targetChangeTimer = TargetChangeInterval;

            return;
        }

        if(isPhaseTwo && specialTimer <= 0f)
        {
            SelectSpecialPattern();
            return;
        }

        TickBasicPatterns(deltaTime);

    }

    // 특수 패턴 직후 그로기 상태 부여
    private bool TickSpecialAftermath(float deltaTime)
    {
        if(isWaitingForSpecial)
        {
            if (owner.Combat.IsActionInProgress)
                return true;

            isWaitingForSpecial = false;
            BeginGroggy();
            return true;
        }

        if(groggyState == GroggyState.DamageWindow)
        {
            owner.Movement.Stop();
            groggyTimer -= deltaTime;

            if(groggyTimer <= 0f)
            {
                groggyState = GroggyState.Recovery;
                groggyTimer = GroggyRecoveryDuration;

                owner.enemyAnimation.SetGroggy(false);
            }
            return true;
        }

        if(groggyState == GroggyState.Recovery)
        {
            owner.Movement.Stop();
            groggyTimer -= deltaTime;

            if(groggyTimer <= 0f)
            {
                groggyState = GroggyState.None;

                specialTimer = SpecialCooldown;
                targetChangeTimer = TargetChangeInterval;
            }
            return true;
        }
        return false;
    }

    private void BeginGroggy()
    {
        SetFlameApproachBoost(false);

        owner.Movement.Stop();

        owner.SetPatternDamageBlock(false);

        groggyState = GroggyState.DamageWindow;
        groggyTimer = GroggyDuration;

        owner.enemyAnimation.SetGroggy(true);
        owner.enemyAnimation.PlaySkill(GroggySkillId);
    }

    private void TickBasicPatterns(float deltaTime)
    {
        basicPatterns.Clear();

        float distance = owner.Distance;
        bool canUseTrackingFire = trackingFireCount < MaxTrackingFireCount;

        if(!isPhaseTwo && distance > WideSectorRange && distance <= FireRange && canUseTrackingFire)
        {
            basicPatterns.Add(BasicPattern.TrackingFire);
        }

        if(distance <= ShortDashRange)
        {
            basicPatterns.Add(BasicPattern.ShortDash);
        }
        if(distance <= ShortSectorRange)
        {
            basicPatterns.Add(BasicPattern.ShortSectorAtk);
        }
        if(distance <= WideSectorRange)
        {
            basicPatterns.Add(BasicPattern.WideSectorAtk);
        }

        if(basicPatterns.Count == 0)
        {
            owner.Movement.Move(owner.Target.position);
            return;
        }

        owner.Movement.Stop();

        if (!RotateTowardsTarget(deltaTime))
            return;

        BasicPattern selected = basicPatterns[Random.Range(0, basicPatterns.Count)];

        TryStartBasicPattern(selected);
    }

    private void TryStartBasicPattern (BasicPattern selected)
    {
        bool started = false;

        switch (selected)
        {
            case BasicPattern.TrackingFire:
                started = owner.Combat.TryTrackingFire(FireBulletCount, FireInterval, FireTurnSpeed, FireRange);
                if (started)
                {
                    owner.enemyAnimation.PlaySkill(TrackingFireSkillId);
                }
                break;
            case BasicPattern.ShortDash:
                started = owner.Combat.TryDashAttackWithRangeVisual(Mathf.Min(owner.Distance, ShortDashRange), ShortDashDuration, owner.DashHitRadius * 2f, ShortDashReadyDuration, BasicRangeColor);
                if (started)
                {
                    owner.enemyAnimation.PlaySkill(ShortDashSkillId);
                }
                break;
            case BasicPattern.ShortSectorAtk:
                started = owner.Combat.TrySectorAttack(ShortSectorRange, ShortSectorAngle, ShortSectorDamageMul, hitDelay: SectorHitDelay);
                if (started)
                {
                    owner.IndicatorSpawner?.ShowCone(owner.transform.position, owner.transform.forward, ShortSectorRange, ShortSectorAngle, SectorHitDelay, true);
                    owner.enemyAnimation.PlaySkill(ShortSectorSkillId);
                }
                break;
            case BasicPattern.WideSectorAtk:
                started = owner.Combat.TrySectorAttack(WideSectorRange, WideSectorAngle, WideSectorDamageMul, hitDelay: SectorHitDelay);
                if (started)
                {
                    owner.IndicatorSpawner?.ShowCone(owner.transform.position, owner.transform.forward, WideSectorRange, WideSectorAngle, SectorHitDelay, true);
                    owner.enemyAnimation.PlaySkill(WideSectorSkillId);
                }
                break;
        }
        if(started)
        {
            RecordStartBasicPattern(selected);
        }
    }

    private void BeginPhaseTransition()
    {
        isPhaseTransition = true;
        pendingSpecial = null;

        owner.Movement.Stop();
        owner.SetPatternDamageBlock(true);
        owner.enemyAnimation.SetPhaseTransition(true);

        bool started = owner.Combat.TrySpinBarrage(TransitionRotationCount, TransitionDuration, TransitionBulletCount, TransitionBulletRange, CompletePhaseTransition);

        if(!started)
        {
            owner.SetPatternDamageBlock(false);
            owner.enemyAnimation.SetPhaseTransition(false);

            isPhaseTransition=false;
            return;
        }

        owner.enemyAnimation.PlaySkill(TransitionSkillId);
        transitionEffect = owner.EffectSpawner.SpawnPersistentEffect(owner.act2TransitionEffect, owner.transform);
    }

    private void CompletePhaseTransition()
    {
        if (transitionEffect != null)
            transitionEffect.StopEffect();
        transitionEffect = null;

        owner.enemyAnimation.SetPhaseTransition(false);

        owner.SetPatternDamageBlock(false);
        isPhaseTwo = true;
        isPhaseTransition = false;

        specialTimer = SpecialCooldown;
        targetChangeTimer = TargetChangeInterval;

        SelectSpecialPattern(); // 페이즈 전환 직후 특수 패턴 실행
    }

    // 특수패턴 선택
    private void SelectSpecialPattern()
    {
        pendingSpecial = (SpecialPattern)Random.Range(0, 4);

        if(pendingSpecial == SpecialPattern.GrabAndSlam)
        {
            owner.TrySelectFarTarget(float.PositiveInfinity);
        }
    }

    private void TickPendingSpecial(float deltaTime)
    {
        bool started = false;
        int skillId = 0;

        switch(pendingSpecial.Value)
        {
            case SpecialPattern.GrabAndSlam:
                owner.Movement.Stop();
                
                if (!RotateTowardsTarget(deltaTime))
                    return;

                started = owner.Combat.TryGrabAndSlam(owner.Target, roarDuration : GrabRoarDuration, maxDashDistance : GrabMaxDashDistance, dashDuration : GrabDashDuration, slamHitDelay : GrabSlamHitDelay, slamRecoveryDuration : GrabRecoveryDuration, collisionRadius : owner.DashHitRadius, damageMul : GrabDamageMul, indicatorColor : CannotControlRangeColor); 

                skillId = GrabSkillId;
                break;

            case SpecialPattern.SummonSelfDestruct:
                owner.Movement.Stop();

                if(minionSpawner == null)
                {
                    CancelPendingSpecial();
                    return;
                }

                int spawnCount = GetActivePlayerCount() * 3;

                started = owner.Combat.TrySummonSelfDestruct(minionSpawner, spawnCount, owner.Target);

                skillId = SummonSkillId;
                break;

            case SpecialPattern.FlameThrow:
                if(owner.Distance > FlameCastRange)
                {
                    SetFlameApproachBoost(true);

                    owner.Movement.Move(owner.Target.position);
                    return;
                }
                SetFlameApproachBoost(false);
                owner.Movement.Stop();

                if (!RotateTowardsTarget(deltaTime))
                    return;

                started = owner.Combat.TryFlameThrow(FlameRange, FlameAngle, FlameDuration, FlameDamageInterval, readyDuration: FlameReadyDuration, indicatorSpawner : owner.IndicatorSpawner);

                skillId = FlameSkillId;
                break;

            case SpecialPattern.SlowPulse:
                if (owner.Distance > SlowPulseRange)
                {
                    owner.Movement.Move(owner.Target.position);
                    return;
                }
                owner.Movement.Stop();

                WBH_StatusEffectData slow = new WBH_StatusEffectData(WBH_StatusEffectType.Slow, SlowDuration, SlowMul);

                Vector3 pulseCenter = owner.transform.position;

                started = owner.Combat.TryAreaDamageAndStatus(pulseCenter, SlowPulseRange, SlowPulseDamageMul, slow, startDelay: SlowPulseStartDelay, hitDelay: SlowPulseHitDelay, SlowPulseRecoveryDuration,
                                                              onStarted: () => { owner.IndicatorSpawner?.ShowCircle(pulseCenter, SlowPulseRange, SlowPulseHitDelay, true); });

                skillId = SlowPulseSkillId;
                break;
        }
        if (!started)
            return;

        trackingFireCount = 0;

        owner.enemyAnimation.PlaySkill(skillId);

        pendingSpecial = null;
        isWaitingForSpecial = true;
    }

    // 특수패턴-화염방사 시, 접근 이속 향상.
    private void SetFlameApproachBoost(bool enabled)
    {
        if (isBoosted == enabled)
            return;

        isBoosted = enabled;
        owner.Status.SetPatternMoveSpeedModifier(enabled ? FlameApproachSpeedMul : 1f);
    }

    // 플레이어 수 체크 (자폭병 소환 시, 플레이어 수 * 3 만큼 소환하기 위함)
    private int GetActivePlayerCount()
    {
        int count = 0;
        // SW 수정: 싱글은 기존 활성 플레이어, 멀티는 서버가 공급한 생존 참가자를 셉니다.
        foreach (Transform player in owner.GetActiveTargets())
        {
            if (player != null && player.gameObject.activeInHierarchy)
                count++;
        }
        return Mathf.Max(1, count);
    }

    private void CancelPendingSpecial()
    {
        SetFlameApproachBoost(false);
        
        pendingSpecial = null;
        specialTimer = 1f;
    }

    // 패턴관련 변수 일괄 초기화
    public void Cleanup()
    {
        if(transitionEffect != null)
            transitionEffect.StopEffect();
        transitionEffect = null;

        SetFlameApproachBoost(false);

        pendingSpecial = null;
        isPhaseTransition = false;
        isWaitingForSpecial = false;

        groggyState = GroggyState.None;
        groggyTimer = 0f;

        trackingFireCount = 0;
        actionDelayTimer = 0f;
        wasActionInProgress = false;

        owner?.enemyAnimation?.SetGroggy(false);
        owner?.enemyAnimation?.SetPhaseTransition(false);
        owner?.SetPatternDamageBlock(false);
    }

    // 패턴간 딜레이 시간 체크. 
    private bool UpdateBasicPatternDelay(float deltaTime)
    {
        bool isActionBusy = IsActionBusy();

        if(isActionBusy)
        {
            wasActionInProgress = true;
            return false;
        }

        if(wasActionInProgress)
        {
            wasActionInProgress = false;
            actionDelayTimer = BasicPatternDelay;
        }

        if(actionDelayTimer <= 0f)
            return false;

        actionDelayTimer = Mathf.Max(0f, actionDelayTimer - deltaTime);
        return actionDelayTimer > 0f;
    }

    private void RecordStartBasicPattern (BasicPattern pattern)
    {
        if(pattern == BasicPattern.TrackingFire)
        {
            trackingFireCount++;
            return;
        }
        trackingFireCount = 0;
    }

    // 타겟을 향해 급하게 회전하는 것을 방지
    private bool RotateTowardsTarget(float deltaTime)
    {
        if (owner.Target == null)
            return false;

        Vector3 dir = owner.Target.position - owner.transform.position;

        dir.y = 0;

        if (dir.sqrMagnitude < 0.001f)
            return true;

        Quaternion targetRotation = Quaternion.LookRotation(dir.normalized);

        float angle = Quaternion.Angle(owner.transform.rotation, targetRotation);

        if (angle <= PatternFacingTolerance)
            return true;

        owner.transform.rotation = Quaternion.RotateTowards(owner.transform.rotation, targetRotation, PatternTurnSpeed * deltaTime);

        return false;
    }

    // 패턴 코루틴 혹은 패턴 애니메이션이 실행 중 여부를 판단
    private bool IsActionBusy()
    {
        return owner.Combat.IsActionInProgress || owner.enemyAnimation.IsSkillAniPlaying;
    }
}
