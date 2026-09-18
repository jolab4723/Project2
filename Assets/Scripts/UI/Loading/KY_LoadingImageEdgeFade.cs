using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Preserve Aspect가 적용된 로딩 이미지의 실제 표시 경계에 맞춰
/// 상하좌우 그라데이션을 배치한다.
/// </summary>
[ExecuteAlways]
public class KY_LoadingImageEdgeFade : MonoBehaviour
{
    [Header("대상")]
    [SerializeField] private Image targetImage;

    [Header("가장자리 그라데이션")]
    [SerializeField] private RectTransform topFade;
    [SerializeField] private RectTransform bottomFade;
    [SerializeField] private RectTransform leftFade;
    [SerializeField] private RectTransform rightFade;

    [Range(0.05f, 0.5f)]
    [SerializeField] private float fadeRatio = 0.2f;

    private Sprite observedSprite;
    private Vector2 observedImageSize;

    private void OnEnable() => Refresh();

    private void LateUpdate()
    {
        if (targetImage == null)
            return;

        Vector2 imageSize = targetImage.rectTransform.rect.size;
        if (observedSprite != targetImage.sprite || observedImageSize != imageSize)
            Refresh();
    }

    /// <summary>현재 스프라이트 비율에 맞춰 네 가장자리의 위치와 크기를 갱신한다.</summary>
    public void Refresh()
    {
        if (targetImage == null || targetImage.sprite == null)
            return;

        Vector2 containerSize = targetImage.rectTransform.rect.size;
        if (containerSize.x <= 0f || containerSize.y <= 0f)
            return;

        Vector2 displaySize = GetDisplayedSize(containerSize, targetImage.sprite, targetImage.preserveAspect);
        float verticalFadeHeight = displaySize.y * fadeRatio;
        float horizontalFadeWidth = displaySize.x * fadeRatio;

        SetVerticalFade(topFade, displaySize.x, displaySize.y, verticalFadeHeight, 1f);
        SetVerticalFade(bottomFade, displaySize.x, displaySize.y, verticalFadeHeight, -1f);
        SetSideFade(leftFade, displaySize.x, displaySize.y, horizontalFadeWidth, -1f);
        SetSideFade(rightFade, displaySize.x, displaySize.y, horizontalFadeWidth, 1f);

        observedSprite = targetImage.sprite;
        observedImageSize = containerSize;
    }

    private static Vector2 GetDisplayedSize(Vector2 containerSize, Sprite sprite, bool preserveAspect)
    {
        if (!preserveAspect || sprite.rect.height <= 0f)
            return containerSize;

        float spriteAspect = sprite.rect.width / sprite.rect.height;
        float containerAspect = containerSize.x / containerSize.y;

        return spriteAspect > containerAspect
            ? new Vector2(containerSize.x, containerSize.x / spriteAspect)
            : new Vector2(containerSize.y * spriteAspect, containerSize.y);
    }

    private static void SetVerticalFade(
        RectTransform fade,
        float displayWidth,
        float displayHeight,
        float fadeHeight,
        float direction)
    {
        if (fade == null)
            return;

        SetCentered(fade);
        fade.sizeDelta = new Vector2(displayWidth, fadeHeight);
        fade.anchoredPosition = new Vector2(0f, direction * (displayHeight - fadeHeight) * 0.5f);
    }

    private static void SetSideFade(
        RectTransform fade,
        float displayWidth,
        float displayHeight,
        float fadeWidth,
        float direction)
    {
        if (fade == null)
            return;

        SetCentered(fade);
        // 좌우 그라데이션은 90도 회전된 상태라 로컬 가로·세로를 뒤집어 지정한다.
        fade.sizeDelta = new Vector2(displayHeight, fadeWidth);
        fade.anchoredPosition = new Vector2(direction * (displayWidth - fadeWidth) * 0.5f, 0f);
    }

    private static void SetCentered(RectTransform rectTransform)
    {
        rectTransform.anchorMin = rectTransform.anchorMax = rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }
}
