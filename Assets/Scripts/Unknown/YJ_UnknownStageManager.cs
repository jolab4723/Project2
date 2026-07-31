using UnityEngine;
using System.Collections.Generic;

public class YJ_UnknownStageManager : MonoBehaviour
{
    [SerializeField] private YJ_UnknownStageContents unknownStageContents;
    [SerializeField] private YJ_ChoiceButtonBox choiceButtonBox;

    [SerializeField] private string stageTitle;
    [SerializeField] private string stageContent;
    [SerializeField] private List<string> buttonTitle;
    [SerializeField] private List<string> buttonContent;
    [SerializeField] private int buttonNumber = 0;

    void Start()
    {
        if (unknownStageContents == null || choiceButtonBox == null)
            return;

        SetStageContents();
    }

    private void GetStageText()
    {
        stageTitle = "타이틀";
        stageContent = "컨텐츠";
    }
    private void GetButtonTextList()
    {
        buttonTitle = new() { "Title1", "Title2", "Title3" };
        buttonContent = new() { "Content1", "Content2", "Content3" };
    }

    private void SetStageContents()
    {
        GetStageText();
        GetButtonTextList();

        if (buttonTitle.Count != buttonNumber || buttonContent.Count != buttonNumber)
            Log.Warning($"선택 버튼 갯수와 텍스트 갯수가 일치하지 않습니다. : {buttonNumber}, {buttonTitle.Count}, {buttonContent.Count}");

        unknownStageContents.StageTitleSet(stageTitle);
        unknownStageContents.StageContentSet(stageContent);

        choiceButtonBox.ButtonCreate(buttonNumber);
        choiceButtonBox.ButtonTextSet(buttonTitle, buttonContent);
    }
}
