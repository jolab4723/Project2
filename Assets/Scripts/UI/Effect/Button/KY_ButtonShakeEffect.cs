using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using System;

/// <summary>
/// 비활성(interactable == false) 버튼을 클릭했을 때 클릭이 불가능 하다며 흔들리는 연출
/// 활성 상태의 버튼 클릭에는 반응하지 않는다.
/// </summary>
public class KY_ButtonShakeEffect : MonoBehaviour, IPointerClickHandler
{
    /// <summary>비활성 버튼을 눌렀을 때, 버튼별 추가 피드백을 연결할 수 있다.</summary>
    public event Action DisabledClicked;

    [Header("Target")]
    [SerializeField] private Button button;

    [Header("Shake Settings")]
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeStrength = 10f;
    [SerializeField] private int vibrato = 20;
    [SerializeField, Range(0f, 90f)] private float randomness = 90f;

    private RectTransform targetRect;
    private Tween shakeTween;
    private Vector2 originalAnchoredPosition;
    private bool isShaking;

    /// <summary>인스펙터에서 button이 지정되지 않았다면 같은 GameObject에서 자동으로 찾는다.</summary>
    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        targetRect = transform as RectTransform;
    }

    /// <summary>비활성 상태에서 클릭 시에만 셰이크를 실행한다. 활성 상태이거나 이미 흔들리는 중이면 무시한다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (button == null || button.interactable) return;

        DisabledClicked?.Invoke();
        if (isShaking) return;

        if (targetRect == null)
            return;

        // UI 레이아웃이 갱신된 뒤의 좌표를 매번 기준점으로 삼는다.
        // DOShakePosition은 월드 Transform 위치를 건드려 UI가 중간 위치에 남을 수 있다.
        shakeTween?.Kill();
        originalAnchoredPosition = targetRect.anchoredPosition;
        isShaking = true;
        shakeTween = targetRect.DOShakeAnchorPos(shakeDuration, shakeStrength, vibrato, randomness)
            .SetUpdate(true)
            .OnComplete(RestorePosition)
            .OnKill(RestorePosition);
    }

    private void OnDisable()
    {
        if (!isShaking)
            return;

        shakeTween?.Kill();
        RestorePosition();
    }

    private void RestorePosition()
    {
        if (targetRect != null)
            targetRect.anchoredPosition = originalAnchoredPosition;

        shakeTween = null;
        isShaking = false;
    }
}
