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
        if (statusText == null) return;
        bool multiplayer = NetworkClient.active;
        int total = multiplayer ? networkWaves != null ? networkWaves.TotalWaveCount : 0
            : singleWaves != null ? singleWaves.WaveCount : 0;
        bool hasWaves = multiplayer ? networkWaves != null : singleWaves != null && total > 0;
        int current = multiplayer ? networkWaves != null ? networkWaves.CurrentWave : 0
            : singleWaves != null ? singleWaves.CurrentWaveIndex + 1 : 0;
        bool completed = multiplayer ? networkWaves != null && networkWaves.SessionPhase == MirrorSessionPhase.Completed
            : singleWaves != null && singleWaves.AllwavesCompleted;
        string message = !hasWaves ? string.Empty : completed ? "전원 처치! · 포털로 이동하세요"
            : current <= 0 ? "전투 준비"
            : total > 0 ? $"웨이브 {current}/{total}" : string.Empty;
        if (statusText.text != message) statusText.text = message;
    }
}
