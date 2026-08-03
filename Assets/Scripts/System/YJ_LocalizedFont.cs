using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class YJ_LocalizedFont : MonoBehaviour
{
    private TMP_Text targetText;
    private YJ_LanguageManager languageManager;

    private void Awake()
    {
        targetText = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        languageManager = YJ_LanguageManager.Instance;

        if (languageManager == null)
        {
            Log.Error("YJ_LanguageManager를 찾을 수 없습니다.");
            return;
        }

        languageManager.LanguageChanged += ApplyFont;
        ApplyFont(languageManager.CurrentLanguage);
    }

    private void OnDestroy()
    {
        if (languageManager != null)
            languageManager.LanguageChanged -= ApplyFont;
    }

    private void ApplyFont(GameLanguage language)
    {
        TMP_FontAsset font = languageManager.GetCurrentFont();

        if (font != null)
            targetText.font = font;
    }
}