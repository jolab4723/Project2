using EnemySystem;
using ItemSystem;
using UnityEngine;

/// <summary>
/// 서버 확정 체력과 피해를 공통 <c>WBH_EnemyView</c>에 전달하는 Mirror 클라이언트 표시 어댑터다.
/// <para>체력바, 피격 점멸과 텍스트 풀·배치는 공통 View가 처리하고,
/// 이 어댑터는 <c>NetworkEnemyAuthority</c>의 복제 결과와 보스 단계만 읽는다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkEnemyAuthority))]
public sealed class NetworkEnemyCombatView : MonoBehaviour
{
    [SerializeField] private NetworkEnemyAuthority authority;
    [SerializeField] private WBH_EnemyView productionView;
    [SerializeField] private WBH_EnemyBossPhaseView_Act1 bossPhaseView;

    private bool missingReferencesReported;
    private bool hasPresentedHealth;
    private float presentedCurrentHealth;
    private float presentedMaxHealth;
    private bool presentedDead;
    private bool bossPhaseTwoApplied;
    private MirrorAct1BossPhase observedBossPhase;

    public uint PresentedDamageTextCount { get; private set; }
    public uint PresentedBurnResponseCount { get; private set; }
    public bool BossPhaseTwoApplied => bossPhaseTwoApplied;

    private void Awake()
    {
        ResolveReferences();
        if (productionView != null)
            productionView.BindExternalPresentation(() => Mirror.NetworkClient.active);
        ReportMissingReferences();
    }

    private void Start()
    {
        if (Mirror.NetworkClient.active)
            RefreshPresentation();
    }

    private void OnDisable()
    {
        hasPresentedHealth = false;
        // Host의 서버 전환 Coroutine은 Authority의 OnStopServer에서 정리한다.
        if (!Mirror.NetworkServer.active && bossPhaseView != null)
            bossPhaseView.CancelTransition();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif

    private void LateUpdate()
    {
        if (!Mirror.NetworkClient.active)
            return;

        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        ReportMissingReferences();
        if (authority == null)
            return;

        RefreshBossPhaseView();
        if (productionView == null)
            return;

        float current = authority.CurrentHealth;
        float max = authority.MaxHealth;
        bool dead = authority.IsDead;
        if (hasPresentedHealth && current == presentedCurrentHealth &&
            max == presentedMaxHealth && dead == presentedDead)
            return;

        productionView.PresentHealth(current, max, dead);
        presentedCurrentHealth = current;
        presentedMaxHealth = max;
        presentedDead = dead;
        hasPresentedHealth = true;
    }

    private void RefreshBossPhaseView()
    {
        if (bossPhaseView == null || authority.EnemyInfo?.enemyAttackType != EnemyAttackType.Boss)
        {
            return;
        }

        MirrorAct1BossPhase phase = authority.BossPhase;
        if (phase == observedBossPhase)
            return;

        observedBossPhase = phase;
        if (phase == MirrorAct1BossPhase.Dead)
        {
            if (!Mirror.NetworkServer.active)
                bossPhaseView.CancelTransition();
            return;
        }

        if (phase == MirrorAct1BossPhase.PhaseOne)
        {
            bossPhaseTwoApplied = false;
            bossPhaseView.SetPhaseOne();
            return;
        }

        if ((phase == MirrorAct1BossPhase.TransitionArmor || phase == MirrorAct1BossPhase.PhaseTwo) &&
            !bossPhaseTwoApplied)
        {
            Random.State previousState = Random.state;
            try
            {
                Random.InitState(authority.BossPhaseVisualSeed);
                // 표시만 적용해 Host의 서버 전환 완료 callback을 유지한다.
                bossPhaseView.SetPhaseTwo();
                bossPhaseTwoApplied = true;
            }
            finally
            {
                Random.state = previousState;
            }
        }
    }

    public void ShowDamage(float damage, bool critical,
        ElementType element, Vector3 enemyPosition, uint attackerId = 0, bool selfAttack = false)
    {
        if (!Mirror.NetworkClient.active || !isActiveAndEnabled ||
            !float.IsFinite(damage) || damage <= 0f)
            return;

        ReportMissingReferences();
        if (productionView == null)
            return;

        WBH_ICombat attacker = null;
        if (attackerId != 0 && Mirror.NetworkClient.spawned.TryGetValue(attackerId, out var identity) &&
            identity != null)
        {
            attacker = identity.GetComponent<T_PlayerController>();
            if (attacker == null)
                attacker = identity.GetComponent<WBH_EnemyController>();
        }

        var result = new WBH_DamageResult(attacker, damage, critical, element);
        if (productionView.ShowDamage(result, enemyPosition, selfAttack))
            PresentedDamageTextCount++;
    }

    /// <summary>서버가 보낸 화상 반응도 공통 View의 숫자·상태 문구 배치를 사용합니다.</summary>
    public void ShowBurnResponse(bool immune, Vector3 enemyPosition)
    {
        if (!Mirror.NetworkClient.active || !isActiveAndEnabled)
            return;

        ReportMissingReferences();
        if (productionView != null && productionView.ShowBurnResponse(immune, enemyPosition))
            PresentedBurnResponseCount++;
    }

    private void ResolveReferences()
    {
        if (authority == null)
            authority = GetComponent<NetworkEnemyAuthority>();
        if (productionView == null)
            productionView = GetComponent<WBH_EnemyView>();
        if (bossPhaseView == null)
            bossPhaseView = GetComponent<WBH_EnemyBossPhaseView_Act1>();
    }

    private void ReportMissingReferences()
    {
        if (missingReferencesReported)
            return;

        bool requiresBossPhaseView = authority != null &&
            authority.EnemyInfo?.patternID == 101;
        if (authority != null && productionView != null && (!requiresBossPhaseView || bossPhaseView != null))
            return;

        missingReferencesReported = true;
        Debug.LogWarning("[NetworkEnemyCombatView] Authority, 공통 EnemyView 또는 보스 PhaseView 필수 참조가 없습니다.", this);
    }
}
