using EnemySystem;
using ItemSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mirror 테스트 적의 서버 확정 체력과 데미지를 기존 Act1 표시 자산으로 보여주는 클라이언트 전용 어댑터다.
/// <para>BH 원본 <c>WBH_EnemyView</c>는 로컬 <c>WBH_EnemyStatus</c> 이벤트와 초기화된 풀을 전제로 하므로
/// 네트워크 적에서는 끈 상태를 유지한다. 이 복제본은 <c>NetworkEnemyAuthority_MirrorTest</c>의 복제 결과만 읽는다.</para>
/// <para>체력바는 적 Prefab에 이미 있는 Slider를 재사용하고, 데미지 숫자는 씬에 하나인
/// <c>WBH_DamageTextPoolManager</c>를 지연 탐색해 모든 클라이언트에서 같은 서버 판정값을 표시한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkEnemyAuthority_MirrorTest))]
public sealed class NetworkEnemyCombatView_MirrorTest : MonoBehaviour
{
    [SerializeField] private NetworkEnemyAuthority_MirrorTest authority;
    [SerializeField] private Transform damageTextRoot;
    [SerializeField] private GameObject healthBarRoot;
    [SerializeField] private Slider healthBarSlider;
    [SerializeField] private WBH_EnemyBossPhaseView_Act1 bossPhaseView;

    [SerializeField] private Vector2 damageTextSpacingPixels = new Vector2(56f, 28f);

    private WBH_FloatTextPoolManager damageTextPool;
    private Camera mainCamera;
    private double damageTextBurstStartedAt = double.NegativeInfinity;
    private int damageTextBurstIndex;
    private bool missingPoolReported;
    private bool bossPhaseTwoApplied;
    private MirrorAct1BossPhase observedBossPhase;

    public uint PresentedDamageTextCount { get; private set; }
    public uint PresentedBurnResponseCount { get; private set; }
    public bool BossPhaseTwoApplied => bossPhaseTwoApplied;

    private void Awake()
    {
        ResolveReferences();
        if (healthBarRoot != null)
            healthBarRoot.SetActive(false);
    }

    private void Start()
    {
        if (Mirror.NetworkClient.active)
            RefreshHealthBar();
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

        if (mainCamera == null)
            mainCamera = Camera.main;
        RefreshHealthBar();

        if (healthBarRoot != null && healthBarRoot.activeSelf && mainCamera != null)
            healthBarRoot.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);
    }

    private void RefreshHealthBar()
    {
        if (authority == null)
            return;

        RefreshBossPhaseView();
        if (healthBarRoot == null || healthBarSlider == null)
            return;

        bool visible = !authority.IsDead && authority.MaxHealth > 0f;
        if (healthBarRoot.activeSelf != visible)
            healthBarRoot.SetActive(visible);

        healthBarSlider.minValue = 0f;
        healthBarSlider.maxValue = 1f;
        healthBarSlider.value = visible
            ? Mathf.Clamp01(authority.CurrentHealth / authority.MaxHealth)
            : 0f;
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
        if (phase == MirrorAct1BossPhase.PhaseOne)
        {
            bossPhaseTwoApplied = false;
            bossPhaseView.SetPhaseOne();
            return;
        }

        if (phase == MirrorAct1BossPhase.TransitionArmor)
        {
            bossPhaseTwoApplied = true;
            Random.State previousState = Random.state;
            Random.InitState(authority.BossPhaseVisualSeed);
            bossPhaseView.PlayPhaseTwoTransition(null);
            Random.state = previousState;
            return;
        }

        if (phase == MirrorAct1BossPhase.PhaseTwo && !bossPhaseTwoApplied)
        {
            bossPhaseTwoApplied = true;
            bossPhaseView.SetPhaseTwo();
        }
    }

    public void ShowDamage(float damage, bool critical,
        ElementType element, Vector3 enemyPosition)
    {
        if (!float.IsFinite(damage) || damage <= 0f)
            return;

        WBH_DamageText text = RentDamageText(enemyPosition);
        if (text == null)
            return;
        text.Show(text.transform.position, new WBH_DamageResult(null, damage, critical, element));
        PresentedDamageTextCount++;
    }

    /// <summary>서버가 보낸 화상 반응을 숫자와 겹치지 않게 같은 풀에서 표시합니다.</summary>
    public void ShowBurnResponse(bool immune, Vector3 enemyPosition)
    {
        WBH_DamageText text = RentDamageText(enemyPosition);
        if (text == null)
            return;
        text.ShowBurnResponse(text.transform.position, immune);
        PresentedBurnResponseCount++;
    }

    /// <summary>숫자와 상태 문구가 카메라·풀·앵커·연속 타격 배치를 공유합니다.</summary>
    private WBH_DamageText RentDamageText(Vector3 enemyPosition)
    {
        if (!Mirror.NetworkClient.active || !isActiveAndEnabled)
            return null;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return null;

        if (damageTextPool == null)
            damageTextPool = FindFirstObjectByType<WBH_FloatTextPoolManager>(FindObjectsInactive.Exclude);
        if (damageTextPool == null)
        {
            if (!missingPoolReported)
            {
                missingPoolReported = true;
                Debug.LogWarning("[NetworkEnemyCombatView_MirrorTest] 씬의 데미지 텍스트 풀이 없습니다.", this);
            }
            return null;
        }

        Vector3 anchorOffset = damageTextRoot != null
            ? damageTextRoot.position - transform.position
            : Vector3.up * 1.5f;
        Vector3 position = OffsetDamageText(enemyPosition + anchorOffset);
        WBH_DamageText damageText = damageTextPool.GetDamageText();
        if (damageText == null)
            return null;

        damageText.Initialize(damageTextPool);
        damageText.transform.position = position;
        return damageText;
    }

    private Vector3 OffsetDamageText(Vector3 position)
    {
        double now = Time.unscaledTimeAsDouble;
        if (now < damageTextBurstStartedAt || now - damageTextBurstStartedAt >= 0.12d)
        {
            damageTextBurstStartedAt = now;
            damageTextBurstIndex = 0;
        }

        int index = damageTextBurstIndex++;
        Vector3 screen = mainCamera.WorldToScreenPoint(position);
        if (screen.z <= 0f)
            return position;

        screen.x += ((index & 1) == 0 ? -0.5f : 0.5f) * damageTextSpacingPixels.x;
        screen.y += (index / 2) * damageTextSpacingPixels.y;
        return mainCamera.ScreenToWorldPoint(screen);
    }

    private void ResolveReferences()
    {
        authority ??= GetComponent<NetworkEnemyAuthority_MirrorTest>();
        bossPhaseView ??= GetComponent<WBH_EnemyBossPhaseView_Act1>();

        if (damageTextRoot == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "DamageTextRoot")
                    continue;

                damageTextRoot = child;
                break;
            }
        }

        healthBarSlider ??= GetComponentInChildren<Slider>(true);
        if (healthBarRoot == null && healthBarSlider != null)
        {
            Canvas canvas = healthBarSlider.GetComponentInParent<Canvas>(true);
            healthBarRoot = canvas != null ? canvas.gameObject : healthBarSlider.gameObject;
        }
    }
}
