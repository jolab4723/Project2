using System.Collections.Generic;
using UnityEngine;
using Core;

/// <summary>
/// 총 12개 패시브 스킬의 고정 디자인 데이터를 담는 에셋. 밸런스 수치를 여기서만 관리한다.
/// 예전엔 정적 클래스+Dictionary였는데, 디자이너가 Inspector에서 직접 값을 조정할 수 있도록
/// ScriptableObject로 옮겼다 (ItemDatabaseSO와 같은 패턴).
/// </summary>
[CreateAssetMenu(fileName = "PassiveSkillDatabase", menuName = "Data/Passive Skill Database")]
public class PassiveSkillDatabaseSO : ScriptableObject
{
    [SerializeField] private List<PassiveSkillDefinition> definitions = new List<PassiveSkillDefinition>();

    private Dictionary<PassiveSkillId, PassiveSkillDefinition> lookup;

    public PassiveSkillDefinition Get(PassiveSkillId id)
    {
        if (lookup == null)
            BuildLookup();

        return lookup.TryGetValue(id, out var definition) ? definition : null;
    }

    private void BuildLookup()
    {
        lookup = new Dictionary<PassiveSkillId, PassiveSkillDefinition>();
        foreach (var definition in definitions)
            lookup[definition.id] = definition;
    }

    private void OnValidate()
    {
        // Inspector에서 값 수정 시 캐시된 lookup이 낡은 상태로 남지 않도록 다음 Get() 호출에서 다시 빌드하게 한다.
        lookup = null;
    }
}
