using EnemySystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WBH 보스/엘리트 체력바 Prefab을 Mirror 서버가 확정한 체력 상태에 연결하는 클라이언트 전용 어댑터다.
/// 원본 <see cref="WBH_HighEnemyHpbarView"/>는 로컬 WBH_EnemyStatus 이벤트를 전제로 하므로 비활성화한다.
/// 보스 씬에서는 보스 체력바만, 엘리트 씬에서는 엘리트 체력바만 상호 배타적으로 노출하여 중첩을 원천 방지한다.
/// 보스 완료 결과와 방장의 로비 복귀 버튼도 같은 서버 상태를 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkBossHealthBar_MirrorTest : MonoBehaviour
{
    private const float SearchInterval = 0.25f;

    [SerializeField] private GameObject viewRoot;
    [SerializeField] private GameObject bossBarRoot;
    [SerializeField] private GameObject eliteBarRoot;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Slider eliteHealthSlider;
    [SerializeField] private TMP_Text eliteNameText;
    [SerializeField] private TMP_Text eliteHealthText;
    [SerializeField] private WBH_HighEnemyHpbarView productionView;
    [SerializeField] private NetworkEnemyWaveSpawner_MirrorTest waveSpawner;
    [SerializeField] private GameObject clearRoot;
    [SerializeField] private Button returnToLobbyButton;
    [SerializeField] private TMP_Text returnToLobbyText;

    private NetworkEnemyAuthority_MirrorTest boundEnemy;
    private MirrorTestNetworkManager manager;
    private bool returnRequested;
    private float nextSearchAt;
    private float displayedHealth = float.NaN;
    private float displayedMaxHealth = float.NaN;

    public NetworkEnemyAuthority_MirrorTest BoundBoss => (boundEnemy != null && IsBoss(boundEnemy)) ? boundEnemy : null;
    public NetworkEnemyAuthority_MirrorTest BoundElite => (boundEnemy != null && IsElite(boundEnemy)) ? boundEnemy : null;
    public bool IsVisible => (bossBarRoot != null && bossBarRoot.activeSelf) || (eliteBarRoot != null && eliteBarRoot.activeSelf);
    public bool IsClearVisible => clearRoot != null && clearRoot.activeSelf;

    private void Awake()
    {
        // 1. 씬 내의 모든 WBH_HighEnemyHpbarView(Canvas_HUD 등 포함)를 비활성화하고,
        // 하위의 EliteBar/BossBar 유령 체력바가 중복 노출되지 않도록 정리한다.
        CleanupProductionHighEnemyHpBars();

        // 2. 엘리트 UI 컴포넌트 자동 바인딩 (미할당 시)
        ResolveEliteComponents();

        // 3. 시작 시 모든 바 비활성화
        if (bossBarRoot != null) bossBarRoot.SetActive(false);
        if (eliteBarRoot != null) eliteBarRoot.SetActive(false);
        if (viewRoot != null) viewRoot.SetActive(false);
        SetClearVisible(false);

        if (returnToLobbyButton != null)
            returnToLobbyButton.onClick.AddListener(RequestLobbyReturn);
    }

    private void ResolveEliteComponents()
    {
        if (eliteBarRoot == null) return;
        if (eliteHealthSlider == null)
            eliteHealthSlider = eliteBarRoot.GetComponentInChildren<Slider>(true);
        if (eliteNameText == null || eliteHealthText == null)
        {
            var texts = eliteBarRoot.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in texts)
            {
                if (eliteNameText == null && t.name.Contains("Name"))
                    eliteNameText = t;
                else if (eliteHealthText == null && (t.name.Contains("HP") || t.name.Contains("Label")))
                    eliteHealthText = t;
            }
        }
    }

    private void CleanupProductionHighEnemyHpBars()
    {
        var views = FindObjectsByType<WBH_HighEnemyHpbarView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var view in views)
        {
            if (view == null) continue;
            view.enabled = false;
            for (int i = 0; i < view.transform.childCount; i++)
            {
                var child = view.transform.GetChild(i);
                if (child.name.Contains("Bar"))
                    child.gameObject.SetActive(false);
            }
        }

        var canvasHud = GameObject.Find("Canvas_HUD");
        if (canvasHud != null)
        {
            var highBar = canvasHud.transform.Find("HUDManager/Top/HighEnemyHpBar");
            if (highBar != null)
            {
                var eb = highBar.Find("EliteBar");
                if (eb != null) eb.gameObject.SetActive(false);
                var bb = highBar.Find("BossBar");
                if (bb != null) bb.gameObject.SetActive(false);
            }
        }
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
            BindEnemy(null);
            return;
        }

        if (RefreshClearState())
        {
            BindEnemy(null);
            return;
        }

        if (boundEnemy == null || boundEnemy.IsDead)
        {
            if (Time.unscaledTime < nextSearchAt)
                return;

            nextSearchAt = Time.unscaledTime + SearchInterval;
            BindEnemy(FindTargetEnemy());
        }

        if (boundEnemy == null || boundEnemy.IsDead)
        {
            HideAllBars();
            return;
        }

        RefreshDisplay();
    }

    private void BindEnemy(NetworkEnemyAuthority_MirrorTest enemy)
    {
        if (boundEnemy == enemy)
            return;

        boundEnemy = enemy;
        displayedHealth = float.NaN;
        displayedMaxHealth = float.NaN;

        if (boundEnemy == null)
        {
            HideAllBars();
            return;
        }

        bool isBoss = IsBoss(boundEnemy);
        if (isBoss)
        {
            if (eliteBarRoot != null) eliteBarRoot.SetActive(false);
            if (bossBarRoot != null) bossBarRoot.SetActive(true);
            if (viewRoot != null) viewRoot.SetActive(true);
            if (bossNameText != null)
                bossNameText.text = boundEnemy.EnemyInfo?.enemyName ?? string.Empty;
        }
        else
        {
            if (bossBarRoot != null) bossBarRoot.SetActive(false);
            if (viewRoot != null) viewRoot.SetActive(false);
            if (eliteBarRoot != null) eliteBarRoot.SetActive(true);
            if (eliteNameText != null)
                eliteNameText.text = boundEnemy.EnemyInfo?.enemyName ?? string.Empty;
        }

        RefreshDisplay();
    }

    private void HideAllBars()
    {
        if (bossBarRoot != null && bossBarRoot.activeSelf)
            bossBarRoot.SetActive(false);
        if (eliteBarRoot != null && eliteBarRoot.activeSelf)
            eliteBarRoot.SetActive(false);
        if (viewRoot != null && viewRoot.activeSelf)
            viewRoot.SetActive(false);
    }

    private void RefreshDisplay()
    {
        if (boundEnemy == null) return;

        float currentHealth = boundEnemy.CurrentHealth;
        float maxHealth = boundEnemy.MaxHealth;
        if (Mathf.Approximately(currentHealth, displayedHealth) &&
            Mathf.Approximately(maxHealth, displayedMaxHealth))
        {
            return;
        }

        displayedHealth = currentHealth;
        displayedMaxHealth = maxHealth;
        float ratio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

        bool isBoss = IsBoss(boundEnemy);
        if (isBoss)
        {
            if (healthSlider != null) healthSlider.value = ratio;
            if (healthText != null)
            {
                healthText.SetText(
                    "{0:0} / {1:0}",
                    Mathf.CeilToInt(currentHealth),
                    Mathf.CeilToInt(maxHealth));
            }
        }
        else
        {
            if (eliteHealthSlider != null) eliteHealthSlider.value = ratio;
            if (eliteHealthText != null)
            {
                eliteHealthText.SetText(
                    "{0:0} / {1:0}",
                    Mathf.CeilToInt(currentHealth),
                    Mathf.CeilToInt(maxHealth));
            }
        }
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
            HideAllBars();
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

    private NetworkEnemyAuthority_MirrorTest FindTargetEnemy()
    {
        waveSpawner ??= FindFirstObjectByType<NetworkEnemyWaveSpawner_MirrorTest>();
        bool isBossStage = waveSpawner != null ? waveSpawner.IsBossSession : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Boss");

        NetworkEnemyAuthority_MirrorTest[] enemies =
            FindObjectsByType<NetworkEnemyAuthority_MirrorTest>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        if (isBossStage)
        {
            // 보스 씬: 오직 보스만 탐색 (엘리트는 절대 탐색/노출하지 않음)
            foreach (var enemy in enemies)
            {
                if (enemy != null && !enemy.IsDead && IsBoss(enemy))
                    return enemy;
            }
            return null;
        }
        else
        {
            // 엘리트/일반 씬: 엘리트 몬스터만 탐색
            foreach (var enemy in enemies)
            {
                if (enemy != null && !enemy.IsDead && IsElite(enemy))
                    return enemy;
            }
            return null;
        }
    }

    private static bool IsBoss(NetworkEnemyAuthority_MirrorTest enemy)
    {
        return enemy != null && (enemy.EnemyInfo?.enemyAttackType == EnemyAttackType.Boss || enemy.EnemyInfo?.enemyGrade == EnemyGrade.Boss);
    }

    private static bool IsElite(NetworkEnemyAuthority_MirrorTest enemy)
    {
        return enemy != null && enemy.EnemyInfo?.enemyGrade == EnemyGrade.Elite;
    }
}
