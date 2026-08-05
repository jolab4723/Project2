using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

public class EnemyIconCaptureTool : MonoBehaviour
{
    [Header("Capture")]
    public Camera captureCamera;

    [FormerlySerializedAs("enemyPrefabs")]
    public GameObject[] captureTargets;

    [Min(1)] public int width = 512;
    [Min(1)] public int height = 512;
    [Range(1f, 2f)] public float framingPadding = 1.15f;
    public string outputFolder = "Assets/Resources/Images/Item/";

    [ContextMenu("Capture All")]
    public void CaptureAll()
    {
        if (captureCamera == null)
        {
            Debug.LogError("Capture Camera가 지정되지 않았습니다.", this);
            return;
        }

        if (captureTargets == null || captureTargets.Length == 0)
        {
            Debug.LogWarning("촬영할 대상이 없습니다.", this);
            return;
        }

        Directory.CreateDirectory(outputFolder);

        foreach (GameObject target in captureTargets)
        {
            if (target != null)
                TryCaptureTarget(target, target.name, out _);
        }

        Debug.Log("장비 아이콘 촬영 완료", this);
    }

    /// <summary>
    /// 대상 하나를 지정한 파일명으로 촬영하고 생성된 Sprite 에셋 경로를 반환합니다.
    /// 씬 오브젝트는 현재 배치를, 프로젝트 에셋은 원점과 기본 회전을 사용합니다.
    /// </summary>
    public bool TryCaptureTarget(
        GameObject target,
        string fileNameSource,
        out string outputAssetPath)
    {
        outputAssetPath = null;

        if (captureCamera == null)
        {
            Debug.LogError("Capture Camera가 지정되지 않았습니다.", this);
            return false;
        }

        if (target == null)
        {
            Debug.LogError("촬영할 대상이 지정되지 않았습니다.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(fileNameSource))
            fileNameSource = target.name;

        Directory.CreateDirectory(outputFolder);

        if (!EditorUtility.IsPersistent(target))
        {
            return CaptureSceneTarget(target, fileNameSource, out outputAssetPath);
        }

        GameObject temporaryTarget = Instantiate(target);
        temporaryTarget.name = target.name;
        temporaryTarget.hideFlags = HideFlags.HideAndDontSave;
        temporaryTarget.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        temporaryTarget.SetActive(true);

        try
        {
            return CaptureSceneTarget(temporaryTarget, fileNameSource, out outputAssetPath);
        }
        finally
        {
            DestroyImmediate(temporaryTarget);
        }
    }

    /// <summary>
    /// 씬 오브젝트는 현재 배치 상태를 유지하고, 임시 생성된 에셋은 초기 위치와 회전으로 촬영한다.
    /// </summary>
    private bool CaptureSceneTarget(
        GameObject target,
        string fileNameSource,
        out string outputAssetPath)
    {
        outputAssetPath = null;
        Renderer[] targetRenderers = GetEnabledRenderers(target);

        if (targetRenderers.Length == 0)
        {
            Debug.LogWarning($"{target.name}: 활성화된 Renderer가 없습니다.", target);
            return false;
        }

        Bounds bounds = CalculateBounds(targetRenderers);
        List<Renderer> hiddenRenderers = HideOtherRenderers(targetRenderers);

        Transform cameraTransform = captureCamera.transform;
        Vector3 originalPosition = cameraTransform.position;
        Quaternion originalRotation = cameraTransform.rotation;
        RenderTexture originalTargetTexture = captureCamera.targetTexture;
        CameraClearFlags originalClearFlags = captureCamera.clearFlags;
        Color originalBackgroundColor = captureCamera.backgroundColor;
        float originalAspect = captureCamera.aspect;
        float originalNearClip = captureCamera.nearClipPlane;
        float originalFarClip = captureCamera.farClipPlane;
        float originalOrthographicSize = captureCamera.orthographicSize;
        RenderTexture originalActive = RenderTexture.active;

        RenderTexture renderTexture = null;
        Texture2D texture = null;

        try
        {
            captureCamera.aspect = width / (float)height;
            FrameCamera(bounds);
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = Color.clear;

            renderTexture = RenderTexture.GetTemporary(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);

            captureCamera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.clear);
            captureCamera.Render();

            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply(false, false);

            string fileName = MakeSafeFileName(fileNameSource) + ".png";
            string outputPath = Path.Combine(outputFolder, fileName).Replace('\\', '/');
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
            ConfigureAsSingleSprite(outputPath);
            outputAssetPath = outputPath;
            return true;
        }
        finally
        {
            cameraTransform.SetPositionAndRotation(originalPosition, originalRotation);
            captureCamera.targetTexture = originalTargetTexture;
            captureCamera.clearFlags = originalClearFlags;
            captureCamera.backgroundColor = originalBackgroundColor;
            captureCamera.aspect = originalAspect;
            captureCamera.nearClipPlane = originalNearClip;
            captureCamera.farClipPlane = originalFarClip;
            captureCamera.orthographicSize = originalOrthographicSize;
            RenderTexture.active = originalActive;

            RestoreRenderers(hiddenRenderers);

            if (renderTexture != null)
                RenderTexture.ReleaseTemporary(renderTexture);

            if (texture != null)
                DestroyImmediate(texture);
        }
    }

    private void FrameCamera(Bounds bounds)
    {
        Transform cameraTransform = captureCamera.transform;
        Vector3 extents = bounds.extents;

        float halfWidth = ProjectExtent(extents, cameraTransform.right) * framingPadding;
        float halfHeight = ProjectExtent(extents, cameraTransform.up) * framingPadding;
        float halfDepth = ProjectExtent(extents, cameraTransform.forward);

        if (captureCamera.orthographic)
        {
            captureCamera.orthographicSize = Mathf.Max(
                halfHeight,
                halfWidth / captureCamera.aspect);

            float distance = halfDepth + bounds.extents.magnitude + 1f;
            cameraTransform.position = bounds.center - cameraTransform.forward * distance;
            return;
        }

        float verticalHalfFov = captureCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float horizontalHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * captureCamera.aspect);
        float distanceForHeight = halfHeight / Mathf.Tan(verticalHalfFov);
        float distanceForWidth = halfWidth / Mathf.Tan(horizontalHalfFov);
        float distanceToCenter = Mathf.Max(distanceForHeight, distanceForWidth) + halfDepth;

        cameraTransform.position = bounds.center - cameraTransform.forward * distanceToCenter;

        float safetyMargin = Mathf.Max(bounds.extents.magnitude * 0.25f, 0.01f);
        captureCamera.nearClipPlane = Mathf.Max(0.001f, distanceToCenter - halfDepth - safetyMargin);
        captureCamera.farClipPlane = Mathf.Max(
            captureCamera.nearClipPlane + 1f,
            distanceToCenter + halfDepth + safetyMargin);
    }

