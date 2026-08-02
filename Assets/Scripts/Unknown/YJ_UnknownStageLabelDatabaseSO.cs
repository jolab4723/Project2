using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AllUnknownStageLabels",
    menuName = "Unknown Stage/Label Database")]
public class YJ_UnknownStageLabelDatabaseSO : ScriptableObject
{
    [SerializeField] private List<YJ_UnknownStageLabel> korLabels = new();
    [SerializeField] private List<YJ_UnknownStageLabel> engLabels = new();
    [SerializeField] private List<YJ_UnknownStageLabel> jpnLabels = new();
    [SerializeField] private List<YJ_UnknownStageLabel> chnLabels = new();

    private readonly Dictionary<GameLanguage, Dictionary<string, YJ_UnknownStageLabel>>
        lookups = new();

    public YJ_UnknownStageLabel GetLabel(string stageId, GameLanguage language)
    {
        if (string.IsNullOrWhiteSpace(stageId))
            return null;

        Dictionary<string, YJ_UnknownStageLabel> lookup =
            GetOrBuildLookup(language);

        if (lookup.TryGetValue(stageId, out YJ_UnknownStageLabel label))
            return label;

        if (language != GameLanguage.KOR)
        {
            Dictionary<string, YJ_UnknownStageLabel> fallback =
                GetOrBuildLookup(GameLanguage.KOR);

            if (fallback.TryGetValue(stageId, out label))
                return label;
        }

        return null;
    }

#if UNITY_EDITOR
    public void SetEditorLabels(
        List<YJ_UnknownStageLabel> korean,
        List<YJ_UnknownStageLabel> english,
        List<YJ_UnknownStageLabel> japanese,
        List<YJ_UnknownStageLabel> chinese)
    {
        korLabels = korean ?? new List<YJ_UnknownStageLabel>();
        engLabels = english ?? new List<YJ_UnknownStageLabel>();
        jpnLabels = japanese ?? new List<YJ_UnknownStageLabel>();
        chnLabels = chinese ?? new List<YJ_UnknownStageLabel>();
        lookups.Clear();
    }
#endif

    private void OnEnable()
    {
        lookups.Clear();
    }

    private Dictionary<string, YJ_UnknownStageLabel> GetOrBuildLookup(
        GameLanguage language)
    {
        if (lookups.TryGetValue(
                language,
                out Dictionary<string, YJ_UnknownStageLabel> lookup))
        {
            return lookup;
        }

        List<YJ_UnknownStageLabel> labels = language switch
        {
            GameLanguage.ENG => engLabels,
            GameLanguage.JPN => jpnLabels,
            GameLanguage.CHN => chnLabels,
            _ => korLabels
        };

        lookup = new Dictionary<string, YJ_UnknownStageLabel>();

        foreach (YJ_UnknownStageLabel label in labels)
        {
            if (label == null || string.IsNullOrWhiteSpace(label.stageId))
                continue;

            lookup[label.stageId] = label;
        }

        lookups[language] = lookup;
        return lookup;
    }
}
