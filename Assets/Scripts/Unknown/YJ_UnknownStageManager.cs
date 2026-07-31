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

    [Header("Localization")]
    [SerializeField] private GameLanguage currentLanguage = GameLanguage.KOR;

    [Header("Runtime")]
    [SerializeField] private YJ_UnknownStageDefinitionSO selectedStage;

    private void Start()
    {
        if ( ! ResolveReferences() ||  ! TrySelectRandomStage())
            return;

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
            stageDatabase =Resources.Load<YJ_UnknownStageDatabaseSO>(StageDatabaseResourcePath);
        }

        if (labelDatabase == null)
        {
            labelDatabase =Resources.Load<YJ_UnknownStageLabelDatabaseSO>(LabelDatabaseResourcePath);
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

    private void ApplySelectedStage(bool createButtons)
    {
        YJ_UnknownStageLabel label = labelDatabase.GetLabel(selectedStage.StageId, currentLanguage);

        if (label == null)
        {
            Log.Error($"Unknown Stage label was not found: {selectedStage.StageId}");
            return;
        }

        unknownStageContents.StageTitleSet(label.stageName);
        unknownStageContents.StageContentSet(label.stageDescription);
        unknownStageContents.StageBackgroundSet(selectedStage.BackgroundImage);

        int choiceCount = Mathf.Clamp(selectedStage.ChoiceNumber, 1, 3);
        BuildChoiceTexts(label, choiceCount, out List<string> titles, out List<string> descriptions);

        if (createButtons)
            choiceButtonBox.ButtonCreate(choiceCount);

        choiceButtonBox.ButtonTextSet(titles, descriptions);
    }

    private static void BuildChoiceTexts(YJ_UnknownStageLabel label, int choiceCount, out List<string> titles, out List<string> descriptions)
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
