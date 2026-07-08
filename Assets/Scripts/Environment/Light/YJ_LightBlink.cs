using UnityEngine;
using System.Collections;

public class YJ_LightBlink : MonoBehaviour
{
    [SerializeField] private ShaderReference shaderReference = ShaderReference.Lit;
    private int emissionColorId;

    private Light blinkLight;
    private Material panelMaterial;
    private Color originalEmission;

    private int maxIntensity = 1000;
    private int minIntensity = 0;

    private float minDelay = 0.05f;
    private float maxDelay = 0.2f;

    void Start()
    {
        blinkLight = GetComponentInChildren<Light>();
        Transform panel = transform.Find("Panel");

        Renderer panelRenderer = panel != null ? panel.GetComponent<Renderer>() : null;

        if (blinkLight == null || panelRenderer == null)
            return;

        emissionColorId = YJ_LightData.GetEmissionColorId(shaderReference);

        panelMaterial = panelRenderer.material;
        originalEmission = panelMaterial.GetColor(emissionColorId);

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
                panelMaterial.SetColor(emissionColorId, originalEmission);
            }
            else
            {
                blinkLight.intensity = minIntensity;
                panelMaterial.DisableKeyword("_EMISSION");
                panelMaterial.SetColor(emissionColorId, Color.black);
            }

            float randomTime = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(randomTime);
        }
    }
}

