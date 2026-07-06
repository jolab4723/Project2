using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KY_SkillSlot : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI keyText;

    public void SetIcon(Sprite sprite)
    {
        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    public void ClearIcon()
    {
        icon.sprite = null;
        icon.enabled = false;
    }

    public void SetKeyText(string key)
    {
        keyText.text = key;
    }
}