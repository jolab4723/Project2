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

    private bool isTrailActive;

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

    public void Trail(Material mat = null, float duration = 0)
    {
        if (isTrailActive)
            return;

        if (mat == null)
            mat = defaultMat;

        if (duration <= 0)
            duration = activeTime;

        StartCoroutine(ActivateTrail(mat, duration));
    }

    private IEnumerator ActivateTrail(Material material, float duration)
    {
        if (targetRenderer == null)
        {
            Debug.LogWarning("smP02_Body의 SkinnedMeshRenderer를 찾지 못했습니다.");
            yield break;
        }

        if (defaultMat == null)
        {
            Debug.LogWarning("잔상에 사용할 Material이 연결되지 않았습니다.");
            yield break;
        }

        isTrailActive = true;
        float timeActive = duration;

        while (timeActive > 0f)
        {
            CreateTrailMesh(material);

            timeActive -= meshRefreshRate;
            yield return new WaitForSeconds(meshRefreshRate);
        }

        isTrailActive = false;
    }

    private void CreateTrailMesh(Material material)
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

        Destroy(trailObject, meshDestroyDelay);
        Destroy(bakedMesh, meshDestroyDelay);
        Destroy(trailMaterial, meshDestroyDelay);
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
            valueToAnimate = Mathf.MoveTowards(valueToAnimate, goal, rate);
            material.SetFloat(shaderVarRef, valueToAnimate);
            yield return new WaitForSeconds(refreshRate);
        }

        material.SetFloat(shaderVarRef, goal);
    }
}
