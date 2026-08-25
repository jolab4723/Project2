using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillPopupController : MonoBehaviour
{
    [Tooltip("스킬 슬롯 1~3(SkillSlot_1~3). 클릭하면 그 스킬이 '지금 설정 중인 스킬'로 선택된다.")]
    [SerializeField] private Button[] skillSlotButtons = new Button[3];

    [Tooltip("진화 선택 1~3(EvolSelect_1~3). 클릭하면 선택된 스킬의 진화가 그 값으로 바뀐다(이미 선택된 걸 다시 누르면 없음으로 해제).")]
    [SerializeField] private Button[] evolutionButtons = new Button[3];

    [Tooltip("강화 선택 1~3(UpgradeSelect_1~3). 클릭하면 선택된 스킬의 강화가 그 값으로 바뀐다(이미 선택된 걸 다시 누르면 없음으로 해제).")]
    [SerializeField] private Button[] enhancementButtons = new Button[3];

    [Tooltip("스킬/진화/강화 설명 라벨 테이블(SkillLabelDatabase.asset).")]
    [SerializeField] private SkillLabelDatabaseSO labelDatabase;

    [Tooltip("Bottom/SkillNameText - 스킬 이름 표시")]
    [SerializeField] private TextMeshProUGUI skillNameText;

    [Tooltip("Bottom/SkillCostText - 스킬 코스트 및 마나 정보")]
    [SerializeField] private TextMeshProUGUI skillCostText;

    [Tooltip("Bottom/skillDescriptionText - 기본 스킬 설명 표시")]
    [SerializeField] private TextMeshProUGUI skillDescriptionText;

    [Tooltip("Bottom/SkillExtraText - 선택된 진화/강화 설명 표시(둘 다 없으면 빈칸)")]
    [SerializeField] private TextMeshProUGUI skillExtraText;

    private int selectedSkillIndex = 0;

    private ISkillController SkillController => ActiveSkillControllerLocator.Find();

    private void Awake()
    {
        for (int i = 0; i < skillSlotButtons.Length; i++)
        {
            int index = i;
            if (skillSlotButtons[i] != null)
                skillSlotButtons[i].onClick.AddListener(() => SelectSkill(index));
        }

        for (int i = 0; i < evolutionButtons.Length; i++)
        {
            int index = i;
            if (evolutionButtons[i] != null)
                evolutionButtons[i].onClick.AddListener(() => ToggleEvolution((SkillEvolutionId)(index + 1)));
        }

        for (int i = 0; i < enhancementButtons.Length; i++)
        {
            int index = i;
            if (enhancementButtons[i] != null)
                enhancementButtons[i].onClick.AddListener(() => ToggleEnhancement((SkillEnhancementId)(index + 1)));
        }
    }

    private void OnEnable() => RefreshAll();

    private void SelectSkill(int index)
    {
        selectedSkillIndex = index;
        RefreshAll();
    }

    private void ToggleEvolution(SkillEvolutionId evolution)
    {
        if (SkillController == null)
            return;

        SkillEvolutionId current = SkillController.GetEvolution(selectedSkillIndex);
        SkillController.SetEvolution(selectedSkillIndex, current == evolution ? SkillEvolutionId.None : evolution);
        RefreshAll();
    }

    private void ToggleEnhancement(SkillEnhancementId enhancement)
    {
        if (SkillController == null)
            return;

        SkillEnhancementId current = SkillController.GetEnhancement(selectedSkillIndex);
        SkillController.SetEnhancement(selectedSkillIndex, current == enhancement ? SkillEnhancementId.None : enhancement);
        RefreshAll();
    }

    private void RefreshAll()
    {
        for (int i = 0; i < skillSlotButtons.Length; i++)
            SetHighlight(skillSlotButtons[i], i == selectedSkillIndex);

        ISkillController controller = SkillController;
        if (controller == null)
            return;

        SkillEvolutionId currentEvo = controller.GetEvolution(selectedSkillIndex);
        for (int i = 0; i < evolutionButtons.Length; i++)
            SetHighlight(evolutionButtons[i], (int)currentEvo == i + 1);

        SkillEnhancementId currentEnh = controller.GetEnhancement(selectedSkillIndex);
        for (int i = 0; i < enhancementButtons.Length; i++)
            SetHighlight(enhancementButtons[i], (int)currentEnh == i + 1);

        RefreshDescription(controller, currentEvo, currentEnh);
    }

    private void RefreshDescription(ISkillController controller, SkillEvolutionId currentEvo, SkillEnhancementId currentEnh)
    {
        SkillDefinitionSO def = controller.GetSkillDefinition(selectedSkillIndex);
        if (def == null || labelDatabase == null)
            return;

        if (skillNameText != null)
            skillNameText.text = def.skillName;

        if (skillCostText != null)
            skillCostText.text = BuildCostSummary(controller, def);

        if (skillDescriptionText != null)
            skillDescriptionText.text = labelDatabase.GetSkillDescription(def.skillId);

        if (skillExtraText == null)
            return;

        string evoLine = currentEvo == SkillEvolutionId.None
            ? string.Empty
            : "진화 : " + labelDatabase.GetEvolutionDescription(def.skillId, currentEvo);

        string enhLine = currentEnh == SkillEnhancementId.None
            ? string.Empty
            : "강화 : " + labelDatabase.GetEnhancementDescription(def.skillId, currentEnh);

        if (string.IsNullOrEmpty(evoLine))
            skillExtraText.text = enhLine;
        else if (string.IsNullOrEmpty(enhLine))
            skillExtraText.text = evoLine;
        else
            skillExtraText.text = evoLine + "\n" + "\n" + enhLine;
    }

    private string BuildCostSummary(ISkillController controller, SkillDefinitionSO def)
    {
        float cooldown = controller.GetEffectiveCooldown(selectedSkillIndex);

        if (def.shapeType == SkillShapeType.Dash)
            return $"쿨타임 {cooldown:0.#}초";

        return $"피해 배율 {def.damageMultiplier * 100f:0}% · 쿨타임 {cooldown:0.#}초";
    }

    private static void SetHighlight(Button button, bool active)
    {
        if (button == null)
            return;

        var slot = button.GetComponent<KY_PassiveSkillSlot>();
        if (slot != null && slot.activeHighlight != null)
            slot.activeHighlight.SetActive(active);
    }
}
