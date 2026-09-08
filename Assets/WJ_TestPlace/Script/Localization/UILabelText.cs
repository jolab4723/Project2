using TMPro;
using UnityEngine;

/// <summary>
/// 화면에 고정으로 표시되는 UI 문구(탭 이름, 버튼 라벨, 옵션 라벨 등) 하나를 담당하는 범용 컴포넌트.
/// 같은 오브젝트의 TextMeshProUGUI에 key로 찾은 UILabelDatabaseSO 문구를 적용하고, 언어가 바뀌면
/// 스스로 다시 적용한다. QuestOfferUI처럼 화면마다 SerializeField를 N개씩 늘리는 대신, 텍스트
/// 오브젝트 각각에 이 컴포넌트 하나(+key+DB)만 붙이면 되므로 옵션창처럼 고정 문구가 많은 화면에도
/// 그대로 재사용할 수 있다.
///
/// !! database가 비어있거나 key에 대응하는 문구가 없으면 아무것도 바꾸지 않는다 - 씬에 이 컴포넌트만
///    붙이고 배선 전이라도 기존에 디자이너가 입력해둔 문구가 그대로 보인다.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class UILabelText : MonoBehaviour
{
    [Tooltip("UILabelDatabaseSO 안의 key. 예: settings_ui.tab_display")]
    [SerializeField] private string key;

    [SerializeField] private UILabelDatabaseSO database;

    private TextMeshProUGUI text;

    private void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;

        Apply();
    }

    private void OnDestroy()
    {
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(GameLanguage _) => Apply();

    private void Apply()
    {
        // 폰트는 문구 유무와 무관하게 언어에 맞춰 갱신한다 - 한국어 전용 폰트(Pretendard 등)로는
        // 일본어/중국어 글자가 깨져(네모 박스) 보이기 때문에, 표시 언어의 폰트(YJ_LanguageManager가
        // 들고 있는 한/영·일·중 폰트 중 하나)로 항상 맞춰준다.
        if (YJ_LanguageManager.Instance != null)
        {
            TMP_FontAsset font = YJ_LanguageManager.Instance.GetCurrentFont();
            if (font != null)
                text.font = font;
        }

        if (database == null || string.IsNullOrEmpty(key))
            return;

        text.text = database.GetLabel(key);
    }
}
