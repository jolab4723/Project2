using UnityEngine;
using TMPro;

/// <summary>HUD 키 가이드 한 칸의 단축키 텍스트를 표시한다.</summary>
public class KY_KeyGuideSlot : MonoBehaviour
{
    public TextMeshProUGUI keyText;

    public void SetKeyText(string key)
    {
        keyText.text = key;
    }
}
