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

    public void Trail(Material mat = null, float duration = 0)
    {
        if (mat == null)
            mat = defaultMat;

        if (duration <= 0)
            duration = activeTime;

        if (!TryStartTrail(mat))
            return;

        CancelTimedStop();
        timedStopCoroutine = StartCoroutine(StopTrailAfter(duration));
    }

    public void StartTrail(Material mat = null)
    {
        CancelTimedStop();

        if (mat == null)
            mat = defaultMat;

        TryStartTrail(mat);
    }

    public void StopTrail()
    {
        CancelTimedStop();

        if (trailCoroutine == null)
            return;

        StopCoroutine(trailCoroutine);
        trailCoroutine = null;
    }

    private bool TryStartTrail(Material material)
    {
        if (trailCoroutine != null)
            return true;

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

        trailCoroutine = StartCoroutine(GenerateTrail(material));
        return true;
    }

    private IEnumerator GenerateTrail(Material material)
    {
        float refreshRate = Mathf.Max(0.01f, meshRefreshRate);
        WaitForSeconds wait = new WaitForSeconds(refreshRate);

        while (true)
        {
            if (ResolveWeaponRenderer())
                CreateTrailMesh(material);

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

    private void CreateTrailMesh(Material material)
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

        Destroy(trailObject, meshDestroyDelay);
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
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.01f, refreshRate));

        while (!Mathf.Approximately(valueToAnimate, goal))
        {
            valueToAnimate = Mathf.MoveTowards(valueToAnimate, goal, rate);
            material.SetFloat(shaderVarRef, valueToAnimate);
            yield return wait;
        }

        material.SetFloat(shaderVarRef, goal);
    }
}
