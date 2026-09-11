using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Core;

/// <summary>
/// 패시브 업그레이드 팝업 전체를 제어하는 컨트롤러 (인스펙터에서 직접 연결한 참조 기반).
///
/// 아이콘 12개(SkillSlot_1~12)는 Core.PassiveSkillId의 enum 선언 순서와 1:1로 대응된다.
///
/// 아이콘 클릭 -> 해당 패시브 선택(펜딩 레벨 = 현재 레벨) -> -/+ 로 "적용해볼 레벨"(pendingLevel)을 조정
/// -> btn_Confilm으로 확정. 이미 해금된 범위 안이면 즉시 무료 적용("OK"), 그 이상이면 크레딧을 써서
/// 해금 후 적용(비용 숫자 표시, 크레딧이 모자라면 빨간색). 다른 아이콘을 선택하면 확정 안 한 변경은 버려진다.
///
/// !! 2026-09-10: 원래 transform.Find(...)로 오브젝트를 찾아 썼으나, 계층 구조가 바뀔 때마다
///    조용히 깨지는 문제가 있어 인스펙터에서 직접 드래그해 연결하는 SerializeField 방식으로 바꿨다.
///    아이콘 슬롯은 클릭 감지/렌더링만 담당하는 뷰 컴포넌트인 KY_PassiveSkillSlot을 그대로 재사용한다
///    (좌클릭만 사용 - 원래 버전이 일반 Button.onClick만 썼던 것과 동일하게 맞춤).
/// </summary>
public class PassiveSkillPanelUI : MonoBehaviour
{
    // enum 선언 순서가 slots 리스트 순서와 1:1로 대응됨.
    private static readonly PassiveSkillId[] IconOrder = (PassiveSkillId[])Enum.GetValues(typeof(PassiveSkillId));

    // 상세 설명 텍스트의 "현재 레벨 / 최대 레벨" 색상 (TMP 리치 텍스트 <color> 태그용).
    private const string DefaultTextColor = "#FFFFFF";
    private const string LevelUpColor = "#ADFF2F";   // 연두색 - 설정하려는 값이 현재 레벨보다 큼(올릴 예정)
    private const string LevelDownColor = "#FFA500"; // 주황색 - 설정하려는 값이 현재 레벨보다 작음(내릴 예정)
    private const string MaxLevelReachedColor = "#FFFF00"; // 노란색 - 최대 레벨까지 전부 해금됨

    [Header("헤더")]
    [FormerlySerializedAs("goldText")]
    [SerializeField] private TextMeshProUGUI creditText;
    [SerializeField] private Button skillClearButton;
    [SerializeField] private Button closeButton;

    [Header("상세 패널")]
    [SerializeField] private TextMeshProUGUI nameText;
    [Tooltip("현재 레벨 / 현재 효과 / 다음 레벨 등 레벨업 정보 표시")]
    [SerializeField] private TextMeshProUGUI levelUpText;
    [Tooltip("패시브 스킬의 설명문(prose). 라벨 DB 우선, 없으면 PassiveSkillDefinition.description")]
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button levelDownButton;
    [SerializeField] private Button levelUpButton;
    [SerializeField] private TextMeshProUGUI pendingLevelText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI confirmButtonText;

    [Header("스킬 아이콘 (Core.PassiveSkillId 선언 순서와 1:1 대응)")]
    [SerializeField] private List<KY_PassiveSkillSlot> slots = new();

    [Header("번역 (비워두면 Resources에서 공용 DB를 자동으로 찾아 쓴다)")]
    [Tooltip("패시브 스킬 이름 다국어 DB")]
    [SerializeField] private PassiveSkillLabelDatabaseSO passiveLabels;
    [Tooltip("골드/레벨 접두사 등 고정 문구 다국어 DB")]
    [SerializeField] private UILabelDatabaseSO uiLabels;

    private const string PassiveLabelResourcePath = "DataFiles/PassiveSkillData/3. GeneratedAssets/PassiveSkillLabelDatabase";
    private const string UiLabelResourcePath = "DataFiles/UIData/3. GeneratedAssets/UILabelDatabase";

    private PassiveSkillId? selectedId;
    private int pendingLevel;

    private void OnEnable()
    {
        if (passiveLabels == null)
            passiveLabels = Resources.Load<PassiveSkillLabelDatabaseSO>(PassiveLabelResourcePath);
        if (uiLabels == null)
            uiLabels = Resources.Load<UILabelDatabaseSO>(UiLabelResourcePath);

        foreach (var slot in slots)
        {
            if (slot == null) continue;
            slot.OnSlotClicked += HandleSlotClicked;
        }

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;

        if (skillClearButton != null) skillClearButton.onClick.AddListener(HandleSkillClearClicked);
        if (closeButton != null) closeButton.onClick.AddListener(HandleCloseClicked);
        if (levelDownButton != null) levelDownButton.onClick.AddListener(HandleLevelDownClicked);
        if (levelUpButton != null) levelUpButton.onClick.AddListener(HandleLevelUpClicked);
        if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmClicked);

