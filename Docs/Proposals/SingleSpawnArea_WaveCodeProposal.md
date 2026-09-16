# 단일 SpawnArea / 자식 SpawnPoint 구조의 웨이브 코드 제안

실제 C# 파일과 씬에 적용하지 않은 교체 코드입니다. Unity 컴파일·플레이 검증 전입니다.

## 확인한 현재 구조

열린 씬: `Assets/WBHTest/Act2_BossStage 1.unity`

```text
EnemySpawnArea
├─ WBH_EnemySpawnArea
├─ WBH_EnemySpawner
├─ SpawnPoint (0)
├─ SpawnPoint (1)
├─ SpawnPoint (2)
├─ SpawnPoint (3)
└─ SpawnPoint (4)
```

두 컴포넌트는 씬에 각각 하나이고 같은 오브젝트에 있습니다. 기존 spawnPoints 배열은 위 자식 5개를 순서대로 참조합니다. SpawnPoint에는 Transform과 WBH_DrawGizmo만 있습니다. 이 배치와 기존 spawnPoints/spawnDatas 필드를 유지합니다.

- 1웨이브: spawnPoints[0], 2웨이브: spawnPoints[1].
- 한 웨이브의 모든 등급은 같은 포인트를 사용합니다.
- 기준은 Inspector 배열 순서입니다. 자식 이름이나 Hierarchy 순서를 자동 파싱하지 않습니다.
- 포인트가 부족하거나 중복·누락되면 시작을 거부합니다. 처음 포인트로 순환하지 않습니다.
- EnemySpawnArea 루트 위치를 옮기거나 자식에 Spawner를 추가하지 않습니다.

## 1. WBH_EnemySpawnArea.cs 전체 교체안

기존 Initialize API와 직렬화 필드 spawnPoints/spawnDatas를 보존합니다. Spawn에는 마지막 인수 spawnPointIndex가 추가됩니다. 아래 SpawnManager가 함께 이 인수를 전달합니다.

```csharp
using EnemySystem;
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WBH_EnemySpawner))]
public class WBH_EnemySpawnArea : MonoBehaviour
{
    [Serializable]
    public class EnemyGradeSpawnData
    {
        public EnemyGrade grade;
        public EnemyDefinitionSO[] enemies;
    }

    [Header("Spawn Setting")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private EnemyGradeSpawnData[] spawnDatas;

    private WBH_EnemySpawner enemySpawner;
    private Func<Vector3, Transform> findClosestPlayer;

    private void Awake()
    {
        enemySpawner = GetComponent<WBH_EnemySpawner>();
    }

    public void Initialize(WBH_EnemySpawnManager spawnManager,
                           WBH_EnemyPoolManager enemyPool,
                           WBH_EnemyDataProvider enemyDataProvider,
                           WBH_EffectSpawner effectSpawner,
                           WBH_ProjectileSpawner projectileSpawner,
                           Transform localPlayer,
                           Func<Vector3, Transform> findClosestPlayer,
                           WBH_FloatTextPoolManager damagePool,
                           WBH_HighEnemyHpbarView eliteView,
                           PlayerWallet playerWallet)
    {
        this.findClosestPlayer = findClosestPlayer;
        enemySpawner = GetComponent<WBH_EnemySpawner>();
        enemySpawner.Initialize(spawnManager, enemyPool, enemyDataProvider,
            effectSpawner, projectileSpawner, localPlayer, damagePool,
            eliteView, playerWallet);
    }

    public bool TryGetSpawnPoint(int index, out Transform point)
    {
        point = null;
        if (spawnPoints == null || index < 0 || index >= spawnPoints.Length)
            return false;

        point = spawnPoints[index];
        return point != null && point != transform &&
               point.IsChildOf(transform) && point.gameObject.activeInHierarchy;
    }

    public bool ValidateSpawnPoints(int waveCount, out string error)
    {
        error = null;
        if (waveCount <= 0 || spawnPoints == null || spawnPoints.Length < waveCount)
        {
            error = $"{name}: 스폰포인트가 최소 {waveCount}개 필요합니다.";
            return false;
        }

        var usedPoints = new HashSet<Transform>();
        for (int i = 0; i < waveCount; i++)
        {
            if (!TryGetSpawnPoint(i, out Transform point) || !usedPoints.Add(point))
            {
                error = $"{name}: SpawnPoints[{i}]의 누락·비활성·중복 또는 자식 관계를 확인하세요.";
                return false;
            }
        }
        return true;
    }

    public bool CanSpawn(EnemyGrade grade)
    {
        if (spawnDatas == null)
            return false;

        EnemyGradeSpawnData selected = null;
        foreach (EnemyGradeSpawnData data in spawnDatas)
        {
            if (data == null || data.grade != grade)
                continue;
            if (selected != null)
                return false;
            selected = data;
        }

        if (selected?.enemies == null || selected.enemies.Length == 0)
            return false;

        foreach (EnemyDefinitionSO definition in selected.enemies)
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.enemyId) ||
                definition.enemyGrade != grade)
                return false;
        }
        return true;
    }

    public int Spawn(EnemyGrade grade, int count,
                     WBH_EnemyStatContext context, int spawnPointIndex)
    {
        if (count <= 0 || enemySpawner == null ||
            !TryGetSpawnPoint(spawnPointIndex, out Transform point) ||
            !CanSpawn(grade))
            return 0;

        EnemyGradeSpawnData data = Array.Find(
            spawnDatas, entry => entry != null && entry.grade == grade);

        int spawnedCount = 0;
        for (int i = 0; i < count; i++)
        {
            EnemyDefinitionSO definition =
                data.enemies[UnityEngine.Random.Range(0, data.enemies.Length)];
            Transform target = findClosestPlayer?.Invoke(point.position);

            WBH_EnemyController enemy = enemySpawner.Spawn(
                definition.enemyId, point, target, context);
            if (enemy == null)
                break;

            spawnedCount++;
        }
        return spawnedCount;
    }
}
```

