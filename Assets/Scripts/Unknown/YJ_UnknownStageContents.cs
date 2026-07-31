using TMPro;
using UnityEngine;

public class YJ_UnknownStageContents : MonoBehaviour
{
    [SerializeField] private TMP_Text stageTitle;
    [SerializeField] private TMP_Text stageContent;

    public void StageTitleSet(string str)
    {
        if (stageTitle == null)
            return;

        stageTitle.text = str;
    }

    public void StageContentSet(string str)
    {
        if (stageContent == null)
            return;

        stageContent.text = str;
    }
}
