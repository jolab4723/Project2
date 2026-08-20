using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WBH_EnemyBossDeathView : MonoBehaviour
{
    private sealed class FadeMaterialSlot
    {
        public Renderer renderer;
        public int materialIndex;
        public float initialOpacity;

        public FadeMaterialSlot(Renderer renderer, int materialIndex, float initialOpacity)
        {
            this.renderer = renderer;
            this.materialIndex = materialIndex;
            this.initialOpacity = initialOpacity;
        }
    }

    private static readonly int DissolveClipId = Shader.PropertyToID("_AdvancedDissolveCutoutStandardClip");

    [SerializeField] private Renderer[] dissolveRenderers;

    [Tooltip("Advanced Dissolve 적용이 어려운 렌더러")]
    [SerializeField] private Renderer[] exceptionRenderers;
    
    [Tooltip("Advanced Dissolve 적용이 어려운 렌더러 페이드 아웃 처리를 위한 셰이더 프로퍼티 이름")]
    [SerializeField] private string exceptionProperty = "_opasity";
    
    [SerializeField] private float startDelay = 0.5f;
    [SerializeField] private float effectDuration = 2.5f;
    [SerializeField] private AnimationCurve dissolveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve exceptionFadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private MaterialPropertyBlock propertyBlock;
    private readonly List<FadeMaterialSlot> exceptionMaterialSlots = new List<FadeMaterialSlot>();

    private int exceptionPropertyId;
    private Coroutine deathEffectCoroutine;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        exceptionPropertyId = Shader.PropertyToID(exceptionProperty);

        CacheExceptionMaterialSlots();
    }

    private void OnEnable()
    {
        ResetVisual();
    }

    private void OnDisable()
    {
        if(deathEffectCoroutine != null)
        {
            StopCoroutine(deathEffectCoroutine);
            deathEffectCoroutine = null;
        }
    }

    public void PlayDeathEffect(Action onCompleted)
    {
        if(deathEffectCoroutine != null)
        {
            StopCoroutine(deathEffectCoroutine);
        }

        SetDissolveClip(0f);
        SetExceptionFade(0f);

        deathEffectCoroutine = StartCoroutine(CoPlayDeathEffect(onCompleted));
    }

    public void ResetVisual()
    {
        if(deathEffectCoroutine != null)
        {
            StopCoroutine(deathEffectCoroutine);
            deathEffectCoroutine = null;
        }

        SetDissolveClip(0f);
        SetExceptionFade(0f);
    }

    private IEnumerator CoPlayDeathEffect(Action onCompleted)
    {
        if(startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        if(effectDuration <= 0f)
        {
            SetDissolveClip(1f);
            SetExceptionFade(1f);

            deathEffectCoroutine = null;
            onCompleted?.Invoke();
            yield break;
        }

        float elapsed = 0f;

        while(elapsed < effectDuration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(elapsed / effectDuration);

            float dissolveValue = Mathf.Clamp01(dissolveCurve.Evaluate(normalizedTime));

            float exceptionValue = Mathf.Clamp01(exceptionFadeCurve.Evaluate(normalizedTime));

            SetDissolveClip(dissolveValue);
            SetExceptionFade(exceptionValue);

            yield return null;
        }

        SetDissolveClip(1f);
        SetExceptionFade(1f);

        deathEffectCoroutine = null;
        onCompleted?.Invoke();
    }

    private void SetDissolveClip(float value)
    {
        value = Mathf.Clamp01(value);

        if (dissolveRenderers == null)
            return;

        foreach(Renderer targetRenderer in dissolveRenderers)
        {
            if (targetRenderer == null)
                continue;

            Material[] materials = targetRenderer.sharedMaterials;

            for(int materialIndex = 0; materialIndex < materials.Length; materialIndex ++)
            {
                Material material = materials[materialIndex];

                if(material == null || !material.HasProperty(DissolveClipId))
                    continue;

                targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);

                propertyBlock.SetFloat(DissolveClipId, value);

                targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
            }

        }
    }

    private void CacheExceptionMaterialSlots()
    {
        exceptionMaterialSlots.Clear();

        if (exceptionRenderers == null)
            return;

        foreach(Renderer targetRenderer in exceptionRenderers)
        {
            if (targetRenderer == null)
                continue;

            Material[] materials = targetRenderer.sharedMaterials;

            for(int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];

                if (material == null || !material.HasProperty(exceptionPropertyId))
                    continue;

                float initialProperty = material.GetFloat(exceptionPropertyId);

                exceptionMaterialSlots.Add(new FadeMaterialSlot(targetRenderer, materialIndex, initialProperty));
            }
        }
    }

    private void SetExceptionFade(float fadeProgress)
    {
        fadeProgress = Mathf.Clamp01(fadeProgress);

        foreach(FadeMaterialSlot slot in exceptionMaterialSlots)
        {
            if (slot.renderer == null)
                continue;

            float opacity = Mathf.Lerp(slot.initialOpacity, 0f, fadeProgress);

            slot.renderer.GetPropertyBlock(propertyBlock, slot.materialIndex);

            propertyBlock.SetFloat(exceptionPropertyId, opacity);

            slot.renderer.SetPropertyBlock(propertyBlock, slot.materialIndex);
        }
    }
}
