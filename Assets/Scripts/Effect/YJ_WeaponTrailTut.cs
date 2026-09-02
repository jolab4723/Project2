using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

public class YJ_WeaponTrailTut : MonoBehaviour
{
    private const string WeaponRootName = "Sword Two-Hander Base";

    [SerializeField] private float activeTime = 2f;
    [SerializeField] private float meshRefreshRate = 0.1f;
    [SerializeField] private float meshDestroyDelay = 0.5f;

    [SerializeField] private string shaderVarRef;
    [SerializeField] private float shaderVarRate = 0.1f;
    [SerializeField] private float shaderVarRefreshRate = 0.05f;

    [SerializeField] private Transform weaponRoot;
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private Material defaultMat;

    private Coroutine trailCoroutine;
    private Coroutine timedStopCoroutine;

    public bool IsTrailActive => trailCoroutine != null;

    private void Awake()
    {
        ResolveWeaponRenderer();
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

        if (!TryStartTrail(mat, refreshRate, destroyDelay))
            return;

        CancelTimedStop();
        timedStopCoroutine = StartCoroutine(StopTrailAfter(duration));
    }

    public void StartTrail(Material mat = null)
    {
        CancelTimedStop();

        if (mat == null)
            mat = defaultMat;

        TryStartTrail(mat, meshRefreshRate, meshDestroyDelay);
    }

    public void StopTrail()
    {
        CancelTimedStop();

        if (trailCoroutine == null)
            return;

        StopCoroutine(trailCoroutine);
        trailCoroutine = null;
    }

    private bool TryStartTrail(Material material, float refreshRate, float destroyDelay)
    {
        if (material == null)
        {
            Debug.LogWarning("잔상에 사용할 Material이 연결되지 않았습니다.", this);
            return false;
        }

        ResolveWeaponRenderer();

        if (weaponRoot == null)
        {
            Debug.LogWarning($"{WeaponRootName} 오브젝트를 찾지 못했습니다.", this);
            return false;
        }

        if (trailCoroutine != null)
            StopCoroutine(trailCoroutine);

        trailCoroutine = StartCoroutine(GenerateTrail(material, refreshRate, destroyDelay));
        return true;
    }

    private IEnumerator GenerateTrail(Material material, float refreshRate, float destroyDelay)
    {
        float interval = Mathf.Max(0.01f, refreshRate);
        WaitForSeconds wait = new WaitForSeconds(interval);

        while (true)
        {
            if (ResolveWeaponRenderer())
                CreateTrailMesh(material, destroyDelay);

            yield return wait;
        }
    }

    private IEnumerator StopTrailAfter(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration));

        timedStopCoroutine = null;
        StopTrail();
    }

    private void CancelTimedStop()
    {
        if (timedStopCoroutine == null)
            return;

        StopCoroutine(timedStopCoroutine);
        timedStopCoroutine = null;
    }

    private void OnDisable()
    {
        StopTrail();
    }

    private bool ResolveWeaponRenderer()
    {
        if (weaponRoot == null)
        {
            Transform[] transforms = transform.root.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in transforms)
            {
                if (child.name == WeaponRootName)
                {
                    weaponRoot = child;
                    break;
                }
            }
        }

        if (weaponRoot == null)
            return false;

        if (IsUsableWeaponRenderer(targetRenderer))
            return true;

        targetRenderer = null;
        MeshRenderer[] renderers = weaponRoot.GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer renderer in renderers)
        {
            if (IsUsableWeaponRenderer(renderer))
            {
                targetRenderer = renderer;
                break;
            }
        }

        return targetRenderer != null;
    }

    private bool IsUsableWeaponRenderer(MeshRenderer renderer)
    {
        return renderer != null
            && renderer.transform.IsChildOf(weaponRoot)
            && renderer.enabled
            && renderer.gameObject.activeInHierarchy
            && HasMesh(renderer);
    }

    private static bool HasMesh(MeshRenderer renderer)
    {
        return renderer != null
            && renderer.TryGetComponent(out MeshFilter meshFilter)
            && meshFilter.sharedMesh != null;
    }

    private void CreateTrailMesh(Material material, float destroyDelay)
    {
        if (!targetRenderer.TryGetComponent(out MeshFilter sourceMeshFilter)
            || sourceMeshFilter.sharedMesh == null)
            return;

        GameObject trailObject = new GameObject($"{targetRenderer.name}_Trail");
        trailObject.layer = targetRenderer.gameObject.layer;

        MeshRenderer meshRenderer = trailObject.AddComponent<MeshRenderer>();
        MeshFilter meshFilter = trailObject.AddComponent<MeshFilter>();

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        Mesh sourceMesh = sourceMeshFilter.sharedMesh;
        meshFilter.sharedMesh = sourceMesh;

        Material trailMaterial = new Material(material);
        Material[] trailMaterials = new Material[Mathf.Max(1, sourceMesh.subMeshCount)];

        for (int i = 0; i < trailMaterials.Length; i++)
            trailMaterials[i] = trailMaterial;

        meshRenderer.sharedMaterials = trailMaterials;

        StartCoroutine(AnimateMaterialFloat(trailMaterial, 0f, shaderVarRate, shaderVarRefreshRate));

        trailObject.transform.SetPositionAndRotation(targetRenderer.transform.position, targetRenderer.transform.rotation);
        trailObject.transform.localScale = targetRenderer.transform.lossyScale;

        float cleanupDelay = Mathf.Max(
            Mathf.Max(0f, destroyDelay),
            GetMaterialAnimationDuration(trailMaterial, 0f, shaderVarRate, shaderVarRefreshRate));

        Destroy(trailObject, cleanupDelay);
        Destroy(trailMaterial, cleanupDelay);
    }

    private float GetMaterialAnimationDuration(Material material, float goal, float rate, float refreshRate)
    {
        if (material == null
            || string.IsNullOrEmpty(shaderVarRef)
            || !material.HasFloat(shaderVarRef)
            || rate <= 0f)
            return 0f;

        float interval = Mathf.Max(0.01f, refreshRate);
        float distance = Mathf.Abs(material.GetFloat(shaderVarRef) - goal);
        int stepCount = Mathf.CeilToInt(distance / rate);

        // 마지막 SetFloat와 같은 프레임에 Material이 파괴되지 않도록 한 주기의 여유를 둔다.
        return (stepCount + 1) * interval;
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
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.01f, refreshRate));

        while (!Mathf.Approximately(valueToAnimate, goal))
        {
            if (material == null)
                yield break;

            valueToAnimate = Mathf.MoveTowards(valueToAnimate, goal, rate);
            material.SetFloat(shaderVarRef, valueToAnimate);
            yield return wait;
        }

        if (material != null)
            material.SetFloat(shaderVarRef, goal);
    }
}
