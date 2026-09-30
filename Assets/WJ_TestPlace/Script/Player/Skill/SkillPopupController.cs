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

    [Tooltip("공용 진화 아이콘(진화1/2/3). 스킬 데이터(SkillDefinitionSO.evolutionIcons)에 전용 아이콘이 없을 때만 쓴다. 비워두면 해당 스킬의 아이콘으로 대신 표시한다.")]
    [SerializeField] private Sprite[] evolutionIcons = new Sprite[3];

    [Tooltip("옵션을 아직 안 고른 배지에 넣을 전용 이미지. 비워두면 Resources의 공용 select_none 아이콘을 쓰고, 그것도 없으면 그 스킬 아이콘을 회색으로 표시한다.")]
    [SerializeField] private Sprite unselectedOptionIcon;

    /// <summary>진화/강화 미선택 배지와 포션 미장착 슬롯이 함께 쓰는 공용 "선택 없음" 아이콘.</summary>
    public const string SelectNoneIconResourcePath = "Images/Icon/Skill/Common/select_none";

    [Tooltip("스킬 슬롯 1~4의 배지(SkillSlot_N/Area_SkillOptions/option_*/img_OptionIcon). 아직 배지를 안 만든 슬롯은 비워두면 된다.")]
    [SerializeField] private SkillOptionBadges[] skillOptionBadges = new SkillOptionBadges[4];

    /// <summary>미선택 배지에 임시로 입히는 색. 전용 이미지(unselectedOptionIcon)가 준비되면 안 쓰인다.</summary>
    private static readonly Color UnselectedBadgeTint = new Color(0.35f, 0.35f, 0.35f, 1f);

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

    /// <summary>스킬 슬롯 아이콘 위에 "지금 이 스킬에 뭐가 적용돼 있는지"를 보여주는 작은 배지 한 쌍.</summary>
    [System.Serializable]
    private class SkillOptionBadges
    {
        [Tooltip("option_SkillRevolution/img_OptionIcon - 배지 틀(배경)이 아니라 안쪽 아이콘 Image를 연결한다.")]
        public Image evolutionBadge;

        [Tooltip("option_SkillUpgrade/img_OptionIcon - 배지 틀(배경)이 아니라 안쪽 아이콘 Image를 연결한다.")]
        public Image enhancementBadge;
    }

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
        if (unselectedOptionIcon == null)
            unselectedOptionIcon = Resources.Load<Sprite>(SelectNoneIconResourcePath);

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
        // 비활성화되면 코루틴은 Unity가 멈추므로 참조만 비운다(다음에 켜질 때 StopCoroutine이 헛돌지 않게).
        deferredLayoutRoutine = null;

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

        ClassSkillIconSet iconSet = ResolveIconSet(controller);
        RefreshIcons(controller, iconSet);
        RefreshOptionBadges(controller, iconSet);
        RefreshDescription(controller, currentEvo, currentEnh);
        RebuildDescriptionLayout();

        // 팝업이 막 켜진 프레임에는 폰트·부모 레이아웃이 아직 확정되지 않을 수 있어 한 프레임 뒤에 한 번 더 맞춘다.
        if (isActiveAndEnabled)
        {
            if (deferredLayoutRoutine != null)
                StopCoroutine(deferredLayoutRoutine);
            deferredLayoutRoutine = StartCoroutine(RebuildDescriptionLayoutNextFrame());
        }
    }

    private Coroutine deferredLayoutRoutine;

    private System.Collections.IEnumerator RebuildDescriptionLayoutNextFrame()
    {
        yield return null;
        deferredLayoutRoutine = null;
        RebuildDescriptionLayout();
    }

    /// <summary>
    /// 하단 설명 영역(Bottom)의 배치를 지금 문구 기준으로 확정한다.
    ///
    /// !! Bottom은 VerticalLayoutGroup(자식 높이 미제어)이고 SkillDescriptionText만 자기 ContentSizeFitter로
    ///    높이를 줄인다. 한 번의 레이아웃 패스에서는 부모가 자식 위치를 먼저 정하고 자식이 나중에 높이를
    ///    바꾸므로, 처음 열 때(또는 언어를 바꿔 문구 길이가 달라질 때) 이전 높이 기준 위치에 텍스트가 겹쳐
    ///    보였다. 텍스트 메시를 먼저 갱신한 뒤 설명 텍스트 → Bottom 순서로 다시 계산한다.
    /// </summary>
    private void RebuildDescriptionLayout()
    {
        if (skillDescriptionText == null)
            return;

        foreach (TextMeshProUGUI text in new[] { skillNameText, skillCostText, skillDescriptionText, skillExtraText })
        {
            if (text != null && text.isActiveAndEnabled)
                text.ForceMeshUpdate();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(skillDescriptionText.rectTransform);
        if (skillDescriptionText.transform.parent is RectTransform bottom)
            LayoutRebuilder.ForceRebuildLayoutImmediate(bottom);
    }

    /// <summary>
    /// 각 스킬 슬롯 아이콘 위에 그 스킬에 지금 적용돼 있는 진화/강화를 작은 배지로 표시한다.
    /// 선택 중인 슬롯만이 아니라 슬롯 전체를 매번 갱신해서, 팝업을 닫지 않고도 어느 스킬에
    /// 뭐가 붙어 있는지 한눈에 보이게 한다.
    ///
    /// 아직 안 고른 옵션도 배지를 끄지 않고 미선택 모습으로 남긴다. 칸이 계속 보여야
    /// "여기에 뭘 넣을 수 있다"가 드러나기 때문이다.
    /// </summary>
    private void RefreshOptionBadges(ISkillController controller, ClassSkillIconSet iconSet)
    {
        for (int i = 0; i < skillOptionBadges.Length; i++)
        {
            SkillOptionBadges badges = skillOptionBadges[i];
            if (badges == null)
                continue;

            bool hasSkill = controller.GetSkillDefinition(i) != null;
            Sprite slotIcon = ResolveSlotIcon(controller, iconSet, i);

            // !! 진화는 스킬마다 내용이 달라서 스킬별 아이콘(SkillDefinitionSO.evolutionIcons)을 먼저 쓴다.
            //    그게 비어 있으면 공용 3칸(evolutionIcons)을 보고, 그것도 없으면 그 스킬 아이콘으로 대신 표시한다.
            SkillEvolutionId evolution = hasSkill ? controller.GetEvolution(i) : SkillEvolutionId.None;
            SetBadge(badges.evolutionBadge,
                     evolution != SkillEvolutionId.None,
                     ResolveEvolutionIcon(controller.GetSkillDefinition(i), evolution) ?? slotIcon,
                     slotIcon);

            SkillEnhancementId enhancement = hasSkill ? controller.GetEnhancement(i) : SkillEnhancementId.None;
            SetBadge(badges.enhancementBadge,
                     enhancement != SkillEnhancementId.None,
                     PickIcon(enhancementIcons, (int)enhancement - 1),
                     slotIcon);
        }
    }

    /// <summary>
    /// 아이콘 배열에서 한 칸을 꺼낸다. 비어 있으면 확실하게 null을 돌려준다.
    ///
    /// !! 직렬화된 오브젝트 배열의 빈 칸은 진짜 null이 아니라 Unity의 "가짜 null"이라
    ///    ReferenceEquals(null)이 false다. 그래서 ?? 연산자가 폴백으로 넘어가지 않고
    ///    빈 칸을 그대로 들고 가 아이콘이 통째로 안 보인다. 여기서 Unity의 == 비교로 한 번 걸러 준다.
    /// </summary>
    private static Sprite PickIcon(Sprite[] icons, int index)
    {
        if (icons == null || index < 0 || index >= icons.Length)
            return null;

        Sprite icon = icons[index];
        return icon != null ? icon : null;
    }

    /// <summary>
    /// 배지 하나를 갱신한다. 고른 옵션이 있으면 그 아이콘을 원래 색으로 보여주고,
    /// 아직 안 골랐으면 미선택 모습으로 남긴다.
    ///
    /// 미선택 전용 이미지(unselectedOptionIcon)가 준비되면 그걸 그대로 쓰고,
    /// 아직 없는 동안은 임시로 해당 스킬 아이콘을 회색으로 깔아 둔다. 직전에 골랐던
    /// 아이콘을 회색으로 남기면 "아직 그게 적용돼 있는데 꺼진 것"처럼 보여서, 미선택일 때는
    /// 항상 같은 그림으로 되돌린다.
    /// </summary>
    private void SetBadge(Image badge, bool applied, Sprite appliedIcon, Sprite emptyFallback)
    {
        if (badge == null)
            return;

        badge.gameObject.SetActive(true);

        if (applied && appliedIcon != null)
        {
            badge.sprite = appliedIcon;
            badge.color = Color.white;
            return;
        }

        bool hasEmptyArt = unselectedOptionIcon != null;
        Sprite empty = hasEmptyArt ? unselectedOptionIcon : emptyFallback;

        if (empty != null)
            badge.sprite = empty;

        badge.color = hasEmptyArt ? Color.white : UnselectedBadgeTint;
    }

    private static ClassSkillIconSet ResolveIconSet(ISkillController controller)
    {
        MonoBehaviour controllerBehaviour = controller as MonoBehaviour;
        return controllerBehaviour != null ? controllerBehaviour.GetComponent<ClassSkillIconSet>() : null;
    }

    /// <summary>
    /// 활성 캐릭터의 스킬 아이콘을 팝업에 반영한다. 아이콘 출처는 HUD(KY_SkillView)와 동일하게
    /// 스킬 데이터(SkillDefinitionSO.icon)가 우선이고, 비어 있으면 캐릭터의 ClassSkillIconSet을 쓴다.
    ///
    /// !! 진화 선택 슬롯은 선택된 스킬의 진화별 아이콘(SkillDefinitionSO.evolutionIcons)을 쓰고, 전용 아이콘이
    ///    없는 칸(강화 포함)은 '지금 선택된 스킬'의 아이콘을 따라간다
    ///    (예전엔 파이터 1번 스킬 아이콘이 고정으로 박혀 있어 거너로 플레이해도 그대로 남았다).
    /// </summary>
    private void RefreshIcons(ISkillController controller, ClassSkillIconSet iconSet)
    {
        for (int i = 0; i < skillIconSlots.Length; i++)
            ApplyIcon(skillIconSlots[i], ResolveSlotIcon(controller, iconSet, i));

        Sprite selectedIcon = ResolveSlotIcon(controller, iconSet, selectedSkillIndex);
        SkillDefinitionSO selectedDefinition = controller.GetSkillDefinition(selectedSkillIndex);
        // 진화 선택 1~3은 선택된 스킬의 진화별 아이콘을 쓰고, 없는 칸은 그 스킬 아이콘을 따라간다.
        for (int i = 0; i < evolutionButtons.Length; i++)
        {
            Button button = evolutionButtons[i];
            Sprite icon = ResolveEvolutionIcon(selectedDefinition, (SkillEvolutionId)(i + 1)) ?? selectedIcon;
            ApplyIcon(button != null ? button.GetComponent<KY_PassiveSkillSlot>() : null, icon);
        }

        // 강화는 종류(위력/쿨타임/범위)가 클래스·스킬과 무관하므로 공용 아이콘을 쓴다.
        for (int i = 0; i < enhancementButtons.Length; i++)
        {
            Sprite icon = i < enhancementIcons.Length && enhancementIcons[i] != null
                ? enhancementIcons[i]
                : selectedIcon;
            ApplyIcon(enhancementButtons[i] != null ? enhancementButtons[i].GetComponent<KY_PassiveSkillSlot>() : null, icon);
        }
    }

    /// <summary>진화 아이콘: 스킬 데이터의 진화별 아이콘 → 공용 evolutionIcons 순서. 둘 다 없으면 null.</summary>
    private Sprite ResolveEvolutionIcon(SkillDefinitionSO definition, SkillEvolutionId evolution)
    {
        if (evolution == SkillEvolutionId.None)
            return null;

        Sprite icon = definition != null ? definition.GetEvolutionIcon(evolution) : null;
        return icon != null ? icon : PickIcon(evolutionIcons, (int)evolution - 1);
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
