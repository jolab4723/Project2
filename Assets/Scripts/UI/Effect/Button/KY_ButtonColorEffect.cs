using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// 버튼 호버/클릭 시 색상 변화를 담당하는 코드
/// 배경, 테두리 등 여러 이미지를 지정 가능하며, 각 타겟마다 개별 색상을 설정할 수 있다.
/// 기본/호버/클릭 3단계 색상을 타겟별로 가질 수 있다.
/// </summary>
public class KY_ButtonColorEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [System.Serializable]
    private class ColorTarget
    {
        public Graphic graphic;
        public Color hoverColor;
        public Color clickColor;
        [HideInInspector] public Color originalColor;
        // WJ 이우진 추가(2026-10-06): SetStateColors로 바꾼 뒤 ResetStateColors가 되돌릴 처음(에디터에서 정한) 색.
        [System.NonSerialized] public Color authoredOriginalColor;
        [System.NonSerialized] public Color authoredHoverColor;
    }

    [Header("Targets")]
    [SerializeField] private ColorTarget[] targets;

    [Header("Timing")]
    [SerializeField] private float hoverDuration = 0.15f;
    [SerializeField] private float clickDuration = 0.1f;

    [Tooltip("지정하지 않으면 같은 GameObject에서 자동으로 찾는다. 있으면 비활성(interactable == false) 상태일 때 호버/클릭 연출을 재생하지 않는다.")]
    [SerializeField] private Button button;

    private bool isHovering = false;
    private Tween clickResetTween;
    private bool colorsCaptured;

    /// <summary>각 타겟의 초기(기본) 색상을 순서대로 캡처하고, button이 비어있으면 같은 GameObject에서 찾는다.</summary>
    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        CaptureColors();
    }

    // WJ 이우진 수정(2026-10-06): 초기 색 캡처를 한 곳으로 모아, Awake 전에 SetStateColors가 불려도 같은 기준을 쓴다.
    private void CaptureColors()
    {
        if (colorsCaptured)
            return;

        foreach (var t in targets)
        {
            t.originalColor = t.graphic.color;
            t.authoredOriginalColor = t.originalColor;
            t.authoredHoverColor = t.hoverColor;
        }
        colorsCaptured = true;
    }

    /// <summary>
    /// WJ 이우진 추가(2026-10-06): 모든 타겟의 기본색과 호버색을 상태에 맞게 바꾼다(예: 보상까지 받은 퀘스트 슬롯은 회색).
    /// 호버 종료·클릭 복귀도 이 색을 기준으로 한다. 클릭 색은 그대로 둔다.
    /// </summary>
    public void SetStateColors(Color baseColor, Color hoverColor)
    {
        CaptureColors();
        foreach (var t in targets)
        {
            t.originalColor = baseColor;
            t.hoverColor = hoverColor;
        }
        ApplyCurrentColors();
    }

    /// <summary>WJ 이우진 추가(2026-10-06): SetStateColors로 바꾼 색을 에디터에서 정한 처음 색으로 되돌린다.</summary>
    public void ResetStateColors()
    {
        CaptureColors();
        foreach (var t in targets)
        {
            t.originalColor = t.authoredOriginalColor;
            t.hoverColor = t.authoredHoverColor;
        }
        ApplyCurrentColors();
    }

    private void ApplyCurrentColors()
    {
        foreach (var t in targets)
        {
            t.graphic.DOKill();
            t.graphic.color = isHovering ? t.hoverColor : t.originalColor;
        }
    }

    /// <summary>호버 진입 시 모든 타겟을 각자의 hoverColor로 전환한다. 버튼이 비활성 상태면 무시한다.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;

        isHovering = true;
        foreach (var t in targets)
        {
            t.graphic.DOKill();
            t.graphic.DOColor(t.hoverColor, hoverDuration).SetUpdate(true);
        }
    }

    /// <summary>호버 종료 시 모든 타겟을 각자의 원래 색으로 복귀시킨다.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHovering) return;

        isHovering = false;
        RestoreOriginalColors(hoverDuration);
    }

    /// <summary>클릭 시 각 타겟을 자신의 clickColor로 전환했다가, 현재 호버 상태에 맞는 색으로 복귀한다. 버튼이 비활성 상태면 무시한다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;

        foreach (var t in targets)
        {
            t.graphic.DOKill();
            t.graphic.DOColor(t.clickColor, clickDuration).SetUpdate(true);
        }

        clickResetTween?.Kill();
        clickResetTween = DOVirtual.DelayedCall(clickDuration, RestoreColorAfterClick)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void OnDisable()
    {
        clickResetTween?.Kill();
        clickResetTween = null;
    }

    /// <summary>마지막 클릭 연출이 끝난 뒤 현재 포인터 상태에 맞는 색으로 복귀한다.</summary>
    private void RestoreColorAfterClick()
    {
        if (isHovering)
        {
            foreach (var t in targets)
            {
                t.graphic.DOKill();
                t.graphic.DOColor(t.hoverColor, hoverDuration).SetUpdate(true);
            }
        }
        else
        {
            RestoreOriginalColors(hoverDuration);
        }

        clickResetTween = null;
    }

    /// <summary>모든 타겟을 각자 캡처해둔 원래 색으로 동시에 복귀시킨다.</summary>
    private void RestoreOriginalColors(float duration)
    {
        foreach (var t in targets)
        {
            t.graphic.DOKill();
            t.graphic.DOColor(t.originalColor, duration).SetUpdate(true);
        }
    }
}
