using UnityEngine;
using TMPro;

// 활성화된 패시브 스킬을 보여주는 코드
public class KY_PassiveSkillEntry : MonoBehaviour
{
    public TextMeshProUGUI effectText;

    public void Render(KY_PassiveSkillData data)
    {
        effectText.text = data.description;
    }
}