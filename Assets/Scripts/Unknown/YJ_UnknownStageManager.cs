using UnityEngine;
using System.Collections.Generic;

public class YJ_UnknownStageManager : MonoBehaviour
{
    [SerializeField] private YJ_ChoiceButtonBox choiceButtonBox;

    [SerializeField] private List<string> buttonTitle = new() {"Title1", "Title2", "Title3"};
    [SerializeField] private List<string> buttonContent = new() { "Content1", "Content2", "Content3"};
    [SerializeField] private int buttonNumber = 0;

    void Start()
    {
        if (choiceButtonBox == null)
            return;

        choiceButtonBox.ButtonCreate(buttonNumber);

        if (buttonTitle.Count != buttonNumber || buttonContent.Count != buttonNumber)
            Log.Warning($"선택 버튼 갯수와 텍스트 갯수가 일치하지 않습니다. : {buttonNumber}, {buttonTitle.Count}, {buttonContent.Count}");

        choiceButtonBox.ButtonTextSet(buttonTitle, buttonContent);
    }
}
