using System.Collections;
using UnityEngine;

public class YJ_LightBreath : MonoBehaviour
{
    [SerializeField] private ShaderReference shaderReference = ShaderReference.Lit;
    private int emissionColorId;

    [SerializeField] private float breathSpeed = 1f;
    [SerializeField] private float minIntensity = 0f;
    [SerializeField] private float maxIntensity = 2f;

    private Material material;
    private Color originalEmission;

    private void Start()
    {
        Renderer renderer = GetComponent<Renderer>();

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
            float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);

            material.SetColor(emissionColorId, originalEmission * intensity);

            yield return null;
        }
    }
}
