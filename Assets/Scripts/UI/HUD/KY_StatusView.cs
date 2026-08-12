using PLAYERTWO.ARPGProject;
using UnityEngine;
using UnityEngine.UI;

// HP, MP, EXP 슬라이더를 관리하는 코드
public class KY_StatusView : MonoBehaviour
{
    public Slider healthSlider;
    public Slider manaSlider;
    public Slider expSlider;

    private WBH_PlayerStatus status;

    private void Awake()
    {
        status = FindFirstObjectByType<WBH_PlayerStatus>(); //!@ WBH 추가 멀티 시, 다른 방법 필요할지도 모르겠음.
    }

    // WBH 추가. ui 메서드 (현재는 WBH_playerStatus 컴포넌트뿐)가 아닌 다른 경로로 hp, mp 값이 변화할 때, 보험으로 작용.
    private void Update()
    {
        healthSlider.value = status.CurrentHp / status.MaxHealth;
        manaSlider.value = status.CurrentMp / status.MaxMana;
        expSlider.value = status.CurrentExp / status.MaxExp;
    }

    private void OnEnable()
    {
        //status.OnHpChanged += UpdateHealth;
        //status.OnMpChanged += UpdateMana;
    }
    private void OnDisable()
    {
        //status.OnHpChanged -= UpdateHealth;
        //status.OnMpChanged -= UpdateMana;
    }

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