using UnityEngine;
using UnityEngine.Serialization;
using TMPro;

public class KY_LocationView : MonoBehaviour
{
    [FormerlySerializedAs("chapterText")]
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private YJ_StageManager stageManager;

    private YJ_StageSaveService stageSaveService;

    void Awake()
    {
        if (stageManager != null)
            return;

        stageManager = FindFirstObjectByType<YJ_StageManager>();
    }

    void OnEnable()
    {
        KY_GameEvents.OnLocationChanged += OnLocationChanged;
        RefreshLocation();
    }

    void OnDisable()
    {
        KY_GameEvents.OnLocationChanged -= OnLocationChanged;
    }

    void OnLocationChanged(int act, int stage)
    {
        if (stageText != null)
        {
            if (stageManager.isBossStage)
                stageText.text = $"Act{act} Boss Stage";
            else
                stageText.text = $"Act{act} Floor{stage}";
        }
            
    }

    private void RefreshLocation()
    {
        if (stageText == null)
            return;

        if (stageSaveService == null)
            stageSaveService = FindFirstObjectByType<YJ_StageSaveService>();

        if (stageSaveService == null)
            stageSaveService = gameObject.AddComponent<YJ_StageSaveService>();

        if (!stageSaveService.HasSaveFile ||
            !stageSaveService.TryLoadSaveData(out StageMapSaveData saveData) ||
            string.IsNullOrWhiteSpace(saveData.pendingNodeId))
        {
            return;
        }

        StageNodeSaveData currentNode = saveData.nodes.Find(
            node => node != null && node.id == saveData.pendingNodeId);
        if (currentNode == null)
            return;

        OnLocationChanged((int)saveData.act, currentNode.floor);
    }
}
