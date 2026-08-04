using UnityEngine;
using System.Collections;

public class YJ_PortalActive : MonoBehaviour
{
    [SerializeField] private Light targetLight;
    [SerializeField] private float maxIntensity = 100f;
    [SerializeField] private float duration = 3f;
    [SerializeField] private bool isCamp = false;

    void Awake()
    {
        targetLight = GetComponentInChildren<Light>();
    }

    void Start()
    {
        gameObject.SetActive(isCamp);

        if (targetLight != null)
            targetLight.intensity = isCamp ? maxIntensity : 0f;
    }

    public void Active(bool active)
    {
        gameObject.SetActive(active);

        if (active && targetLight != null)
            StartCoroutine(LightOn());
    }

    private IEnumerator LightOn()
    {
        targetLight.intensity = 0f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsedTime / duration);
            targetLight.intensity = Mathf.Lerp(0f, maxIntensity, ratio);

            yield return null;
        }

        targetLight.intensity = maxIntensity;
    }
}
