using System.Collections.Generic;
using UnityEngine;

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

    private static readonly Color BasicRangeColor = new Color(1f, 0.15f, 0.1f, 0.35f);
    private static readonly Color CannotControlRangeColor = new Color(1f, 0.5f, 0.1f, 0.35f);
    private static readonly Color DebuffRangeColor = new Color(0.2f, 0.7f, 1f, 0.35f);

    // 페이즈 및 패턴 관련 변수
    private const float PhaseTwoHpRatio = 0.5f;
    private const float TargetChangeInterval = 10f;
    private const float SpecialCooldown = 20f;

    // 1페이즈 기본 패턴 관련 변수
    private const float FireRange = 12f;
    private const int FireBulletCount = 6;
    private const float FireInterval = 0.15f;
    private const float FireTurnSpeed = 540f;

    // 1,2 페이즈 공통 기본 패턴 관련 변수
    private const float ShortDashRange = 5f;
    private const float ShortDashDuration = 0.45f;
    private const float ShortDashReadyDuration = 0.25f;

    private const float ShortSectorRange = 4f;
    private const float ShortSectorAngle = 150f;
    private const float ShortSectorDamageMul = 0.8f;

    private const float WideSectorRange = 5f;
    private const float WideSectorAngle = 180f;
    private const float WideSectorDamageMul = 1f;
    private const float SectorHitDelay = 0.35f;

    // 페이즈 전환 패턴
    private const int TransitionRotationCount = 3;
    private const float TransitionDuration = 4f;
    private const int TransitionBulletCount = 24;
    private const float TransitionBulletRange = 15f;

    // 2페이즈 특수 패턴 관련 변수
    private const float GrabDamageMul = 1.8f;

    private const float FlameRange = 8f;
    private const float FlameRangeOffset = 2f;
    private const float FlameCastRange = FlameRange - FlameRangeOffset;
    private const float FlameApproachSpeedMul = 1.5f;
    private const float FlameAngle = 60f;
    private const float FlameDuration = 2.5f;
    private const float FlameDamageInterval = 0.25f;
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
    private const float SlowPulseDelay =0.5f;

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

        owner.SetPatternDamageBlock(false);
    }

    public void Tick(float deltaTime)
    {
        targetChangeTimer -= deltaTime;

        if(isPhaseTwo)
        {
            specialTimer -= deltaTime;
        }

        if(!isPhaseTwo && !isPhaseTransition && owner.HealthRatio <= PhaseTwoHpRatio)
        {
            BeginPhaseTransition();
            return;
        }

        if (isPhaseTransition || owner.Combat.IsActionInProgress)
            return;

        if(pendingSpecial.HasValue)
        {
            TickPendingSpecial();
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

        TickBasicPatterns();

    }

    private void TickBasicPatterns()
    {
        basicPatterns.Clear();

        float distance = owner.Distance;

        if(!isPhaseTwo && distance > WideSectorRange && distance <= FireRange)
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

        BasicPattern selected = basicPatterns[Random.Range(0, basicPatterns.Count)];

        TryStartBasicPattern(selected);
    }

    private void TryStartBasicPattern (BasicPattern selected)
    {
        switch (selected)
        {
            case BasicPattern.TrackingFire:
                if(owner.Combat.TryTrackingFire(FireBulletCount,FireInterval, FireTurnSpeed, FireRange))
                {
                    owner.enemyAnimation.PlaySkill(TrackingFireSkillId);
                }
                break;
            case BasicPattern.ShortDash:
                if(owner.Combat.TryDashAttackWithRangeVisual(Mathf.Min(owner.Distance, ShortDashRange), ShortDashDuration, owner.DashHitRadius * 2f, ShortDashReadyDuration, BasicRangeColor))
                {
                    owner.enemyAnimation.PlaySkill(ShortDashSkillId);
                }
                break;
            case BasicPattern.ShortSectorAtk:
                if(owner.Combat.TrySectorAttack(ShortSectorRange,ShortSectorAngle,ShortSectorDamageMul, hitDelay : SectorHitDelay))
                {
                    SkillRangeVisual.ShowSector(owner.transform.position, owner.transform.forward, ShortSectorRange, ShortSectorAngle, BasicRangeColor, SectorHitDelay);
                    owner.enemyAnimation.PlaySkill(ShortSectorSkillId);
                }
                break;
            case BasicPattern.WideSectorAtk:
                if (owner.Combat.TrySectorAttack(WideSectorRange, WideSectorAngle, WideSectorDamageMul, hitDelay: SectorHitDelay))
                {
                    SkillRangeVisual.ShowSector(owner.transform.position, owner.transform.forward, WideSectorRange, WideSectorAngle, BasicRangeColor, SectorHitDelay);
                    owner.enemyAnimation.PlaySkill(WideSectorSkillId);
                }
                break;
        }
    }

    private void BeginPhaseTransition()
    {
        isPhaseTransition = true;
        pendingSpecial = null;

        owner.Movement.Stop();
        owner.SetPatternDamageBlock(true);

        bool started = owner.Combat.TrySpinBarrage(TransitionRotationCount, TransitionDuration, TransitionBulletCount, TransitionBulletRange, CompletePhaseTransition);

        if(!started)
        {
            owner.SetPatternDamageBlock(false);
            isPhaseTransition=false;
            return;
        }

        owner.enemyAnimation.PlaySkill(TransitionSkillId);
    }

    private void CompletePhaseTransition()
    {
        owner.SetPatternDamageBlock(false);
        isPhaseTwo = true;
        isPhaseTransition = false;

        specialTimer = SpecialCooldown;
        targetChangeTimer = TargetChangeInterval;
    }

    private void SelectSpecialPattern()
    {
        pendingSpecial = (SpecialPattern)Random.Range(0, 4);

        if(pendingSpecial == SpecialPattern.GrabAndSlam)
        {
            owner.TrySelectFarTarget(float.PositiveInfinity);
        }
    }

    private void TickPendingSpecial()
    {
        bool started = false;
        int skillId = 0;

        switch(pendingSpecial.Value)
        {
            case SpecialPattern.GrabAndSlam:
                owner.Movement.Stop();

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

                started = owner.Combat.TryFlameThrow(FlameRange, FlameAngle, FlameDuration, FlameDamageInterval);

                if(started)
                {
                    SkillRangeVisual.ShowSector(owner.transform.position, owner.transform.forward, FlameRange, FlameAngle, BasicRangeColor, FlameDuration);
                }

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

                started = owner.Combat.TryAreaDamageAndStatus(pulseCenter, SlowPulseRange, SlowPulseDamageMul, slow, hitDelay: SlowPulseDelay);

                if(started)
                {
                    SkillRangeVisual.ShowSector(pulseCenter, owner.transform.forward, SlowPulseRange, 360f, DebuffRangeColor, SlowPulseDelay);
                }

                skillId = SlowPulseSkillId;
                break;
        }
        if (!started)
            return;

        owner.enemyAnimation.PlaySkill(skillId);

        pendingSpecial = null;

        specialTimer = SpecialCooldown;
    }

    private void SetFlameApproachBoost(bool enabled)
    {
        if (isBoosted == enabled)
            return;

        isBoosted = enabled;
        owner.Status.SetPatternMoveSpeedModifier(enabled ? FlameApproachSpeedMul : 1f);
    }

    private int GetActivePlayerCount()
    {
        T_PlayerController[] players = Object.FindObjectsByType<T_PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        int count = 0;

        foreach(T_PlayerController player in players)
        {
            if (player.isActiveAndEnabled)
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

    public void Cleanup()
    {
        SetFlameApproachBoost(false);

        pendingSpecial = null;
        isPhaseTransition = false;

        owner?.SetPatternDamageBlock(false);
    }

}
