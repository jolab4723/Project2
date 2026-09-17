using UnityEngine;
using TMPro;
using DG.Tweening;

public class KY_StatRow : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI totalValueText;
    public TextMeshProUGUI detailValueText;

    private const string ValueFormat = "0.##";
    private const string IntegerFormat = "0";

    [Tooltip("최대 체력·마나처럼 소수점이 의미 없는 행은 켠다. 총합과 세부값 모두 정수로 표시한다.")]
    [SerializeField] private bool displayAsInteger;

    private Color baseColor;
    private Color equipColor;
    private Color passiveColor;
    private Color buffColor;
    private Tween detailTween;
    private Tween totalPulseTween;
    private Vector3 detailBaseScale;
    private Vector3 totalBaseScale;

    void Awake()
    {
        ColorUtility.TryParseHtmlString("#FFFFFF", out baseColor);    // 캐릭터(레벨)
        ColorUtility.TryParseHtmlString("#FFD700", out equipColor);   // 장비
        ColorUtility.TryParseHtmlString("#4DA6FF", out passiveColor); // 패시브
        ColorUtility.TryParseHtmlString("#00FF99", out buffColor);    // 버프

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

        float total = data.Total;
        float baseValue = data.baseValue;
        float equipValue = data.equipValue;
        float passiveValue = data.passiveValue;
        float buffValue = data.buffValue;

        if (displayAsInteger)
        {
            // 각 항목을 따로 반올림하면 합이 총합과 어긋난다(1170 + 321.66 + 175.5 -> 1170+322+176 = 1668, 총합 1667.16).
            // 총합은 PlayerStat.Recalculate와 같게 올림으로 확정하고, 네 항목을 반올림한 뒤
            // 남은 잔차는 **가장 큰 항목**에 흡수시킨다.
            //
            // !! 잔차를 버프 칸에 몰면 버프가 하나도 없는데 "+1"로 표시돼 없는 버프가 있는 것처럼 보인다.
            //    0인 칸은 0으로 남아야 해서, 값이 가장 큰 칸(보통 캐릭터)에 넘긴다.
            total = Mathf.Ceil(total);
            baseValue = Mathf.Round(baseValue);
            equipValue = Mathf.Round(equipValue);
            passiveValue = Mathf.Round(passiveValue);
            buffValue = Mathf.Round(buffValue);

            float residual = total - (baseValue + equipValue + passiveValue + buffValue);
            if (!Mathf.Approximately(residual, 0f))
            {
                float max = Mathf.Max(Mathf.Abs(baseValue), Mathf.Abs(equipValue), Mathf.Abs(passiveValue), Mathf.Abs(buffValue));
                if (Mathf.Approximately(max, Mathf.Abs(baseValue))) baseValue += residual;
                else if (Mathf.Approximately(max, Mathf.Abs(equipValue))) equipValue += residual;
                else if (Mathf.Approximately(max, Mathf.Abs(passiveValue))) passiveValue += residual;
                else buffValue += residual;
            }
        }

        string format = displayAsInteger ? IntegerFormat : ValueFormat;
        totalValueText.text = total.ToString(format);

        detailValueText.gameObject.SetActive(isDetailed);

        if (isDetailed)
        {
            // 캐릭터(흰색) + 장비(노랑) + 패시브(파랑) + 버프(초록)
            detailValueText.text =
                $"(<color=#{ColorUtility.ToHtmlStringRGB(baseColor)}>{baseValue.ToString(format)}</color>" +
                $" + <color=#{ColorUtility.ToHtmlStringRGB(equipColor)}>{equipValue.ToString(format)}</color>" +
                $" + <color=#{ColorUtility.ToHtmlStringRGB(passiveColor)}>{passiveValue.ToString(format)}</color>" +
                $" + <color=#{ColorUtility.ToHtmlStringRGB(buffColor)}>{buffValue.ToString(format)}</color>)";
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
