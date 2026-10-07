using UnityEngine;
using System.Collections;
using Core;

public class YJ_StageManager : MonoBehaviour
{
    private DataManager dataManager;
    [SerializeField] private bool bootScene = false;
    public bool isBossStage = false;

    [SerializeField] private WBH_EnemySpawnManager enemySpawnManager;
    [SerializeField] private YJ_PlayerSpawner playerSpawner;
    [SerializeField] private YJ_StageSaveService stageSaveService;
    [SerializeField] private StageNodeType directSceneNodeType = StageNodeType.Battle;
    [SerializeField] private bool useDirectSceneNodeType;

    [Header("일반 / 엘리트 웨이브")]
    [SerializeField, Min(1)] private int waveCount = 5;
    [SerializeField, Min(1)] private int totalEnemyCount = 50;
    [SerializeField] private WBH_WavePercentRange[] waveRanges;
    [SerializeField] private int directSceneWaveSeed = 1234;

    private bool stageClear;
    private string unknownBattleKey;
    private bool unknownBattleCompleted;

    public bool StageClear => stageClear;
    public bool IsGameplayReady { get; private set; }
    public string GameplayPreparationError { get; private set; }

    private void Awake()
    {
        enemySpawnManager ??= FindFirstObjectByType<WBH_EnemySpawnManager>();
        if (playerSpawner == null)
            playerSpawner = FindFirstObjectByType<YJ_PlayerSpawner>();
        stageSaveService ??= GetComponent<YJ_StageSaveService>();
    }

