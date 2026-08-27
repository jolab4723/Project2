using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 선택된 캐릭터 정보가 표시되는 슬롯입니다.
public class KY_LobbyCharacterSlot : MonoBehaviour
{
    public Image portraitImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public KY_LobbyRadarChart radarChart;

    public void Show(KY_LobbyCharacterData data)
    {
        portraitImage.sprite = data.portraitImage;
        nameText.text = data.characterName;
        descriptionText.text = data.description;

        if (radarChart != null)
            radarChart.SetValues(data.powerGrade, data.healthGrade, data.difficultyGrade);
    }
}