## 2. WBH_EnemySpawnManager.cs 전체 교체안

씬의 기존 EnemySpawnArea 하나를 spawnArea 필드에 연결합니다. 기존 spawnAreas 배열에서 단일 필드로 바뀌므로 Inspector에서 한 번 연결해야 합니다. 기존 EnemySpawnArea와 WBH_EnemySpawner 오브젝트/컴포넌트 참조는 이동하거나 재생성하지 않습니다.

StageManager가 호출할 TrySetWaves, 보스 SO용 API, 생성 실패 시 미생성 수량 보존, 재시도, 완료 검사를 포함합니다.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WBH_EnemyPoolManager))]
[RequireComponent(typeof(WBH_EnemyDataProvider))]
public class WBH_EnemySpawnManager : MonoBehaviour
{
    [SerializeField] private WBH_WaveSetSO defaultWaveSet;
    [SerializeField] private WBH_EnemySpawnArea spawnArea;
    [SerializeField] private Transform player;
    [SerializeField] private WBH_EnemyDataProvider enemyDataProvider;
    [SerializeField] private WBH_EnemyStatContext statContext =
        new WBH_EnemyStatContext(1, "normal", 1);
    [SerializeField] private WBH_HighEnemyHpbarView highEnemyView;
    [SerializeField] private WBH_EffectSpawner effectSpawner;
    [SerializeField] private WBH_ProjectileSpawner projectileSpawner;

    private WBH_EnemyPoolManager enemyPool;
    private WBH_FloatTextPoolManager damagePool;
    private PlayerWallet wallet;
    private WBH_WaveSetSO activeWaveSet;
    private WBH_WaveData[] activeWaves;
    private readonly Queue<EnemyGrade> pendingSpawns = new();

    private int currentWave = -1;
    private int aliveEnemyCount;
    private bool waveInProgress;
    private bool isSpawningWave;
    private bool spawnAreaInitialized;

    public event Action WaveCompleted;
    public WBH_WaveSetSO ActiveWaveSet => activeWaveSet;
    public int CurrentWaveIndex => currentWave;
    public bool HasUsableWaveSet => activeWaves != null && activeWaves.Length > 0;
    public int WaveCount => activeWaves?.Length ?? 0;
    public bool HasNextWave => currentWave + 1 < WaveCount;
    public bool AllWavesCompleted => HasUsableWaveSet &&
        currentWave == WaveCount - 1 && !waveInProgress &&
        !isSpawningWave && pendingSpawns.Count == 0 && aliveEnemyCount == 0;

