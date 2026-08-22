using TMPro;
using UnityEngine;

public class WBH_CreditText : MonoBehaviour
{
    [SerializeField] private TextMeshPro creditText;

    [Header("Animation")]
    [SerializeField] private float duration = 0.8f;
    [SerializeField] private float fadeInTime = 0.15f;
    [SerializeField] private float moveDistance = 1.0f;

    [SerializeField] private float startScale = 0.5f;
    [SerializeField] private float peakScale = 1.2f;
    [SerializeField] private float endScale = 1.0f;

    [SerializeField] Color textColor = new Color(1f, 0.75f, 0.15f, 1f);

    private WBH_FloatTextPoolManager poolManager;
    private Camera mainCamera;

    private Vector3 startPos;
    private float timer;

    public void Initialize(WBH_FloatTextPoolManager pool)
    {
        poolManager = pool;
    }

    public void Show(Vector3 position, int amount)
    {
        if(amount <= 0)
        {
            poolManager?.ReturnCreditText(this);
            return;
        }

        mainCamera = Camera.main;
        timer = 0f;

        Vector3 randomOffset = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0f, 0.15f), Random.Range(-0.15f, 0.15f));

        startPos = position + randomOffset;
        transform.position = startPos;
        transform.localScale = Vector3.one * startScale;

        creditText.text = $"+{amount: N0} Credit";

        Color initialColor = textColor;
        initialColor.a = 0f;
        creditText.color = initialColor;

        gameObject.SetActive(true);
    }

    private void Update()
    {
        FaceCamera();

        timer += Time.deltaTime;

        float normalizedTime = Mathf.Clamp01(timer / duration);

        transform.position = startPos + Vector3.up * moveDistance * normalizedTime;

        UpdateAppearance();

        if(timer >= duration)
        {
            poolManager.ReturnCreditText(this);
        }
    }

    private void FaceCamera()
    {
        if (mainCamera == null)
            return;

        transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);
    }

    private void UpdateAppearance()
    {
        float safeFadeInTime = Mathf.Min(fadeInTime, duration);

        Color currentColor = textColor;

        if(timer <= safeFadeInTime)
        {
            float fade = Mathf.Clamp01(timer / Mathf.Max(0.01f, safeFadeInTime));

            currentColor.a = fade;

            transform.localScale = Vector3.one * Mathf.Lerp(startScale, peakScale, fade);
        }
        else
        {
            float fadeOutDuration = Mathf.Max(0.01f, duration - safeFadeInTime);

            float fade = Mathf.Clamp01((timer - safeFadeInTime) / fadeOutDuration);

            currentColor.a = 1f - fade;

            transform.localScale = Vector3.one * Mathf.Lerp(peakScale, endScale, fade);
        }

        creditText.color = currentColor;
    }
}
