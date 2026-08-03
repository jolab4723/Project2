using System;
using TMPro;
using UnityEngine;

public enum GameLanguage
{
    KOR,
    ENG,
    JPN,
    CHN
}


[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class YJ_LanguageManager : MonoBehaviour
{
    public static YJ_LanguageManager Instance { get; private set; }
    public GameLanguage CurrentLanguage { get; private set; }

    public event Action<GameLanguage> LanguageChanged;

    [Header("Language")]
    [SerializeField] private GameLanguage inspectorLanguage = GameLanguage.KOR;

    [Header("Fonts")]
    [SerializeField] private TMP_FontAsset koreanEnglishFont;
    [SerializeField] private TMP_FontAsset japaneseFont;
    [SerializeField] private TMP_FontAsset simplifiedChineseFont;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CurrentLanguage = inspectorLanguage;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && Instance == this)
            SetLanguage(inspectorLanguage);
    }
#endif

    public TMP_FontAsset GetCurrentFont()
    {
        return CurrentLanguage switch
        {
            GameLanguage.JPN => japaneseFont,
            GameLanguage.CHN => simplifiedChineseFont,
            _ => koreanEnglishFont
        };
    }

    public void SetLanguage(GameLanguage language)
    {
        if (CurrentLanguage == language)
            return;

        inspectorLanguage = language;
        CurrentLanguage = language;

        LanguageChanged?.Invoke(language);
    }

    // Dropdown의 값 0~3을 연결할 때 사용합니다.
    public void SetLanguageByIndex(int index)
    {
        if ( ! Enum.IsDefined(typeof(GameLanguage), index))
            return;

        SetLanguage((GameLanguage)index);
    }
}