        if (PassiveSkillManager.Instance != null)
            PassiveSkillManager.Instance.OnProfileChanged += RefreshAll;

        if (selectedId == null && IconOrder.Length > 0)
            selectedId = IconOrder[0];

        RefreshAll();
    }

    private void OnDisable()
    {
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            slot.OnSlotClicked -= HandleSlotClicked;
        }

        if (skillClearButton != null) skillClearButton.onClick.RemoveListener(HandleSkillClearClicked);
        if (closeButton != null) closeButton.onClick.RemoveListener(HandleCloseClicked);
        if (levelDownButton != null) levelDownButton.onClick.RemoveListener(HandleLevelDownClicked);
        if (levelUpButton != null) levelUpButton.onClick.RemoveListener(HandleLevelUpClicked);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirmClicked);

        if (PassiveSkillManager.Instance != null)
            PassiveSkillManager.Instance.OnProfileChanged -= RefreshAll;

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(GameLanguage _) => RefreshAll();

    /// <summary>uiLabels에서 key 문구를 가져오되, DB가 없거나 매칭 실패면 한국어 폴백을 쓴다.</summary>
    private string L(string key, string korFallback)
    {
        if (uiLabels == null) return korFallback;
        string value = uiLabels.GetLabel(key);
        return string.IsNullOrEmpty(value) || value == key ? korFallback : value;
    }

    /// <summary>패시브 스킬 이름을 현재 언어로. DB가 없거나 매칭 실패면 디자인 데이터(displayName)로 폴백.</summary>
    private string ResolvePassiveName(PassiveSkillId id, PassiveSkillDefinition definition)
    {
        if (passiveLabels != null)
        {
            string localized = passiveLabels.GetName(id);
            if (!string.IsNullOrEmpty(localized) && localized != id.ToString())
                return localized;
        }
        return definition != null ? definition.displayName : id.ToString();
    }

    /// <summary>패시브 스킬 설명문을 현재 언어로. 라벨 DB 우선, 없으면 PassiveSkillDefinition.description으로 폴백.</summary>
    private string ResolvePassiveDescription(PassiveSkillId id, PassiveSkillDefinition definition)
    {
        if (passiveLabels != null)
        {
            string localized = passiveLabels.GetDescription(id);
            if (!string.IsNullOrEmpty(localized))
                return localized;
        }
        return definition != null ? definition.description : string.Empty;
    }

    private void HandleSlotClicked(PassiveSkillData data)
    {
        if (data == null) return;
        SelectSkill(data.id);
    }

    private void SelectSkill(PassiveSkillId id)
    {
        selectedId = id;
        pendingLevel = PassiveSkillManager.Instance != null ? PassiveSkillManager.Instance.GetCurrentLevel(id) : 0;
        RefreshDetailPanel();
    }

    private void HandleLevelDownClicked()
    {
        if (selectedId == null)
            return;

        pendingLevel = Mathf.Max(0, pendingLevel - 1);
        RefreshDetailPanel();
    }

    private void HandleLevelUpClicked()
    {
        if (selectedId == null || PassiveSkillManager.Instance == null)
            return;

        var definition = PassiveSkillManager.Instance.GetDefinition(selectedId.Value);
        int maxLevel = definition != null ? definition.maxLevel : 0;
        pendingLevel = Mathf.Min(maxLevel, pendingLevel + 1);
        RefreshDetailPanel();
    }

    private void HandleConfirmClicked()
    {
        if (selectedId == null || PassiveSkillManager.Instance == null)
            return;

        // 성공하면 OnProfileChanged -> RefreshAll이 이미 화면을 갱신해준다.
        // 골드 부족으로 실패해도 pendingLevel은 그대로 둬서 사용자가 다시 시도할 수 있게 한다.
        PassiveSkillManager.Instance.TryApplyLevel(selectedId.Value, pendingLevel);
        RefreshDetailPanel();
    }

    private void HandleSkillClearClicked()
    {
        PassiveSkillManager.Instance?.ResetAllCurrentLevels();
        if (selectedId != null)
            SelectSkill(selectedId.Value);
    }

    private void HandleCloseClicked()
    {
        gameObject.SetActive(false);
    }

    private void RefreshAll()
    {
        var manager = PassiveSkillManager.Instance;
        var profile = manager != null ? manager.CurrentProfile : null;

        if (creditText != null)
            creditText.text = profile != null
                ? string.Format(L("passive_skill_ui.credit_format", "크레딧 | {0}"), profile.credit.ToString("N0"))
                : L("passive_skill_ui.credit_none", "크레딧 | -");

        for (int i = 0; i < slots.Count && i < IconOrder.Length; i++)
        {
            if (slots[i] == null) continue;

            var id = IconOrder[i];
            slots[i].Render(new PassiveSkillData
            {
                id = id,
                definition = manager != null ? manager.GetDefinition(id) : null,
                unlockedLevel = manager != null ? manager.GetUnlockedLevel(id) : 0,
                currentLevel = manager != null ? manager.GetCurrentLevel(id) : 0
            });
        }

        RefreshDetailPanel();
    }

    /// <summary>현재 선택된 슬롯의 OutLine만 켜고 나머지는 끈다.</summary>
    private void UpdateSlotHighlights()
    {
        for (int i = 0; i < slots.Count && i < IconOrder.Length; i++)
        {
            if (slots[i] == null || slots[i].activeHighlight == null) continue;
            slots[i].activeHighlight.SetActive(selectedId.HasValue && IconOrder[i] == selectedId.Value);
        }
    }

    private void RefreshDetailPanel()
    {
        UpdateSlotHighlights();

        if (selectedId == null || PassiveSkillManager.Instance == null)
            return;

        PassiveSkillId id = selectedId.Value;
        var definition = PassiveSkillManager.Instance.GetDefinition(id);
        if (definition == null)
            return;

        var manager = PassiveSkillManager.Instance;
        int currentLevel = manager.GetCurrentLevel(id);
        int unlockedLevel = manager.GetUnlockedLevel(id);

        if (nameText != null)
            nameText.text = ResolvePassiveName(id, definition);

        if (descriptionText != null)
            descriptionText.text = ResolvePassiveDescription(id, definition);

        if (levelUpText != null)
        {
            // "현재 효과"/"다음 레벨"은 확정된 currentLevel이 아니라 지금 -/+로 미리보는 pendingLevel 기준으로 표시.
            float previewEffect = definition.GetValue(pendingLevel);
            float nextEffect = definition.GetValue(Mathf.Min(pendingLevel + 1, definition.maxLevel));

            // 설정하려는 값(pendingLevel)이 현재 레벨보다 크면 연두색(올릴 예정), 작으면 주황색(내릴 예정), 같으면 기본색.
            string currentLevelColor = pendingLevel > currentLevel ? LevelUpColor
                : pendingLevel < currentLevel ? LevelDownColor
                : DefaultTextColor;
            // 최대 레벨까지 전부 해금했으면 MaxLevel 표시를 노란색으로.
            string maxLevelColor = unlockedLevel >= definition.maxLevel ? MaxLevelReachedColor : DefaultTextColor;

            // 접두사만 다국어 DB에서 가져오고 <color>/숫자 포맷은 코드가 유지한다({0}/{1}로 값을 끼워넣는다).
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Format(
                L("passive_skill_ui.detail_current_level", "현재 레벨: {0} / {1}"),
                $"<color={currentLevelColor}>{pendingLevel}</color>",
                $"<color={maxLevelColor}>{definition.maxLevel}</color>"));
            sb.AppendLine(string.Format(
                L("passive_skill_ui.detail_current_effect", "현재 효과: {0}"),
                $"<color={currentLevelColor}>+{previewEffect:0.#}%</color>"));
            if (pendingLevel < definition.maxLevel)
                sb.Append(string.Format(
                    L("passive_skill_ui.detail_next_level", "다음 레벨: {0}"),
                    $"+{nextEffect:0.#}%"));

            levelUpText.text = sb.ToString();
        }

        if (pendingLevelText != null)
            pendingLevelText.text = pendingLevel.ToString();

        if (levelDownButton != null)
            levelDownButton.interactable = pendingLevel > 0;

        if (levelUpButton != null)
            levelUpButton.interactable = pendingLevel < definition.maxLevel;

        if (confirmButtonText != null)
        {
            if (pendingLevel <= unlockedLevel)
            {
                confirmButtonText.text = L("passive_skill_ui.confirm_ok", "OK");
                confirmButtonText.color = Color.white;
            }
            else
            {
                int cost = manager.GetUnlockCostToLevel(id, pendingLevel);
                bool canAfford = manager.CurrentProfile != null && manager.CurrentProfile.credit >= cost;
                confirmButtonText.text = cost.ToString();
                confirmButtonText.color = canAfford ? Color.white : Color.red;
            }
        }
    }
}
