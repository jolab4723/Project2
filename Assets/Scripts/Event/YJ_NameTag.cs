using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class YJ_NameTag : MonoBehaviour
{
    private TMP_Text text;
    private Image image;

    private void Awake()
    {
        text = GetComponentInChildren<TMP_Text>();
        image = GetComponentInChildren<Image>();
    }

    public void Active(bool active)
    {
        this.gameObject.SetActive(active);
    }

    public void ChangeText(string str)
    {
        text.text = str;
    }
}
