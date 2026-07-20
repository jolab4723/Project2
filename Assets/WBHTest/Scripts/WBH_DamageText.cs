using UnityEngine;
using TMPro;

public class WBH_DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshPro damageText;

    [Header("Animation")]
    [SerializeField] private float duration = 0.8f;
    [SerializeField] private float fadeInTime = 0.15f;
    [SerializeField] private float moveDistance = 1.0f;

    [SerializeField] private float startScale = 0.5f;
    [SerializeField] private float peakScale = 1.2f;
    [SerializeField] private float endScale = 1.0f;

    private WBH_DamageTextPoolManager poolManager;
    private Camera mainCamera;
    private float timer;
    private Vector3 startPos; // 데미지 폰트 시작 위치
    private Color textColor;  // 치명타 색상 변경 (차후 속성도 적용?)

    private void Update()
    {
        transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);

        timer += Time.deltaTime;
        float t = timer / duration;
        Animate(t);

        if(timer >= duration)
        {
            poolManager.ReturnPool(this);
        }
    }

    public void Initialize(WBH_DamageTextPoolManager poolManager)
    {
        this.poolManager = poolManager;
        mainCamera = Camera.main;
    }

    // 텍스트 표출
    public void Show(Vector3 position, WBH_DamageResult result)
    {
        // 데미지 텍스트 시작위치 랜덤 오프셋 적용.
        Vector3 randomOffset = new Vector3 (Random.Range(-0.2f, 0.2f), Random.Range(0f, 0.15f), Random.Range(-0.2f, 0.2f));

        position += randomOffset;
        startPos = position;
        transform.position = position;
        timer = 0f;

        // 반올림 후 천단위 "," 적용 
        damageText.text = result.FinalDamage.ToString("N0");
        textColor = result.IsCritical ? Color.yellow : Color.white;

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
