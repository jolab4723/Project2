using UnityEngine;
using TMPro;

// 패시브 스킬 팝업의 커서가 올라간 스킬 효과를 보여주는 스크립트
public class KY_PassiveSkillDescriptionView : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    public void Render(KY_PassiveSkillData data)
    {
        nameText.text = data.skillName;
        descriptionText.text = data.description;
    }

    public void Clear()
    {
        nameText.text = "";
        descriptionText.text = "";
    }
}