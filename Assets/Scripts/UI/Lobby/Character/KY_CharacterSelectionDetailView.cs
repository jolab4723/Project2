using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>선택한 캐릭터의 이름, 설명, 능력치를 표시하는 상세 영역이다.</summary>
public class KY_CharacterSelectionDetailView : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public KY_CharacterStatRadarChart radarChart;

    /// <summary>캐릭터 정보를 상세 UI와 레이더 차트에 표시한다.</summary>
    public void Show(KY_CharacterInfoData data)
    {
        nameText.text = data.characterName;
        descriptionText.text = data.description;

        if (radarChart != null)
            radarChart.SetValues(data.powerGrade, data.healthGrade, data.difficultyGrade);
    }
}
