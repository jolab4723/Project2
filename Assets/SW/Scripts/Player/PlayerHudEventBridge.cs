using UnityEngine;

/// <summary>실제 플레이어의 체력·마나·경험치를 기존 HUD 이벤트에 전달합니다. 로컬 Context를 명시적으로 바인딩할 수 있습니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerHudEventBridge : MonoBehaviour
{
    [SerializeField] private PlayerHealthManager healthManager;
    [SerializeField] private PlayerManaManager manaManager;
    private PlayerStatManager stats;
    private MirrorCooldownHud cooldownHud;
    private BuffIconUIContainer buffHud;

    public PlayerContext BoundContext { get; private set; }

    private void OnEnable() { SubscribeState(); PublishAll(); }
    private void Start() { PublishAll(); }
    private void OnDisable() { UnsubscribeState(); }

    private void Awake()
    {
        if (!MirrorNetworkManager.OwnsGameplay) return;
        // 실제 네트워크 보스/엘리트 바는 별도 NetworkBossHealthBar가 표시한다.
        // 캠프처럼 해당 어댑터가 없는 씬에서도 원본 HUD의 샘플 바가 남지 않게 한다.
        foreach (var view in GetComponentsInChildren<WBH_HighEnemyHpbarView>(true))
            view.gameObject.SetActive(false);
    }

    public void Bind(PlayerContext context)
    {
        if (BoundContext == context)
        {
            PublishAll();
            return;
        }

        Unbind();
        BoundContext = context;

        BindPlayerViews(context);

        if (context == null)
            return;

        healthManager = context.Health;
        manaManager = context.Mana;
        stats = context.Stats;
        cooldownHud ??= GetComponent<MirrorCooldownHud>();
        buffHud ??= GetComponentInChildren<BuffIconUIContainer>(true);

        if (isActiveAndEnabled) SubscribeState();

        cooldownHud?.Bind(context);
        buffHud?.Bind(context.Buffs);

        PublishAll();
    }

    public void Unbind()
    {
        BindPlayerViews(null);
        UnsubscribeState();

        cooldownHud?.Unbind();
        buffHud?.Unbind();

        BoundContext = null;
        healthManager = null;
        manaManager = null;
        stats = null;
    }

    /// <summary>HUD의 레벨·수치 툴팁·방향 표시도 같은 로컬 플레이어만 참조하게 한다.</summary>
    private void BindPlayerViews(PlayerContext context)
    {
        var status = context != null ? context.GetComponent<WBH_PlayerStatus>() : null;
        foreach (var view in GetComponentsInChildren<YJ_PlayerInformation>(true)) view.BindPlayer(status);
        foreach (var view in GetComponentsInChildren<YJ_HUDInformationView>(true)) view.BindPlayer(status);
        foreach (var view in GetComponentsInChildren<YJ_MinimapPlayer>(true))
            view.BindPlayer(context != null ? context.transform : null);
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void SubscribeState()
    {
        UnsubscribeState();
        if (healthManager != null)
            healthManager.OnHealthChanged += PublishHealth;

        if (manaManager != null)
            manaManager.OnManaChanged += PublishMana;

        if (stats?.Stat != null)
            stats.Stat.OnStatChanged += PublishExperience;

    }

    private void UnsubscribeState()
    {
        if (healthManager != null)
            healthManager.OnHealthChanged -= PublishHealth;

        if (manaManager != null)
            manaManager.OnManaChanged -= PublishMana;

        if (stats?.Stat != null)
            stats.Stat.OnStatChanged -= PublishExperience;

    }

    private void PublishAll()
    {
        PublishHealth();
        PublishMana();
        PublishExperience();
    }

    private void PublishHealth()
    {
        if (healthManager != null && healthManager.MaxHealth > 0f)
            KY_GameEvents.HealthChanged(healthManager.CurrentHealth, healthManager.MaxHealth);
    }

    private void PublishMana()
    {
        if (manaManager != null && manaManager.MaxMana > 0f)
            KY_GameEvents.ManaChanged(manaManager.CurrentMana, manaManager.MaxMana);
    }

    private void PublishExperience()
    {
        if (stats != null)
            KY_GameEvents.ExpChanged(stats.CurrentExp, stats.ExpToNextLevel);
    }
}