    private static float ProjectExtent(Vector3 extents, Vector3 axis)
    {
        axis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
        return Vector3.Dot(extents, axis);
    }

    private static Renderer[] GetEnabledRenderers(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(false);
        List<Renderer> enabledRenderers = new List<Renderer>(renderers.Length);

        foreach (Renderer renderer in renderers)
        {
            if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                enabledRenderers.Add(renderer);
        }

        return enabledRenderers.ToArray();
    }

    private static Bounds CalculateBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private static List<Renderer> HideOtherRenderers(Renderer[] targetRenderers)
    {
        HashSet<Renderer> targetSet = new HashSet<Renderer>(targetRenderers);
        Renderer[] sceneRenderers = Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        List<Renderer> hiddenRenderers = new List<Renderer>();

        foreach (Renderer renderer in sceneRenderers)
        {
            if (renderer.enabled && !targetSet.Contains(renderer))
            {
                renderer.enabled = false;
                hiddenRenderers.Add(renderer);
            }
        }

        return hiddenRenderers;
    }

    private static void RestoreRenderers(List<Renderer> renderers)
    {
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = true;
        }
    }

    private static string MakeSafeFileName(string value)
    {
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');

        return value;
    }

    private static void ConfigureAsSingleSprite(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            Debug.LogError($"Sprite 임포터를 찾을 수 없습니다: {assetPath}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }
}
