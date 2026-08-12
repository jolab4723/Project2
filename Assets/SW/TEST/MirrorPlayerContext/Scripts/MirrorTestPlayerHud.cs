using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MirrorTestPlayerHud : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider manaSlider;
    [SerializeField] private TMP_Text playerText;
    [SerializeField] private TMP_Text potionText;
    [SerializeField] private TMP_Text buffText;

    private PlayerContext context;

    public PlayerContext BoundContext => context;

    public void Bind(PlayerContext newContext)
    {
        Unbind();
        context = newContext;

        if (context == null)
            return;

        context.Health.OnHealthChanged += RefreshHealth;
        context.Mana.OnManaChanged += RefreshMana;
        context.Buffs.OnBuffsChanged += RefreshBuffs;
        context.Potions.ChargesChanged += RefreshPotions;

        RefreshAll();
    }

    public void Unbind()
    {
        if (context == null)
            return;

        context.Health.OnHealthChanged -= RefreshHealth;
        context.Mana.OnManaChanged -= RefreshMana;
        context.Buffs.OnBuffsChanged -= RefreshBuffs;
        context.Potions.ChargesChanged -= RefreshPotions;
        context = null;
        RefreshAll();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void RefreshAll()
    {
        RefreshHealth();
        RefreshMana();
        RefreshBuffs();

        if (context != null)
        {
            RefreshPotions(context.Potions.CurrentCharges, context.Potions.MaxCharges);
            if (playerText != null)
                playerText.text = context.name;
        }
        else
        {
            if (playerText != null)
                playerText.text = "No local player";
            if (potionText != null)
                potionText.text = "Potion -/-";
        }
    }

    private void RefreshHealth()
    {
        if (healthSlider == null)
            return;

        healthSlider.maxValue = context != null ? context.Health.MaxHealth : 1f;
        healthSlider.value = context != null ? context.Health.CurrentHealth : 0f;
    }

    private void RefreshMana()
    {
        if (manaSlider == null)
            return;

        manaSlider.maxValue = context != null ? context.Mana.MaxMana : 1f;
        manaSlider.value = context != null ? context.Mana.CurrentMana : 0f;
    }

    private void RefreshBuffs()
    {
        if (buffText != null)
            buffText.text = context != null ? $"Buffs {context.Buffs.ActiveBuffs.Count}" : "Buffs 0";
    }

    private void RefreshPotions(int current, int max)
    {
        if (potionText != null)
            potionText.text = $"Potion {current}/{max}";
    }
}
