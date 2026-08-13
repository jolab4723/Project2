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

        if (playerStatus == null)
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
