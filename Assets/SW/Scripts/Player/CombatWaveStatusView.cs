using Mirror;
using TMPro;
using UnityEngine;

/// <summary>서버 완료 상태 또는 싱글 스포너의 확정 진행을 같은 HUD에 표시합니다.</summary>
public sealed class CombatWaveStatusView : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private NetworkEnemyWaveSpawner networkWaves;
    [SerializeField] private WBH_EnemySpawnManager singleWaves;

    private void Update()
    {
        if (statusText == null)
            return;

        // SW 수정 : 멀티는 서버 상태, 싱글은 현재 스포너의 진행 상태를 표시한다.
        int totalWaves;
        int currentWave;
        bool hasWaves;
        bool completed;
        if (NetworkClient.active)
        {
            totalWaves = networkWaves != null ? networkWaves.TotalWaveCount : 0;
            currentWave = networkWaves != null ? networkWaves.CurrentWave : 0;
            hasWaves = networkWaves != null;
            completed = networkWaves != null && networkWaves.SessionPhase == MirrorSessionPhase.Completed;
        }
        else
        {
            totalWaves = singleWaves != null ? singleWaves.WaveCount : 0;
            currentWave = singleWaves != null ? singleWaves.CurrentWaveIndex + 1 : 0;
            hasWaves = singleWaves != null && totalWaves > 0;
            completed = singleWaves != null && singleWaves.AllwavesCompleted;
        }

        string message = string.Empty;
        if (hasWaves)
        {
            if (completed)
                message = "전원 처치! · 포털로 이동하세요";
            else if (currentWave <= 0)
                message = "전투 준비";
            else if (totalWaves > 0)
                message = $"웨이브 {currentWave}/{totalWaves}";
        }

        if (statusText.text != message)
            statusText.text = message;
    }
}
