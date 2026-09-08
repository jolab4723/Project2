using EnemySystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WBH 보스 체력바 Prefab을 Mirror 서버가 확정한 보스 체력에 연결하는 클라이언트 전용 어댑터다.
/// 원본 <see cref="WBH_HighEnemyHpbarView"/>는 로컬 WBH_EnemyStatus 이벤트를 전제로 하므로 비활성화한다.
/// 보스 완료 결과와 방장의 로비 복귀 버튼도 같은 서버 상태를 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkBossHealthBar_MirrorTest : MonoBehaviour
{
    private const float BossSearchInterval = 0.25f;

    [SerializeField] private GameObject viewRoot;
    [SerializeField] private GameObject bossBarRoot;
    [SerializeField] private GameObject eliteBarRoot;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private WBH_HighEnemyHpbarView productionView;
    [SerializeField] private NetworkEnemyWaveSpawner_MirrorTest waveSpawner;
    [SerializeField] private GameObject clearRoot;
    [SerializeField] private Button returnToLobbyButton;
    [SerializeField] private TMP_Text returnToLobbyText;

    private NetworkEnemyAuthority_MirrorTest boundBoss;
    private MirrorTestNetworkManager manager;
    private bool returnRequested;
    private float nextBossSearchAt;
    private float displayedHealth = float.NaN;
    private float displayedMaxHealth = float.NaN;

    public NetworkEnemyAuthority_MirrorTest BoundBoss => boundBoss;
    public bool IsVisible => viewRoot != null && viewRoot.activeSelf;
    public bool IsClearVisible => clearRoot != null && clearRoot.activeSelf;

    private void Awake()
    {
        if (productionView != null)
            productionView.enabled = false;

        if (eliteBarRoot != null)
            eliteBarRoot.SetActive(false);

        SetVisible(false);
        SetClearVisible(false);
        if (returnToLobbyButton != null)
            returnToLobbyButton.onClick.AddListener(RequestLobbyReturn);
    }

    private void Start()
    {
        manager = Mirror.NetworkManager.singleton as MirrorTestNetworkManager;
        if (manager != null) manager.AdmissionStatusChanged += HandleReturnRejected;
    }

    private void OnDestroy()
    {
        if (returnToLobbyButton != null) returnToLobbyButton.onClick.RemoveListener(RequestLobbyReturn);
        if (manager != null) manager.AdmissionStatusChanged -= HandleReturnRejected;
    }

    private void RequestLobbyReturn()
    {
        if (!returnRequested && IsClearVisible && manager != null)
            returnRequested = manager.RequestReturnToLobby();
    }

    private void HandleReturnRejected(string _)
    {
        returnRequested = false;
    }

    private void Update()
    {
        if (!Mirror.NetworkClient.active)
        {
            SetClearVisible(false);
            BindBoss(null);
            return;
        }

        if (RefreshClearState())
        {
            BindBoss(null);
            return;
        }

        if (boundBoss == null || boundBoss.IsDead)
        {
            if (Time.unscaledTime < nextBossSearchAt)
                return;

            nextBossSearchAt = Time.unscaledTime + BossSearchInterval;
            BindBoss(FindLivingBoss());
        }

        if (boundBoss == null || boundBoss.IsDead)
        {
            SetVisible(false);
            return;
        }

        RefreshDisplay();
    }

    private void BindBoss(NetworkEnemyAuthority_MirrorTest boss)
    {
        if (boundBoss == boss)
            return;

        boundBoss = boss;
        displayedHealth = float.NaN;
        displayedMaxHealth = float.NaN;

        if (boundBoss == null)
        {
            SetVisible(false);
            return;
        }

        if (bossNameText != null)
            bossNameText.text = boundBoss.EnemyInfo?.enemyName ?? string.Empty;

        SetVisible(true);
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        float currentHealth = boundBoss.CurrentHealth;
        float maxHealth = boundBoss.MaxHealth;
        if (Mathf.Approximately(currentHealth, displayedHealth) &&
            Mathf.Approximately(maxHealth, displayedMaxHealth))
        {
            return;
        }

        displayedHealth = currentHealth;
        displayedMaxHealth = maxHealth;
        float ratio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

        if (healthSlider != null)
            healthSlider.value = ratio;

        if (healthText != null)
        {
            healthText.SetText(
                "{0:0} / {1:0}",
                Mathf.CeilToInt(currentHealth),
                Mathf.CeilToInt(maxHealth));
        }
    }

    private void SetVisible(bool visible)
    {
        if (viewRoot != null && viewRoot.activeSelf != visible)
            viewRoot.SetActive(visible);

        if (bossBarRoot != null && bossBarRoot.activeSelf != visible)
            bossBarRoot.SetActive(visible);
    }

    private bool RefreshClearState()
    {
        waveSpawner ??= FindFirstObjectByType<NetworkEnemyWaveSpawner_MirrorTest>();
        bool visible = waveSpawner != null &&
                       waveSpawner.IsBossSession &&
                       waveSpawner.SessionPhase == MirrorTestSessionPhase.Completed;
        SetClearVisible(visible);
        if (visible)
        {
            bool leader = manager != null && manager.CanLocalClientControlSession;
            if (returnToLobbyButton != null) returnToLobbyButton.interactable = leader && !returnRequested;
            if (returnToLobbyText != null)
            {
                if (returnRequested) returnToLobbyText.text = "로비로 이동 중…";
                else if (leader) returnToLobbyText.text = "로비로 돌아가기";
                else returnToLobbyText.text = "방장의 복귀를 기다리는 중";
            }
        }
        return visible;
    }

    private void SetClearVisible(bool visible)
    {
        if (clearRoot != null && clearRoot.activeSelf != visible)
            clearRoot.SetActive(visible);
    }

    private static NetworkEnemyAuthority_MirrorTest FindLivingBoss()
    {
        NetworkEnemyAuthority_MirrorTest[] enemies =
            FindObjectsByType<NetworkEnemyAuthority_MirrorTest>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        foreach (NetworkEnemyAuthority_MirrorTest enemy in enemies)
        {
            if (enemy != null &&
                !enemy.IsDead &&
                enemy.EnemyInfo?.enemyAttackType == EnemyAttackType.Boss)
            {
                return enemy;
            }
        }

        return null;
    }
}
