using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>범위·디버프 VFX 재생성 후 검증된 재질 보정을 다시 적용한다. 열린 씬은 저장하지 않는다.</summary>
public static class UniqueEffectVfxReviewPolish
{
    private const string Materials = "Assets/SW/Materials/UniqueEffectVFX/";

    [MenuItem("SW/VFX/범위·디버프 VFX 품질 보정 적용")]
    public static void Apply()
    {
        // 기존 재생성 메뉴와 별개로 필요한 재질과 결정의 정점 스트림만 보정한다.
        Shader corona = Shader.Find("SW/UniqueEffectVFX/SunfallCorona");
        Shader ice = Shader.Find("Universal Render Pipeline/Particles/Lit");
        Material ring = AssetDatabase.LoadAssetAtPath<Material>(Materials + "M_UE_Sunfall_Ring_Add.mat");
        Material crystal = AssetDatabase.LoadAssetAtPath<Material>(Materials + "M_UE_Ice_Crystal_Add.mat");
        Texture noise = AssetDatabase.LoadAssetAtPath<Texture>("Assets/SW/Textures/UniqueEffectVFX/T_UEVFX_Noise.png");
        Texture eclipseBand = AssetDatabase.LoadAssetAtPath<Texture>("Assets/SW/Textures/UniqueEffectVFX/T_UEVFX_EclipseRing.png");
        if (corona == null || ice == null || ring == null || crystal == null || noise == null || eclipseBand == null)
            throw new InvalidOperationException("고유효과 품질 보정에 필요한 셰이더·재질·노이즈가 없습니다.");

        Undo.RecordObjects(new UnityEngine.Object[] { ring, crystal }, "Polish unique effect materials");
        ring.shader = corona;
        ring.SetTexture("_BaseMap", eclipseBand);
        ring.SetTexture("_NoiseMap", noise);
        ring.SetColor("_TintColor", Color.white);
        ring.SetFloat("_BandIntensity", 1.1f);
        ring.SetFloat("_CoreIntensity", 3.0f);
        ring.SetFloat("_CoronaIntensity", 1.6f);
        ring.SetFloat("_CoronaSpeed", 0.55f);
        ring.SetFloat("_Distortion", 0.022f);
        ring.renderQueue = (int)RenderQueue.Transparent;

        // 가산 흰색 대신 빛을 받는 결정 표면을 써 회전 중 면의 명암을 보존한다.
        crystal.shader = ice;
        crystal.shaderKeywords = Array.Empty<string>();
        crystal.SetTexture("_BaseMap", null);
        crystal.SetColor("_BaseColor", new Color(0.08f, 0.45f, 0.8f, 0.92f));
        crystal.SetColor("_EmissionColor", new Color(0.02f, 0.12f, 0.2f, 1f));
        crystal.SetFloat("_Metallic", 0.05f);
        crystal.SetFloat("_Smoothness", 0.7f);
        crystal.SetFloat("_Surface", 1f);
        crystal.SetFloat("_Blend", 0f);
        crystal.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        crystal.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        crystal.SetFloat("_ZWrite", 0f);
        crystal.SetFloat("_Cull", (float)CullMode.Off);
        crystal.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        crystal.EnableKeyword("_EMISSION");
        crystal.SetOverrideTag("RenderType", "Transparent");
        crystal.renderQueue = (int)RenderQueue.Transparent;

        EditorUtility.SetDirty(ring);
        EditorUtility.SetDirty(crystal);
        AssetDatabase.SaveAssetIfDirty(ring);
        AssetDatabase.SaveAssetIfDirty(crystal);
        const string coolingPath = "Assets/SW/Resources/UniqueEffectVFX/UEVFX_CoolingStack.prefab";
        GameObject cooling = PrefabUtility.LoadPrefabContents(coolingPath);
        try
        {
            foreach (ParticleSystemRenderer renderer in cooling.GetComponentsInChildren<ParticleSystemRenderer>(true))
                if (renderer.sharedMaterial == crystal)
                    renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                    {
                        ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
                        ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV
                    });
            PrefabUtility.SaveAsPrefabAsset(cooling, coolingPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(cooling); }
        Debug.Log("[UniqueEffectVfxReviewPolish] 일식 코로나·냉각 결정 재질 보정 완료");
    }
}
