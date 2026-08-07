using UnityEngine;
using TMPro;

public class KY_LocationView : MonoBehaviour
{
    public TextMeshProUGUI stageText;

    void OnEnable()
    {
        KY_GameEvents.OnLocationChanged += OnLocationChanged;
    }

    void OnDisable()
    {
        KY_GameEvents.OnLocationChanged -= OnLocationChanged;
    }

    void OnLocationChanged(int currentStage, int totalStage)
    {
        stageText.text = ($"Stage : {currentStage.ToString()} / {totalStage.ToString()}");
    }
}