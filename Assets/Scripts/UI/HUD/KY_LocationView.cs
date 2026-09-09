using UnityEngine;
using UnityEngine.Serialization;
using TMPro;

public class KY_LocationView : MonoBehaviour
{
    [FormerlySerializedAs("chapterText")]
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private YJ_StageManager stageManager;

    private YJ_StageSaveService stageSaveService;
    [SerializeField] private bool usesExternalLocation; // SW 수정

    /// <summary>서버가 확정한 현재 구역을 표시하며 로컬 저장 파일을 읽지 않는다.</summary>
    public void BindLocation(int act, int floor, bool boss, bool camp)
    {
        usesExternalLocation = true;
        if (stageText != null)
            stageText.text = camp ? $"Act{act} Camp" : boss ? $"Act{act} Boss Stage" : $"Act{act} Floor{floor}";
    }

    void Awake()
    {
        if (stageManager != null || usesExternalLocation)
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
        if (usesExternalLocation) return;
        if (stageText != null)
        {
            if (stageManager != null && stageManager.isBossStage)
                stageText.text = $"Act{act} Boss Stage";
            else
                stageText.text = $"Act{act} Floor{stage}";
        }
            
    }

    private void RefreshLocation()
    {
        if (stageText == null || usesExternalLocation)
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
