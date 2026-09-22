using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 화면 위에 짧은 비차단 안내 문구를 표시한다.
/// 팝업 바깥에 배치해, 호출한 팝업이 닫혀도 안내가 정상적으로 끝난다.
/// </summary>
public class KY_ScreenNotice : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [Min(0f)] [SerializeField] private float displayDuration = 2.5f;

    private KY_CurtainEffect curtainEffect;
    private Tween hideTween;

    private void Awake()
    {
        curtainEffect = GetComponent<KY_CurtainEffect>();
    }

    private void OnDestroy()
    {
        hideTween?.Kill();
    }

    /// <summary>문구를 갱신하고 커튼 연출과 함께 잠시 표시한다.</summary>
    public void Show(string message)
    {
        if (messageText != null)
            messageText.text = message;

        gameObject.SetActive(true);
        curtainEffect ??= GetComponent<KY_CurtainEffect>();
        curtainEffect?.Open();

        hideTween?.Kill();
        hideTween = DOVirtual.DelayedCall(displayDuration, Hide, true);
    }

    private void Hide()
    {
        hideTween?.Kill();
        hideTween = null;

        if (!gameObject.activeSelf || curtainEffect == null)
        {
            gameObject.SetActive(false);
            return;
        }

        curtainEffect.Close().OnComplete(() =>
        {
            if (this != null)
                gameObject.SetActive(false);
        });
    }
}
