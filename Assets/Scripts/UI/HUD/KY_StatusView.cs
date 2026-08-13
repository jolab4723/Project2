using UnityEngine;
using UnityEngine.UI;

// HP, MP, EXP 슬라이더를 관리하는 코드
public class KY_StatusView : MonoBehaviour
{
    public Slider healthSlider;
    public Slider manaSlider;
    public Slider expSlider;

    public void UpdateHealth(float current, float max)
    {
        SetSlider(healthSlider, current, max);
    }

    public void UpdateMana(float current, float max)
    {
        SetSlider(manaSlider, current, max);
    }

    public void UpdateExp(float current, float max)
    {
        SetSlider(expSlider, current, max);
    }

    private void SetSlider(Slider slider, float current, float max)
    {
        if (slider == null)
            return;
        slider.value = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }
}