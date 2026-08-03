using System.Collections.Generic;
using UnityEngine;

public class YJ_UnknownStageManager : MonoBehaviour
{
    private const string StageDatabaseResourcePath =
        "DataFiles/UnknownStageData/3. GeneratedAssets/AllUnknownStages";
    private const string LabelDatabaseResourcePath =
        "DataFiles/UnknownStageData/3. GeneratedAssets/AllUnknownStageLabels";

    [Header("UI")]
    [SerializeField] private YJ_UnknownStageContents unknownStageContents;
    [SerializeField] private YJ_ChoiceButtonBox choiceButtonBox;

    [Header("Data")]
    [SerializeField] private YJ_UnknownStageDatabaseSO stageDatabase;
    [SerializeField] private YJ_UnknownStageLabelDatabaseSO labelDatabase;
    [SerializeField] private YJ_StageSaveService stageSaveService;

    [Header("Localization")]
    [SerializeField] private GameLanguage currentLanguage = GameLanguage.KOR;

    [Header("Runtime")]
    [SerializeField] private YJ_UnknownStageDefinitionSO selectedStage;

    private void Start()
    {
        if (!ResolveReferences())
            return;

        if (!TrySelectSavedStage() &&
            selectedStage == null &&
            !TrySelectRandomStage())
        {
            return;
        }

        ApplySelectedStage(true);
    }

    public void SetLanguage(GameLanguage language)
    {
        if (currentLanguage == language)
            return;

        currentLanguage = language;

        if (selectedStage != null)
            ApplySelectedStage(false);
    }

    private bool ResolveReferences()
    {
        if (stageDatabase == null)
        {
            stageDatabase =
                Resources.Load<YJ_UnknownStageDatabaseSO>(
                    StageDatabaseResourcePath);
        }

        if (labelDatabase == null)
        {
            labelDatabase =
                Resources.Load<YJ_UnknownStageLabelDatabaseSO>(
                    LabelDatabaseResourcePath);
        }

        if (unknownStageContents == null)
        {
            Log.Error("YJ_UnknownStageContents reference is missing.");
            return false;
        }

        if (choiceButtonBox == null)
        {
            Log.Error("YJ_ChoiceButtonBox reference is missing.");
            return false;
        }

        if (stageDatabase == null)
        {
            Log.Error("AllUnknownStages database could not be loaded.");
            return false;
        }

        if (labelDatabase == null)
        {
            Log.Error("AllUnknownStageLabels database could not be loaded.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Stage Select가 저장한 pending Event 노드의 고정 이벤트 ID를 불러옵니다.
    /// </summary>
    private bool TrySelectSavedStage()
    {
        FindSaveService();

        if (stageSaveService == null ||
            !stageSaveService.HasSaveFile ||
            !stageSaveService.TryGetPendingNode(
                out StageNodeSaveData pendingNode))
        {
            return false;
        }

        if (pendingNode.type != StageNodeType.Event ||
            string.IsNullOrWhiteSpace(pendingNode.unknownStageId))
        {
            Log.Warning(
                $"pending 노드에 Unknown 이벤트 ID가 없습니다: " +
                $"{pendingNode.id}");
            return false;
        }

        selectedStage =
            stageDatabase.GetById(pendingNode.unknownStageId);

        if (selectedStage != null)
            return true;

        Log.Warning(
            $"저장된 Unknown 이벤트를 데이터베이스에서 찾지 못했습니다: " +
            $"{pendingNode.unknownStageId}");
        return false;
    }

    /// <summary>
    /// Unknown 씬 단독 실행처럼 저장된 Event 노드가 없을 때 사용할 테스트용 임의 이벤트를 선택합니다.
    /// </summary>
    private bool TrySelectRandomStage()
    {
        IReadOnlyList<YJ_UnknownStageDefinitionSO> stages =
            stageDatabase.Stages;

        if (stages == null || stages.Count == 0)
        {
            Log.Error("Unknown Stage database is empty.");
            return false;
        }

        selectedStage = stages[Random.Range(0, stages.Count)];

        if (selectedStage != null)
            return true;

        Log.Error("The randomly selected Unknown Stage is null.");
        return false;
    }

    /// <summary>
    /// Inspector, 같은 오브젝트, 현재 씬 순서로 저장 서비스를 찾고 없으면 추가합니다.
    /// </summary>
    private void FindSaveService()
    {
        if (stageSaveService != null)
            return;

        stageSaveService = GetComponent<YJ_StageSaveService>();
        if (stageSaveService == null)
            stageSaveService =
                FindFirstObjectByType<YJ_StageSaveService>();

        if (stageSaveService == null)
            stageSaveService =
                gameObject.AddComponent<YJ_StageSaveService>();
    }

    private void ApplySelectedStage(bool createButtons)
    {
        YJ_UnknownStageLabel label = labelDatabase.GetLabel(
            selectedStage.StageId,
            currentLanguage);

        if (label == null)
        {
            Log.Error($"Unknown Stage label was not found: {selectedStage.StageId}");
            return;
        }

        unknownStageContents.StageBackgroundSet(selectedStage.BackgroundImage);

        int choiceCount = Mathf.Clamp(selectedStage.ChoiceNumber, 1, 3);
        BuildChoiceTexts(label, choiceCount, out List<string> titles, out List<string> descriptions);

        if (createButtons)
            choiceButtonBox.ButtonCreate(choiceCount);

        choiceButtonBox.ButtonTextSet(titles, descriptions);
        choiceButtonBox.HideButtons();
        unknownStageContents.PlayTextReveal(
            label.stageName,
            label.stageDescription,
            choiceButtonBox.PlayReveal);
    }

    private static void BuildChoiceTexts(
        YJ_UnknownStageLabel label,
        int choiceCount,
        out List<string> titles,
        out List<string> descriptions)
    {
        titles = new List<string>(choiceCount);
        descriptions = new List<string>(choiceCount);

        if (choiceCount >= 1)
        {
            titles.Add(label.choice1Name);
            descriptions.Add(label.choice1Description);
        }

        if (choiceCount >= 2)
        {
            titles.Add(label.choice2Name);
            descriptions.Add(label.choice2Description);
        }

        if (choiceCount >= 3)
        {
            titles.Add(label.choice3Name);
            descriptions.Add(label.choice3Description);
        }
    }
}
