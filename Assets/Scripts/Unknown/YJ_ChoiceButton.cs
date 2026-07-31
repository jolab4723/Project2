using UnityEngine;
using TMPro;

public class YJ_ChoiceButton : MonoBehaviour
{
    [SerializeField] private TMP_Text buttonTitle;
    [SerializeField] private TMP_Text buttonContent;

    public void ButtonTitleSet(string str)
    {
        if (buttonTitle == null)
            return;

        buttonTitle.text = str;
    }

    public void ButtonContentSet(string str)
    {
        if (buttonContent == null)
            return;

        buttonContent.text = str;
    }

    public void OnClick()
    {
        Log.Print($"[{buttonTitle.text}] 버튼 클릭함");
    }
}
