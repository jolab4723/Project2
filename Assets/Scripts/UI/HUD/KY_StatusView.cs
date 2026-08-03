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
        healthSlider.value = current / max;
    }

    public void UpdateMana(float current, float max)
    {
        manaSlider.value = current / max;
    }

    public void UpdateExp(float current, float max)
    {
        expSlider.value = current / max;
    }
}