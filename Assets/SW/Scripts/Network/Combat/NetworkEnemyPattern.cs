using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 원본 적 패턴에 서버 명부의 대상과 공격·투사체 권한을 연결한다.
/// Act1 보스의 주기·추격·변신·돌진·점프는 WBH 패턴과 이동/전투가 한 번만 실행한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkEnemyAuthority), typeof(WBH_EnemyPattern))]
public sealed class NetworkEnemyPattern : MonoBehaviour
{
    [SerializeField] private NetworkEnemyAuthority authority;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform grenadePoint;
    private WBH_EnemyPattern pattern;
    private WBH_EnemyCombat combat;
    private WBH_EnemyMovement movement;
    private WBH_EnemyBossPhaseView_Act1 phase;
    private PlayerContext target;
    private readonly List<Transform> serverTargets = new();
    public Transform FirePoint => firePoint != null ? firePoint : transform;
    public Transform GrenadePoint => grenadePoint != null ? grenadePoint : FirePoint;

    [Server]
    public void InitializeServer(NetworkEnemyAuthority owner)
    {
        authority = owner;
        pattern = GetComponent<WBH_EnemyPattern>();
        combat = GetComponent<WBH_EnemyCombat>();
        movement = GetComponent<WBH_EnemyMovement>();
        phase = GetComponent<WBH_EnemyBossPhaseView_Act1>();
        pattern.BindExternalTargets(GetServerTargets);
        combat.ExternalBeginGrab = player => player.GetComponent<FighterSkillAuthority>()?.ServerTryBeginGrab() == true;
        combat.ExternalHoldGrab = (player, point) => player.GetComponent<FighterSkillAuthority>()?.ServerHoldGrab(point);
        combat.ExternalEndGrab = (player, point) => player.GetComponent<FighterSkillAuthority>()?.ServerEndGrab(point);
        GetComponent<WBH_EnemyAnimation>().ExternalSkillRequested = PlayPatternSkill;
        combat.BindExternalActions(() => authority.ServerTryBeginAttack(target),
            () => authority.IsAttackPending,
            (direction, range) => authority.ServerLaunchBossProjectile(direction, range), PlayPatternSkill);
        combat.BindExternalMissile((point, duration, radius) => authority.ServerLaunchBossMissile(point, duration, radius));
        combat.enabled = true;
        if (authority.EnemyInfo.patternID == 101 && phase != null)
        {
            phase.BindPhaseSimulation(state => authority.ServerSetBossPhase(state switch
            {
                WBH_EnemyBossPhaseView_Act1.PhaseState.One => MirrorAct1BossPhase.PhaseOne,
                WBH_EnemyBossPhaseView_Act1.PhaseState.TransitionMissiles => MirrorAct1BossPhase.TransitionMissiles,
                WBH_EnemyBossPhaseView_Act1.PhaseState.TransitionArmor => MirrorAct1BossPhase.TransitionArmor,
                _ => MirrorAct1BossPhase.PhaseTwo
            }));
            authority.ServerSetBossPhase(MirrorAct1BossPhase.PhaseOne);
        }
        enabled = true;
    }

    [Server]
    public void StopServer()
    {
        target = null;
        authority?.ServerSetTarget(null);
        combat?.CancelCurrentAction();
        phase?.CancelTransition();
        phase?.BindPhaseSimulation(null);
        if (pattern != null) { pattern.Die(); pattern.BindExternalTargets(null); }
        if (combat != null)
        {
            combat.ExternalBeginGrab = null;
            combat.ExternalHoldGrab = null;
            combat.ExternalEndGrab = null;
            combat.BindExternalActions(null, null, null, null);
            combat.BindExternalMissile(null);
            combat.enabled = false;
        }
        GetComponent<WBH_EnemyAnimation>().ExternalSkillRequested = null;
        movement?.Stop();
        enabled = false;
    }

    private void Update()
    {
        if (authority == null || !authority.isServer || authority.IsDead || pattern == null) return;
        var targets = GetServerTargets();
        if (target == null || !targets.Contains(target.transform))
        {
            target = null;
            float closest = float.MaxValue;
            foreach (Transform candidate in targets)
            {
                float distance = (candidate.position - transform.position).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance;
                target = candidate.GetComponent<PlayerContext>();
            }
        }
        authority.ServerSetTarget(target);
        if (target == null) { movement.Stop(); return; }
        pattern.TickWithTarget(target.transform, Time.deltaTime);
        target = pattern.Target != null ? pattern.Target.GetComponent<PlayerContext>() : null;
        authority.ServerSetTarget(target);
    }

    private List<Transform> GetServerTargets()
    {
        serverTargets.Clear();
        if (NetworkManager.singleton is MirrorNetworkManager manager)
            foreach (PlayerContext context in manager.ServerPlayerContexts)
                if (context != null && context.gameObject.activeInHierarchy && context.RuntimeState?.IsDead != true &&
                    context.GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent != true)
                    serverTargets.Add(context.transform);
        return serverTargets;
    }

    private void PlayPatternSkill(int skill)
    {
        if (authority.EnemyInfo.patternID != 101) { authority.ServerPlayPatternSkill(skill); return; }
        switch (skill)
        {
            case 2: authority.ServerRecordBossBurst(); break;
            case 3: authority.ServerRecordBossBarrage(); break;
            case 4: case 5: authority.ServerRecordBossMissileVolley(); break;
            case 6: authority.ServerRecordBossJump(); authority.ServerPlayBossJumpAnimation(); return;
            case 7: authority.ServerRecordBossDash(); break;
        }
        authority.ServerPlayBossSkill(skill);
    }
}
