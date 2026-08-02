using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Artifice;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SW.EditorTools
{
    public enum DestructionVisualPreset
    {
        GeneralEnemySimultaneous,
        BossSequential
    }

    /// <summary>
    /// Creates a clean, static destruction-visual prefab without changing the source hierarchy.
    /// All skinned renderers are baked before anything is destroyed or reset, so bone order,
    /// imported UCX colliders, and FBX scale do not affect the result.
    /// </summary>
    public static class SafeStaticDestructionVisualConverter
    {
        private const string EnemyPrefabFolder =
            "Assets/Resources_GoogleDrive/Props/Enemy";

        private const string ConvertGeneralMenu =
            "SW/Artificer/일반 적 파괴 연출 프리팹 생성 (동시)";

        private const string ConvertBossMenu =
            "SW/Artificer/보스 파괴 연출 프리팹 생성 (순차)";

        private const string ValidateMenu =
        "SW/Artificer/Enemy 전체 변환 호환성 검사";

        private const MeshUpdateFlags MeshFlags =
            MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontValidateIndices;

        [MenuItem(ConvertGeneralMenu)]
        private static void ConvertGeneralSelection()
        {
            ConvertSelection(DestructionVisualPreset.GeneralEnemySimultaneous);
        }

        [MenuItem(ConvertBossMenu)]
        private static void ConvertBossSelection()
        {
            ConvertSelection(DestructionVisualPreset.BossSequential);
        }

        private static void ConvertSelection(DestructionVisualPreset preset)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "안전 파괴 연출 생성",
                    "Project 또는 Hierarchy에서 변환할 로봇 루트를 선택하세요.",
                    "확인");
                return;
            }

            string defaultFolder = GetDefaultOutputFolder(selected);
            string suffix = preset == DestructionVisualPreset.BossSequential
                ? "_BossDestructionVisual.prefab"
                : "_DestructionVisual.prefab";
            string defaultName = selected.name + suffix;
            string outputPath = EditorUtility.SaveFilePanelInProject(
                "파괴 연출용 정적 프리팹 저장",
                defaultName,
                "prefab",
                "원본이 아닌 팀원 프리팹 폴더에 저장하세요.",
                defaultFolder);

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outputPath) != null)
            {
                outputPath = AssetDatabase.GenerateUniqueAssetPath(outputPath);
            }

            try
            {
                ConversionResult result = Convert(selected, outputPath, preset);
                Selection.activeObject = result.OutputPrefab;
                EditorGUIUtility.PingObject(result.OutputPrefab);

                EditorUtility.DisplayDialog(
                    "파괴 연출 프리팹 생성 완료",
                    result.GetUserSummary(),
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "파괴 연출 프리팹 생성 실패",
                    exception.Message +
                    "\n\n원본과 기존 프리팹은 변경되지 않았습니다.",
                    "확인");
            }
        }

        [MenuItem(ConvertGeneralMenu, true)]
        private static bool ValidateConvertGeneralSelection()
        {
            return ValidateConvertSelection();
        }

        [MenuItem(ConvertBossMenu, true)]
        private static bool ValidateConvertBossSelection()
        {
            return ValidateConvertSelection();
        }

        private static bool ValidateConvertSelection()
        {
            return Selection.activeGameObject != null &&
                   !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem(ValidateMenu)]
        private static void ValidateEnemyPrefabsMenu()
        {
            string report = ValidateEnemyPrefabsForAutomation();
            EditorUtility.DisplayDialog(
                "Enemy 로봇 변환 호환성 검사",
                report,
                "확인");
        }

        /// <summary>
        /// MCP, tests, and CI can call this without creating any assets.
        /// </summary>
        public static string ValidateEnemyPrefabsForAutomation()
        {
            BatchValidationReport report = ValidateFolder(EnemyPrefabFolder);
            string text = report.ToString();

            if (report.Failures.Count == 0)
            {
                Debug.Log("[SW Safe Static Converter] " + text);
            }
            else
            {
                Debug.LogError("[SW Safe Static Converter] " + text);
            }

            return text;
        }

        public static ConversionResult Convert(
            GameObject source,
            string outputPrefabPath)
        {
            return Convert(
                source,
                outputPrefabPath,
                DestructionVisualPreset.GeneralEnemySimultaneous);
        }

        public static ConversionResult Convert(
            GameObject source,
            string outputPrefabPath,
            DestructionVisualPreset preset)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (string.IsNullOrWhiteSpace(outputPrefabPath) ||
                !outputPrefabPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "출력 경로는 Assets 아래의 prefab 경로여야 합니다.",
                    nameof(outputPrefabPath));
            }

            if (!outputPrefabPath.EndsWith(
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "출력 파일 확장자는 .prefab이어야 합니다.",
                    nameof(outputPrefabPath));
            }

            string requestedPath = outputPrefabPath.Replace('\\', '/');
            string requestedDirectory = GetAssetDirectory(requestedPath);
            EnsureAssetFolder(requestedDirectory);

            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
            string prefabDirectory = GetAssetDirectory(prefabPath);
            string prefabName = GetAssetFileNameWithoutExtension(prefabPath);
            string meshFolder = AssetDatabase.GenerateUniqueAssetPath(
                prefabDirectory + "/" + prefabName + "_Meshes");
            string artificerFolder = AssetDatabase.GenerateUniqueAssetPath(
                prefabDirectory + "/" + prefabName + "_Artificer");

            var createdAssets = new List<string>();
            bool meshFolderCreated = false;
            bool artificerFolderCreated = false;
            Scene outputScene = default;
            GameObject outputRoot = null;
            List<BakedPart> parts = null;

            try
            {
                using (SourceContext context = SourceContext.Create(source))
                {
                    parts = BakeVisibleParts(context.Root, out SourceStatistics stats);
                    ValidateInMemory(context.Root, parts, stats);

                    outputScene = EditorSceneManager.NewPreviewScene();
                    outputRoot = BuildOutputHierarchy(
                        context.Root.name,
                        parts,
                        outputScene,
                        preset);

                    EnsureAssetFolder(prefabDirectory);
                    EnsureAssetFolder(meshFolder);
                    meshFolderCreated = true;

                    for (int i = 0; i < parts.Count; i++)
                    {
                        string meshPath = AssetDatabase.GenerateUniqueAssetPath(
                            meshFolder + "/" +
                            SanitizeFileName(parts[i].Name) + ".asset");
                        AssetDatabase.CreateAsset(parts[i].Mesh, meshPath);
                        createdAssets.Add(meshPath);
                        parts[i].IsSavedAsset = true;
                    }

                    EnsureAssetFolder(artificerFolder);
                    artificerFolderCreated = true;
                    string buildDataPath =
                        artificerFolder + "/" + prefabName + "_BuildData.asset";
                    var buildData = ScriptableObject.CreateInstance<BuildData>();
                    buildData.name = prefabName + "_BuildData";
                    AssetDatabase.CreateAsset(buildData, buildDataPath);
                    createdAssets.Add(buildDataPath);

                    Artificer artificer = outputRoot.GetComponent<Artificer>();
                    artificer.buildData = buildData;
                    artificer.BuildData();
                    EditorUtility.SetDirty(buildData);

                    int meshElementCount = buildData.meshes != null
                        ? buildData.meshes.Count
                        : 0;
                    ValidatePresetBuildData(preset, meshElementCount);

                    GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(
                        outputRoot,
                        prefabPath);
                    if (savedPrefab == null)
                    {
                        throw new InvalidOperationException(
                            "Unity가 출력 프리팹을 저장하지 못했습니다.");
                    }

                    createdAssets.Add(prefabPath);
                    AssetDatabase.SaveAssets();

                    ValidateSavedPrefab(
                        savedPrefab,
                        parts,
                        stats,
                        preset,
                        meshElementCount);

                    return new ConversionResult(
                        savedPrefab,
                        prefabPath,
                        buildDataPath,
                        preset,
                        parts.Count,
                        meshElementCount,
                        stats.SkinnedRendererCount,
                        stats.StaticRendererCount,
                        stats.VertexCount,
                        stats.TriangleCount,
                        stats.SkippedRendererCount);
                }
            }
            catch
            {
                for (int i = createdAssets.Count - 1; i >= 0; i--)
                {
                    AssetDatabase.DeleteAsset(createdAssets[i]);
                }

                if (meshFolderCreated)
                {
                    AssetDatabase.DeleteAsset(meshFolder);
                }

                if (artificerFolderCreated)
                {
                    AssetDatabase.DeleteAsset(artificerFolder);
                }

                AssetDatabase.Refresh();
                throw;
            }
            finally
            {
                if (outputRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(outputRoot);
                }

                if (outputScene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(outputScene);
                }

                DestroyUnsavedMeshes(parts);
            }
        }

        private static BatchValidationReport ValidateFolder(string folder)
        {
            var report = new BatchValidationReport(folder);
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { folder });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject root = null;
                List<BakedPart> parts = null;

                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    FreezeAnimators(root);
                    if (!HasConvertibleRenderer(root))
                    {
                        report.SkippedPrefabCount++;
                        continue;
                    }

                    parts = BakeVisibleParts(root, out SourceStatistics stats);
                    ValidateInMemory(root, parts, stats);

                    report.PassedPrefabCount++;
                    report.RendererCount += parts.Count;
                    report.SkinnedRendererCount += stats.SkinnedRendererCount;
                    report.StaticRendererCount += stats.StaticRendererCount;
                    report.VertexCount += stats.VertexCount;
                    report.TriangleCount += stats.TriangleCount;
                    report.SkippedRendererCount += stats.SkippedRendererCount;
                }
                catch (Exception exception)
                {
                    report.Failures.Add(
                        path + "\n  " + exception.GetBaseException().Message);
                }
                finally
                {
                    DestroyUnsavedMeshes(parts);
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            report.TotalPrefabCount = guids.Length;
            return report;
        }

        private static bool HasConvertibleRenderer(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled ||
                    !renderers[i].gameObject.activeInHierarchy ||
                    IsColliderVisual(renderers[i]))
                {
                    continue;
                }

                if (renderers[i] is SkinnedMeshRenderer)
                {
                    return true;
                }

                if (renderers[i] is MeshRenderer &&
                    renderers[i].GetComponent<MeshFilter>() is MeshFilter filter &&
                    filter.sharedMesh != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void FreezeAnimators(GameObject root)
        {
            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                // The temporary copy keeps the already evaluated bone transforms.
                // Disabling here prevents an Animator update from changing the pose
                // between renderer collection and BakeMesh.
                animators[i].enabled = false;
            }
        }

        private static List<BakedPart> BakeVisibleParts(
            GameObject root,
            out SourceStatistics stats)
        {
            var parts = new List<BakedPart>();
            stats = new SourceStatistics();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            // Snapshot every renderer before creating, destroying, or resetting anything.
            // This makes renderer order and bone hierarchy order irrelevant.
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    stats.SkippedRendererCount++;
                    continue;
                }

                if (IsColliderVisual(renderer))
                {
                    stats.SkippedRendererCount++;
                    continue;
                }

                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    if (skinned.sharedMesh == null)
                    {
                        throw new InvalidOperationException(
                            GetRelativePath(root.transform, skinned.transform) +
                            ": SkinnedMeshRenderer에 Mesh가 없습니다.");
                    }

                    mesh = new Mesh
                    {
                        name = SanitizeFileName(renderer.name) + "_Static"
                    };

                    // useScale=true returns local mesh data. The full renderer matrix is
                    // then applied exactly once below.
                    skinned.BakeMesh(mesh, true);
                    stats.SkinnedRendererCount++;
                }
                else if (renderer is MeshRenderer meshRenderer)
                {
                    MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                    {
                        // A MeshRenderer without a mesh produces no visible geometry.
                        // Some environment prefabs contain these empty placeholders.
                        stats.SkippedRendererCount++;
                        continue;
                    }

                    mesh = CloneMeshReadable(filter.sharedMesh);
                    mesh.name = SanitizeFileName(renderer.name) + "_Static";
                    stats.StaticRendererCount++;
                }
                else
                {
                    stats.SkippedRendererCount++;
                    continue;
                }

                Matrix4x4 rootLocalMatrix =
                    root.transform.worldToLocalMatrix *
                    renderer.transform.localToWorldMatrix;
                ApplyTransform(mesh, rootLocalMatrix);
                ValidateMesh(mesh, renderer, root);

                var part = new BakedPart(
                    GetUniquePartName(root.transform, renderer.transform, i),
                    mesh,
                    renderer.sharedMaterials,
                    RendererSettings.Capture(renderer));
                parts.Add(part);

                stats.VertexCount += mesh.vertexCount;
                stats.TriangleCount += CountTriangles(mesh);
                stats.MaterialSlotCount += renderer.sharedMaterials.Length;
                stats.SubMeshCount += mesh.subMeshCount;
                Encapsulate(ref stats.Bounds, ref stats.HasBounds, mesh.bounds);
            }

            if (parts.Count == 0)
            {
                throw new InvalidOperationException(
                    root.name + ": 활성 상태의 변환 가능한 Renderer가 없습니다.");
            }

            return parts;
        }

        private static Mesh CloneMeshReadable(Mesh source)
        {
            Mesh.MeshDataArray readData = MeshUtility.AcquireReadOnlyMeshData(source);
            try
            {
                Mesh.MeshData data = readData[0];
                var clone = new Mesh
                {
                    name = source.name + "_Static",
                    indexFormat = data.indexFormat
                };

                var attributes = new List<VertexAttributeDescriptor>();
                foreach (VertexAttribute attribute in
                         Enum.GetValues(typeof(VertexAttribute)))
                {
                    if (!data.HasVertexAttribute(attribute))
                    {
                        continue;
                    }

                    attributes.Add(new VertexAttributeDescriptor(
                        attribute,
                        data.GetVertexAttributeFormat(attribute),
                        data.GetVertexAttributeDimension(attribute),
                        data.GetVertexAttributeStream(attribute)));
                }

                clone.SetVertexBufferParams(
                    data.vertexCount,
                    attributes.ToArray());

                for (int stream = 0; stream < data.vertexBufferCount; stream++)
                {
                    NativeArray<byte> bytes = data.GetVertexData<byte>(stream);
                    clone.SetVertexBufferData(
                        bytes,
                        0,
                        0,
                        bytes.Length,
                        stream,
                        MeshFlags);
                }

                if (data.indexFormat == IndexFormat.UInt16)
                {
                    NativeArray<ushort> indices = data.GetIndexData<ushort>();
                    clone.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
                    clone.SetIndexBufferData(
                        indices,
                        0,
                        0,
                        indices.Length,
                        MeshFlags);
                }
                else
                {
                    NativeArray<uint> indices = data.GetIndexData<uint>();
                    clone.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
                    clone.SetIndexBufferData(
                        indices,
                        0,
                        0,
                        indices.Length,
                        MeshFlags);
                }

                clone.subMeshCount = data.subMeshCount;
                for (int subMesh = 0; subMesh < data.subMeshCount; subMesh++)
                {
                    clone.SetSubMesh(
                        subMesh,
                        data.GetSubMesh(subMesh),
                        MeshFlags);
                }

                clone.bounds = source.bounds;
                return clone;
            }
            finally
            {
                readData.Dispose();
            }
        }

        private static void ApplyTransform(Mesh mesh, Matrix4x4 matrix)
        {
            float determinant = matrix.determinant;
            if (!IsFinite(determinant) || Mathf.Abs(determinant) < 0.0000001f)
            {
                throw new InvalidOperationException(
                    mesh.name + ": 0 또는 비정상 스케일 행렬은 변환할 수 없습니다.");
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            }

            mesh.vertices = vertices;

            Vector3[] normals = mesh.normals;
            if (normals.Length == vertices.Length)
            {
                Matrix4x4 normalMatrix = matrix.inverse.transpose;
                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                }

                mesh.normals = normals;
            }

            Vector4[] tangents = mesh.tangents;
            if (tangents.Length == vertices.Length)
            {
                bool mirrored = determinant < 0f;
                for (int i = 0; i < tangents.Length; i++)
                {
                    Vector3 tangent = matrix.MultiplyVector(
                        new Vector3(
                            tangents[i].x,
                            tangents[i].y,
                            tangents[i].z));

                    if (normals.Length == vertices.Length)
                    {
                        tangent -= normals[i] * Vector3.Dot(normals[i], tangent);
                    }

                    tangent.Normalize();
                    tangents[i] = new Vector4(
                        tangent.x,
                        tangent.y,
                        tangent.z,
                        mirrored ? -tangents[i].w : tangents[i].w);
                }

                mesh.tangents = tangents;
            }

            if (determinant < 0f)
            {
                FlipTriangleWinding(mesh);
            }

            mesh.RecalculateBounds();
        }

        private static void FlipTriangleWinding(Mesh mesh)
        {
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                {
                    continue;
                }

                int[] triangles = mesh.GetTriangles(subMesh);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    (triangles[i + 1], triangles[i + 2]) =
                        (triangles[i + 2], triangles[i + 1]);
                }

                mesh.SetTriangles(triangles, subMesh, false);
            }
        }

        private static void ValidateMesh(
            Mesh mesh,
            Renderer sourceRenderer,
            GameObject sourceRoot)
        {
            if (mesh.vertexCount == 0)
            {
                throw new InvalidOperationException(
                    sourceRenderer.name + ": 변환 결과의 정점이 0개입니다.");
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!IsFinite(vertices[i]))
                {
                    throw new InvalidOperationException(
                        sourceRenderer.name +
                        ": 변환 결과에 NaN 또는 Infinity 정점이 있습니다.");
                }
            }

            if (!IsFinite(mesh.bounds.center) || !IsFinite(mesh.bounds.size))
            {
                throw new InvalidOperationException(
                    sourceRenderer.name + ": 변환 bounds가 비정상입니다.");
            }

            Bounds worldBounds = TransformBounds(
                sourceRoot.transform.localToWorldMatrix,
                mesh.bounds);
            Bounds sourceBounds = sourceRenderer.bounds;

            // Skinned localBounds may contain padding. The generated geometry may be
            // smaller, but it must never explode beyond the original renderer bounds.
            Vector3 allowed = sourceBounds.size * 1.5f + Vector3.one * 0.05f;
            Vector3 generated = worldBounds.size;
            if (generated.x > allowed.x ||
                generated.y > allowed.y ||
                generated.z > allowed.z)
            {
                throw new InvalidOperationException(
                    sourceRenderer.name +
                    ": 변환 bounds가 원본보다 비정상적으로 큽니다. " +
                    "원본=" + sourceBounds.size.ToString("F3") +
                    ", 변환=" + generated.ToString("F3"));
            }
        }

        private static void ValidateInMemory(
            GameObject sourceRoot,
            List<BakedPart> parts,
            SourceStatistics stats)
        {
            int vertices = 0;
            int triangles = 0;
            int subMeshes = 0;
            int materials = 0;
            Bounds bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < parts.Count; i++)
            {
                BakedPart part = parts[i];
                vertices += part.Mesh.vertexCount;
                triangles += CountTriangles(part.Mesh);
                subMeshes += part.Mesh.subMeshCount;
                materials += part.Materials.Length;
                Encapsulate(ref bounds, ref hasBounds, part.Mesh.bounds);
            }

            if (vertices != stats.VertexCount ||
                triangles != stats.TriangleCount ||
                subMeshes != stats.SubMeshCount ||
                materials != stats.MaterialSlotCount ||
                !BoundsApproximately(bounds, stats.Bounds))
            {
                throw new InvalidOperationException(
                    sourceRoot.name +
                    ": 메모리 변환 검증에서 메시 통계가 일치하지 않습니다.");
            }
        }

        private static GameObject BuildOutputHierarchy(
            string sourceName,
            List<BakedPart> parts,
            Scene outputScene,
            DestructionVisualPreset preset)
        {
            string suffix = preset == DestructionVisualPreset.BossSequential
                ? "_BossDestructionVisual"
                : "_DestructionVisual";
            var root = new GameObject(sourceName + suffix);
            SceneManager.MoveGameObjectToScene(root, outputScene);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            for (int i = 0; i < parts.Count; i++)
            {
                BakedPart part = parts[i];
                var child = new GameObject(part.Name);
                child.transform.SetParent(root.transform, false);

                MeshFilter filter = child.AddComponent<MeshFilter>();
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                filter.sharedMesh = part.Mesh;
                renderer.sharedMaterials = part.Materials;
                part.Settings.Apply(renderer);
            }

            ConfigureDestructionComponents(root, preset);

            return root;
        }

        private static void ConfigureDestructionComponents(
            GameObject root,
            DestructionVisualPreset preset)
        {
            Artificer artificer = root.AddComponent<Artificer>();
            artificer.target = root;
            bool optimizeBossGroups =
                preset == DestructionVisualPreset.BossSequential;

            // 일반 몬스터는 기존의 연결된 메시 파츠를 보존한다.
            // 파츠 수가 많은 보스만 렌더러/머티리얼 단위로 묶어 파괴 비용을 줄인다.
            artificer.splitMode = optimizeBossGroups
                ? SplitMode.Materials
                : SplitMode.Elements;
            artificer.usePosition = !optimizeBossGroups;
            artificer.useUV = false;
            artificer.useUV2 = false;
            artificer.useNormal = false;
            artificer.useColor = false;
            artificer.useMaterial = false;
            artificer.dismantleTime =
                preset == DestructionVisualPreset.BossSequential
                    ? 0.3f
                    : 0.000001f;

            CombatDroneArtificerDestruction destruction =
                root.AddComponent<CombatDroneArtificerDestruction>();
            ArtificerFragmentBurstProfile burstProfile =
                root.GetComponent<ArtificerFragmentBurstProfile>();
            ArtificerRuntimeTuningTarget tuningTarget =
                root.GetComponent<ArtificerRuntimeTuningTarget>();
            EnemyDestructionVisual destructionVisual =
                root.AddComponent<EnemyDestructionVisual>();

            var destructionSerialized = new SerializedObject(destruction);
            destructionSerialized.FindProperty("artificer").objectReferenceValue =
                artificer;
            destructionSerialized.ApplyModifiedPropertiesWithoutUndo();

            var burstSerialized = new SerializedObject(burstProfile);
            burstSerialized.FindProperty("artificer").objectReferenceValue =
                artificer;
            burstSerialized.ApplyModifiedPropertiesWithoutUndo();
            artificer.customDismantle = burstProfile;

            var tuningSerialized = new SerializedObject(tuningTarget);
            tuningSerialized.FindProperty("artificer").objectReferenceValue = artificer;
            SerializedProperty settings =
                tuningSerialized.FindProperty("activeSettings");
            settings.FindPropertyRelative("releaseMode").enumValueIndex =
                preset == DestructionVisualPreset.BossSequential
                    ? (int)ArtificerRuntimeReleaseMode.Sequential
                    : (int)ArtificerRuntimeReleaseMode.Simultaneous;
            settings.FindPropertyRelative("dismantleTime").floatValue = 0.3f;
            settings.FindPropertyRelative("orderMode").enumValueIndex =
                (int)ArtificerRuntimeOrderMode.Baked;
            tuningSerialized.ApplyModifiedPropertiesWithoutUndo();

            var visualSerialized = new SerializedObject(destructionVisual);
            visualSerialized.FindProperty("artificer").objectReferenceValue = artificer;
            visualSerialized.FindProperty("destruction").objectReferenceValue =
                destruction;
            visualSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidatePresetBuildData(
            DestructionVisualPreset preset,
            int meshElementCount)
        {
            if (meshElementCount <= 0)
            {
                throw new InvalidOperationException(
                    "Artificer BuildData에 파괴 가능한 MeshElement가 없습니다.");
            }

            if (preset == DestructionVisualPreset.BossSequential &&
                meshElementCount < 2)
            {
                throw new InvalidOperationException(
                    "보스 순차 파괴에는 MeshElement가 2개 이상 필요하지만 " +
                    "현재 결과는 1개입니다. 원본 메시가 실제로 한 덩어리라면 " +
                    "Blender 같은 모델링 도구에서 파츠별 메시로 나누거나, " +
                    "Artificer 분할 기준을 별도로 설계해야 합니다.");
            }
        }

        private static void ValidateSavedPrefab(
            GameObject prefab,
            List<BakedPart> parts,
            SourceStatistics stats,
            DestructionVisualPreset preset,
            int expectedMeshElementCount)
        {
            if (prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null ||
                prefab.GetComponentInChildren<Animator>(true) != null ||
                prefab.GetComponentInChildren<Collider>(true) != null)
            {
                throw new InvalidOperationException(
                    "출력 프리팹에 SkinnedMeshRenderer, Animator 또는 Collider가 남았습니다.");
            }

            Artificer artificer = prefab.GetComponent<Artificer>();
            if (artificer == null ||
                prefab.GetComponent<CombatDroneArtificerDestruction>() == null ||
                prefab.GetComponent<ArtificerFragmentBurstProfile>() == null ||
                prefab.GetComponent<ArtificerRuntimeTuningTarget>() == null ||
                prefab.GetComponent<EnemyDestructionVisual>() == null)
            {
                throw new InvalidOperationException(
                    "출력 프리팹 루트에 필요한 다섯 개의 파괴 연출 컴포넌트가 없습니다.");
            }

            int savedMeshElementCount =
                artificer.buildData != null && artificer.buildData.meshes != null
                    ? artificer.buildData.meshes.Count
                    : 0;
            if (savedMeshElementCount != expectedMeshElementCount)
            {
                throw new InvalidOperationException(
                    "저장된 Artificer BuildData의 MeshElement 수가 변환 결과와 다릅니다.");
            }
            ValidatePresetBuildData(preset, savedMeshElementCount);

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (filters.Length != parts.Count || renderers.Length != parts.Count)
            {
                throw new InvalidOperationException(
                    "저장된 프리팹의 Renderer 수가 변환 결과와 다릅니다.");
            }

            int vertices = 0;
            int triangles = 0;
            int materials = 0;
            int subMeshes = 0;
            Bounds bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null)
                {
                    throw new InvalidOperationException(
                        filters[i].name + ": 저장된 MeshFilter의 Mesh가 없습니다.");
                }

                vertices += mesh.vertexCount;
                triangles += CountTriangles(mesh);
                subMeshes += mesh.subMeshCount;
                materials += renderers[i].sharedMaterials.Length;
                Encapsulate(ref bounds, ref hasBounds, mesh.bounds);
            }

            if (vertices != stats.VertexCount ||
                triangles != stats.TriangleCount ||
                subMeshes != stats.SubMeshCount ||
                materials != stats.MaterialSlotCount ||
                !BoundsApproximately(bounds, stats.Bounds))
            {
                throw new InvalidOperationException(
                    "저장 후 검증에서 정점, 삼각형, 재질 또는 bounds가 달라졌습니다.");
            }
        }

        private static bool IsColliderVisual(Renderer renderer)
        {
            string objectName = renderer.name;
            string meshName = null;

            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                meshName = skinned.sharedMesh.name;
            }
            else
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    meshName = filter.sharedMesh.name;
                }
            }

            return objectName.StartsWith("UCX_", StringComparison.OrdinalIgnoreCase) ||
                   objectName.StartsWith("UBX_", StringComparison.OrdinalIgnoreCase) ||
                   objectName.StartsWith("USP_", StringComparison.OrdinalIgnoreCase) ||
                   objectName.StartsWith("UCP_", StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrEmpty(meshName) &&
                    (meshName.StartsWith("UCX_", StringComparison.OrdinalIgnoreCase) ||
                     meshName.StartsWith("UBX_", StringComparison.OrdinalIgnoreCase) ||
                     meshName.StartsWith("USP_", StringComparison.OrdinalIgnoreCase) ||
                     meshName.StartsWith("UCP_", StringComparison.OrdinalIgnoreCase)));
        }

        private static int CountTriangles(Mesh mesh)
        {
            int count = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                if (mesh.GetTopology(subMesh) == MeshTopology.Triangles)
                {
                    count += (int)mesh.GetIndexCount(subMesh) / 3;
                }
            }

            return count;
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            extents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, extents * 2f);
        }

        private static bool BoundsApproximately(Bounds a, Bounds b)
        {
            const float tolerance = 0.0005f;
            return Vector3.Distance(a.center, b.center) <= tolerance &&
                   Vector3.Distance(a.size, b.size) <= tolerance;
        }

        private static void Encapsulate(
            ref Bounds target,
            ref bool hasBounds,
            Bounds addition)
        {
            if (!hasBounds)
            {
                target = addition;
                hasBounds = true;
            }
            else
            {
                target.Encapsulate(addition);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static string GetDefaultOutputFolder(GameObject selected)
        {
            string assetPath = AssetDatabase.GetAssetPath(selected);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder) &&
                    !folder.StartsWith(
                        "Assets/Resources_GoogleDrive/",
                        StringComparison.Ordinal))
                {
                    return folder;
                }
            }

            return "Assets/SW/Prefabs/Enemy";
        }

        private static string GetAssetDirectory(string assetPath)
        {
            int separator = assetPath.LastIndexOf('/');
            if (separator <= 0)
            {
                throw new ArgumentException(
                    "유효한 Assets 하위 폴더가 필요합니다.",
                    nameof(assetPath));
            }

            return assetPath.Substring(0, separator);
        }

        private static string GetAssetFileNameWithoutExtension(string assetPath)
        {
            int separator = assetPath.LastIndexOf('/');
            string fileName = separator >= 0
                ? assetPath.Substring(separator + 1)
                : assetPath;
            int extension = fileName.LastIndexOf('.');
            return extension > 0 ? fileName.Substring(0, extension) : fileName;
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root)
            {
                return root.name;
            }

            var names = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static string GetUniquePartName(
            Transform root,
            Transform target,
            int index)
        {
            return index.ToString("D3", CultureInfo.InvariantCulture) + "_" +
                   SanitizeFileName(
                       GetRelativePath(root, target).Replace('/', '_'));
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                if (invalid.Contains(characters[i]) || characters[i] == '/')
                {
                    characters[i] = '_';
                }
            }

            return new string(characters);
        }

        private static void EnsureAssetFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void DestroyUnsavedMeshes(List<BakedPart> parts)
        {
            if (parts == null)
            {
                return;
            }

            for (int i = 0; i < parts.Count; i++)
            {
                if (!parts[i].IsSavedAsset && parts[i].Mesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(parts[i].Mesh);
                }
            }
        }

        private sealed class SourceContext : IDisposable
        {
            private readonly bool isPrefabContents;
            private readonly Scene previewScene;

            public GameObject Root { get; }

            private SourceContext(
                GameObject root,
                bool isPrefabContents,
                Scene previewScene)
            {
                Root = root;
                this.isPrefabContents = isPrefabContents;
                this.previewScene = previewScene;
            }

            public static SourceContext Create(GameObject source)
            {
                string assetPath = AssetDatabase.GetAssetPath(source);
                if (!string.IsNullOrEmpty(assetPath) &&
                    assetPath.EndsWith(
                        ".prefab",
                        StringComparison.OrdinalIgnoreCase))
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
                    FreezeAnimators(root);
                    return new SourceContext(root, true, default);
                }

                Scene scene = EditorSceneManager.NewPreviewScene();
                GameObject clone = UnityEngine.Object.Instantiate(source);
                clone.name = source.name;
                SceneManager.MoveGameObjectToScene(clone, scene);
                FreezeAnimators(clone);
                return new SourceContext(clone, false, scene);
            }

            public void Dispose()
            {
                if (isPrefabContents)
                {
                    PrefabUtility.UnloadPrefabContents(Root);
                    return;
                }

                if (Root != null)
                {
                    UnityEngine.Object.DestroyImmediate(Root);
                }

                if (previewScene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(previewScene);
                }
            }
        }

        private sealed class BakedPart
        {
            public readonly string Name;
            public readonly Mesh Mesh;
            public readonly Material[] Materials;
            public readonly RendererSettings Settings;
            public bool IsSavedAsset;

            public BakedPart(
                string name,
                Mesh mesh,
                Material[] materials,
                RendererSettings settings)
            {
                Name = name;
                Mesh = mesh;
                Materials = materials;
                Settings = settings;
            }
        }

        private struct RendererSettings
        {
            public ShadowCastingMode ShadowCastingMode;
            public bool ReceiveShadows;
            public LightProbeUsage LightProbeUsage;
            public ReflectionProbeUsage ReflectionProbeUsage;
            public MotionVectorGenerationMode MotionVectorGenerationMode;
            public uint RenderingLayerMask;
            public int RendererPriority;

            public static RendererSettings Capture(Renderer renderer)
            {
                return new RendererSettings
                {
                    ShadowCastingMode = renderer.shadowCastingMode,
                    ReceiveShadows = renderer.receiveShadows,
                    LightProbeUsage = renderer.lightProbeUsage,
                    ReflectionProbeUsage = renderer.reflectionProbeUsage,
                    MotionVectorGenerationMode = renderer.motionVectorGenerationMode,
                    RenderingLayerMask = renderer.renderingLayerMask,
                    RendererPriority = renderer.rendererPriority
                };
            }

            public void Apply(Renderer renderer)
            {
                renderer.shadowCastingMode = ShadowCastingMode;
                renderer.receiveShadows = ReceiveShadows;
                renderer.lightProbeUsage = LightProbeUsage;
                renderer.reflectionProbeUsage = ReflectionProbeUsage;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode;
                renderer.renderingLayerMask = RenderingLayerMask;
                renderer.rendererPriority = RendererPriority;
            }
        }

        private struct SourceStatistics
        {
            public int SkinnedRendererCount;
            public int StaticRendererCount;
            public int SkippedRendererCount;
            public int VertexCount;
            public int TriangleCount;
            public int MaterialSlotCount;
            public int SubMeshCount;
            public Bounds Bounds;
            public bool HasBounds;
        }
    }

    public sealed class ConversionResult
    {
        public GameObject OutputPrefab { get; }
        public string OutputPath { get; }
        public string BuildDataPath { get; }
        public DestructionVisualPreset Preset { get; }
        public int RendererCount { get; }
        public int MeshElementCount { get; }
        public int SkinnedRendererCount { get; }
        public int StaticRendererCount { get; }
        public int VertexCount { get; }
        public int TriangleCount { get; }
        public int SkippedRendererCount { get; }

        public ConversionResult(
            GameObject outputPrefab,
            string outputPath,
            string buildDataPath,
            DestructionVisualPreset preset,
            int rendererCount,
            int meshElementCount,
            int skinnedRendererCount,
            int staticRendererCount,
            int vertexCount,
            int triangleCount,
            int skippedRendererCount)
        {
            OutputPrefab = outputPrefab;
            OutputPath = outputPath;
            BuildDataPath = buildDataPath;
            Preset = preset;
            RendererCount = rendererCount;
            MeshElementCount = meshElementCount;
            SkinnedRendererCount = skinnedRendererCount;
            StaticRendererCount = staticRendererCount;
            VertexCount = vertexCount;
            TriangleCount = triangleCount;
            SkippedRendererCount = skippedRendererCount;
        }

        public string GetUserSummary()
        {
            return "저장 위치: " + OutputPath +
                   "\nBuildData: " + BuildDataPath +
                   "\n프리셋: " +
                   (Preset == DestructionVisualPreset.BossSequential
                       ? "보스 순차 파괴"
                       : "일반 적 동시 파괴") +
                   "\nRenderer: " + RendererCount +
                   " (스킨 " + SkinnedRendererCount +
                   ", 정적 " + StaticRendererCount + ")" +
                   "\nArtificer MeshElement: " + MeshElementCount +
                   "\n정점: " + VertexCount.ToString("N0") +
                   "\n삼각형: " + TriangleCount.ToString("N0") +
                   "\n제외된 비활성/비메시 Renderer: " + SkippedRendererCount +
                   "\n\nArtificer, CombatDroneArtificerDestruction, " +
                   "ArtificerFragmentBurstProfile, " +
                   "ArtificerRuntimeTuningTarget, EnemyDestructionVisual을 " +
                   "자동으로 붙이고 연결했습니다." +
                   "\nCollider, Animator, 뼈, 게임플레이 스크립트는 복사하지 않았습니다.";
        }
    }

    public sealed class BatchValidationReport
    {
        public string Folder { get; }
        public int TotalPrefabCount;
        public int PassedPrefabCount;
        public int SkippedPrefabCount;
        public int RendererCount;
        public int SkinnedRendererCount;
        public int StaticRendererCount;
        public int SkippedRendererCount;
        public long VertexCount;
        public long TriangleCount;
        public readonly List<string> Failures = new List<string>();

        public BatchValidationReport(string folder)
        {
            Folder = folder;
        }

        public override string ToString()
        {
            string summary =
                "검사 폴더: " + Folder +
                "\n전체 프리팹: " + TotalPrefabCount +
                "\n변환 검증 통과: " + PassedPrefabCount +
                "\n변환 대상 없음: " + SkippedPrefabCount +
                "\n실패: " + Failures.Count +
                "\n검증 Renderer: " + RendererCount +
                " (스킨 " + SkinnedRendererCount +
                ", 정적 " + StaticRendererCount + ")" +
                "\n정점: " + VertexCount.ToString("N0") +
                " / 삼각형: " + TriangleCount.ToString("N0") +
                "\n제외된 비활성/비메시 Renderer: " + SkippedRendererCount;

            if (Failures.Count > 0)
            {
                summary += "\n\n실패 목록:\n" +
                           string.Join("\n", Failures.Take(20));
            }

            return summary;
        }
    }
}
