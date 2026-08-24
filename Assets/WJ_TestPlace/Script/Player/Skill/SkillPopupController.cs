using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// KY님이 만든 SkillPopup(진화/강화 선택 UI)을 실제 ISkillController(현재 활성 캐릭터)에 연결한다.
/// 기존 K키 패널(SkillEvolutionSelectUI)과 같은 데이터(GetEvolution/SetEvolution/GetEnhancement/
/// SetEnhancement, ActiveSkillControllerLocator로 활성 캐릭터 자동 탐색)를 쓰지만, 조작 방식은 다르다 -
/// K키 패널은 슬롯 하나당 버튼 하나로 순환(없음→1→2→3→없음)하는 반면, 이 팝업은 SkillSlot(1~3)로
/// "어떤 스킬을 설정할지" 먼저 고른 뒤 EvolSelect(1~3)/UpgradeSelect(1~3)로 그 스킬의 진화/강화를
/// 직접 지정하는 방식이다(127번).
///
/// KY님 스크립트(KY_PassiveSkillSlot/KY_SkillPopup)는 전혀 안 건드리고, 이미 있는 Button과
/// KY_PassiveSkillSlot.activeHighlight(각 슬롯의 OutLine 자식)만 외부에서 참조해서 선택 표시에 쓴다.
///
/// SkillSlot_4는 대응하는 4번째 스킬이 없어서(ISkillController.SkillCount==3, Skill1~3만 존재)
/// 선택 대상에서 제외했다 - KY_SkillView 주석("Skill1~4+Dodge")에 따르면 이 자리는 Dodge용으로 보인다.
///
/// Bottom 영역(Name/that/Description 3개 TMP)에는 SkillLabelDatabaseSO(128번 - 스킬/진화/강화 설명
/// 라벨 테이블)에서 읽어온 텍스트를 표시한다(129번). Name=스킬 이름, that=기본 설명, Description=
/// 지금 선택된 진화/강화가 있을 때만 "진화 : ~"/"강화 : ~" 줄을 추가한다(둘 다 없으면 빈칸).
/// </summary>
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

    [Tooltip("Bottom/Name - 스킬 이름 표시.")]
    [SerializeField] private TextMeshProUGUI nameText;

    [Tooltip("Bottom/that - 기본 스킬 설명 표시.")]
    [SerializeField] private TextMeshProUGUI summaryText;

    [Tooltip("Bottom/Description - 선택된 진화/강화 설명 표시(둘 다 없으면 빈칸).")]
    [SerializeField] private TextMeshProUGUI descriptionText;

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

        if (nameText != null)
            nameText.text = def.skillName;

        if (summaryText != null)
            summaryText.text = labelDatabase.GetSkillDescription(def.skillId);

        if (descriptionText == null)
            return;

        string evoLine = currentEvo == SkillEvolutionId.None
            ? string.Empty
            : "진화 : " + labelDatabase.GetEvolutionDescription(def.skillId, currentEvo);

        string enhLine = currentEnh == SkillEnhancementId.None
            ? string.Empty
            : "강화 : " + labelDatabase.GetEnhancementDescription(def.skillId, currentEnh);

        if (string.IsNullOrEmpty(evoLine))
            descriptionText.text = enhLine;
        else if (string.IsNullOrEmpty(enhLine))
            descriptionText.text = evoLine;
        else
            descriptionText.text = evoLine + "\n" + enhLine;
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
