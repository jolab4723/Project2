using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.IO;

public class KY_PassiveSkillPopup : KY_PopupBase
{
    [Header("데이터")]
    public List<KY_PassiveSkillData> allSkills;
    public int maxPoints = 12;

    [Header("슬롯 (씬에 미리 배치, 순서대로 매핑)")]
    public List<KY_PassiveSkillSlot> slots;

    [Header("View")]
    public TextMeshProUGUI pointText;
    public KY_PassiveSkillDescriptionView descriptionView;
    public KY_PassiveSkillListView activeListView;

    private int usedPoints;

    void Awake()
    {
        LoadSkillData();

        foreach (var slot in slots)
        {
            slot.OnSlotClicked += OnSkillClicked;
            slot.OnSlotHoverEnter += OnSkillHoverEnter;
            slot.OnSlotHoverExit += OnSkillHoverExit;
        }
    }

    void LoadSkillData()
    {
        string path = Path.Combine(Application.dataPath, "Scripts/UI/Popup/PassiveSkill/KY_PassiveSkillData.json");
        string jsonText = File.ReadAllText(path);
        KY_PassiveSkillDataList dataList = JsonUtility.FromJson<KY_PassiveSkillDataList>(jsonText);
        allSkills = dataList.skills;
    }

    public override void Open()
    {
        base.Open();
        RefreshAll();
    }

    void RefreshAll()
    {
        for (int i = 0; i < allSkills.Count && i < slots.Count; i++)
            slots[i].Render(allSkills[i]);

        RefreshPointText();
        RefreshActiveList();
        descriptionView.Clear();
    }

    void RefreshPointText()
    {
        pointText.text = "스킬 포인트 : [" + (maxPoints - usedPoints) + "] ";
    }

    void RefreshActiveList()
    {
        List<KY_PassiveSkillData> activeSkills = new List<KY_PassiveSkillData>();
        foreach (var skill in allSkills)
            if (skill.isActive) activeSkills.Add(skill);

        activeListView.Render(activeSkills);
    }

    void OnSkillClicked(KY_PassiveSkillData data)
    {
        if (data.isActive)
        {
            data.isActive = false;
            usedPoints -= data.cost;
        }
        else
        {
            if (usedPoints + data.cost > maxPoints)
            {
                // TODO: 포인트 부족 피드백 연출
                return;
            }
            data.isActive = true;
            usedPoints += data.cost;
        }

        RefreshAll();
    }

    void OnSkillHoverEnter(KY_PassiveSkillData data)
    {
        descriptionView.Render(data);
    }

    void OnSkillHoverExit()
    {
        descriptionView.Clear();
    }

    public void OnClickReset()
    {
        foreach (var skill in allSkills)
            skill.isActive = false;
        usedPoints = 0;
        RefreshAll();
    }

    public void OnClickConfirm()
    {
        // TODO: 실제 저장 로직 연결
        KY_PopupManager.Instance.Hide();
    }
}