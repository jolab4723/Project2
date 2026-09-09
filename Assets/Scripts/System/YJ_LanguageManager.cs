using System;
using Core;
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
public class YJ_LanguageManager : Singleton<YJ_LanguageManager>, IManagerModule
{
    public string ModuleName => "YJ_LanguageManager";

    public GameLanguage CurrentLanguage { get; private set; }

    public event Action<GameLanguage> LanguageChanged;

    [Header("Language")]
    [SerializeField] private GameLanguage inspectorLanguage = GameLanguage.KOR;

    [Header("Fonts")]
    [Tooltip("비워두면 Resources에서 공용 폰트를 자동으로 찾아 쓴다(씬에 직접 배치하지 않은 경우 대비).")]
    [SerializeField] private TMP_FontAsset koreanEnglishFont;
    [SerializeField] private TMP_FontAsset japaneseFont;
    [SerializeField] private TMP_FontAsset simplifiedChineseFont;

    protected override void Awake()
    {
        base.Awake();

        // 중복 인스턴스면 base.Awake()가 이미 Destroy 처리했다 - 더 이상 초기화하지 않는다.
        if (Instance != this)
            return;

        if (koreanEnglishFont == null)
            koreanEnglishFont = Resources.Load<TMP_FontAsset>("Font/Pretendard-Medium SDF");
        if (japaneseFont == null)
            japaneseFont = Resources.Load<TMP_FontAsset>("Font/Noto_Sans_JP/static/NotoSansJP-Medium SDF");
        if (simplifiedChineseFont == null)
            simplifiedChineseFont = Resources.Load<TMP_FontAsset>("Font/Noto_Sans_SC/static/NotoSansSC-Medium SDF");

        CurrentLanguage = inspectorLanguage;
    }

    // IManagerModule - 다른 코어 매니저와 동일한 활성화 알림. 실제 초기화(폰트/언어)는 Awake에서
    // 이미 끝나 있어야 하므로(씬에 배치 안 해도 어디서든 Instance 접근 시 자동 생성되고 바로 준비돼야
    // 함) 여기서는 완료 로그만 남긴다.
    public void Activate()
    {
        Debug.Log("[YJ_LanguageManager] 활성화 완료. 현재 언어=" + CurrentLanguage);
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
