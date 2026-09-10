using UnityEngine;
using TMPro;
using DG.Tweening;

public class KY_StatRow : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI totalValueText;
    public TextMeshProUGUI detailValueText;

    private const string ValueFormat = "0.##";

    private Color baseColor;
    private Color equipColor;
    private Color buffColor;
    private Tween detailTween;
    private Tween totalPulseTween;
    private Vector3 detailBaseScale;
    private Vector3 totalBaseScale;

    void Awake()
    {
        ColorUtility.TryParseHtmlString("#FFFFFF", out baseColor);
        ColorUtility.TryParseHtmlString("#FFD700", out equipColor);
        ColorUtility.TryParseHtmlString("#00FF99", out buffColor);

        if (detailValueText != null)
            detailBaseScale = detailValueText.transform.localScale;

        if (totalValueText != null)
            totalBaseScale = totalValueText.transform.localScale;
    }

    /// <summary>이 행이 어떤 스탯인지 나타내는 이름 텍스트를 설정한다. 값이 바뀌지 않는 한 한 번만 호출하면 된다.</summary>
    public void SetLabel(string label)
    {
        if (nameText != null)
            nameText.text = label;
    }

    public void UpdateMode(KY_StatTypeData data, bool isDetailed)
    {
        Debug.Log("[Row] UpdateMode 호출됨, isDetailed = " + isDetailed);

        totalValueText.text = data.Total.ToString(ValueFormat);

        detailValueText.gameObject.SetActive(isDetailed);

        if (isDetailed)
        {
            detailValueText.text =
                $"(<color=#{ColorUtility.ToHtmlStringRGB(baseColor)}>{data.baseValue.ToString(ValueFormat)}</color>" +
                $" + <color=#{ColorUtility.ToHtmlStringRGB(equipColor)}>{data.equipValue.ToString(ValueFormat)}</color>" +
                $" + <color=#{ColorUtility.ToHtmlStringRGB(buffColor)}>{data.buffValue.ToString(ValueFormat)}</color>)";
        }
    }

    // 레이아웃 그룹의 위치는 건드리지 않고, 세부 값의 표시 속성만 연출한다.
    public void PlayDetailTransition(bool showDetail, float delay = 0f)
    {
        if (detailValueText == null || totalValueText == null)
            return;

        detailTween?.Kill();
        totalPulseTween?.Kill();

        detailValueText.gameObject.SetActive(true);
        detailValueText.transform.localScale = showDetail ? detailBaseScale * 0.96f : detailBaseScale;
        detailValueText.alpha = showDetail ? 0f : 1f;

        if (showDetail)
        {
            detailTween = DOTween.Sequence()
                .AppendInterval(delay)
                .Append(detailValueText.DOFade(1f, 0.18f).SetEase(Ease.OutCubic))
                .Join(detailValueText.transform.DOScale(detailBaseScale, 0.2f).SetEase(Ease.OutBack))
                .SetLink(gameObject);
        }
        else
        {
            detailTween = DOTween.Sequence()
                .AppendInterval(delay)
                .Append(detailValueText.DOFade(0f, 0.14f).SetEase(Ease.InCubic))
                .AppendCallback(() => detailValueText.gameObject.SetActive(false))
                .SetLink(gameObject);
        }

        totalValueText.transform.localScale = totalBaseScale;
        totalPulseTween = totalValueText.transform
            .DOScale(totalBaseScale * 1.03f, 0.1f)
            .SetDelay(delay)
            .SetLoops(2, LoopType.Yoyo)
            .SetEase(Ease.OutCubic)
            .SetLink(gameObject);
    }

    private void OnDisable()
    {
        detailTween?.Kill();
        totalPulseTween?.Kill();
    }
}
