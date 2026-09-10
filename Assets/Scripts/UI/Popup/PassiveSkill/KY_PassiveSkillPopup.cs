using System;
using System.Collections.Generic;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>실제 패시브의 골드와 단계를 표시하고, 확인한 변경만 기존 매니저에 전달한다.
/// !! 2026-09-10: WJ_StatSystemTestScene에서는 이 컴포넌트를 비활성화하고 PassiveSkillPanelUI로
/// 교체했다(요청: "ky가 작업한 스크립트는 일단 다 비활성화"). KY_PassiveSkillSlot이 PassiveSkillData
/// 타입을 쓰도록 바뀌어서 그 경계에 닿는 시그니처만 최소한으로 맞춰 컴파일만 유지해뒀다 - 이 클래스
/// 자체의 나머지 로직/필드(allSkills, descriptionView 등)는 그대로 KY_PassiveSkillData를 쓴다.</summary>
public class KY_PassiveSkillPopup : KY_PopupBase
{
    // SW 수정
    [Header("데이터")]
    [SerializeField] private PassiveSkillManager skillManager;
    public List<KY_PassiveSkillData> allSkills = new();

    [Header("슬롯 (패시브 ID 순서)")]
    public List<KY_PassiveSkillSlot> slots;

    [Header("View")]
    public TextMeshProUGUI pointText;
    public KY_PassiveSkillDescriptionView descriptionView;
    public KY_PassiveSkillListView activeListView;

    [Header("단계 조절 버튼 (새 패시브 화면)")]
    [SerializeField] private Button levelUpButton;
    [SerializeField] private Button levelDownButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TMP_Text pendingLevelText;
    [SerializeField] private TMP_Text confirmButtonText;

    [SerializeField] private bool allowChanges = true;
    [SerializeField] private string unavailableReason;
    private PassiveSkillId? selectedId;
    private int pendingLevel;

