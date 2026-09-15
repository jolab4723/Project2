using DG.Tweening;
using UnityEngine;

// UI가 압축되었다가 가로나 세로로 커지는 연출용 코드입니다.
// 속도, 압축되는 크기를 조절할 수 있습니다.
public enum CurtainAxis { Vertical, Horizontal } // 가로 세로 전환

public class KY_CurtainEffect : MonoBehaviour
{
    [Header("압축 축")]
    public CurtainAxis axis = CurtainAxis.Vertical;

    [Header("타이밍")]
    public float openDuration = 0.01f;
    public float closeDuration = 0.01f;
    public Ease openEase = Ease.OutExpo;
    public Ease closeEase = Ease.InExpo;

    [Header("일시정지 영향 여부")]
    public bool ignoreTimeScale = false;

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Tween curtainTween;

    void Awake()
    {
        EnsureInitialized();
    }

    // 패널이 열릴 때 호출
    public Tween Open()
    {
        if (!EnsureInitialized())
            return DOTween.Sequence();

        curtainTween?.Kill();

        Vector3 compressed = GetCompressedScale();
        rectTransform.localScale = compressed;

        curtainTween = rectTransform.DOScale(originalScale, openDuration)
            .SetEase(openEase)
            .SetUpdate(ignoreTimeScale)
            .SetLink(gameObject);

        return curtainTween;
    }

    // 패널이 닫힐 때 호출
    public Tween Close()
    {
        if (!EnsureInitialized())
            return DOTween.Sequence();

        curtainTween?.Kill();

        Vector3 compressed = GetCompressedScale();

        curtainTween = rectTransform.DOScale(compressed, closeDuration)
            .SetEase(closeEase)
            .SetUpdate(ignoreTimeScale)
            .SetLink(gameObject);

        return curtainTween;
    }

    private bool EnsureInitialized()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (rectTransform == null)
        {
            Debug.LogWarning("[KY_CurtainEffect] RectTransform이 없어 커튼 연출을 실행할 수 없습니다.", this);
            return false;
        }

        if (originalScale == Vector3.zero)
            originalScale = rectTransform.localScale;

        return true;
    }

    // 축에 따라 압축된 스케일 값을 계산
    private Vector3 GetCompressedScale()
    {
        if (axis == CurtainAxis.Vertical)
            return new Vector3(originalScale.x, 0f, originalScale.z); // 가로 커튼
        else
            return new Vector3(0f, originalScale.y, originalScale.z); // 세로 커튼
    }
}
