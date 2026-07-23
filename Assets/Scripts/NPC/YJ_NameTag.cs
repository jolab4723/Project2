using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class YJ_NameTag : MonoBehaviour
{
    private TMP_Text nameTagText;
    private Image nameTagImage;

    private void Awake()
    {
        nameTagText = GetComponentInChildren<TMP_Text>();
        nameTagImage = GetComponentInChildren<Image>();
    }

    public void Active(bool active)
    {
        this.gameObject.SetActive(active);
    }

    public void ChangeText(string str)
    {
        nameTagText.text = str;
    }
}
