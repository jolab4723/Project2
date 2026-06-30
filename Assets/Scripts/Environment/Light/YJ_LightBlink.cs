using System.Collections;
using UnityEngine;

public class YJ_LightBlink : MonoBehaviour
{
    private Light blinkLight;
    private Transform panel;
    private Material panelMaterial;
    private Color originalEmission;

    private int maxIntensity = 1000;
    private int minIntensity = 0;

    private float minDelay = 0.05f;
    private float maxDelay = 0.2f;

    void Start()
    {
        blinkLight = GetComponentInChildren<Light>();
        panel = gameObject.transform.Find("Panel");

        Renderer panelRenderer = panel != null ? panel.GetComponent<Renderer>() : null;

        if (blinkLight == null || panelRenderer == null)
            return;

        panelMaterial = panelRenderer.material;
        originalEmission = panelMaterial.GetColor("_EmissionColor");

        blinkLight.intensity = maxIntensity;
        StartCoroutine(BlinkRoutine());
    }

    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            bool lightOn = Random.value > 0.2f;

            if (lightOn)
            {
                blinkLight.intensity = maxIntensity;
                panelMaterial.EnableKeyword("_EMISSION");
                panelMaterial.SetColor("_EmissionColor", originalEmission);
            }
            else
            {
                blinkLight.intensity = minIntensity;
                panelMaterial.DisableKeyword("_EMISSION");
                panelMaterial.SetColor("_EmissionColor", Color.black);
            }

            float randomTime = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(randomTime);
        }
    }
}

