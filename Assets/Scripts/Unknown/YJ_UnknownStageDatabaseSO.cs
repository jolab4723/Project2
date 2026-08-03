using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AllUnknownStages",
    menuName = "Unknown Stage/Database")]
public class YJ_UnknownStageDatabaseSO : ScriptableObject
{
    [SerializeField]
    private List<YJ_UnknownStageDefinitionSO> stages = new();

    private Dictionary<string, YJ_UnknownStageDefinitionSO> stageLookup;

    public IReadOnlyList<YJ_UnknownStageDefinitionSO> Stages => stages;

    public YJ_UnknownStageDefinitionSO GetById(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
            return null;

        BuildLookupIfNeeded();
        stageLookup.TryGetValue(stageId, out YJ_UnknownStageDefinitionSO stage);
        return stage;
    }

#if UNITY_EDITOR
    public void SetEditorStages(
        List<YJ_UnknownStageDefinitionSO> generatedStages)
    {
        stages = generatedStages ?? new List<YJ_UnknownStageDefinitionSO>();
        stageLookup = null;
    }
#endif

    private void OnEnable()
    {
        stageLookup = null;
    }

    private void BuildLookupIfNeeded()
    {
        if (stageLookup != null)
            return;

        stageLookup = new Dictionary<string, YJ_UnknownStageDefinitionSO>();

        foreach (YJ_UnknownStageDefinitionSO stage in stages)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.StageId))
                continue;

            if (!stageLookup.TryAdd(stage.StageId, stage))
                Log.Warning($"Duplicate Unknown stage ID: {stage.StageId}");
        }
    }
}
