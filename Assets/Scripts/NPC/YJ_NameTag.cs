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
        // SW 수정: 한 번도 켜지지 않아 Awake 전이거나 텍스트 자식이 꺼져 있어도 찾아서 쓰고, 없으면 건너뛴다.
        if (nameTagText == null)
            nameTagText = GetComponentInChildren<TMP_Text>(true);
        if (nameTagText != null)
            nameTagText.text = str;
    }
}
