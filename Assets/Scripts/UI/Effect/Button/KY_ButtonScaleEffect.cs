using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

    [Tooltip("지정하지 않으면 같은 GameObject에서 자동으로 찾는다. 있으면 비활성(interactable == false) 상태일 때 호버/클릭 연출을 재생하지 않는다.")]
    [SerializeField] private Button button;

    private Vector3 originalScale;  // 버튼의 원래 크기를 저장할 변수.
    private bool isHovering = false;
    private Tween scaleTween;

    /// <summary>버튼의 원래 크기를 저장하고, button이 비어있으면 같은 GameObject에서 찾는다.</summary>
    void Awake()
    {
        originalScale = transform.localScale;

        if (button == null)
            button = GetComponent<Button>();
    }

    /// <summary>호버시 hoverScale로 확대. 버튼이 비활성 상태면 무시한다.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;

        isHovering = true;
        scaleTween?.Kill();
        scaleTween = transform.DOScale(originalScale * hoverScale, hoverDuration)
            .SetEase(ease)
            .SetUpdate(true);
    }

    /// <summary>호버 종료 시 원래 크기로 복귀.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHovering) return;

        isHovering = false;
        scaleTween?.Kill();
        scaleTween = transform.DOScale(originalScale, hoverDuration)
            .SetEase(ease)
            .SetUpdate(true);
    }

    /// <summary>클릭 시 눌림 스케일로 축소했다가, 현재 호버 상태에 맞는 크기로 복귀한다. 버튼이 비활성 상태면 무시한다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;

        scaleTween?.Kill();

        Vector3 restoreTarget = isHovering ? originalScale * hoverScale : originalScale;

        scaleTween = transform.DOScale(originalScale * clickScale, clickDuration)
            .SetEase(ease)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                scaleTween = transform.DOScale(restoreTarget, clickDuration)
                    .SetEase(ease)
                    .SetUpdate(true);
            });
    }

    private void OnDisable()
    {
        scaleTween?.Kill();
        scaleTween = null;
        transform.localScale = originalScale;
        isHovering = false;
    }
}
