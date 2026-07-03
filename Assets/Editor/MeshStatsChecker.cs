#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MeshStatsChecker
{
    [MenuItem("Tools/Check Selected Mesh Stats")]
    private static void CheckSelectedMeshStats()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.Log("No GameObject selected.");
            return;
        }

        MeshFilter[] meshFilters = selected.GetComponentsInChildren<MeshFilter>();
        SkinnedMeshRenderer[] skinnedMeshes = selected.GetComponentsInChildren<SkinnedMeshRenderer>();

        int totalVerts = 0;
        int totalTris = 0;

        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            totalVerts += mf.sharedMesh.vertexCount;
            totalTris += mf.sharedMesh.triangles.Length / 3;
        }

        foreach (SkinnedMeshRenderer smr in skinnedMeshes)
        {
            if (smr.sharedMesh == null) continue;

            totalVerts += smr.sharedMesh.vertexCount;
            totalTris += smr.sharedMesh.triangles.Length / 3;
        }

        Debug.Log($"{selected.name} - Verts: {totalVerts}, Tris: {totalTris}");
    }
}
#endif