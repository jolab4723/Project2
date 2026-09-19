using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로딩 화면의 이미지와 팁 제목·상세 설명을 코드 목록에서 함께 표시한다.
/// </summary>
public class KY_LoadingPresentation : MonoBehaviour
{
    // 로딩 씬이 다시 열려도 직전 팁을 기억해 연속 표시를 막는다.
    private static int lastShownEntryIndex = -1;

    [Header("표시 대상")]
    [SerializeField] private Image loadingImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailText;

    [Header("표시 방식")]
    [SerializeField] private bool chooseRandomEntry = true;
    [SerializeField] private int previewEntryIndex;
    private int currentEntryIndex;

    private void OnEnable()
    {
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;
        ShowEntry(chooseRandomEntry ? GetRandomEntryIndex() : previewEntryIndex);
    }

    private void OnDisable()
    {
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(GameLanguage _)
    {
        // 로딩 중 언어가 바뀌어도 같은 항목을 다시 표시한다.
        ShowEntry(currentEntryIndex);
    }

    /// <summary>Inspector 테스트나 외부 호출에서 특정 팁을 표시한다.</summary>
    public void ShowEntry(int index)
    {
        if (KY_LoadingTipDatabase.Count == 0)
            return;

        index = Mathf.Clamp(index, 0, KY_LoadingTipDatabase.Count - 1);
        currentEntryIndex = index;
        KY_LoadingTipDatabase.Entry entry = KY_LoadingTipDatabase.Get(index);
        lastShownEntryIndex = index;
        Sprite image = Resources.Load<Sprite>(entry.ImageResourcePath);

        if (loadingImage != null)
        {
            loadingImage.sprite = image;
            loadingImage.enabled = image != null;
        }

        if (titleText != null)
            titleText.text = entry.Title;

        if (detailText != null)
            detailText.text = entry.Detail;
    }

    private int GetRandomEntryIndex()
    {
        int count = KY_LoadingTipDatabase.Count;
        if (count <= 1 || lastShownEntryIndex < 0 || lastShownEntryIndex >= count)
            return UnityEngine.Random.Range(0, count);

        // 직전 번호 하나를 제외한 개수만큼만 먼저 무작위로 뽑는다.
        int index = UnityEngine.Random.Range(0, count - 1);
        // 직전 번호 이상이면 한 칸 건너뛰어 그 번호가 다시 선택되지 않게 한다.
        return index >= lastShownEntryIndex ? index + 1 : index;
    }
}
