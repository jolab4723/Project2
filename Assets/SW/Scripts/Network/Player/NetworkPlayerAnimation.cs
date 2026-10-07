using Mirror;
using UnityEngine;

/// <summary>
/// 공통 WBH_PlayerAnimation의 표시를 재사용하고 클립 이벤트의 소유자·서버 권한을 결정한다.
/// 평타 타격은 소유자의 요청 번호로 확정하며 스킬·부활은 서버에서 한 번 실행한다.
/// 전용 서버도 Animator를 항상 평가해 스킬의 실제 타격 시점을 유지한다.
/// 재접속 사망·부활 스냅샷은 공통 표시 API로 현재 모션에 즉시 반영한다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-150)]
[RequireComponent(typeof(WBH_PlayerAnimation), typeof(NetworkAnimator))]
public sealed class NetworkPlayerAnimation : NetworkBehaviour
{
    private WBH_PlayerAnimation view;
    private NetworkAnimator networkAnimator;
    private WBH_PlayerStateMachine state;
    private PlayerCombatAuthority combat;
    private FighterSkillAuthority skills;
    private PlayerRuntimeStateSync runtime;

    private void Awake()
    {
        view = GetComponent<WBH_PlayerAnimation>();
        networkAnimator = GetComponent<NetworkAnimator>();
        state = GetComponent<WBH_PlayerStateMachine>();
        combat = GetComponent<PlayerCombatAuthority>();
        skills = GetComponent<FighterSkillAuthority>();
        runtime = GetComponent<PlayerRuntimeStateSync>();
        view.BindAuthority(() => isLocalPlayer, () => isServer || isLocalPlayer,
            () => isClient, name => networkAnimator.SetTrigger(name), ExecuteGameplay,
            part => skills?.PlayPendingSkillEffect(part),
            (animationEvent, animator) => skills?.PlayPendingSkillSfx(animationEvent, animator));
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        view.RefreshAnimation();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
        view.RefreshAnimation();
    }

    private void ExecuteGameplay(WBH_PlayerAnimation.GameplayEvent kind, AnimationEvent animationEvent)
    {
        switch (kind)
        {
            case WBH_PlayerAnimation.GameplayEvent.Attack:
                if (isLocalPlayer) combat?.TryConfirmLocalAttackImpactFromAnimation();
                break;
            case WBH_PlayerAnimation.GameplayEvent.EndAttack:
                if (isLocalPlayer && state.Is(PlayerState.Attack)) state.ChangeState(PlayerState.Idle);
                break;
            case WBH_PlayerAnimation.GameplayEvent.EndHit:
                if (isLocalPlayer && state.Is(PlayerState.Hit)) state.ChangeState(PlayerState.Idle);
                break;
            case WBH_PlayerAnimation.GameplayEvent.Skill:
                if (isServer) skills?.ServerExecuteSkillFromAnimation(animationEvent);
                break;
            case WBH_PlayerAnimation.GameplayEvent.Backstep:
                if (isServer) skills?.ServerExecuteBackstepFromAnimation(animationEvent);
                break;
            case WBH_PlayerAnimation.GameplayEvent.EndSkill:
                if (isServer) skills?.EndPendingSkillAnimation();
                break;
            case WBH_PlayerAnimation.GameplayEvent.EndDead:
                if (isServer) runtime?.ServerTryPassiveRevive();
                break;
            case WBH_PlayerAnimation.GameplayEvent.EndRevive:
                if (isServer) runtime?.ServerCompletePassiveRevive();
                break;
        }
    }

    public void PlaySkillAnimation(int skillId, bool charging, float duration) => view.PlayFighterSkillAnimation(skillId, charging, duration);
    public void SetChargingAnimation(bool charging) => view.SetChargingAnimation(charging);
    public void PlayGunnerSkillAnimation(int skillId, int evolution, float duration) => view.PlayGunnerSkillAnimation(skillId, evolution, duration);
    public void CancelSkillAnimation() => view.CancelSkillAnimation();
    public void ApplyAuthoritativeDeath() => view.ApplyAuthoritativeDeath();
    public void ApplyAuthoritativeRevive() => view.ApplyAuthoritativeRevive();
    public void ApplyAuthoritativePassiveRevive() => view.ApplyAuthoritativePassiveRevive();
}
