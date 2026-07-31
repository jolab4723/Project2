using Core;
using TMPro;
using UnityEngine;

public class YJ_ChoiceButton : MonoBehaviour
{
    [SerializeField] private TMP_Text buttonTitle;
    [SerializeField] private TMP_Text buttonContent;
    [SerializeField] private string stageSelectSceneName = "StageSelect";
    [SerializeField] private bool completePendingStage = true;

    private bool transitionRequested;

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
        if (transitionRequested)
            return;

        if (string.IsNullOrWhiteSpace(stageSelectSceneName))
        {
            Log.Error("Stage Select scene name is empty.");
            return;
        }

        SceneLoader sceneLoader = SceneLoader.Instance;
        if (sceneLoader == null)
        {
            Log.Error("SceneLoader could not be found.");
            return;
        }

        if (sceneLoader.IsLoading)
            return;

        if (completePendingStage && !CompletePendingStage())
        {
            Log.Error("Failed to complete the pending Unknown stage node.");
            return;
        }

        transitionRequested = true;
        sceneLoader.LoadScene(stageSelectSceneName);
    }

    private bool CompletePendingStage()
    {
        YJ_StageSaveService saveService =
            FindFirstObjectByType<YJ_StageSaveService>();

        if (saveService == null)
            saveService = gameObject.AddComponent<YJ_StageSaveService>();

        if (!saveService.HasSaveFile)
        {
            Log.Warning("Stage map save file was not found. Pending node completion was skipped.");
            return true;
        }

        return saveService.CompletePendingNode();
    }
}
