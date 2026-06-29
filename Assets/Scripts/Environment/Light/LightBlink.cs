using UnityEngine;
using System.Collections;

public class LightBlink : MonoBehaviour
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

        if (panel != null)
        {
            panelMaterial = panel.GetComponent<MeshRenderer>().material;
            originalEmission = panelMaterial.GetColor("_EmissionColor");
        }

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
                if (blinkLight != null) blinkLight.intensity = maxIntensity;

                if (panelMaterial != null)
                {
                    panelMaterial.EnableKeyword("_EMISSION");
                    panelMaterial.SetColor("_EmissionColor", originalEmission);
                }
            }
            else
            {
                if (blinkLight != null) blinkLight.intensity = minIntensity;

                if (panelMaterial != null)
                {
                    panelMaterial.DisableKeyword("_EMISSION");
                    panelMaterial.SetColor("_EmissionColor", Color.black);
                }
            }

            float randomTime = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(randomTime);
        }
    }
}

