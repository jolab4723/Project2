using UnityEngine;
using System.Collections;
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

    private IEnumerator Start()
    {
        if (bootScene)
        {
            Initialize();
            yield break;
        }

        // 씬에 배치된 활성 컴포넌트들의 Start 실행을 기다립니다.
        yield return null;

        if ( ! TryStartScene())
            yield break;

        StartStage();
    }

    public void Initialize()
    {
        if ( ! TryGetDataManager())
            return;

        // 런(게임플레이) 데이터는 새로 시작하지만, 영구 프로필(골드/패시브 스킬트리)은 런과 무관하게
        // 항상 이어져야 하므로 리셋하지 않고 그대로 불러온다.
        dataManager.LoadPassiveData();
        dataManager.ResetGameplayData();
        Log.Print("ResetGameplayData");
    }

    public void StartScene()
    {
        TryStartScene();
    }

    private bool TryStartScene()
    {
        if ( ! TryGetDataManager())
            return false;

        dataManager.LoadPassiveData();

        if ( ! dataManager.TryLoadGameplayData())
        {
            Log.Error("플레이어 데이터 초기화/복원에 실패하여 스테이지 시작을 중단합니다.");
            return false;
        }

        return true;
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