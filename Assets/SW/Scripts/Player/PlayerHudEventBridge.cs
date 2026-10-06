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
    private YJ_MinimapPing[] minimapPings = System.Array.Empty<YJ_MinimapPing>();

    public PlayerContext BoundContext { get; private set; }

    private void OnEnable()
    {
        SubscribeState();
        PublishAll();
    }

    private void Start()
    {
        PublishAll();
    }

    private void OnDisable()
    {
        UnsubscribeState();
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
        // 멀티 HUD에는 비활성 사본이 함께 붙은 경우가 있어 실제 표시하는(활성) 컨테이너를 우선 연결한다.
        if (buffHud == null)
        {
            BuffIconUIContainer[] buffHuds = GetComponentsInChildren<BuffIconUIContainer>(true);
            buffHud = System.Array.Find(buffHuds, candidate => candidate.enabled) ??
                      (buffHuds.Length > 0 ? buffHuds[0] : null);
        }

        if (isActiveAndEnabled)
            SubscribeState();

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
        foreach (var view in GetComponentsInChildren<WBH_HighEnemyHpbarView>(true))
            view.Initialize(context != null ? context.transform : null);

        var status = context != null ? context.GetComponent<WBH_PlayerStatus>() : null;
        foreach (var view in GetComponentsInChildren<YJ_PlayerInformation>(true))
            view.BindPlayer(status);
        foreach (var view in GetComponentsInChildren<YJ_HUDInformationView>(true))
            view.BindPlayer(status);
        foreach (var view in GetComponentsInChildren<YJ_MinimapPlayer>(true))
            view.BindPlayer(context != null ? context.transform : null);
        // 적·포탈 미니맵도 첫 번째 T_PlayerController(원격일 수 있음)가 아닌 로컬 플레이어를 중심으로 한다.
        foreach (var view in GetComponentsInChildren<YJ_MinimapEnemy>(true))
            view.BindPlayer(context != null ? context.transform : null);
        foreach (var view in GetComponentsInChildren<YJ_MinimapPortal>(true))
            view.BindPlayer(context != null ? context.transform : null);
        // 멀티 핑은 원본 입력기 이벤트 대신 파티 공유 NetworkPlayerPing에서 받는다.
        minimapPings = GetComponentsInChildren<YJ_MinimapPing>(true);
        foreach (var view in minimapPings)
            view.BindPlayer(context != null ? context.transform : null, null);

        NetworkPlayerPing.Shown -= ShowMinimapPing;
        if (context != null)
            NetworkPlayerPing.Shown += ShowMinimapPing;

        // 스킬·회피·포션 슬롯은 싱글과 같은 뷰를 로컬 플레이어에 연결해 사용한다.
        foreach (var view in GetComponentsInChildren<KY_SkillView>(true))
            view.Bind(context);
        foreach (var view in GetComponentsInChildren<PotionSlotView>(true))
            view.Bind(context != null ? context.Potions : null);
    }

    private void ShowMinimapPing(Vector3 position, float lifetime)
    {
        foreach (var view in minimapPings)
        {
            if (view != null && view.isActiveAndEnabled)
                view.ShowPing(position, lifetime);
        }
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
