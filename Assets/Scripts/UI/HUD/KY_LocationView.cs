using UnityEngine;
using TMPro;

public class KY_LocationView : MonoBehaviour
{
    public TextMeshProUGUI chapterText;
    public TextMeshProUGUI zoneText;

    void OnEnable()
    {
        KY_GameEvents.OnLocationChanged += OnLocationChanged;
    }

    void OnDisable()
    {
        KY_GameEvents.OnLocationChanged -= OnLocationChanged;
    }

    void OnLocationChanged(int chapter, int zone)
    {
        chapterText.text = chapter.ToString();
        zoneText.text = zone.ToString();
    }
}