    private void Awake()
    {
        levelUpButton?.onClick.AddListener(OnClickLevelUp);
        levelDownButton?.onClick.AddListener(OnClickLevelDown);
        confirmButton?.onClick.AddListener(OnClickConfirm);
        resetButton?.onClick.AddListener(OnClickReset);
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            slot.OnSlotClicked += OnSkillClicked;
            slot.OnSlotDecreaseRequested += OnSkillDecreaseRequested;
            slot.OnSlotHoverEnter += OnSkillHoverEnter;
            slot.OnSlotHoverExit += OnSkillHoverExit;
        }
    }

    private void OnEnable()
    {
        if (skillManager == null) skillManager = PassiveSkillManager.Instance;
        if (skillManager != null && skillManager.CurrentProfile == null)
            DataManager.Instance?.LoadPassiveData();
        if (skillManager != null) skillManager.OnProfileChanged += RefreshAll;
        selectedId = null;
        RefreshAll();
    }

    private void OnDisable()
    {
        if (skillManager != null) skillManager.OnProfileChanged -= RefreshAll;
    }

    private void OnDestroy()
    {
        levelUpButton?.onClick.RemoveListener(OnClickLevelUp);
        levelDownButton?.onClick.RemoveListener(OnClickLevelDown);
        confirmButton?.onClick.RemoveListener(OnClickConfirm);
        resetButton?.onClick.RemoveListener(OnClickReset);
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            slot.OnSlotClicked -= OnSkillClicked;
            slot.OnSlotDecreaseRequested -= OnSkillDecreaseRequested;
            slot.OnSlotHoverEnter -= OnSkillHoverEnter;
            slot.OnSlotHoverExit -= OnSkillHoverExit;
        }
    }

    /// <summary>표시할 프로필을 연결한다. 저장 준비가 끝나기 전에는 변경을 막을 수 있다.</summary>
    public void Bind(PassiveSkillManager manager, bool canChange = true, string reason = null)
    {
        if (isActiveAndEnabled && skillManager != null) skillManager.OnProfileChanged -= RefreshAll;
        skillManager = manager;
        allowChanges = canChange;
        unavailableReason = reason;
        if (isActiveAndEnabled && skillManager != null) skillManager.OnProfileChanged += RefreshAll;
        selectedId = null;
        RefreshAll();
    }

    private void RefreshAll()
    {
        allSkills.Clear();
        var activeSkills = new List<KY_PassiveSkillData>();
        int index = 0;
        foreach (PassiveSkillId id in Enum.GetValues(typeof(PassiveSkillId)))
        {
            var definition = skillManager != null ? skillManager.GetDefinition(id) : null;
            int level = skillManager != null ? skillManager.GetCurrentLevel(id) : 0;
            var data = new KY_PassiveSkillData
            {
                id = id.ToString(),
                skillName = definition != null ? definition.displayName : "프로필 준비 중",
                description = definition != null ? DescribeEffect(id, definition, level) : "패시브 데이터 연결을 확인하세요.",
                isActive = level > 0
            };
            allSkills.Add(data);
            if (index < slots.Count && slots[index] != null)
            {
                slots[index].Render(new PassiveSkillData
                {
                    id = id,
                    definition = definition,
                    unlockedLevel = skillManager != null ? skillManager.GetUnlockedLevel(id) : 0,
                    currentLevel = level
                });
                if (slots[index].activeHighlight != null) slots[index].activeHighlight.SetActive(data.isActive);
            }
            if (data.isActive) activeSkills.Add(data);
            index++;
        }
        if (activeListView != null) activeListView.Render(activeSkills);
        if (pointText != null)
            pointText.text = skillManager != null && skillManager.CurrentProfile != null
                ? $"골드 : {skillManager.CurrentProfile.gold:N0}" : "프로필 준비 중";
        RefreshSelection();
    }

    private void OnSkillClicked(PassiveSkillData data) { if (data != null) ChangePendingLevel(data.id, levelUpButton != null ? 0 : 1); }
    private void OnSkillDecreaseRequested(PassiveSkillData data) { if (data != null) ChangePendingLevel(data.id, -1); }

    /// <summary>선택한 스킬의 미리보기 단계만 바꾼다. 구매는 확인 버튼에서 처리한다.</summary>
    public void OnClickLevelUp() => ChangeSelectedLevel(1);
    public void OnClickLevelDown() => ChangeSelectedLevel(-1);

    private void ChangeSelectedLevel(int change)
    {
        if (!selectedId.HasValue) return;
        ChangePendingLevel(selectedId.Value, change);
    }

    /// <summary>좌클릭은 한 단계 올리고 우클릭은 내린다. 확인 전에는 골드와 저장값을 바꾸지 않는다.</summary>
    private void ChangePendingLevel(PassiveSkillId id, int change)
    {
        if (skillManager == null) return;
        var definition = skillManager.GetDefinition(id);
        if (definition == null) return;
        if (selectedId != id) pendingLevel = skillManager.GetCurrentLevel(id);
        selectedId = id;
        if (allowChanges && id != PassiveSkillId.Undecided)
            pendingLevel = Mathf.Clamp(pendingLevel + change, 0, definition.maxLevel);
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        RefreshControls();
        if (descriptionView == null) return;
        if (!allowChanges)
        {
            descriptionView.Render(new KY_PassiveSkillData
            {
                skillName = "패시브 조회",
                description = unavailableReason ?? "현재는 프로필을 변경할 수 없습니다."
            });
            return;
        }
        if (!selectedId.HasValue || skillManager == null)
        {
            descriptionView.Render(new KY_PassiveSkillData
            {
                skillName = "패시브 선택",
                description = levelUpButton != null
                    ? "패시브를 선택한 뒤 + / - 버튼으로 단계를 조절하세요.\n확인을 눌러 적용합니다."
                    : "아이콘 좌클릭: 단계 올리기\n우클릭: 단계 내리기\n확인을 눌러 적용합니다."
            });
            return;
        }
        PassiveSkillId id = selectedId.Value;
        var definition = skillManager.GetDefinition(id);
        if (definition == null) return;
        int cost = skillManager.GetUnlockCostToLevel(id, pendingLevel);
        descriptionView.Render(new KY_PassiveSkillData
        {
            skillName = definition.displayName,
            description = id == PassiveSkillId.Undecided ? "효과가 아직 정해지지 않아 구매할 수 없습니다." :
                $"현재 {skillManager.GetCurrentLevel(id)} → 적용할 단계 {pendingLevel}/{definition.maxLevel}\n" +
                DescribeEffect(id, definition, pendingLevel) + $"\n해금 비용: {cost:N0} 골드\n" +
                (skillManager.CurrentProfile == null ? "프로필을 먼저 불러오세요." :
                    skillManager.CurrentProfile.gold < cost ? "골드가 부족합니다." : "확인을 누르면 적용됩니다.")
        });
    }

    private void RefreshControls()
    {
        var definition = selectedId.HasValue && skillManager != null ? skillManager.GetDefinition(selectedId.Value) : null;
        bool canChange = allowChanges && skillManager != null && skillManager.CurrentProfile != null;
        bool selected = canChange && definition != null && selectedId != PassiveSkillId.Undecided;
        int cost = selected ? skillManager.GetUnlockCostToLevel(selectedId.Value, pendingLevel) : 0;
        if (pendingLevelText != null) pendingLevelText.text = selectedId.HasValue ? pendingLevel.ToString() : "-";
        if (levelUpButton != null) levelUpButton.interactable = selected && pendingLevel < definition.maxLevel;
        if (levelDownButton != null) levelDownButton.interactable = selected && pendingLevel > 0;
        if (confirmButton != null) confirmButton.interactable = selected &&
            pendingLevel != skillManager.GetCurrentLevel(selectedId.Value) && skillManager.CurrentProfile.gold >= cost;
        if (resetButton != null) resetButton.interactable = canChange && allSkills.Exists(skill => skill.isActive);
        if (confirmButtonText != null) confirmButtonText.text = cost > 0 ? $"{cost:N0} 골드" : "적용";
    }

    private void OnSkillHoverEnter(PassiveSkillData data)
    {
        // KY_PassiveSkillSlot 이벤트가 PassiveSkillData를 주므로, descriptionView(KY_PassiveSkillData 전용)에
        // 넘기기 위해 allSkills에서 같은 id의 기존 데이터를 찾아 그대로 쓴다.
        if (data == null || descriptionView == null) return;
        var kyData = allSkills.Find(s => s.id == data.id.ToString());
        if (kyData != null) descriptionView.Render(kyData);
    }

    private void OnSkillHoverExit() => RefreshSelection();

    /// <summary>해금한 단계와 골드는 유지하고 현재 적용한 단계만 모두 해제한다.</summary>
    public void OnClickReset()
    {
        if (!allowChanges || skillManager == null || skillManager.CurrentProfile == null) return;
        selectedId = null;
        skillManager.ResetAllCurrentLevels();
    }

    /// <summary>선택한 단계의 비용과 해금 여부는 기존 매니저가 검사하고 저장한다.</summary>
    public void OnClickConfirm()
    {
        if (!allowChanges || skillManager == null || !selectedId.HasValue || selectedId == PassiveSkillId.Undecided) return;
        skillManager.TryApplyLevel(selectedId.Value, pendingLevel);
        RefreshSelection();
    }

    private static string DescribeEffect(PassiveSkillId id, PassiveSkillDefinition definition, int level)
    {
        float value = definition.GetValue(level);
        return id switch
        {
            PassiveSkillId.Revive => level > 0 ? $"부활 시 최대 체력의 {value:0.#}% 회복" : "부활 효과 미적용",
            PassiveSkillId.CampHealBonus => $"캠프에서 최대 체력의 {(level > 0 ? value : 20f):0.#}% 회복",
            PassiveSkillId.ShopEnhance => $"상점 할인 {value:0.#}% / 추가 리롤 {(level > 0 ? definition.extraRerollCount : 0)}회",
            PassiveSkillId.Undecided => "효과 미정",
            _ => $"{definition.displayName} +{value:0.#}%"
        };
    }
}
