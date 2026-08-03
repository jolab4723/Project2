using UnityEngine;
using System.Collections;

public class YJ_PortalActive : MonoBehaviour
{
    [SerializeField] private Light light;
    private float maxIntensity = 100f;
    private float duration = 3f;

    void Awake()
    {
        light = GetComponentInChildren<Light>();
    }

    void Start()
    {
        gameObject.SetActive(false);

        if (light != null)
            light.intensity = 0f;
    }

    public void Active(bool active)
    {
        gameObject.SetActive(active);

        if (active && light != null)
        {
            StartCoroutine(LightOn());
        }
    }

    private IEnumerator LightOn()
    {
        light.intensity = 0f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float ratio = Mathf.Clamp01(elapsedTime / duration);
            light.intensity = Mathf.Lerp(0f, maxIntensity, ratio);

            yield return null;
        }

        light.intensity = maxIntensity;
    }
}
