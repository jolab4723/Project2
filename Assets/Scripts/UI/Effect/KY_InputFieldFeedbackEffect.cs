using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// TMP 입력칸의 키보드 포커스와 오류 상태를 테두리 색으로 표시한다.
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public sealed class KY_InputFieldFeedbackEffect : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Graphic borderTarget;
    [SerializeField] private Color focusColor = new(0.1f, 0.9f, 1f, 0.9f);
    [SerializeField] private Color errorColor = new(1f, 0.25f, 0.3f, 0.95f);
    [SerializeField] private float transitionDuration = 0.15f;
    [SerializeField] private float errorDuration = 0.45f;

    private Color defaultBorderColor;
    private KY_UIShakeEffect shakeEffect;
    private Tween colorTween;
    private Tween borderTween;
    private bool isShowingError;

    private void Awake()
    {
        inputField ??= GetComponent<TMP_InputField>();
        borderTarget ??= transform.Find("Outline")?.GetComponent<Graphic>();
        if (borderTarget == null)
        {
            Debug.LogWarning($"[{nameof(KY_InputFieldFeedbackEffect)}] {name}에서 Outline 이미지를 찾지 못했습니다.", this);
            return;
        }

        defaultBorderColor = borderTarget.color;
        shakeEffect = GetComponent<KY_UIShakeEffect>() ?? gameObject.AddComponent<KY_UIShakeEffect>();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!isShowingError)
            SetBorderColor(focusColor);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!isShowingError)
            SetBorderColor(defaultBorderColor);
    }

    /// <summary>오류를 붉은 테두리와 흔들림으로 알린 뒤 현재 포커스 상태로 되돌린다.</summary>
    public void PlayError()
    {
        if (borderTarget == null)
            return;

        isShowingError = true;
        colorTween?.Kill();
        shakeEffect?.Play();
        SetBorderColor(errorColor);
        colorTween = DOVirtual.DelayedCall(errorDuration, () =>
        {
            isShowingError = false;
            SetBorderColor(inputField != null && inputField.isFocused ? focusColor : defaultBorderColor);
        }).SetLink(gameObject);
    }

    private void SetBorderColor(Color color)
    {
        if (borderTarget == null)
            return;

        borderTween?.Kill();
        borderTween = DOTween.To(() => borderTarget.color, value => borderTarget.color = value, color, transitionDuration)
            .SetLink(gameObject);
    }

    private void OnDisable()
    {
        colorTween?.Kill();
        borderTween?.Kill();
        if (borderTarget != null)
            borderTarget.color = defaultBorderColor;
    }
}
