using UnityEngine;

public class WBH_EnemyGradeVisual : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int MetalicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    [Header("크기 적용 대상")]
    [SerializeField] private Transform scaleTarget;
    
    [Header("마테리얼 적용 대상")]
    [SerializeField] private Renderer[] targetRenderers;

    [SerializeField, Min(1f)] private float advancedScaleMul = 1.2f;
    [Tooltip("흰색에 가까울 수록 원본 색상 유지")]
    [SerializeField] private Color advancedTint = new Color(1f, 0.55f, 0.55f, 1f);

    [SerializeField, Range(0f, 1f)] private float tintStrength = 0.25f;
    [SerializeField, Range(0f, 1f)] private float metallicBoost = 0.15f;
    [SerializeField, Range(0f, 1f)] private float smoothnessBoost = 0.1f;

    [SerializeField] private ParticleSystem advancedEffect;

    private MaterialPropertyBlock propertyBlock;

    private Vector3 originalScale;
    private bool initialized;


    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    public void ApplyGrade(EnemyGrade grade)
    {
        CacheOriginalState();

        switch(grade)
        {
            case EnemyGrade.Advanced:
                ApplyAdvanced();
                break;

            default:
                ApplyNormal();
                break;
        }
    }

    // 기존 scale 저장
    private void CacheOriginalState()
    {
        if (initialized)
            return;

        if(scaleTarget == null)
        {
            scaleTarget = transform;
        }

        originalScale = scaleTarget.localScale;

        if(targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = scaleTarget.GetComponentsInChildren<Renderer>(includeInactive: true);
        }

        initialized = true;
    }

    private void ApplyAdvanced()
    {
        scaleTarget.localScale = originalScale * advancedScaleMul;

        Color tintMultiplier = Color.Lerp(Color.white, advancedTint, tintStrength);

        ApplyMaterialProperties(tintMultiplier, metallicBoost, smoothnessBoost);

        if (advancedEffect == null)
            return;

        advancedEffect.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        advancedEffect.Play(withChildren: true);
    }

    private void ApplyNormal()
    {
        scaleTarget.localScale = originalScale;

        ApplyMaterialProperties(Color.white, metalicBoost: 0f, smoothnessBoost: 0f);

        if (advancedEffect == null)

        advancedEffect.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
    }

    // 색상, 메탈릭, 부드러움 변환 및 적용.
    private void ApplyMaterialProperties(Color tintMultiplier, float metalicBoost, float smoothnessBoost)
    {
        if (targetRenderers == null)
            return;

        foreach(Renderer targetRenderer in targetRenderers)
        {
            if(targetRenderer == null)
                continue;

            Material[] materials = targetRenderer.sharedMaterials;

            for (int materialIndex =0; materialIndex < materials.Length; materialIndex ++)
            {
                Material sourceMaterial = materials[materialIndex];

                if (sourceMaterial == null)
                    continue;

                targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);

                if(sourceMaterial.HasProperty(BaseColorId))
                {
                    Color originalColor = sourceMaterial.GetColor(BaseColorId);

                    Color resultColor = originalColor * tintMultiplier;
                    resultColor.a = originalColor.a;

                    propertyBlock.SetColor(BaseColorId, resultColor);
                }

                if(sourceMaterial.HasProperty(MetalicId))
                {
                    float originalMetalic = sourceMaterial.GetFloat(MetalicId);

                    propertyBlock.SetFloat(MetalicId, Mathf.Clamp01(originalMetalic + metalicBoost));
                }

                if(sourceMaterial.HasProperty(SmoothnessId))
                {
                    float originalSmoothness = sourceMaterial.GetFloat(SmoothnessId);

                    propertyBlock.SetFloat(SmoothnessId, Mathf.Clamp01(originalSmoothness + smoothnessBoost));
                }

                targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
        }
    }

    // -- Advanced 적용 테스트용
    //public bool test;

    //private void Update()
    //{
    //    if (test)
    //    {
    //        ApplyGrade(EnemyGrade.Advanced);
    //    }
    //}
}