    private void Awake()
    {
        enemyPool = GetComponent<WBH_EnemyPoolManager>();
        enemyDataProvider = GetComponent<WBH_EnemyDataProvider>();
        damagePool = FindFirstObjectByType<WBH_FloatTextPoolManager>();
        wallet = FindFirstObjectByType<PlayerWallet>();
        if (highEnemyView == null)
            highEnemyView = FindFirstObjectByType<WBH_HighEnemyHpbarView>();
        if (player == null)
        {
            T_PlayerController foundPlayer = FindAnyObjectByType<T_PlayerController>();
            player = foundPlayer != null ? foundPlayer.transform : null;
        }
        // spawnArea는 Inspector에 연결한 참조를 사용합니다.
    }

    private void OnEnable()
    {
        WBH_EnemyController.OnEnemyDead += EnemyDead;
    }

    private void OnDisable()
    {
        WBH_EnemyController.OnEnemyDead -= EnemyDead;
    }

    private bool TryInitializeSpawnArea()
    {
        if (spawnAreaInitialized)
            return true;
        if (spawnArea == null || !spawnArea.isActiveAndEnabled ||
            enemyPool == null || enemyDataProvider == null ||
            player == null || highEnemyView == null)
        {
            Log.Error("SpawnArea, 플레이어, HP UI, 풀, 데이터 참조를 확인하세요.");
            return false;
        }

        spawnArea.Initialize(this, enemyPool, enemyDataProvider,
            effectSpawner, projectileSpawner, player, FindClosePlayer,
            damagePool, highEnemyView, wallet);
        spawnAreaInitialized = true;
        return true;
    }

    public bool TrySetWaves(IReadOnlyList<WBH_WaveData> waves)
    {
        if (waveInProgress || isSpawningWave)
        {
            Log.Error("웨이브 진행 중에는 데이터를 교체할 수 없습니다.");
            return false;
        }
        if (waves == null || waves.Count == 0 ||
            spawnArea == null || !spawnArea.isActiveAndEnabled)
        {
            Log.Error("웨이브 데이터와 SpawnArea 참조를 확인하세요.");
            return false;
        }
        if (!spawnArea.ValidateSpawnPoints(waves.Count, out string error))
        {
            Log.Error(error);
            return false;
        }

        var copiedWaves = new WBH_WaveData[waves.Count];
        for (int i = 0; i < waves.Count; i++)
        {
            WBH_WaveGradeCount[] entries = waves[i]?.enemies;
            if (entries == null || entries.Length == 0)
            {
                Log.Error($"{i + 1}웨이브의 적 구성이 없습니다.");
                return false;
            }

            var copiedEntries = new WBH_WaveGradeCount[entries.Length];
            long total = 0;
            for (int j = 0; j < entries.Length; j++)
            {
                WBH_WaveGradeCount entry = entries[j];
                if (entry == null || entry.count < 0 ||
                    (entry.count > 0 && !spawnArea.CanSpawn(entry.grade)))
                {
                    Log.Error($"{i + 1}웨이브의 등급별 수량과 SpawnData를 확인하세요.");
                    return false;
                }
                total += entry.count;
                copiedEntries[j] = new WBH_WaveGradeCount
                {
                    grade = entry.grade,
                    count = entry.count
                };
            }
            if (total <= 0 || total > int.MaxValue)
            {
                Log.Error($"{i + 1}웨이브의 전체 수량이 잘못됐습니다.");
                return false;
            }
            copiedWaves[i] = new WBH_WaveData { enemies = copiedEntries };
        }

        activeWaves = copiedWaves;
        activeWaveSet = null;
        currentWave = -1;
        aliveEnemyCount = 0;
        waveInProgress = false;
        isSpawningWave = false;
        pendingSpawns.Clear();
        return true;
    }

    public bool TrySetWaveSet(WBH_WaveSetSO waveSet)
    {
        if (waveSet == null)
        {
            Log.Error("적용할 WaveSet이 없습니다.");
            return false;
        }
        if (!TrySetWaves(waveSet.Waves))
            return false;
        activeWaveSet = waveSet;
        return true;
    }

    public bool TryUseDefaultWaveSet()
    {
        return TrySetWaveSet(defaultWaveSet);
    }

    public void SetStatContext(WBH_EnemyStatContext context)
    {
        if (!context.IsValid)
        {
            Log.Error("잘못된 적 능력치 컨텍스트입니다.");
            return;
        }
        statContext = context;
    }

