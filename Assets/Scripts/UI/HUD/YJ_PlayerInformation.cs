using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class YJ_PlayerInformation : MonoBehaviour
{
    private const float PlayerSearchInterval = 0.5f;

    [SerializeField] private WBH_PlayerStatus playerStatus;

    [SerializeField] private TMP_Text playerLevel;
    [SerializeField] private Image playerImage;

    private float displayedLevel = float.NaN;
    private float nextPlayerSearchTime;
    private bool usesExternalPlayer; // SW 수정

    /// <summary>멀티플레이에서 정보를 표시할 로컬 플레이어를 전달받는다.</summary>
    public void BindPlayer(WBH_PlayerStatus owner)
    {
        usesExternalPlayer = true;
        playerStatus = owner;
        displayedLevel = float.NaN;
        RefreshLevel();
    }

    private void Awake()
    {
        if (playerLevel == null)
            playerLevel = GetComponentInChildren<TMP_Text>(true);
    }

    private void Start()
    {
        ResolvePlayerStatus();
        RefreshLevel();
    }

    private void Update()
    {
        if (playerStatus == null && Time.unscaledTime >= nextPlayerSearchTime)
            ResolvePlayerStatus();

        RefreshLevel();
    }

    private void ResolvePlayerStatus()
    {
        nextPlayerSearchTime = Time.unscaledTime + PlayerSearchInterval;

        if (playerStatus == null && !usesExternalPlayer)
            playerStatus = FindFirstObjectByType<WBH_PlayerStatus>();
    }

    private void RefreshLevel()
    {
        if (playerStatus == null || playerLevel == null)
            return;

        float currentLevel = playerStatus.CurrentLevel;
        if (Mathf.Approximately(displayedLevel, currentLevel))
            return;

        displayedLevel = currentLevel;
        playerLevel.SetText("{0:0}", currentLevel);
    }
}
