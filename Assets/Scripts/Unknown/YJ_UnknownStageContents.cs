using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class YJ_UnknownStageContents : MonoBehaviour
{
    [SerializeField] private TMP_Text stageTitle;
    [SerializeField] private TMP_Text stageContent;
    [SerializeField] private Image stageBackground;

    private void Awake()
    {
        if (stageBackground != null || transform.parent == null)
            return;

        Transform background = transform.parent.Find("Background");
        if (background != null)
            stageBackground = background.GetComponent<Image>();
    }

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

    public void StageBackgroundSet(Sprite sprite)
    {
        if (stageBackground == null || sprite == null)
            return;

        stageBackground.sprite = sprite;
    }
}
