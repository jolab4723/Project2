using UnityEngine;

/// <summary>
/// 한 번의 원정 동안 결과 화면에 필요한 기록을 모은다.
/// 실제 적 사망·보상·종료 코드는 이 컴포넌트의 공개 메서드만 호출하면 된다.
/// </summary>
public sealed class KY_RunStatsTracker : MonoBehaviour
{
    public static KY_RunStatsTracker Instance { get; private set; }

    [SerializeField] private KY_ResultPayload payload;

    private KY_RunStats stats;
    private float runStartRealtime;
    private bool runActive;

    public KY_RunStats CurrentStats => stats;

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

    public void SetStage(string stageName)
    {
        if (stats == null)
            stats = new KY_RunStats();

        stats.stageName = stageName ?? string.Empty;
    }

    public void RecordEnemyDefeated(int count = 1)
    {
        EnsureRun();
        stats.defeatedEnemies += Mathf.Max(0, count);
    }

    public void RecordCreditsEarned(int amount)
    {
        EnsureRun();
        stats.earnedCredits += Mathf.Max(0, amount);
    }

    /// <summary>원정을 종료하고 결과 Payload에 현재 기록을 쓴다.</summary>
    public bool FinishRun(bool cleared)
    {
        if (payload == null || stats == null)
            return false;

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
