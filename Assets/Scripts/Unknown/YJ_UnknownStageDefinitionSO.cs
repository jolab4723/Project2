using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[CreateAssetMenu(
    fileName = "UnknownStageDefinition",
    menuName = "Unknown Stage/Definition")]
public class YJ_UnknownStageDefinitionSO : ScriptableObject
{
    [SerializeField] private string stageId;
    [SerializeField] private string stageName;
    [SerializeField, MinMax(0, 3)] private int choiceNumber = 1;
    [SerializeField] private Sprite backgroundImage;

    public string StageId => stageId;
    public string StageName => stageName;
    public int ChoiceNumber => choiceNumber;
    public Sprite BackgroundImage => backgroundImage;

#if UNITY_EDITOR
    public void SetEditorData(
        string id,
        string displayName,
        int choices,
        Sprite background)
    {
        stageId = id;
        stageName = displayName;
        choiceNumber = Mathf.Clamp(choices, 0, 3);
        backgroundImage = background;
    }
#endif
}
