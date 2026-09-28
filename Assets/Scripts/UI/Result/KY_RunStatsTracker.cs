using UnityEngine;

/// <summary>
/// 한 번의 원정 동안 결과 화면에 필요한 기록을 모은다.
/// 실제 적 사망·보상·종료 코드는 이 컴포넌트의 공개 메서드만 호출하면 된다.
/// ResultPayload에는 결과 씬의 KY_ResultScreen과 같은 ResultPayload.asset을 연결한다.
/// </summary>
public sealed class KY_RunStatsTracker : MonoBehaviour
{
    public static KY_RunStatsTracker Instance { get; private set; }

    [SerializeField] private KY_ResultPayload payload;

    private KY_RunStats stats;
    private float runStartRealtime;
    private bool runActive;

    public KY_RunStats CurrentStats => stats;

    /// <summary>싱글 인스턴스로 등록하고 씬 전환 후에도 기록을 유지한다.</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Start 씬의 Managers 아래에 배치되어도 이 오브젝트만 씬 전환 후 유지한다.
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>파괴될 때 정적 참조를 정리한다.</summary>
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>새 원정을 시작하고 이전 기록을 비운다.</summary>
    public void BeginRun(string initialStageName = null)
    {
        stats = new KY_RunStats
        {
            stageName = initialStageName ?? string.Empty
        };
        runStartRealtime = Time.realtimeSinceStartup;
        runActive = true;
    }

    /// <summary>현재 도달한 스테이지 이름을 기록한다.</summary>
    public void SetStage(string stageName)
    {
        EnsureRun();

        stats.stageName = stageName ?? string.Empty;
    }

    /// <summary>적 처치 수를 더한다.</summary>
    public void RecordEnemyDefeated(int count = 1)
    {
        EnsureRun();
        stats.defeatedEnemies += Mathf.Max(0, count);
    }

    /// <summary>
    /// 원정을 종료하고 현재 지갑·스테이지 진행도와 함께 결과 Payload에 기록한다.
    /// earnedCredits를 넘기면 결과 화면에 그 금액을 표시한다(실제 계정 적립액 - 사망 30%, 클리어·정산은
    /// 보유 크레딧 + 아이템 원가 50%, DataManager.CalculateRunEndCredits). 생략하면 기존처럼 지갑 잔액을 표시한다.
    /// </summary>
    public bool FinishRun(bool cleared, int? earnedCredits = null)
    {
        if (payload == null)
        {
            Debug.LogError("[KY_RunStatsTracker] ResultPayload가 연결되지 않았습니다.");
            return false;
        }

        // 처치·보상 없이 종료된 원정도 빈 결과로 표시할 수 있게 한다.
        EnsureRun();

        if (runActive)
            stats.elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - runStartRealtime);

        payload.SetResult(new KY_ResultData
        {
            cleared = cleared,
            stageName = ResolveReachedStage(),
            defeatedEnemies = stats.defeatedEnemies,
            playTimeSeconds = stats.elapsedSeconds,
            earnedCredits = earnedCredits.HasValue ? Mathf.Max(0, earnedCredits.Value) : ResolveRemainingCredits(),
            combo = 0
        });

        runActive = false;
        return true;
    }

    /// <summary>
    /// 원정을 끝내지 않고 현재 기록만 액트 중간 정산 결과로 복사한다.
    /// 다음 Act에서도 시간과 처치 수를 이어서 집계한다.
    /// </summary>
    public bool PublishActClearSnapshot()
    {
        if (payload == null)
        {
            Debug.LogError("[KY_RunStatsTracker] ResultPayload가 연결되지 않았습니다.");
            return false;
        }

        EnsureRun();

        if (runActive)
            stats.elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - runStartRealtime);

        payload.SetResult(new KY_ResultData
        {
            resultType = KY_ResultType.ActClear,
            cleared = true,
            stageName = ResolveReachedStage(),
            defeatedEnemies = stats.defeatedEnemies,
            playTimeSeconds = stats.elapsedSeconds,
            // 액트 중간에는 계정 적립이 없으므로 "파밍 가치 현황"으로 보유 크레딧과 장비 가치(원가 50%)를 나눠 보여준다.
            earnedCredits = ResolveRemainingCredits(),
            itemValueCredits = ResolveItemValueCredits(),
            combo = 0
        });

        return true;
    }

    /// <summary>인게임 지갑의 종료 시점 크레디트를 결과 보상으로 사용한다.</summary>
    private static int ResolveRemainingCredits()
    {
        PlayerWallet wallet = InventoryController.Instance != null
            ? InventoryController.Instance.PlayerWallet
            : null;

        return wallet != null ? Mathf.Max(0, wallet.Gold) : 0;
    }

    /// <summary>인벤토리·장착 장비 원가 합에서 정산 때 크레딧으로 바뀌는 비율(50%)만큼을 반환한다.</summary>
    private static int ResolveItemValueCredits()
    {
        int basePrice = ItemSystem.ItemValueCalculator.GetOwnedItemsBasePrice(InventoryController.Instance);
        return (int)((long)basePrice * Core.DataManager.ItemValueCreditPercent / 100);
    }

    /// <summary>저장된 현재 노드 또는 마지막 클리어 노드에서 Act와 도달 층을 만든다.</summary>
    private string ResolveReachedStage()
    {
        YJ_StageSaveService stageSaveService = FindFirstObjectByType<YJ_StageSaveService>();
        if (stageSaveService == null || !stageSaveService.TryLoadSaveData(out StageMapSaveData saveData))
            return stats.stageName;

        int floor = saveData.clearedFloor;
        if (!string.IsNullOrWhiteSpace(saveData.pendingNodeId))
        {
            StageNodeSaveData pendingNode = saveData.nodes?.Find(
                node => node != null && node.id == saveData.pendingNodeId);

            if (pendingNode != null)
                floor = pendingNode.floor;
        }

        if ((int)saveData.act <= 0 || floor <= 0)
            return stats.stageName;

        return $"ACT {(int)saveData.act} · FLOOR {floor}";
    }

    /// <summary>기록 호출이 먼저 와도 새 런을 시작해 누락을 막는다.</summary>
    private void EnsureRun()
    {
        if (stats == null || !runActive)
            BeginRun();
    }
}

[System.Serializable]
/// <summary>한 번의 원정에서 수집한 결과 화면 표시용 수치다.</summary>
public sealed class KY_RunStats
{
    public string stageName;
    public int defeatedEnemies;
    public float elapsedSeconds;
}
