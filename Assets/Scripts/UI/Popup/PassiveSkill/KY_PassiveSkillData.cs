using UnityEngine;

[System.Serializable]

// 패시브 스킬 팝업 테스트 용도로 만든 구조입니다. 새 구조가 생기면 삭제하시오
public class KY_PassiveSkillData
{
    public string id;
    public string skillName;
    [TextArea] public string description;
    public string iconName;
    public int cost;

    [System.NonSerialized] public Sprite icon;
    [System.NonSerialized] public bool isActive;
}