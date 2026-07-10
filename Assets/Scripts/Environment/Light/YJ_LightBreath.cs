using System.Collections;
using UnityEngine;

public class YJ_LightBreath : MonoBehaviour
{
    [SerializeField] private ShaderReference shaderReference = ShaderReference.Lit;
    private int emissionColorId;

    [SerializeField] private float breathSpeed = 1f;

    [Header("Emission")]
    [SerializeField] private float minEmissionIntensity = 0f;
    [SerializeField] private float maxEmissionIntensity = 2f;

    [Header("Light")]
    [SerializeField] private float minLightIntensity = 0f;
    [SerializeField] private float maxLightIntensity = 50f;

    private Material material;
    private Light light;
    private Color originalEmission;

    private void Start()
    {
        Renderer renderer = GetComponent<Renderer>();
        light = GetComponent<Light>();

        if (renderer == null)
            return;

        emissionColorId = YJ_LightData.GetEmissionColorId(shaderReference);

        material = renderer.material;
        originalEmission = material.GetColor(emissionColorId);
        material.EnableKeyword("_EMISSION");
        StartCoroutine(BreathRoutine());
    }

    private IEnumerator BreathRoutine()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.time * breathSpeed) + 1f) * 0.5f;

            if (material != null)
            {
                float emissionIntensity = Mathf.Lerp(minEmissionIntensity, maxEmissionIntensity, t);
                material.SetColor(emissionColorId, originalEmission * emissionIntensity);
            }

            if (light != null)
            {
                float lightIntensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, t);
                light.intensity = lightIntensity;
            }

            yield return null;
        }
    }
}
