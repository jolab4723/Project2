using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerStatManager가 들고 있는 최종 스탯(PlayerStat)을 UI에 표시.
/// PlayerStat.OnStatChanged를 구독해서 값이 바뀔 때마다 자동 갱신된다.
///
/// 구체적인 UI 필드 구성(툴팁 때처럼 항목별로 나눌지)을 아직 안 주셔서,
/// 일단 체력바 + 레벨/경험치 + 상세 스탯 텍스트 한 덩어리로 최소 구성함.
/// 나중에 필드별로 정확히 나눠서 보여주고 싶으면 원하는 필드 구성 알려주시면
/// 그에 맞춰 쪼개드릴 수 있음 (툴팁 UI 작업 때와 동일한 방식).
/// </summary>
public class PlayerStatUIManager : MonoBehaviour
{
    [Header("소스")]
    [Tooltip("비워두면 PlayerStatManager.Instance를 사용")]
    [SerializeField] private PlayerStatManager playerStatManager;

    [Header("체력")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("레벨 / 경험치")]
    [SerializeField] private TextMeshProUGUI levelText;
    [Tooltip("다음 레벨까지 필요한 경험치 총량 개념이 아직 없어서, 현재 경험치 수치만 그대로 표시함")]
    [SerializeField] private TextMeshProUGUI expText;

    [Header("상세 스탯 (한 텍스트에 전부 표시)")]
    [SerializeField] private TextMeshProUGUI detailStatText;

    private PlayerStatManager Manager => playerStatManager != null ? playerStatManager : PlayerStatManager.Instance;
    private PlayerStat Stat => Manager != null ? Manager.Stat : null;

    private void OnEnable()
    {
        // PlayerStatManager.Awake가 이 컴포넌트보다 먼저 실행돼서 Stat이 준비돼있어야 구독 가능.
        // 순서가 꼬이면(예: 이 컴포넌트가 씬 로드 시점에 더 먼저 활성화됨) Script Execution Order로
        // PlayerStatManager를 먼저 실행되게 지정해야 함.
        if (Stat == null)
        {
            Debug.LogWarning("[PlayerStatUIManager] PlayerStatManager.Stat이 아직 준비되지 않았습니다.");
            return;
        }

        Stat.OnStatChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (Stat != null)
            Stat.OnStatChanged -= Refresh;
    }

    private void Refresh()
    {
        if (Stat == null)
            return;

        float currentHealth = PlayerHealthManager.Instance != null ? PlayerHealthManager.Instance.CurrentHealth : 0f;

        if (healthSlider != null)
            healthSlider.value = Stat.maxHealth > 0 ? (float)currentHealth / Stat.maxHealth : 0f;

        if (healthText != null)
            healthText.text = $"{currentHealth} / {Stat.maxHealth}";

        if (levelText != null)
            levelText.text = $"Lv. {Stat.currentLevel}";

        if (expText != null)
            expText.text = $"EXP {Stat.currentExp:F0}";

        if (detailStatText != null)
            detailStatText.text = BuildDetailStatText();
    }

    private string BuildDetailStatText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"공격력 {Stat.attackPower}");
        sb.AppendLine($"방어력 {Stat.defensePower}");
        sb.AppendLine($"이동속도 {Stat.moveSpeed:F1}");
        sb.AppendLine($"공격속도 {Stat.attackSpeed:F2}");
        sb.AppendLine($"치명타 확률 {Stat.critRate:F1}%");
        sb.AppendLine($"치명타 피해 {Stat.critMult:F2}");
        sb.AppendLine($"쿨타임 감소 {Stat.cdr:F1}%");
        sb.AppendLine($"마나 재생 {Stat.mpRegen:F1}");
        sb.AppendLine($"마나 {(PlayerManaManager.Instance != null ? PlayerManaManager.Instance.CurrentMana.ToString("F0") : "?")} / {Stat.maxMana}");
        sb.AppendLine($"관통력 {Stat.pen}");
        sb.AppendLine($"스킬 사거리 {Stat.skillRange:F1}");
        sb.AppendLine($"화염 피해 {Stat.fireBonus:F1}");
        sb.AppendLine($"빙결 피해 {Stat.iceBonus:F1}");
        sb.AppendLine($"전기 피해 {Stat.electricBonus:F1}");
        return sb.ToString().TrimEnd();
    }
}
