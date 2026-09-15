using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로딩 화면의 이미지와 팁 제목·상세 설명을 한 묶음으로 교체한다.
/// SceneLoader와 독립적으로 동작하므로 로딩 씬에 배치해 사용한다.
/// </summary>
public class KY_LoadingPresentation : MonoBehaviour
{
    [Serializable]
    private class LoadingTipEntry
    {
        public Sprite image;
        public string title;

        [TextArea(2, 5)]
        public string detail;
    }

    [Header("표시 대상")]
    [SerializeField] private Image loadingImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailText;

    [Header("팁 목록")]
    [SerializeField] private LoadingTipEntry[] entries;
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
        // 로딩 중 언어가 바뀌어도 같은 팁의 이미지와 키를 유지한다.
        ShowEntry(currentEntryIndex);
    }

    /// <summary>Inspector 테스트나 외부 호출에서 특정 팁을 표시한다.</summary>
    public void ShowEntry(int index)
    {
        if (entries == null || entries.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, entries.Length - 1);
        currentEntryIndex = index;
        LoadingTipEntry entry = entries[index];

        if (loadingImage != null)
        {
            loadingImage.sprite = entry.image;
            loadingImage.enabled = entry.image != null;
        }

        if (titleText != null)
            titleText.text = entry.title ?? string.Empty;

        if (detailText != null)
            detailText.text = entry.detail ?? string.Empty;
    }

    private int GetRandomEntryIndex()
    {
        return UnityEngine.Random.Range(0, entries.Length);
    }
}
