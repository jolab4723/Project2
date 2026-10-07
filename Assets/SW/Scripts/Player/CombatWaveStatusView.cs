using Mirror;
using TMPro;
using UnityEngine;

/// <summary>서버 완료 상태 또는 싱글 스포너의 확정 진행을 같은 HUD에 표시합니다.</summary>
public sealed class CombatWaveStatusView : MonoBehaviour
{
    private const string LabelDatabasePath = "DataFiles/UIData/3. GeneratedAssets/UILabelDatabase";

    [SerializeField] private TMP_Text statusText;
    [SerializeField] private NetworkEnemyWaveSpawner networkWaves;
    [SerializeField] private WBH_EnemySpawnManager singleWaves;

    private UILabelDatabaseSO labels;
    private GameLanguage shownLanguage;
    private int shownWave = -1, shownTotal = -1;
    private bool shownCompleted, shownHasWaves;

    // SW 수정 : 전투 배경·적과 겹쳐도 읽히도록 문구 길이에 맞춘 HUD 띠를 깐다.
    private void Awake() => HudTextBackplate.Attach(statusText, new Vector2(56f, 8f));

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

        // SW 수정 : 상태나 언어가 바뀐 때만 공용 UI 라벨로 문구를 다시 만들어 매 프레임 문자열을 생성하지 않는다.
        YJ_LanguageManager language = YJ_LanguageManager.Instance;
        GameLanguage currentLanguage = language != null ? language.CurrentLanguage : default;
        if (currentWave == shownWave && totalWaves == shownTotal && completed == shownCompleted &&
            hasWaves == shownHasWaves && currentLanguage == shownLanguage)
            return;
        shownWave = currentWave; shownTotal = totalWaves; shownCompleted = completed; shownHasWaves = hasWaves; shownLanguage = currentLanguage;

        string message = string.Empty;
        if (hasWaves)
        {
            if (completed)
                message = Label("hud_ui.wave_cleared", "전원 처치! 포탈로 이동하세요");
            else if (currentWave <= 0)
                message = Label("hud_ui.wave_ready", "전투 준비");
            else if (totalWaves > 0)
                message = string.Format(Label("hud_ui.wave_progress", "웨이브 {0}/{1}"), currentWave, totalWaves);
        }

        // 일본어·중국어 글자가 한국어 폰트에서 깨지지 않도록 UILabelText와 같은 언어별 폰트를 쓴다.
        TMP_FontAsset font = language != null ? language.GetCurrentFont() : null;
        if (font != null && statusText.font != font)
            statusText.font = font;
        statusText.text = message;
    }

    private string Label(string key, string fallback)
    {
        if (labels == null)
            labels = Resources.Load<UILabelDatabaseSO>(LabelDatabasePath);
        string value = labels != null ? labels.GetLabel(key) : null;
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }
}
