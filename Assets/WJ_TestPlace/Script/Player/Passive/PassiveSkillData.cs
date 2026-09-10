using UnityEngine;
using Core;

/// <summary>
/// 패시브 스킬 하나의 "지금 상태"를 UI가 그대로 그릴 수 있게 담은 뷰 데이터.
/// KY_PassiveSkillData(문자열 id/이름/설명을 직접 채워야 하는 테스트용 구조, "새 구조가 생기면
/// 삭제하시오" 주석이 달려있던 프로토타입)를 대체한다. 이 클래스는 실제 PassiveSkillDefinition(디자인
/// 데이터)과 PassiveSkillEntry(세이브된 해금/적용 레벨)를 그대로 들고 있어서, 이름/최대레벨/수치가
/// 항상 DB 원본과 일치하고 별도로 복사해서 채울 필요가 없다.
/// </summary>
public class PassiveSkillData
{
    public PassiveSkillId id;
    public PassiveSkillDefinition definition;
    public int unlockedLevel;
    public int currentLevel;

    [Tooltip("아직 아이콘 스프라이트 체계가 없어 항상 null - 나중에 아이콘이 생기면 채워서 쓴다.")]
    public Sprite icon;

    public string DisplayName => definition != null ? definition.displayName : id.ToString();
    public int MaxLevel => definition != null ? definition.maxLevel : 0;
    public bool IsActive => currentLevel > 0;

    /// <summary>아이콘이 없을 때 대체로 보여줄 "이름\n현재/최대" 라벨.</summary>
    public string LevelLabel => $"{DisplayName}\n{currentLevel}/{MaxLevel}";

    /// <summary>아이콘 슬롯의 작은 레벨 배지용 텍스트. 최대 레벨에 도달하면 숫자 대신 "M".</summary>
    public string LevelBadgeText => MaxLevel > 0 && currentLevel >= MaxLevel ? "M" : currentLevel.ToString();

    public float GetEffectValue(int level) => definition != null ? definition.GetValue(level) : 0f;
    public int GetUnlockCost(int level) => definition != null ? definition.GetUnlockCost(level) : 0;
}