    private void OnEnable()
    {
        if (enemySpawnManager != null)
            enemySpawnManager.WaveCompleted += HandleWaveCompleted;
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(CompleteStage));
        if (enemySpawnManager != null)
            enemySpawnManager.WaveCompleted -= HandleWaveCompleted;
    }

    /// <summary>SW 수정: 싱글 초기화만 실행하며 멀티의 웨이브·저장은 서버 세션이 담당합니다.</summary>
    private IEnumerator Start()
    {
        if (MirrorNetworkManager.OwnsGameplay) yield break;
        if (bootScene)
        {
            Initialize();
            IsGameplayReady = true;
            yield break;
        }

        // 씬에 배치된 활성 컴포넌트들의 Start 실행을 기다립니다.
        yield return null;

        if (playerSpawner != null && playerSpawner.SpawnedPlayer == null)
        {
            Log.Error("선택 캐릭터가 생성되지 않아 스테이지 시작을 중단합니다. PlayerSpawner 설정을 확인하세요.");
            yield break;
        }

        if (!TryStartScene())
            yield break;

        // SW 수정: 저장 데이터 복원 후 현재 장착 외형과 선택 스킬만 준비합니다.
        if (playerSpawner != null)
        {
            PlayerContext context = playerSpawner.SpawnedPlayer.GetComponent<PlayerContext>();
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            while (true)
            {
                bool ready = false;
                string error = "플레이어 상태 연결이 없습니다.";
                if (context == null || !context.TryPreparePresentation(out ready, out error))
                {
                    GameplayPreparationError = context == null ? "플레이어 상태 연결이 없습니다." : error;
                    Log.Error(GameplayPreparationError);
                    yield break;
                }
                if (ready) break;
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    GameplayPreparationError = "플레이 준비 시간이 초과되었습니다.";
                    Log.Error(GameplayPreparationError);
                    yield break;
                }
                yield return null;
            }
        }

        // 기존 고정 플레이어 씬에서는 SpawnManager가 최초 웨이브 직전에 참조를 찾습니다.
        if (playerSpawner != null && enemySpawnManager != null &&
            !enemySpawnManager.TrySetPlayer(playerSpawner.SpawnedPlayer))
            yield break;

        IsGameplayReady = true;
        playerSpawner?.SetGameplayInput(true);
        StartStage();
    }

    public void Initialize()
    {
        if (MirrorNetworkManager.OwnsGameplay) return;
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
        if (MirrorNetworkManager.OwnsGameplay) return false;
        if (!TryGetDataManager())
            return false;

        dataManager.LoadPassiveData();

        unknownBattleKey = null;
        unknownBattleCompleted = false;
        if (!Mirror.NetworkClient.active && !Mirror.NetworkServer.active && !useDirectSceneNodeType)
        {
            if (!TryGetCurrentNodeType(out StageNodeType nodeType)) return false;
            if (UsesEnemyWaves(nodeType) && stageSaveService != null && stageSaveService.HasSaveFile)
            {
                if (!stageSaveService.TryLoadSaveData(out StageMapSaveData map) || map == null ||
                    string.IsNullOrWhiteSpace(map.pendingNodeId)) return false;
                unknownBattleKey = $"{(int)map.act}:{map.mapSeed}:{map.pendingNodeId}";
                if (!dataManager.TryPrepareUnknownBattle(unknownBattleKey, out unknownBattleCompleted)) return false;
            }
        }

        if (!dataManager.TryLoadGameplayData(unknownBattleKey))
        {
            Log.Error("플레이어 데이터 초기화/복원에 실패하여 스테이지 시작을 중단합니다.");
            return false;
        }

        return true;
    }

    /// <summary>SW 수정: 멀티 참가자의 진행을 오프라인 저장 파일에 기록하지 않습니다.</summary>
    public void EndScene()
    {
        if (MirrorNetworkManager.OwnsGameplay) return;
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

        // 클리어 저장 후 포탈 이동 전에 재로드했다면 전투/효과를 다시 소모하지 않는다.
        if (unknownBattleCompleted)
        {
            CompleteStage();
            return;
        }

        if (enemySpawnManager == null)
            return;

        if(!TryConfigureWaveSet(currentNodeType)) // 위 조건문을 통과하였다면 웨이브 정보 선택
        {
            Log.Error("적 웨이브 구성에 실패하여 스테이지 시작을 중단합니다.");
            return;
        }
        ApplyEnemyStatContext(); // 첫 웨이브 생성(AdvanceStage) 전에 층 배율 컨텍스트를 맞춘다.
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

        if (!string.IsNullOrEmpty(unknownBattleKey) && !unknownBattleCompleted)
        {
            if (!dataManager.TryCompleteUnknownBattle(unknownBattleKey))
            {
                // 저장에 실패한 상태에서 포탈을 열지 않는다. 임시 I/O 실패는 재시도할 수 있다.
                CancelInvoke(nameof(CompleteStage));
                Invoke(nameof(CompleteStage), 2f);
                return;
            }
            unknownBattleCompleted = true;
        }

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

    /// <summary>SW 수정: 로컬 저장이나 적 생성을 실행하지 않고 서버 노드로 원본 웨이브만 확정합니다.</summary>
    public bool TryPrepareSessionWaves(StageMapSaveData snapshot)
    {
        if (!MirrorNetworkManager.OwnsGameplay || !Mirror.NetworkServer.active ||
            enemySpawnManager == null || snapshot?.nodes == null) return false;
        StageNodeSaveData node = snapshot.nodes.Find(value => value != null && value.id == snapshot.pendingNodeId);
        if (node == null) return false;
        return node.type == StageNodeType.Boss
            ? TryUseDefaultWaveSet("서버 보스 스테이지")
            : UsesEnemyWaves(node.type) && TryConfigureGeneratedWaves(node.type, CreateWaveSeed(snapshot.mapSeed, node));
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

    /// <summary>
    /// WJ 이우진 추가(2026-10-01): 저장된 현재 노드의 액트·층으로 적 능력치 컨텍스트(층 배율)를 맞춘다.
    /// 이전에는 아무도 SetStatContext를 부르지 않아 싱글의 모든 적이 1층 배율(1배)로 생성됐다.
    /// 층 번호는 액트마다 1부터라 FloorStatScaleTable.ToContextFloor로 표의 누적 번호로 바꿔서 넘긴다.
    /// 보스 스테이지도 같은 경로를 탄다. 저장이 없는 직접 실행/노드 직접 지정 테스트는 기본값(Act1 1층)을 그대로 쓴다.
    /// 싱글은 난이도 normal, 인원 1 고정(멀티는 NetworkEnemyWaveSpawner가 따로 넘긴다).
    ///
    /// WJ 이우진 수정(2026-10-06): Act1·Act2 보스 씬은 노드 직접 지정(useDirectSceneNodeType)이 켜져 있어
    /// 맵을 거쳐 정상 진입해도 층 배율을 건너뛰었다(Act3 보스·멀티와 불일치). 직접 지정이어도 저장의 현재 노드가
    /// 바로 이 씬의 노드면 그 액트·층을 쓴다. 다른 노드의 저장을 가진 채 씬을 직접 실행한 테스트는 기존처럼 기본값.
    /// </summary>
    private void ApplyEnemyStatContext()
    {
        if (enemySpawnManager == null || !TryGetStageSaveService() || !stageSaveService.HasSaveFile)
            return;

        if (!stageSaveService.TryLoadSaveData(out StageMapSaveData saveData) || saveData == null)
            return;

        StageNodeSaveData pendingNode = saveData.nodes?.Find(node => node != null && node.id == saveData.pendingNodeId);
        if (pendingNode == null)
            return;

        if (useDirectSceneNodeType && pendingNode.sceneName != gameObject.scene.name)
            return;

        int contextFloor = EnemySystem.FloorStatScaleTable.ToContextFloor((int)saveData.act, pendingNode.floor);
        enemySpawnManager.SetStatContext(new WBH_EnemyStatContext(contextFloor, "normal", 1));
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
