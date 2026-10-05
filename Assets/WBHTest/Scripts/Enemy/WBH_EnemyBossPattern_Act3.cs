using System;
using System.Collections;
using System.Collections.Generic;
using EnemySystem;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class WBH_EnemyBossPattern_Act3 : WBH_IEnemyPattern
{
    private enum Phase
    {
        One, Transition, Two
    }

    private enum BasicPattern
    {
        SectorAttack,
        CircleAttack,
        TeleportNearTarget,
        ChaseSectorAttack,
        GateBarrage,
        Grenades
    }

    private enum SpecialPattern
    {
        SummonSelfDestruct,
        TeleportGrab,
        ThreeStrips
    }

    [Serializable]
    public sealed class Settings
    {
        public Transform mapCenter;
        public Vector2 mapSize = new Vector2(30f, 30f);

        public Transform gate12;
        public Transform gate3;
        public Transform gate9;

        public EnemyDefinitionSO cloneEnemy;
        public Transform[] cloneSpawnPoints;

        [Min(0.1f)] public float cloneTimeLimit = 5f;
        [Min(0.1f)] public float transitionWarning = 1f;
        [Min(0f)] public float transitionDamageMultiplier = 3f;
        [Min(0.1f)] public float successGroggyDuration = 3f;

        [Min(1f)] public float rageAttackMultiplier = 1.5f;
        [Range(0f,1f)] public float rageDefenseMultiplier = 0.7f;

        public bool enableGateVolley = true;
    }

    private sealed class CloneEntry
    {
        public readonly WBH_EnemyController Enemy;
        public readonly WBH_EnemyStatus Status;
        public readonly float InitialMaxHp;
        public readonly Action DeathHandler;

        public bool Defeated;

        public CloneEntry(WBH_EnemyController enemy)
        {
            Enemy = enemy;
            Status = enemy.GetComponent<WBH_EnemyStatus>();
            InitialMaxHp = Status.MaxHealth;

            DeathHandler = () => Defeated = true;
            Status.OnDead += DeathHandler;
        }

        public float RemainingHp =>
            Defeated ? 0f : Status != null ? Mathf.Clamp(Status.CurrentHp, 0f, InitialMaxHp) : InitialMaxHp;

        public void Unsubscribe()
        {
            if (Status != null)
                Status.OnDead -= DeathHandler;
        }
    }

    private const float PhaseTwoHpRatio = 0.6f;
    private const float RageHpRatio = 0.1f;
    private const float SpecialCooldown = 12f;
    private const float ProjectileCooldown = 10f;
    private const float PatternGap = 0.7f;

    private const float SectorAttackRange = 4f;
    private const float CircleRange = 5f;
    private const float ShortSectorRange = 4f;
    private const float AttackWarning = 0.8f;
    private const float Recovery = 0.5f;

    private readonly Settings settings;
    private readonly WBH_ProjectileSpawner projectileSpawner;
    private readonly Func<int> getParticipantCount;

    private readonly List<BasicPattern> basicCandidates = new(6);
    private readonly List<CloneEntry> clones = new();

    private WBH_EnemyPattern owner;
    private WBH_BossMinionSpawner minionSpawner;
    private Transform[] gates;

    private Phase phase;
    private Coroutine transitionRoutine;

    private float nextActionAt;
    private float nextSpecialAt;
    private float nextProjectileAt;
    private float transitionRetryAt;
    private float groggyUntil;

    private bool rageSuppressed;
    private bool isRaging;
    private bool configured;

    public WBH_EnemyBossPattern_Act3(Settings settings, WBH_ProjectileSpawner projectileSpawner, Func<int> getParticipantCount)
    {
        this.settings = settings;
        this.projectileSpawner = projectileSpawner;
        this.getParticipantCount = getParticipantCount;
    }

    public void Initialize(WBH_EnemyPattern owner)
    {
        this.owner = owner;
        minionSpawner = owner.GetComponent<WBH_BossMinionSpawner>();

        configured = settings != null &&
                     settings.mapCenter != null &&
                     settings.mapSize.x > 0f &&
                     settings.mapSize.y > 0f &&
                     settings.gate12 != null &&
                     settings.gate3 != null &&
                     settings.gate9 != null &&
                     settings.cloneEnemy != null &&
                     minionSpawner != null &&
                     projectileSpawner != null &&
                     getParticipantCount != null;

        if(!configured)
        {
            Log.Error("Act3 보스의 맵, 관문, 소환, 인원 참조가 부족합니다.");
            return;
        }

        gates = new[]
        {
            settings.gate12,
            settings.gate3,
            settings.gate9
        };

        phase = Phase.One;
        rageSuppressed = false;
        isRaging = false;

        nextActionAt = nextSpecialAt = nextProjectileAt = 0f;
        transitionRetryAt = groggyUntil = 0f;

        owner.SetPatternDamageBlock(false);
        owner.enemyAnimation.SetPhaseTransition(false);
        owner.enemyAnimation.SetGroggy(false);

        //!@ 버프 적용 만들 것.
    }

    public void Tick(float deltaTime)
    {
        if (!configured || owner.Status.IsDead)
            return;

        if (phase == Phase.Transition)
            return;

        if(groggyUntil > 0f)
        {
            owner.Movement.Stop();
            if (Time.time < groggyUntil)
                return;

            groggyUntil = 0f;
            owner.enemyAnimation.SetGroggy(false);
        }

        if(phase == Phase.Two && !rageSuppressed && !isRaging && owner.HealthRatio <= RageHpRatio)
        {
            isRaging = true;

            //!@ 버프 적용 받게 만들 것.
        }

        if (owner.Combat.IsActionInProgress || owner.enemyAnimation.IsSkillAniPlaying || Time.time < nextActionAt)
            return;

        if(phase == Phase.One && owner.HealthRatio <= PhaseTwoHpRatio && Time.time >= transitionRetryAt)
        {
            phase = Phase.Transition;
            transitionRoutine = owner.StartCoroutine(CoTransition());
            return;
        }

        if (owner.Target == null)
            return;

        if(phase == Phase.Two && Time.time >= nextSpecialAt)
        {
            if(TrySpecialPattern())//!@ 특수 패턴 실행
            {
                nextSpecialAt = Time.time + SpecialCooldown;
                nextActionAt = Time.time + PatternGap;
                return;
            }
        }

        TryBasicPattern();
    }

    private void TryBasicPattern()
    {
        basicCandidates.Clear();

        float distance = owner.Distance;

        if (distance <= SectorAttackRange)
            basicCandidates.Add(BasicPattern.SectorAttack);

        if (distance <= CircleRange)
            basicCandidates.Add(BasicPattern.CircleAttack);

        basicCandidates.Add(BasicPattern.TeleportNearTarget);
        basicCandidates.Add(BasicPattern.ChaseSectorAttack);

        if(Time.time >= nextProjectileAt)
        {
            if (settings.enableGateVolley)
                basicCandidates.Add(BasicPattern.GateBarrage);

            basicCandidates.Add(BasicPattern.Grenades);
        }

        BasicPattern selected = basicCandidates[UnityEngine.Random.Range(0, basicCandidates.Count)];

        bool started = false;
        int skillId = 0;

        switch (selected)
        {
            case BasicPattern.SectorAttack:
                started = owner.Combat.TryWarnedSectorAttack(SectorAttackRange, 120f, 1f, AttackWarning, Recovery, owner.IndicatorSpawner);
                skillId = 1;
                break;
            case BasicPattern.CircleAttack:
                started = owner.Combat.TryCircleAttack(owner.transform.position, CircleRange, 1f, AttackWarning, Recovery, owner.IndicatorSpawner);
                skillId = 2;
                break;
            case BasicPattern.TeleportNearTarget:
                started = owner.Combat.TryTeleportNearTarget(owner.Target, 2.5f, settings.mapCenter, settings.mapSize);
                skillId = 3;
                break;
            case BasicPattern.ChaseSectorAttack:
                started = owner.Combat.TryChaseSectorAttack(ShortSectorRange, 150f, maxAttackCount: 3, maxDuration: 4f, damageMultiplier: 1f, warningDuration: Recovery, indicator: owner.IndicatorSpawner);
                skillId = 4;
                break;
            case BasicPattern.GateBarrage:
                Transform gate = gates[UnityEngine.Random.Range(0, gates.Length)];
                started = owner.Combat.TryGateBarrage(projectileSpawner, gate.position, settings.mapCenter.position - gate.position, bulletCount: 10, spreadAngle: 120f, range: settings.mapSize.magnitude, warningDuration: AttackWarning,
                    indicator: owner.IndicatorSpawner);
                skillId = 5;
                break;
            case BasicPattern.Grenades:
                started = owner.Combat.TryMissile(CreateGrenadePoints(6), explosionRadius: 3f, warningDuration: AttackWarning, recoveryDuration: Recovery, indicatorSpawner: owner.IndicatorSpawner, impactEffectCue:WBH_EnemyEffectCue.None);
                skillId = 6;
                break;
        }

        if (!started)
            return;

        if(selected == BasicPattern.GateBarrage || selected == BasicPattern.Grenades)
        {
            nextProjectileAt = Time.time + ProjectileCooldown;
        }

        owner.enemyAnimation.PlaySkill(skillId);
        nextActionAt = Time.time + PatternGap;
    }

    private bool TrySpecialPattern()
    {
        SpecialPattern selected = (SpecialPattern)UnityEngine.Random.Range(0, 3);

        bool started = false;
        int skillId = 0;

        switch (selected)
        {
            case SpecialPattern.SummonSelfDestruct:
                started = owner.Combat.TrySummonAtGates(minionSpawner, gates, Mathf.Max(1, getParticipantCount()) * 6, owner.Target);
                skillId = 7;
                break;
            case SpecialPattern.TeleportGrab:
                started = owner.Combat.TryTeleportGrabAndBurst(owner.Target, grabRange: 4f, grabAngle: 60f, holdDuration: 1f, burstRadius: 8f, damagePerCapturedPlayer: 1f, indicator: owner.IndicatorSpawner, onMiss: () => BeginGroggy(1f));
                skillId = 8;
                break;
            case SpecialPattern.ThreeStrips:
                int gateIndex = UnityEngine.Random.Range(0, gates.Length);
                BuildStrips(gateIndex, out Vector3[] origins, out Vector3 forward, out float width, out float length);
                started = owner.Combat.TryCenterTeleportAndStrips(settings.mapCenter.position, origins, forward, width, length, warningDuration: AttackWarning, hitInterval: 0.25f, damageMultiplier: 1f, indicator: owner.IndicatorSpawner);
                skillId = 9;
                break;
        }

        if(started)
            owner.enemyAnimation.PlaySkill(skillId);
        
        return started;
    }

    private IEnumerator CoTransition()
    {
        yield return null;

        owner.Movement.Stop();
        owner.SetPatternDamageBlock(true);
        owner.enemyAnimation.SetPhaseTransition(true);
        owner.enemyAnimation.PlaySkill(10);

        int count = Mathf.Max(1, getParticipantCount());

        if(!TrySpawnClones(count))
        {
            AbortTransition();
            yield break;
        }

        float deadline = Time.time + settings.cloneTimeLimit;

        while (Time.time < deadline && GetRemainingCloneRatio() > 0f)
            yield return null;

        float remainingRatio = GetRemainingCloneRatio(); // 클론 hp 비율 반환.
        ClearClones(); // 클론 제거
        transitionRoutine = null;

        if(remainingRatio <= 0f) // 클론 제거 성공 분기
        {
            rageSuppressed = true;
            CompleteTransition(); // 페이즈 전환 완료.
            BeginGroggy(settings.successGroggyDuration);

            nextSpecialAt = Time.time + settings.successGroggyDuration + SpecialCooldown;
            yield break;
        }

        float damageMultiplier = settings.transitionDamageMultiplier * remainingRatio;

        bool started = owner.Combat.TryArenaAttack(settings.mapCenter.position, settings.mapSize, settings.mapCenter.rotation, damageMultiplier, settings.transitionWarning, owner.IndicatorSpawner, onCompleted: CompleteTransition);

        if (!started)
            AbortTransition();
    }

    private bool TrySpawnClones(int count)
    {
        Transform[] points = settings.cloneSpawnPoints;

        if(points == null || points.Length < count)
        {
            Log.Error("소환 포인트가 적절한 숫자로 배치되지 않았습니다.");
            return false;
        }

        for(int i = 0; i < count; i ++)
        {
            if (points[i] == null)
                return false;

            WBH_EnemyController enemy = minionSpawner.SpawnAt(settings.cloneEnemy, points[i].position, owner.Target);

            if (enemy == null)
                return false;

            clones.Add(new CloneEntry(enemy));
        }
        return true;
    }

    private float GetRemainingCloneRatio()
    {
        float totalMax = 0f;
        float totalRemaining = 0f;

        foreach(CloneEntry clone in clones)
        {
            totalMax += clone.InitialMaxHp;
            totalRemaining += clone.RemainingHp;
        }

        return totalMax > 0f ? Mathf.Clamp01(totalRemaining / totalMax) : 1f;
    }

    private void CompleteTransition()
    {
        phase = Phase.Two;

        owner.SetPatternDamageBlock(false);
        owner.enemyAnimation.SetPhaseTransition(false);
        owner.enemyAnimation.ResetSkillAniState();

        nextSpecialAt = Time.time + SpecialCooldown;
        nextActionAt = Time.time + PatternGap;
    }

    private void AbortTransition()
    {
        ClearClones();
        transitionRoutine = null;
        phase = Phase.One;
        transitionRetryAt = Time.time + 1f;

        owner.SetPatternDamageBlock(false);
        owner.enemyAnimation.SetPhaseTransition(false);
        owner.enemyAnimation.ResetSkillAniState();
    }

    private void BeginGroggy(float duration)
    {
        groggyUntil = Time.time + duration;
        owner.Movement.Stop();
        owner.enemyAnimation.SetGroggy(true);
        owner.enemyAnimation.PlaySkill(11); // !@ 차후 애니메이션 구성 시 그로기 시작변수를 스킬 아이디가 아니라 다른 것으로 둔다면 해당 코드 삭제
    }

    private Vector3[] CreateGrenadePoints(int count)
    {
        Vector3[] points = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            float x = UnityEngine.Random.Range(-settings.mapSize.x * 0.45f, settings.mapSize.x * 0.45f);
            float z = UnityEngine.Random.Range(-settings.mapSize.y * 0.45f, settings.mapSize.y * 0.45f);

            points[i] = settings.mapCenter.position + settings.mapCenter.right * x + settings.mapCenter.forward * z;
        }
        return points;
    }

    private void BuildStrips(int gateIndex, out Vector3[] origins, out Vector3 forward, out float width, out float length)
    {
        Vector3 center = settings.mapCenter.position;
        Vector3 right = settings.mapCenter.right;
        Vector3 north = settings.mapCenter.forward;

        origins = new Vector3[3];

        if(gateIndex == 0)
        {
            width = settings.mapSize.x / 3;
            length = settings.mapSize.y;
            forward = -north;

            for (int i=0; i <3; i++)
            {
                origins[i] = center + north * (length * 0.5f) + right * ((i - 1) * width);
            }
        }
        else
        {
            width = settings.mapSize.y / 3f;
            length = settings.mapSize.x;
            forward = gateIndex == 1 ? -right : right;

            for(int i = 0; i < 3; i++)
            {
                origins[i] = center - forward * (length * 0.5f) + north * ((1 - i) * width);
            }
        }
    }

    private void ClearClones()
    {
        foreach(CloneEntry clone in clones)
        {
            clone.Unsubscribe();

            if(!clone.Defeated && clone.Enemy != null && clone.Enemy.gameObject.activeInHierarchy)
            {
                minionSpawner.DespawnTracked(clone.Enemy);
            }
        }
        clones.Clear();
    }

    // 초기화
    public void Cleanup()
    {
        if (transitionRoutine != null)
            owner.StopCoroutine(transitionRoutine);

        transitionRoutine = null;
        ClearClones();

        owner.SetPatternDamageBlock(false);
        owner.Combat.CancelCurrentAction();
        //!@ 광폭화 해제로 버프 미적용
        owner.enemyAnimation.SetPhaseTransition(false);
        owner.enemyAnimation.SetGroggy(false);
        owner.enemyAnimation.ResetSkillAniState();

        configured = false;
    }
}
