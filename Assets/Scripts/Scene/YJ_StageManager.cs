using UnityEngine;
using System.Collections;
using Core;

public class YJ_StageManager : MonoBehaviour
{
    private DataManager dataManager;
    [SerializeField] private bool bootScene = false;
    public bool isBossStage = false;

    [SerializeField] private WBH_WaveSetCatalogSO waveSetCatalog;
    [SerializeField] private WBH_EnemySpawnManager enemySpawnManager;
    [SerializeField] private YJ_StageSaveService stageSaveService;
    [SerializeField] private StageNodeType directSceneNodeType = StageNodeType.Battle;
    [SerializeField] private bool useDirectSceneNodeType;

    private bool stageClear;

    public bool StageClear => stageClear;

    private void Awake()
    {
        enemySpawnManager ??= FindFirstObjectByType<WBH_EnemySpawnManager>();
        stageSaveService ??= GetComponent<YJ_StageSaveService>();
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

        if (!TryStartScene())
            yield break;

        StartStage();
    }

    public void Initialize()
    {
        if (!TryGetDataManager())
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
        if (!TryGetDataManager())
            return false;

        dataManager.LoadPassiveData();

        if (!dataManager.TryLoadGameplayData())
        {
            Log.Error("플레이어 데이터 초기화/복원에 실패하여 스테이지 시작을 중단합니다.");
            return false;
        }

        return true;
    }

    public void EndScene()
    {
        if (!TryGetDataManager())
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

        if (!TryGetCurrentNodeType(out StageNodeType currentNodeType))
            return;

        if (!UsesEnemyWaves(currentNodeType))
            return;

        if (enemySpawnManager == null)
            return;

        if(!TryConfigureWaveSet()) // 위 조건문을 통과하였다면 웨이브 정보 선택
        {
            Log.Error("적 웨이브 구성에 실패하여 스테이지 시작을 중단합니다.");
            return;
        }

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

        if (enemySpawnManager.HasNextWave)
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

        if (portalActive != null)
        {
            portalActive.Active(true);
        }
        else
        {
            Log.Error("스테이지 클리어 포탈이 연결되지 않았습니다.");
        }

        Log.Print("Stage Clear");
    }

    // 씬에 저장된 default 웨이브 정보를 불러오거나 웨이브 정보 로드를 실패할 경우들의 오류 처리
    private bool TryConfigureWaveSet()
    {
        // 직접 씬 테스트에서는 저장데이터와 카탈로그 무시.
        if (useDirectSceneNodeType)
            return TryUseDefaultWaveSet("직접 씬 테스트");

        if (isBossStage)
            return TryUseDefaultWaveSet("보스 스테이지");
        if (!TryGetStageSaveService())
            return TryUseDefaultWaveSet("YJ_StageSaveService 컴포넌트가 연결된 오브젝트를 찾을 수 없습니다.");
        if (!stageSaveService.HasSaveFile)
            return TryUseDefaultWaveSet("선택 노드 저장데이터가 없습니다.(전투 씬이 직접 실행되었습니다.)");
        if(!stageSaveService.TryLoadSaveData(out StageMapSaveData saveData))
        {
            Log.Error("스테이지 맵 저장 데이터를 불러오지 못했습니다.");
            return false;
        }
        if(string.IsNullOrWhiteSpace(saveData.pendingNodeId))
        {
            Log.Error("현재 진행중인 pending 노드가 없습니다.");
            return false;
        }

        StageNodeSaveData pendingNode = saveData.nodes?.Find(node => node != null && node.id == saveData.pendingNodeId);

        if(pendingNode == null)
        {
            Log.Error($"pending 노드를 찾지 못했습니다 : {saveData.pendingNodeId}");
            return false;
        }

        if(pendingNode.type != StageNodeType.Battle && pendingNode.type != StageNodeType.Elite)
        {
            return TryUseDefaultWaveSet($"{pendingNode.type} 노드는 Catalog 선택 대상이 아닙니다.");
        }

        if(waveSetCatalog == null)
        {
            Log.Error("YJ_StageManager 에 WaveSetCatalog 가 연결되지 않았습니다.");
            return false;
        }

        bool eliteStage = pendingNode.type == StageNodeType.Elite;

        int waveSeed = CreateWaveSeed(saveData.mapSeed, pendingNode);

        if(!waveSetCatalog.TrySelect(eliteStage, waveSeed, out WBH_WaveSetSO selectedWaveSet))
        {
            Log.Error($"{pendingNode.type} 노드에 사용할 WaveSet 이 없습니다.");
            return false;
        }

        if(!enemySpawnManager.TrySetWaveSet(selectedWaveSet))
            return false;

        Log.Print($"노드 웨이브 결정: {pendingNode.id} / {pendingNode.type} / {selectedWaveSet.WaveSetId}");

        return true;
    }

    // YJ_StageSaveService 가 전투 씬에서 누락되어 있다면 YJ_StageManager 가 연결된 오브젝트에 YJ_StageSaveService 컴포넌트 추가
    private bool TryGetStageSaveService()
    {
        if (stageSaveService != null)
            return true;

        stageSaveService = GetComponent<YJ_StageSaveService>();

        if(stageSaveService == null)
        {
            stageSaveService = gameObject.AddComponent<YJ_StageSaveService>();
        }
        return stageSaveService != null;
    }

    private bool TryGetCurrentNodeType(out StageNodeType currentNodeType)
    {
        if(useDirectSceneNodeType)
        {
            currentNodeType = isBossStage ? StageNodeType.Boss : directSceneNodeType;
            return true;
        }

        currentNodeType = default;

        if(!TryGetStageSaveService() || !stageSaveService.HasSaveFile)
        {
            currentNodeType = isBossStage ? StageNodeType.Boss : directSceneNodeType;
            return true;
        }

        if(!stageSaveService.TryGetPendingNode(out StageNodeSaveData pendingNode))
        {
            Log.Error("현재 진행 중인 노드 정보를 불러오지 못했습니다.");
            return false;
        }

        currentNodeType = pendingNode.type;
        return true;
    }

    // 전투씬 직접 실행, 보스 스테이지의 경우 씬 자체에 저장된 웨이브 정보를 활용
    private bool TryUseDefaultWaveSet(string reason)
    {
        if(!enemySpawnManager.TryUseDefaultWaveSet())
            return false;
        
        Log.Print($"{reason} : SpawnManager 의 기본 WaveSet 을 사용합니다.");
        
        return true;
    }

    private static bool UsesEnemyWaves(StageNodeType nodeType)
    {
        return nodeType == StageNodeType.Battle || nodeType == StageNodeType.Elite || nodeType == StageNodeType.Boss;
    }

    // 맵 시드, 층, 노드 인덱스, 노드 타입(노말, 엘리트)이 같다면 항상 동일한 WaveSet 사용
    private static int CreateWaveSeed(int mapSeed, StageNodeSaveData node)
    {
        unchecked
        {
            int seed = mapSeed;
            seed = seed * 397 ^ node.floor;
            seed = seed * 397 ^ node.nodeIndex;
            seed = seed * 397 ^ (int)node.type;

            return seed;
        }
    }
}