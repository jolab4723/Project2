using System;
using System.Collections.Generic;
using Core;
using TMPro;
using UnityEngine;

/// <summary>실제 패시브의 골드와 단계를 표시하고, 확인한 변경만 기존 매니저에 전달한다.</summary>
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

    [SerializeField] private bool allowChanges = true;
    [SerializeField] private string unavailableReason;
    private PassiveSkillId? selectedId;
    private int pendingLevel;

    private void Awake()
    {
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
                skillName = definition != null ? $"{definition.displayName}\n{level}/{definition.maxLevel}" : "프로필 준비 중",
                description = definition != null ? DescribeEffect(id, definition, level) : "패시브 데이터 연결을 확인하세요.",
                isActive = level > 0
            };
            allSkills.Add(data);
            if (index < slots.Count && slots[index] != null) slots[index].Render(data);
            if (data.isActive) activeSkills.Add(data);
            index++;
        }
        if (activeListView != null) activeListView.Render(activeSkills);
        if (pointText != null)
            pointText.text = skillManager != null && skillManager.CurrentProfile != null
                ? $"골드 : {skillManager.CurrentProfile.gold:N0}" : "프로필 준비 중";
        RefreshSelection();
    }

    private void OnSkillClicked(KY_PassiveSkillData data) => ChangePendingLevel(data, 1);
    private void OnSkillDecreaseRequested(KY_PassiveSkillData data) => ChangePendingLevel(data, -1);

    /// <summary>좌클릭은 한 단계 올리고 우클릭은 내린다. 확인 전에는 골드와 저장값을 바꾸지 않는다.</summary>
    private void ChangePendingLevel(KY_PassiveSkillData data, int change)
    {
        if (data == null || !Enum.TryParse(data.id, out PassiveSkillId id) || skillManager == null) return;
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
                description = "아이콘 좌클릭: 단계 올리기\n우클릭: 단계 내리기\n확인을 눌러 적용합니다."
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

    private void OnSkillHoverEnter(KY_PassiveSkillData data)
    {
        if (data != null && descriptionView != null) descriptionView.Render(data);
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
