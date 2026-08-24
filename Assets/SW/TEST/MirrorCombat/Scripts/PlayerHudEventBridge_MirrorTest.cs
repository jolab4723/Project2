using UnityEngine;

/// <summary>
/// 김성우 MergeTest 씬의 <see cref="KY_HUDManager"/> 디자인을 Mirror 테스트의 로컬
/// <see cref="PlayerContext"/>에 연결하는 중계기다.
/// <para>원본 <c>PlayerHudEventBridge</c>와 달리 Inspector의 고정 플레이어를 사용하지 않고
/// <see cref="Bind"/>로 전달받은 로컬 플레이어의 Health, Mana, Stat 이벤트만 구독한다.</para>
/// <para>UI 디자인과 기존 <see cref="KY_GameEvents"/> 형식은 그대로 재사용하며 플레이어 상태를
/// 새로 저장하지 않는다.</para>
/// <para>WJ 원본 버프 HUD의 싱글톤 탐색 대신 <see cref="BuffIconUIContainer_MirrorTest"/>에도
/// 같은 로컬 Context의 Buff Manager를 명시적으로 전달한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerHudEventBridge_MirrorTest : MonoBehaviour
{
    private PlayerHealthManager health;
    private PlayerManaManager mana;
    private PlayerStatManager stats;
    private MirrorCooldownHud_MirrorTest cooldownHud;
    private BuffIconUIContainer_MirrorTest buffHud;

    public PlayerContext BoundContext { get; private set; }

    public void Bind(PlayerContext context)
    {
        if (BoundContext == context)
        {
            PublishAll();
            return;
        }

        Unbind();
        BoundContext = context;

        if (context == null)
            return;

        health = context.Health;
        mana = context.Mana;
        stats = context.Stats;
        cooldownHud ??= GetComponent<MirrorCooldownHud_MirrorTest>();
        buffHud ??= GetComponentInChildren<BuffIconUIContainer_MirrorTest>(true);

        if (health != null)
            health.OnHealthChanged += PublishHealth;

        if (mana != null)
            mana.OnManaChanged += PublishMana;

        if (stats?.Stat != null)
            stats.Stat.OnStatChanged += PublishExperience;

        cooldownHud?.Bind(context);
        buffHud?.Bind(context.Buffs);

        PublishAll();
    }

    public void Unbind()
    {
        if (health != null)
            health.OnHealthChanged -= PublishHealth;

        if (mana != null)
            mana.OnManaChanged -= PublishMana;

        if (stats?.Stat != null)
            stats.Stat.OnStatChanged -= PublishExperience;

        cooldownHud?.Unbind();
        buffHud?.Unbind();

        BoundContext = null;
        health = null;
        mana = null;
        stats = null;
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void PublishAll()
    {
        PublishHealth();
        PublishMana();
        PublishExperience();
    }

    private void PublishHealth()
    {
        if (health != null && health.MaxHealth > 0f)
            KY_GameEvents.HealthChanged(health.CurrentHealth, health.MaxHealth);
    }

    private void PublishMana()
    {
        if (mana != null && mana.MaxMana > 0f)
            KY_GameEvents.ManaChanged(mana.CurrentMana, mana.MaxMana);
    }

    private void PublishExperience()
    {
        if (stats != null)
            KY_GameEvents.ExpChanged(stats.CurrentExp, stats.ExpToNextLevel);
    }
}
