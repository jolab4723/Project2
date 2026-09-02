using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

// 회피 모션을 실행할 때 잔상을 남기기위한 이펙트 스크립트
public class YJ_MeshTrailTut : MonoBehaviour
{
    [SerializeField] private float activeTime = 2f;
    [SerializeField] private float meshRefreshRate = 0.1f;
    [SerializeField] private float meshDestroyDelay = 0.5f;

    [SerializeField] private string shaderVarRef;
    [SerializeField] private float shaderVarRate = 0.1f;
    [SerializeField] private float shaderVarRefreshRate = 0.05f;

    [SerializeField] private SkinnedMeshRenderer targetRenderer;
    [SerializeField] private Material defaultMat;

    private Coroutine trailCoroutine;

    private void Awake()
    {
        if (targetRenderer != null)
            return;

        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);

        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (renderer.name == "smP02_Body")
            {
                targetRenderer = renderer;
                break;
            }
        }
    }

    public void Trail(
        Material mat = null,
        float duration = 0f,
        float refreshRate = 0f,
        float destroyDelay = -1f)
    {
        if (mat == null)
            mat = defaultMat;

        if (duration <= 0)
            duration = activeTime;

        if (refreshRate <= 0f)
            refreshRate = meshRefreshRate;

        if (destroyDelay < 0f)
            destroyDelay = meshDestroyDelay;

        if (trailCoroutine != null)
            StopCoroutine(trailCoroutine);

        trailCoroutine = StartCoroutine(ActivateTrail(mat, duration, refreshRate, destroyDelay));
    }

    private IEnumerator ActivateTrail(
        Material material,
        float duration,
        float refreshRate,
        float destroyDelay)
    {
        if (targetRenderer == null)
        {
            Debug.LogWarning("smP02_Body의 SkinnedMeshRenderer를 찾지 못했습니다.");
            trailCoroutine = null;
            yield break;
        }

        if (material == null)
        {
            Debug.LogWarning("잔상에 사용할 Material이 연결되지 않았습니다.");
            trailCoroutine = null;
            yield break;
        }

        float timeActive = duration;
        float interval = Mathf.Max(0.01f, refreshRate);

        while (timeActive > 0f)
        {
            CreateTrailMesh(material, destroyDelay);

            timeActive -= interval;
            yield return new WaitForSeconds(interval);
        }

        trailCoroutine = null;
    }

    private void OnDisable()
    {
        if (trailCoroutine == null)
            return;

        StopCoroutine(trailCoroutine);
        trailCoroutine = null;
    }

    private void CreateTrailMesh(Material material, float destroyDelay)
    {
        GameObject trailObject = new GameObject($"{targetRenderer.name}_Trail");

        MeshRenderer meshRenderer = trailObject.AddComponent<MeshRenderer>();
        MeshFilter meshFilter = trailObject.AddComponent<MeshFilter>();

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        Mesh bakedMesh = new Mesh();
        targetRenderer.BakeMesh(bakedMesh, true);

        meshFilter.sharedMesh = bakedMesh;

        Material trailMaterial = new Material(material);
        meshRenderer.sharedMaterial = trailMaterial;

        StartCoroutine(AnimateMaterialFloat(trailMaterial, 0f, shaderVarRate, shaderVarRefreshRate));

        trailObject.transform.SetPositionAndRotation(targetRenderer.transform.position, targetRenderer.transform.rotation);
        trailObject.transform.localScale = Vector3.one;

        float cleanupDelay = Mathf.Max(0f, destroyDelay);

        Destroy(trailObject, cleanupDelay);
        Destroy(bakedMesh, cleanupDelay);
        Destroy(trailMaterial, cleanupDelay);
    }

    private IEnumerator AnimateMaterialFloat(Material material, float goal, float rate, float refreshRate)
    {
        if (material == null || string.IsNullOrEmpty(shaderVarRef) || !material.HasFloat(shaderVarRef))
            yield break;

        if (rate <= 0f)
        {
            material.SetFloat(shaderVarRef, goal);
            yield break;
        }

        float valueToAnimate = material.GetFloat(shaderVarRef);

        while ( ! Mathf.Approximately(valueToAnimate, goal))
        {
            if (material == null)
                yield break;

            valueToAnimate = Mathf.MoveTowards(valueToAnimate, goal, rate);
            material.SetFloat(shaderVarRef, valueToAnimate);
            yield return new WaitForSeconds(Mathf.Max(0.01f, refreshRate));
        }

        if (material != null)
            material.SetFloat(shaderVarRef, goal);
    }
}
