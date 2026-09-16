using UnityEngine;
using System.Collections;
using Core;

public class YJ_StageManager : MonoBehaviour
{
    private DataManager dataManager;
    [SerializeField] private bool bootScene = false;
    public bool isBossStage = false;

    [SerializeField] private WBH_EnemySpawnManager enemySpawnManager;
    [SerializeField] private YJ_StageSaveService stageSaveService;
    [SerializeField] private StageNodeType directSceneNodeType = StageNodeType.Battle;
    [SerializeField] private bool useDirectSceneNodeType;

    [Header("일반 / 엘리트 웨이브")]
    [SerializeField, Min(1)] private int waveCount = 5;
    [SerializeField, Min(1)] private int totalEnemyCount = 50;
    [SerializeField] private WBH_WavePercentRange[] waveRanges;
    [SerializeField] private int directSceneWaveSeed = 1234;

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

        if ( ! TryGetCurrentNodeType(out StageNodeType currentNodeType))
            return;

        if ( ! UsesEnemyWaves(currentNodeType))
            return;

        if (enemySpawnManager == null)
            return;

        if(!TryConfigureWaveSet(currentNodeType)) // 위 조건문을 통과하였다면 웨이브 정보 선택
        {
            Log.Error("적 웨이브 구성에 실패하여 스테이지 시작을 중단합니다.");
            return;
        }
        PlayStageBgm(currentNodeType);
        AdvanceStage();
    }

    private void PlayStageBgm(StageNodeType nodeType)
    {
        YJ_BgmPlayer bgmPlayer = YJ_BgmPlayer.Instance;

        if(bgmPlayer == null)
        {
            Log.Warning("YJ_BgmPlayer 가 없어 BGM 재생을 생략합니다.");
            return;
        }

        int act = RefreshLocation();
        bool bossStage = nodeType == StageNodeType.Boss;

        YJ_BgmPlayer.YJ_BgmType bgmType;

        switch (act)
        {
            case 1:
                bgmType = bossStage
                    ? YJ_BgmPlayer.YJ_BgmType.Act1BossBgm
                    : YJ_BgmPlayer.YJ_BgmType.Act1Bgm;
                break;

            case 2:
                bgmType = bossStage
                    ? YJ_BgmPlayer.YJ_BgmType.Act2BossBgm
                    : YJ_BgmPlayer.YJ_BgmType.Act2Bgm;
                break;

            case 3:
                bgmType = bossStage
                    ? YJ_BgmPlayer.YJ_BgmType.Act3BossBgm
                    : YJ_BgmPlayer.YJ_BgmType.Act3Bgm;
                break;

            default:
                // 저장 데이터에서 Act를 확인하지 못한 경우.
                return;
        }

        bgmPlayer.Play(bgmType);
    }

    private void HandleWaveCompleted()
    {
        AdvanceStage();
    }

    private void AdvanceStage()
    {
        if (stageClear || enemySpawnManager == null)
            return;

        if (enemySpawnManager.AllwavesCompleted)
        {
            CompleteStage();
            return;
        }
        if(enemySpawnManager.HasNextWave && !enemySpawnManager.TrySpawnNextWave())
        {
            Log.Error("웨이브를 시작하거나 적을 생성하지 못했습니다.");
        }    
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

        YJ_BgmPlayer.Instance.Stop();
        Log.Print("Stage Clear");
    }

    private bool TryConfigureGeneratedWaves(StageNodeType nodeType, int seed)
    {
        if(!WBH_WaveCountBuilder.TryDistribute(totalEnemyCount, waveCount, waveRanges, seed, out int[] counts, out string error))
        {
            Log.Error(error);
            return false;
        }

        var waves = new WBH_WaveData[counts.Length];

        for(int i = 0; i < counts.Length; i ++)
        {
            bool eliteFinalWave = nodeType == StageNodeType.Elite && i == counts.Length - 1;
            EnemyGrade specialGrade = eliteFinalWave ? EnemyGrade.Elite : EnemyGrade.Advanced;

            waves[i] = new WBH_WaveData
            {
                enemies = new[]
                {
                    new WBH_WaveGradeCount
                    {
                        grade = EnemyGrade.Normal,
                        count = counts[i] - 1
                    },
                    new WBH_WaveGradeCount
                    {
                        grade = specialGrade,
                        count =1
                    }
                }
            };
        }
        return enemySpawnManager.TrySetWaves(waves);
    }

    // 씬에 저장된 default 웨이브 정보를 불러오거나 웨이브 정보 로드를 실패할 경우들의 오류 처리
    private bool TryConfigureWaveSet(StageNodeType nodeType)
    {
        // 보스는 기존 고정 WaveSet 사용.
        if (nodeType == StageNodeType.Boss)
            return TryUseDefaultWaveSet("보스 스테이지");

        if (nodeType != StageNodeType.Battle && nodeType != StageNodeType.Elite)
            return false;

        // 직접 테스트도 Inspector 설정으로 웨이브 생성.
        if (useDirectSceneNodeType)
            return TryConfigureGeneratedWaves(nodeType, directSceneWaveSeed);

        if (!TryGetStageSaveService())
        {
            Log.Error("StageSaveService를 준비하지 못했습니다.");
            return false;
        }

        // 저장 데이터 없는 전투 씬 직접 실행.
        if (!stageSaveService.HasSaveFile)
            return TryConfigureGeneratedWaves(nodeType, directSceneWaveSeed);

        if (!stageSaveService.TryLoadSaveData(out StageMapSaveData saveData) || saveData == null || string.IsNullOrWhiteSpace(saveData.pendingNodeId))
        {
            Log.Error("현재 노드의 저장 데이터를 불러오지 못했습니다.");
            return false;
        }

        StageNodeSaveData pendingNode = saveData.nodes?.Find(node => node != null && node.id == saveData.pendingNodeId);

        if (pendingNode == null || pendingNode.type != nodeType)
        {
            Log.Error("현재 노드와 저장된 노드 정보가 일치하지 않습니다.");
            return false;
        }

        int waveSeed = CreateWaveSeed(saveData.mapSeed, pendingNode);

        if (!TryConfigureGeneratedWaves(nodeType, waveSeed))
            return false;

        Log.Print($"노드 웨이브 결정: {pendingNode.id} / {pendingNode.type} / " + $"{waveCount}웨이브 / 전체 {totalEnemyCount}마리");

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

        if( ! TryGetStageSaveService() || !stageSaveService.HasSaveFile)
        {
            currentNodeType = isBossStage ? StageNodeType.Boss : directSceneNodeType;
            return true;
        }

        if( ! stageSaveService.TryGetPendingNode(out StageNodeSaveData pendingNode))
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

    private int RefreshLocation()
    {
        if (stageSaveService == null)
            stageSaveService = FindFirstObjectByType<YJ_StageSaveService>();

        if (stageSaveService == null)
            stageSaveService = gameObject.AddComponent<YJ_StageSaveService>();

        if (!stageSaveService.HasSaveFile ||
            !stageSaveService.TryLoadSaveData(out StageMapSaveData saveData) ||
            string.IsNullOrWhiteSpace(saveData.pendingNodeId))
        {
            return 0;
        }

        StageNodeSaveData currentNode = saveData.nodes.Find(
            node => node != null && node.id == saveData.pendingNodeId);
        if (currentNode == null)
            return 0;

        return (int)saveData.act;
    }
}