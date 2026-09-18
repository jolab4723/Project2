using System;
using TMPro;
using UnityEngine;

public class YJ_ChoiceButton : MonoBehaviour
{
    [SerializeField] private TMP_Text buttonTitle;
    [SerializeField] private TMP_Text buttonContent;
    private string stageId;
    private int choiceIndex = -1;
    private Action<string, int> onSelected;

    public void Initialize(string eventId, int index, Action<string, int> callback)
    {
        stageId = eventId;
        choiceIndex = index;
        onSelected = callback;
    }

    public void ButtonTitleSet(string str)
    {
        if (buttonTitle == null)
            return;

        SetRichText(buttonTitle, str);
    }

    public void ButtonContentSet(string str)
    {
        if (buttonContent == null)
            return;

        SetRichText(buttonContent, str);
    }

    private static void SetRichText(TMP_Text target, string text)
    {
        target.richText = true;
        target.text = text ?? string.Empty;
    }

    public void OnClick()
    {
        if (!isActiveAndEnabled)
            return;

        if (string.IsNullOrWhiteSpace(stageId) || choiceIndex < 0 || onSelected == null)
        {
            Log.Error("Unknown 선택 버튼의 이벤트 ID/번호/콜백이 연결되지 않았습니다.");
            return;
        }

        onSelected.Invoke(stageId, choiceIndex);
    }
}
