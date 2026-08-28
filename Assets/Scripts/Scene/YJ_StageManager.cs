using UnityEngine;
using Core;

public class YJ_StageManager : MonoBehaviour
{
    private DataManager dataManager;
    [SerializeField] private bool bootScene = false;
    public bool isBossStage = false;

    [SerializeField] private WBH_EnemySpawnManager enemySpawnManager;

    private bool stageClear;

    public bool StageClear => stageClear;

    private void Awake()
    {
        enemySpawnManager ??= FindFirstObjectByType<WBH_EnemySpawnManager>();
    }

    private void OnEnable()
    {
        if (enemySpawnManager != null)
            enemySpawnManager.WaveCompleted += HandleWaveCompleted;
    }

    private void OnDisable()
    {
        if (enemySpawnManager != null)
            enemySpawnManager.WaveCompleted -= HandleWaveCompleted;
    }

    private void Start()
    {
        if (bootScene)
        {
            Initialize();
            return;
        }

        StartScene();
        StartStage();
    }

    public void Initialize()
    {
        if ( ! TryGetDataManager())
            return;

        dataManager.ResetGameplayData();
        Log.Print("ResetGameplayData");
    }

    public void StartScene()
    {
        if ( ! TryGetDataManager())
            return;

        dataManager.LoadGameplayData();
    }

    public void EndScene()
    {
        if ( ! TryGetDataManager())
            return;

        dataManager.SaveGameplayData();
    }

    private bool TryGetDataManager()
    {
        if (dataManager == null)
            dataManager = DataManager.Instance;

        if (dataManager != null)
            return true;

        Log.Error("DataManager를 찾을 수 없습니다.");
        return false;
    }

    private void StartStage()
    {
        stageClear = false;

        if (enemySpawnManager == null)
            return;

        AdvanceStage();
    }

    private void HandleWaveCompleted()
    {
        AdvanceStage();
    }

    private void AdvanceStage()
    {
        if (stageClear || enemySpawnManager == null)
            return;

        if(enemySpawnManager.HasNextWave)
        {
            enemySpawnManager.TrySpawnNextWave();
            return;
        }

        CompleteStage();
    }

    private void CompleteStage()
    {
        if (stageClear)
            return;

        stageClear = true;

        YJ_PortalActive portalActive = FindFirstObjectByType<YJ_PortalActive>();

        if(portalActive != null)
        {
            portalActive.Active(true);
        }
        else
        {
            Log.Error("스테이지 클리어 포탈이 연결되지 않았습니다.");
        }

        Log.Print("Stage Clear");
    }

}