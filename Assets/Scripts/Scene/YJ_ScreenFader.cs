using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class YJ_ScreenFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Duration")]
    [FormerlySerializedAs("duration")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.5f;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (canvasGroup == null)
        {
            Debug.LogError("[YJ_ScreenFader] CanvasGroup이 연결되지 않았습니다.", this);
            enabled = false;
            return;
        }

        // 투명한 페이드 화면이 첫 프레임부터 UI 입력을 가로채지 않게 한다.
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.transform.localScale = Vector3.one;
    }

    public IEnumerator FadeToBlack()
    {
        yield return Fade(1f, fadeOutDuration);
    }

    public IEnumerator FadeFromBlack()
    {
        yield return Fade(0f, fadeInDuration);
    }

    // 이번 호출에만 적용하며 인스펙터의 기본 시간은 변경하지 않습니다.
    public IEnumerator FadeToBlack(float duration)
    {
        yield return Fade(1f, Mathf.Max(0f, duration));
    }

    private IEnumerator Fade(float targetAlpha, float duration)
    {
        if (canvasGroup == null)
            yield break;

        float startAlpha = canvasGroup.alpha;

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            canvasGroup.blocksRaycasts = targetAlpha > 0f;
            yield break;
        }

        // 씬 활성화에 사용된 이전 프레임의 긴 deltaTime이 첫 계산에 섞이지 않도록
        // 페이드가 실제로 시작된 시각부터 경과 시간을 직접 측정한다.
        double startedAt = Time.realtimeSinceStartupAsDouble;

        while (true)
        {
            yield return null;

            float elapsed = (float)(Time.realtimeSinceStartupAsDouble - startedAt);
            float progress = Mathf.Clamp01(elapsed / duration);

            canvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                progress);

            if (progress >= 1f)
                break;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.blocksRaycasts = targetAlpha > 0f;
    }
}