    public bool TrySpawnNextWave()
    {
        if (!isActiveAndEnabled || !HasUsableWaveSet ||
            waveInProgress || !HasNextWave || !TryInitializeSpawnArea())
            return false;

        currentWave++;
        aliveEnemyCount = 0;
        waveInProgress = true;
        pendingSpawns.Clear();
        foreach (WBH_WaveGradeCount entry in activeWaves[currentWave].enemies)
        {
            for (int i = 0; i < entry.count; i++)
                pendingSpawns.Enqueue(entry.grade);
        }
        return TrySpawnPendingEnemies();
    }

    public bool TrySpawnPendingEnemies()
    {
        if (!isActiveAndEnabled || !waveInProgress || isSpawningWave)
            return false;
        if (!TryInitializeSpawnArea() || spawnArea == null ||
            !spawnArea.isActiveAndEnabled ||
            !spawnArea.TryGetSpawnPoint(currentWave, out _))
        {
            Log.Error($"{currentWave + 1}웨이브의 스폰포인트를 사용할 수 없습니다.");
            return false;
        }

        bool succeeded = true;
        isSpawningWave = true;
        try
        {
            while (pendingSpawns.Count > 0)
            {
                EnemyGrade grade = pendingSpawns.Peek();
                // 같은 SpawnArea 안에서 웨이브 인덱스로 포인트를 선택.
                int spawned = spawnArea.Spawn(grade, 1, statContext, currentWave);
                if (spawned != 1)
                {
                    Log.Error($"{currentWave + 1}웨이브 생성 실패: {grade}, " +
                        $"미생성 {pendingSpawns.Count}마리. 데이터·풀·NavMesh를 확인하세요.");
                    succeeded = false;
                    break;
                }
                pendingSpawns.Dequeue();
                aliveEnemyCount++;
            }
        }
        finally
        {
            isSpawningWave = false;
        }
        TryCompleteCurrentWave();
        return succeeded;
    }

    [ContextMenu("남은 웨이브 적 생성 재시도")]
    private void RetryPendingSpawns()
    {
        if (Application.isPlaying)
            TrySpawnPendingEnemies();
    }

    private void EnemyDead()
    {
        if (!waveInProgress || aliveEnemyCount <= 0)
            return;
        aliveEnemyCount--;
        TryCompleteCurrentWave();
    }

    private void TryCompleteCurrentWave()
    {
        if (!waveInProgress || isSpawningWave ||
            pendingSpawns.Count > 0 || aliveEnemyCount > 0)
            return;
        waveInProgress = false;
        WaveCompleted?.Invoke();
    }

    public void RegisterAdditionalEnemies(int count)
    {
        if (!waveInProgress || count <= 0)
            return;
        aliveEnemyCount += count;
    }

    private Transform FindClosePlayer(Vector3 origin)
    {
        T_PlayerController[] players = FindObjectsByType<T_PlayerController>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Transform closest = null;
        float closestDistance = float.MaxValue;
        foreach (T_PlayerController candidate in players)
        {
            if (!candidate.isActiveAndEnabled)
                continue;
            float distance = (candidate.transform.position - origin).sqrMagnitude;
            if (distance >= closestDistance)
                continue;
            closestDistance = distance;
            closest = candidate.transform;
        }
        return closest;
    }
}
```

Awake의 spawnArea는 Inspector에 연결한 참조를 사용합니다. 자동으로 SpawnArea를 여러 개 수집하지 않습니다.

## 3. StageManager 연결

작성한 TryConfigureGeneratedWaves와 WBH_WaveCountBuilder는 그대로 사용합니다. 디스크 확인 시 TryConfigureGeneratedWaves는 추가되어 있지만 StartStage의 실행 경로는 아직 기존 TryConfigureWaveSet()입니다.

StartStage의 `if (!TryConfigureWaveSet())`를 `if (!TryConfigureWaveSet(currentNodeType))`로 바꿉니다. 기존 TryConfigureWaveSet 전체를 아래로 교체합니다. 더 이상 사용하지 않는 waveSetCatalog 필드는 제거 가능하며 SO 자산 삭제는 필요하지 않습니다.

```csharp
private bool TryConfigureWaveSet(StageNodeType nodeType)
{
    if (nodeType == StageNodeType.Boss)
        return TryUseDefaultWaveSet("보스 스테이지");
    if (nodeType != StageNodeType.Battle && nodeType != StageNodeType.Elite)
        return false;
    if (useDirectSceneNodeType)
        return TryConfigureGeneratedWaves(nodeType, directSceneWaveSeed);
    if (!TryGetStageSaveService())
    {
        Log.Error("StageSaveService를 준비하지 못했습니다.");
        return false;
    }
    if (!stageSaveService.HasSaveFile)
        return TryConfigureGeneratedWaves(nodeType, directSceneWaveSeed);
    if (!stageSaveService.TryLoadSaveData(out StageMapSaveData saveData) ||
        saveData == null || string.IsNullOrWhiteSpace(saveData.pendingNodeId))
    {
        Log.Error("현재 노드의 저장 데이터를 불러오지 못했습니다.");
        return false;
    }
    StageNodeSaveData node = saveData.nodes?.Find(
        item => item != null && item.id == saveData.pendingNodeId);
    if (node == null || node.type != nodeType)
    {
        Log.Error("현재 노드와 저장된 노드 정보가 일치하지 않습니다.");
        return false;
    }
    return TryConfigureGeneratedWaves(nodeType, CreateWaveSeed(saveData.mapSeed, node));
}

