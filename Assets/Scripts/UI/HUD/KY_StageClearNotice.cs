using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 스테이지 클리어 시 상단에 잠시 표시하는 비모달 알림입니다.
/// 스테이지 판정 코드와 분리되어 있으며, StageCleared 이벤트의 수신부에서 Show()를 호출합니다.
/// </summary>
public sealed class KY_StageClearNotice : MonoBehaviour
{
    [Header("표시 대상")]
    [SerializeField] private CanvasGroup noticeGroup;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private string message = "스테이지를 클리어했습니다!\n포탈을 통해 다음 스테이지를 선택하십시오!";

    [Header("표시 시간")]
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.2f;
    [Min(0f)] [SerializeField] private float visibleDuration = 2.5f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.5f;

    private Coroutine displayRoutine;

    private void Awake()
    {
        if (noticeGroup == null)
            noticeGroup = GetComponent<CanvasGroup>();

        if (messageText != null)
            messageText.text = message;

        SetVisible(false);
    }

    /// <summary>
    /// StageCleared 이벤트 수신부에서 호출할 공개 진입점입니다.
    /// </summary>
    public void Show()
    {
        if (!isActiveAndEnabled)
            return;

        if (displayRoutine != null)
            StopCoroutine(displayRoutine);

        displayRoutine = StartCoroutine(DisplayRoutine());
    }

    public void Hide()
    {
        if (displayRoutine != null)
            StopCoroutine(displayRoutine);

        displayRoutine = null;
        SetVisible(false);
    }

    private IEnumerator DisplayRoutine()
    {
        SetVisible(true);
        yield return Fade(0f, 1f, fadeInDuration);
        yield return new WaitForSecondsRealtime(visibleDuration);
        yield return Fade(1f, 0f, fadeOutDuration);

        displayRoutine = null;
        SetVisible(false);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (noticeGroup == null)
            yield break;

        if (duration <= 0f)
        {
            noticeGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        noticeGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            noticeGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        noticeGroup.alpha = to;
    }

    private void SetVisible(bool visible)
    {
        if (noticeGroup == null)
            return;

        noticeGroup.alpha = visible ? 1f : 0f;
        noticeGroup.interactable = false;
        noticeGroup.blocksRaycasts = false;
    }
}
