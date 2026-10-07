using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// SW 수정 : 타이틀 프로필의 이름 박스를 눌러 닉네임을 바꾼다. 값은 <see cref="PlayerNicknameProfile"/>에 저장되어
/// 멀티 로비 닉네임 칸에 그대로 채워지고, 로비에서 바꾼 이름도 다음 타이틀에 표시된다.
/// Enter·바깥 클릭은 저장, Esc는 취소이며 규칙에 맞지 않으면 이름 박스 위 계정 캡션에 잠깐 안내한다.
/// </summary>
public sealed class TitleNicknameEditor : MonoBehaviour
{
    [Tooltip("평소 이름을 보여 주는 기존 문구(KY_TitleSceneManager.userInfoText).")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("편집 중에만 보이는 입력칸.")]
    [SerializeField] private TMP_InputField input;
    [Tooltip("이름 박스 전체를 누르는 영역.")]
    [SerializeField] private UnityEngine.UI.Button editButton;
    [Tooltip("이름 박스 위 계정 캡션. 잘못된 이름 안내에 잠깐 사용한다.")]
    [SerializeField] private TMP_Text hintText;

    private const float HintSeconds = 3f;
    private string currentName = string.Empty;
    private UILabelDatabaseSO labels;
    private Coroutine hintRoutine;
    private string hintBeforeError;

    private void Awake()
    {
        labels = Resources.Load<UILabelDatabaseSO>(SessionUIMessageLocalizer.DatabasePath);
        if (input != null)
        {
            input.characterLimit = PlayerNicknameProfile.MaxLength;
            input.onEndEdit.AddListener(EndEdit);
            input.gameObject.SetActive(false);
        }
        if (editButton != null)
            editButton.onClick.AddListener(BeginEdit);
    }

    private void Start()
    {
        currentName = PlayerNicknameProfile.Load();
        ShowName();
    }

    private void OnDestroy()
    {
        if (input != null) input.onEndEdit.RemoveListener(EndEdit);
        if (editButton != null) editButton.onClick.RemoveListener(BeginEdit);
    }

    private void BeginEdit()
    {
        if (input == null || input.gameObject.activeSelf || Core.SceneLoader.Instance?.IsLoading == true)
            return;
        TMP_FontAsset font = YJ_LanguageManager.Instance != null ? YJ_LanguageManager.Instance.GetCurrentFont() : null;
        if (font != null && input.textComponent != null)
            input.textComponent.font = font;
        // 로그아웃·로그인으로 저장 소유가 바뀌면 이름도 그 저장의 닉네임이므로 편집 직전에 다시 읽는다.
        currentName = PlayerNicknameProfile.Load();
        input.SetTextWithoutNotify(currentName);
        if (nameText != null) nameText.enabled = false;
        input.gameObject.SetActive(true);
        input.ActivateInputField();
        input.Select();
    }

    private void EndEdit(string value)
    {
        if (input == null || !input.gameObject.activeSelf)
            return;
        string nickname = value?.Trim() ?? string.Empty;
        if (nickname.Length > 0 && nickname != currentName)
        {
            if (PlayerNicknameProfile.TrySave(nickname))
                currentName = nickname;
            else
                ShowHint("닉네임은 1~24자로 입력해 주세요.");
        }
        input.DeactivateInputField();
        input.gameObject.SetActive(false);
        ShowName();
    }

    private void ShowName()
    {
        if (nameText == null) return;
        nameText.text = string.IsNullOrWhiteSpace(currentName) ? PlayerNicknameProfile.DefaultName : currentName;
        nameText.enabled = true;
    }

    private void ShowHint(string koreanSource)
    {
        if (hintText == null) return;
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        else hintBeforeError = hintText.text;
        hintText.text = SessionUIMessageLocalizer.GetMessage(labels, koreanSource);
        hintText.gameObject.SetActive(true);
        hintRoutine = StartCoroutine(RestoreHint());
    }

    private IEnumerator RestoreHint()
    {
        yield return new WaitForSecondsRealtime(HintSeconds);
        if (hintText != null) hintText.text = hintBeforeError;
        hintRoutine = null;
    }
}
