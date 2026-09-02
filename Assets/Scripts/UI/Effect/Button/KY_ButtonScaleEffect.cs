using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// 버튼 호버/클릭 시 스케일 변화를 담당하는 연출 컴포넌트.
/// </summary>
public class KY_ButtonScaleEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Hover")]   // 호버시 변동
    [SerializeField, Range(1f, 1.5f)] private float hoverScale = 1.08f;
    [SerializeField] private float hoverDuration = 0.15f;

    [Header("Click")]   // 클릭시 변동
    [SerializeField, Range(0.5f, 1f)] private float clickScale = 0.95f;
    [SerializeField] private float clickDuration = 0.1f;

    [Header("Ease")]
    [SerializeField] private Ease ease = Ease.OutQuad;

    private Vector3 originalScale;  // 버튼의 원래 크기를 저장할 변수. 
    private bool isHovering = false;

    /// <summary>버튼의 원래 크기를 저장한다.</summary>
    void Awake()
    {
        originalScale = transform.localScale;
    }

    /// <summary>호버시 hoverScale로 확대.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        transform.DOKill();
        transform.DOScale(originalScale * hoverScale, hoverDuration)
            .SetEase(ease)
            .SetUpdate(true);
    }

    /// <summary>호버 종료 시 원래 크기로 복귀.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        transform.DOKill();
        transform.DOScale(originalScale, hoverDuration)
            .SetEase(ease)
            .SetUpdate(true);
    }

    /// <summary>클릭 시 눌림 스케일로 축소했다가, 현재 호버 상태에 맞는 크기로 복귀한다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        transform.DOKill();

        Vector3 restoreTarget = isHovering ? originalScale * hoverScale : originalScale;

        transform.DOScale(originalScale * clickScale, clickDuration)
            .SetEase(ease)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                transform.DOScale(restoreTarget, clickDuration)
                    .SetEase(ease)
                    .SetUpdate(true);
            });
    }
}