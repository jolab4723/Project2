using UnityEngine;
using System.Collections.Generic;

// 활성화된 패시브 스킬의 효과 목록 리스트를 보여주는 스크립트입니다.
public class KY_PassiveSkillListView : MonoBehaviour
{
    public List<KY_PassiveSkillEntry> entries;

    public void Render(List<KY_PassiveSkillData> activeSkills)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (i < activeSkills.Count)
            {
                entries[i].gameObject.SetActive(true);
                entries[i].Render(activeSkills[i]);
            }
            else
            {
                entries[i].gameObject.SetActive(false);
            }
        }
    }
}