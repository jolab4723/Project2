using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// 비활성(interactable == false) 버튼을 클릭했을 때 클릭이 불가능 하다며 흔들리는 연출
/// 활성 상태의 버튼 클릭에는 반응하지 않는다.
/// </summary>
public class KY_ButtonShakeEffect : MonoBehaviour, IPointerClickHandler
{
    [Header("Target")]
    [SerializeField] private Button button;

    [Header("Shake Settings")]
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeStrength = 10f;
    [SerializeField] private int vibrato = 20;
    [SerializeField, Range(0f, 90f)] private float randomness = 90f;

    private bool isShaking = false;

    /// <summary>인스펙터에서 button이 지정되지 않았다면 같은 GameObject에서 자동으로 찾는다.</summary>
    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    /// <summary>비활성 상태에서 클릭 시에만 셰이크를 실행한다. 활성 상태이거나 이미 흔들리는 중이면 무시한다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (button.interactable) return;
        if (isShaking) return;

        isShaking = true;
        transform.DOShakePosition(shakeDuration, shakeStrength, vibrato, randomness)
            .SetUpdate(true)
            .OnComplete(() => isShaking = false);
    }
}