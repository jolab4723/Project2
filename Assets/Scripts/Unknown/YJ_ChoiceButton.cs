using Core;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class YJ_ChoiceButton : MonoBehaviour
{
    [SerializeField] private TMP_Text buttonTitle;
    [SerializeField] private TMP_Text buttonContent;
    [SerializeField] private string stageSelectSceneName = "StageSelect";
    [SerializeField] private bool completePendingStage = true;

    private YJ_ChoiceButtonBox choiceButtonBox;
    private bool transitionRequested;

    private void Awake()
    {
        choiceButtonBox = GetComponentInParent<YJ_ChoiceButtonBox>();
    }

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

        if (choiceButtonBox == null)
        {
            Log.Error("YJ_ChoiceButtonBox could not be found.");
            return;
        }

        if (choiceButtonBox.IsExitPlaying)
            return;

        if (completePendingStage && ! CompletePendingStage())
        {
            Log.Error("Failed to complete the pending Unknown stage node.");
            return;
        }

        // 버튼 클릭으로 인한 효과 넣는곳

        // 스테이지 셀렉트 씬으로 변경
        TweenCallback loadScene = () =>
        {
            if (sceneLoader == null || sceneLoader.IsLoading)
                return;

            sceneLoader.LoadScene(stageSelectSceneName);
        };

        if (choiceButtonBox.PlayExit(this, loadScene))
            transitionRequested = true;
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
