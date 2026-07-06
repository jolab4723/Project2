using UnityEngine;
using TMPro;

public class KY_KeyGuideSlot : MonoBehaviour
{
    public TextMeshProUGUI keyText;

    public void SetKeyText(string key)
    {
        keyText.text = key;
    }
}