private void AdvanceStage()
{
    if (stageClear || enemySpawnManager == null)
        return;
    if (enemySpawnManager.AllWavesCompleted)
    {
        CompleteStage();
        return;
    }
    if (enemySpawnManager.HasNextWave && !enemySpawnManager.TrySpawnNextWave())
        Log.Error("웨이브를 시작하거나 적을 생성하지 못했습니다.");
}
```

## 4. Inspector와 확인 범위

1. SpawnManager의 새 Spawn Area 필드에 기존 EnemySpawnArea 오브젝트 하나를 연결합니다.
2. 기존 SpawnArea의 Spawn Points 배열과 자식 위치는 유지합니다. 5웨이브는 요소 0~4를 사용합니다.
3. StageManager의 Wave Count와 Wave Ranges 배열 길이를 일치시킵니다.
4. SpawnData에 Normal/Advanced를 등록합니다. 엘리트 노드를 테스트하려면 Elite 후보도 등록해야 합니다. 현재 열린 씬에는 Boss/Normal/Advanced만 등록돼 있습니다.
5. 기존 WBH_EnemySpawner, 보스 타임라인의 Spawner 참조, 적 풀은 유지합니다.
6. 생성 실패 시 큐의 미생성 항목이 남고 웨이브는 완료되지 않습니다. 데이터·풀 등록·NavMesh를 고친 뒤 SpawnManager 컴포넌트 컨텍스트 메뉴의 재시도를 사용합니다. 생성 성공한 항목은 다시 소환하지 않습니다.

## 제한과 검증 상태

- 기존 전역 WBH_EnemyController.OnEnemyDead 집계를 유지합니다. SpawnManager 한 개와 등록된 전투 적을 전제로 합니다. 미등록 적의 사망이나 매니저를 비활성화한 동안의 사망은 별도 개체 추적이 필요한 기존 한계입니다.
- 전체 적 b는 웨이브 계획의 기본 생성 수량입니다. 보스 패턴의 추가 소환수는 기존 RegisterAdditionalEnemies로 생존 카운트에 추가됩니다.
- 이 제안은 웨이브 안의 적을 한 번에 같은 포인트에서 생성합니다. 시간차 생성이 필요하면 별도 생성 간격을 두되 미생성 큐를 유지해야 합니다.
- 기존 WBH_EnemySpawner의 NavMesh.SamplePosition 보정을 재사용하므로 실제 위치는 선택 포인트 주변 NavMesh로 보정될 수 있습니다.
- 배열/후보 데이터는 사전 검사하지만 풀 등록·NavMesh 실패는 기존 Spawner 반환값으로 감지합니다. 모든 예외 상황에서 생성 성공을 보장한다는 의미는 아닙니다.
- 확인한 것은 현재 씬 구조, 직렬화 참조, 코드 호출 흐름입니다. 이 교체안의 Unity 컴파일·플레이·실제 생성 총합은 미검증입니다.
- 실제 스크립트·씬을 수정하거나 저장하지 않았으며 개인 구현 로그는 갱신하지 않았습니다.
