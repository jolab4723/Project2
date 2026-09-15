using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillPopupController : MonoBehaviour
{
    [Tooltip("스킬 슬롯 1~4(SkillSlot_1~4, 4번은 궁극기). 클릭하면 그 스킬이 '지금 설정 중인 스킬'로 선택된다.")]
    [SerializeField] private Button[] skillSlotButtons = new Button[4];

    [Tooltip("상단 스킬 슬롯 아이콘(SkillSlot_1~4). 파이터/거너 등 활성 캐릭터의 스킬 아이콘으로 자동 교체한다.")]
    [SerializeField] private KY_PassiveSkillSlot[] skillIconSlots = new KY_PassiveSkillSlot[4];

    [Tooltip("강화 선택 아이콘(Enhance1 위력 / Enhance2 쿨타임 감소 / Enhance3 범위). 강화 종류는 클래스와 무관해서 공용 아이콘을 쓴다. 비워두면 선택된 스킬 아이콘을 그대로 쓴다.")]
    [SerializeField] private Sprite[] enhancementIcons = new Sprite[3];

    [Tooltip("진화 선택 1~3(EvolSelect_1~3). 클릭하면 선택된 스킬의 진화가 그 값으로 바뀐다(이미 선택된 걸 다시 누르면 없음으로 해제).")]
    [SerializeField] private Button[] evolutionButtons = new Button[3];

    [Tooltip("강화 선택 1~3(UpgradeSelect_1~3). 클릭하면 선택된 스킬의 강화가 그 값으로 바뀐다(이미 선택된 걸 다시 누르면 없음으로 해제).")]
    [SerializeField] private Button[] enhancementButtons = new Button[3];

    [Tooltip("스킬/진화/강화 설명 라벨 테이블(SkillLabelDatabase.asset).")]
    [SerializeField] private SkillLabelDatabaseSO labelDatabase;

    [Tooltip("고정 UI 문구(진화/강화 접두어, 쿨타임 표기 등) 다국어 테이블. 비워두면 하드코딩된 한국어 문구를 그대로 쓴다.")]
    [SerializeField] private UILabelDatabaseSO uiLabels;

    [Tooltip("Bottom/SkillNameText - 스킬 이름 표시")]
    [SerializeField] private TextMeshProUGUI skillNameText;

    [Tooltip("Bottom/SkillCostText - 스킬 코스트 및 마나 정보")]
    [SerializeField] private TextMeshProUGUI skillCostText;

    [Tooltip("Bottom/skillDescriptionText - 기본 스킬 설명 표시")]
    [SerializeField] private TextMeshProUGUI skillDescriptionText;

    [Tooltip("Bottom/SkillExtraText - 선택된 진화/강화 설명 표시(둘 다 없으면 빈칸)")]
    [SerializeField] private TextMeshProUGUI skillExtraText;

    private int selectedSkillIndex = 0;

    /// <summary>이 팝업이 지금 열려있는지(활성 상태인지). 열려있는 동안은 스킬 사용을 막는 데 쓴다
    /// (FighterSkillController/GunnerSkillController.CanUseSkill에서 참조) - 팝업 하나만 쓰는 구조라
    /// static으로 간단히 노출한다.</summary>
    public static bool IsOpen { get; private set; }

    private ISkillController boundController;
    private bool usesExternalController;
    private ISkillController SkillController => usesExternalController ? boundController : ActiveSkillControllerLocator.Find();

    // SW 수정
    /// <summary>이 팝업이 조작할 플레이어의 스킬을 연결한다. 연결 해제 시 다른 캐릭터를 자동 선택하지 않는다.</summary>
    public void Bind(ISkillController controller)
    {
        usesExternalController = true;
        boundController = controller;
        if (isActiveAndEnabled) RefreshAll();
    }

    private void Awake()
    {
        // 씬에서 직접 안 배선해도(다른 맵/스테이지 씬 등) Resources의 공용 DB를 자동으로 찾아 쓴다.
        if (labelDatabase == null)
            labelDatabase = Resources.Load<SkillLabelDatabaseSO>("DataFiles/CharData/SkillData/3. GeneratedAssets/SkillLabelDatabase");
        if (uiLabels == null)
            uiLabels = Resources.Load<UILabelDatabaseSO>("DataFiles/UIData/3. GeneratedAssets/UILabelDatabase");

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

    private void OnEnable()
    {
        IsOpen = true;
        RefreshAll();

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        IsOpen = false;

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= OnLanguageChanged;
    }

    // 팝업이 열려있는 동안 설정에서 언어를 바꾸면 다음에 새로 열 때가 아니라 바로 반영되게 한다.
    private void OnLanguageChanged(GameLanguage _) => RefreshAll();

    private void SelectSkill(int index)
    {
        // 아직 그 슬롯의 스킬 데이터가 없는 컨트롤러(슬롯 3칸짜리 등)에서는 선택을 무시한다.
        // 그냥 넘기면 이름/설명이 이전 스킬 것으로 남아 잘못된 정보를 보여준다.
        ISkillController controller = SkillController;
        if (controller != null && controller.GetSkillDefinition(index) == null)
            return;

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

        RefreshIcons(controller);
        RefreshDescription(controller, currentEvo, currentEnh);
    }

    /// <summary>
    /// 활성 캐릭터의 스킬 아이콘을 팝업에 반영한다. 아이콘 출처는 HUD(KY_SkillView)와 동일하게
    /// 스킬 데이터(SkillDefinitionSO.icon)가 우선이고, 비어 있으면 캐릭터의 ClassSkillIconSet을 쓴다.
    ///
    /// !! 진화/강화 선택 슬롯은 선택지별 전용 아이콘이 아직 없어서 '지금 선택된 스킬'의 아이콘을 따라간다
    ///    (예전엔 파이터 1번 스킬 아이콘이 고정으로 박혀 있어 거너로 플레이해도 그대로 남았다).
    /// </summary>
    private void RefreshIcons(ISkillController controller)
    {
        MonoBehaviour controllerBehaviour = controller as MonoBehaviour;
        ClassSkillIconSet iconSet = controllerBehaviour != null
            ? controllerBehaviour.GetComponent<ClassSkillIconSet>()
            : null;

        for (int i = 0; i < skillIconSlots.Length; i++)
            ApplyIcon(skillIconSlots[i], ResolveSlotIcon(controller, iconSet, i));

        Sprite selectedIcon = ResolveSlotIcon(controller, iconSet, selectedSkillIndex);
        foreach (Button button in evolutionButtons)
            ApplyIcon(button != null ? button.GetComponent<KY_PassiveSkillSlot>() : null, selectedIcon);

        // 강화는 종류(위력/쿨타임/범위)가 클래스·스킬과 무관하므로 공용 아이콘을 쓴다.
        for (int i = 0; i < enhancementButtons.Length; i++)
        {
            Sprite icon = i < enhancementIcons.Length && enhancementIcons[i] != null
                ? enhancementIcons[i]
                : selectedIcon;
            ApplyIcon(enhancementButtons[i] != null ? enhancementButtons[i].GetComponent<KY_PassiveSkillSlot>() : null, icon);
        }
    }

    private static Sprite ResolveSlotIcon(ISkillController controller, ClassSkillIconSet iconSet, int index)
    {
        SkillDefinitionSO definition = controller.GetSkillDefinition(index);
        Sprite icon = definition != null ? definition.icon : null;

        if (icon == null && iconSet != null)
            icon = iconSet.GetSlotIcon(index);

        return icon;
    }

    /// <summary>아이콘이 없으면 기존 이미지를 그대로 둔다(아직 아이콘이 준비되지 않은 슬롯 대비).</summary>
    private static void ApplyIcon(KY_PassiveSkillSlot slot, Sprite icon)
    {
        if (slot == null || slot.iconImage == null || icon == null)
            return;

        slot.iconImage.sprite = icon;
        slot.iconImage.enabled = true;
    }

    private void RefreshDescription(ISkillController controller, SkillEvolutionId currentEvo, SkillEnhancementId currentEnh)
    {
        SkillDefinitionSO def = controller.GetSkillDefinition(selectedSkillIndex);
        if (def == null || labelDatabase == null)
            return;

        if (skillNameText != null)
        {
            string localizedName = labelDatabase.GetSkillName(def.skillId);
            skillNameText.text = string.IsNullOrEmpty(localizedName) ? def.skillName : localizedName;
        }

        if (skillCostText != null)
            skillCostText.text = BuildCostSummary(controller, def);

        if (skillDescriptionText != null)
            skillDescriptionText.text = labelDatabase.GetSkillDescription(def.skillId);

        if (skillExtraText == null)
            return;

        string evoLine = currentEvo == SkillEvolutionId.None
            ? string.Empty
            : string.Format(GetUILabel("skill_ui.evolution_format", "진화 : {0}"), labelDatabase.GetEvolutionDescription(def.skillId, currentEvo));

        string enhLine = currentEnh == SkillEnhancementId.None
            ? string.Empty
            : string.Format(GetUILabel("skill_ui.enhancement_format", "강화 : {0}"), labelDatabase.GetEnhancementDescription(def.skillId, currentEnh));

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
            return string.Format(GetUILabel("skill_ui.cooldown_only", "쿨타임 {0}초"), $"{cooldown:0.#}");

        return string.Format(GetUILabel("skill_ui.damage_and_cooldown", "피해 배율 {0}% · 쿨타임 {1}초"),
            $"{def.damageMultiplier * 100f:0}", $"{cooldown:0.#}");
    }

    private string GetUILabel(string key, string fallback) =>
        uiLabels != null ? uiLabels.GetLabel(key) : fallback;

    private static void SetHighlight(Button button, bool active)
    {
        if (button == null)
            return;

        var slot = button.GetComponent<KY_PassiveSkillSlot>();
        if (slot != null && slot.activeHighlight != null)
            slot.activeHighlight.SetActive(active);
    }
}
