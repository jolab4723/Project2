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

    /// <summary>이번 런에서 얻은 크레디트를 더한다.</summary>
    public void RecordCreditsEarned(int amount)
    {
        EnsureRun();
        stats.earnedCredits += Mathf.Max(0, amount);
    }

    /// <summary>원정을 종료하고 결과 Payload에 현재 기록을 쓴다.</summary>
    public bool FinishRun(bool cleared)
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
            stageName = stats.stageName,
            defeatedEnemies = stats.defeatedEnemies,
            playTimeSeconds = stats.elapsedSeconds,
            earnedCredits = stats.earnedCredits,
            combo = 0
        });

        runActive = false;
        return true;
    }

    /// <summary>기록 호출이 먼저 와도 새 런을 시작해 누락을 막는다.</summary>
    private void EnsureRun()
    {
        if (stats == null || !runActive)
            BeginRun();
    }
}

[System.Serializable]
public sealed class KY_RunStats
{
    public string stageName;
    public int defeatedEnemies;
    public float elapsedSeconds;
    public int earnedCredits;
}
