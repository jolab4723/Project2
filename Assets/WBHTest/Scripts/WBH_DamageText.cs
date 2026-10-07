using UnityEngine;
using TMPro;

public class WBH_DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshPro damageText;
    [Tooltip("SW 수정: 화상 저항·면역 문구용 한글 글꼴입니다. 숫자는 원래 글꼴을 유지합니다.")]
    [SerializeField] private TMP_FontAsset statusFont;
    private TMP_FontAsset numberFont;
    private Material numberMaterial;

    [Header("Animation")]
    [SerializeField] private float duration = 0.8f;
    [SerializeField] private float fadeInTime = 0.15f;
    [SerializeField] private float moveDistance = 1.0f;

    [SerializeField] private float startScale = 0.5f;
    [SerializeField] private float peakScale = 1.2f;
    [SerializeField] private float endScale = 1.0f;

    private WBH_FloatTextPoolManager poolManager;
    private Camera mainCamera;
    private float timer;
    private Vector3 startPos; // 데미지 폰트 시작 위치
    private Color textColor;  // 치명타 색상 변경 (차후 속성도 적용?)

    /// <summary>SW 수정: 풀 재사용 시 상태 문구에서 숫자 표시로 돌아갈 글꼴을 보관합니다.</summary>
    private void Awake()
    {
        numberFont = damageText.font;
        numberMaterial = damageText.fontSharedMaterial;
    }

    private void Update()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null)
            transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);

        timer += Time.deltaTime;
        float t = timer / duration;
        Animate(t);

        if(timer >= duration)
        {
            poolManager.ReturnDamageText(this);
        }
    }

    public void Initialize(WBH_FloatTextPoolManager poolManager)
    {
        this.poolManager = poolManager;
        mainCamera = Camera.main;
    }

    /// <summary>SW 수정: 숫자와 상태 문구가 같은 표시·반납 흐름을 사용합니다.</summary>
    public void Show(Vector3 position, WBH_DamageResult result)
    {
        damageText.font = numberFont;
        damageText.fontSharedMaterial = numberMaterial;
        ShowText(position, result.FinalDamage.ToString("N0"),
            result.IsCritical ? Color.yellow : Color.white);
    }

    /// <summary>SW 수정: 확정된 화상 반응을 기존 텍스트 풀로 보여줍니다.</summary>
    public void ShowBurnResponse(Vector3 position, bool immune)
    {
        if (statusFont != null)
        {
            damageText.font = statusFont;
            damageText.fontSharedMaterial = statusFont.material;
        }
        ShowText(position, immune ? "화상 면역" : "화상 저항",
            immune ? new Color(0.8f, 0.85f, 0.9f) : new Color(1f, 0.65f, 0.25f));
    }

    /// <summary>SW 수정: 풀에서 다시 꺼낼 때 위치·문구·색·애니메이션을 초기화합니다.</summary>
    private void ShowText(Vector3 position, string message, Color color)
    {
        // 데미지 텍스트 시작위치 랜덤 오프셋 적용.
        Vector3 randomOffset = new Vector3 (Random.Range(-0.2f, 0.2f), Random.Range(0f, 0.15f), Random.Range(-0.2f, 0.2f));

        position += randomOffset;
        startPos = position;
        transform.position = position;
        timer = 0f;

        // SW 수정: 호출부에서 정한 숫자 또는 상태 문구를 표시합니다.
        damageText.text = message;
        textColor = color;

        textColor.a = 0f;
        damageText.color = textColor;
        transform.localScale = Vector3.one * startScale;

        gameObject.SetActive(true);
    }

    // moveDistance 만큼 t초 동안 이동. fadeInTime 전에는 페이드 인. 이후에는 페이드 아웃.
    private void Animate(float t)
    {
        transform.position = startPos + Vector3.up * moveDistance * t;

        if(timer <= fadeInTime)
        {
            float fade = timer / fadeInTime;

            textColor.a = fade;
            float scale = Mathf.Lerp(startScale, peakScale, fade);
            transform.localScale = Vector3.one * scale;
        }
        else
        {
            float fade = (timer - fadeInTime) / (duration - fadeInTime);
            textColor.a = 1f - fade;
            float scale = Mathf.Lerp(peakScale, endScale, fade);
            transform.localScale = Vector3.one * scale;
        }
        damageText.color = textColor;
    }
}
