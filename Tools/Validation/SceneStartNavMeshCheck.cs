using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// SW 수정: Assets 밖 Editor run_script에서 빌드 씬을 하나씩 열어 공유 씬 배치 오류를 찾는다.
/// 1) NetworkStartPosition 주변 4m(MirrorSpawnedPlayerBinder의 보정 거리) 안에 NavMesh가 없으면 해당 참가 슬롯이
///    배치 실패로 준비 화면에 멈춘다(실패). 1m를 넘으면 보정에 기대는 배치로 보고 경고한다.
/// 2) 싱글·멀티 CameraBounds가 다르면 한 모드에서만 카메라가 캐릭터를 놓친다(실패).
/// 씬은 저장하지 않으며, 끝나면 처음 열려 있던 씬을 다시 연다.
/// </summary>
public static class SceneStartNavMeshCheck
{
    private const float FailDistance = 4f;
    private const float WarnDistance = 1f;

    public static string Run()
        => RunScenes(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));

    public static string RunCamps()
        => RunScenes(new[] {
            "Assets/Scenes/Maps/Act1_Maps/Act1_Camp/Act1_Camp.unity",
            "Assets/Scenes/Maps/Act2_Maps/Act2_Camp/Act2_Camp.unity",
            "Assets/Scenes/Maps/Act3_Maps/Act3_Camp/Act3_Camp.unity" });

    private static string RunScenes(IEnumerable<string> paths)
    {
        if (Application.isPlaying) return "FAIL Play Mode를 종료해야 합니다.";
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) return "FAIL Prefab Stage를 닫아야 합니다.";
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) return "FAIL Dirty Scene을 저장하거나 닫아야 합니다.";

        var original = EditorSceneManager.GetSceneManagerSetup();
        var problems = new List<string>();
        var warnings = new List<string>();
        int scenes = 0, starts = 0;
        try
        {
            foreach (string path in paths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                scenes++;
                foreach (NetworkStartPosition start in Object.FindObjectsByType<NetworkStartPosition>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    starts++;
                    Vector3 position = start.transform.position;
                    if (!NavMesh.SamplePosition(position, out NavMeshHit hit, FailDistance, NavMesh.AllAreas))
                        problems.Add($"{scene.name}: 시작점 {PathOf(start.transform)} {position} 주변 {FailDistance}m에 NavMesh 없음");
                    else if (Vector3.Distance(hit.position, position) > WarnDistance)
                        warnings.Add($"{scene.name}: 시작점 {PathOf(start.transform)} {position} → NavMesh {Vector3.Distance(hit.position, position):F2}m");
                }

                var bounds = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(box => box.name == "CameraBounds").ToList();
                BoxCollider single = bounds.FirstOrDefault(box => !IsUnder(box.transform, "Multiplayer"));
                BoxCollider multi = bounds.FirstOrDefault(box => IsUnder(box.transform, "Multiplayer"));
                if (single != null && multi != null &&
                    (Vector3.Distance(single.transform.position + single.center, multi.transform.position + multi.center) > 0.01f ||
                     Vector3.Distance(Vector3.Scale(single.size, single.transform.lossyScale), Vector3.Scale(multi.size, multi.transform.lossyScale)) > 0.01f))
                    problems.Add($"{scene.name}: 싱글·멀티 CameraBounds 불일치 (싱글 {single.transform.position}/{single.size}, 멀티 {multi.transform.position}/{multi.size})");
            }
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(original);
        }

        var lines = new List<string> { (problems.Count == 0 ? "PASS" : "FAIL") + $" scenes={scenes} starts={starts} warnings={warnings.Count}" };
        lines.AddRange(problems.Select(line => "FAIL " + line));
        lines.AddRange(warnings.Select(line => "WARN " + line));
        string report = string.Join("\n", lines);
        System.IO.Directory.CreateDirectory("RunValidation/MirrorBugCloseout_20261001");
        System.IO.File.WriteAllText("RunValidation/MirrorBugCloseout_20261001/scene-starts.txt", report);
        return report;
    }

    private static bool IsUnder(Transform target, string rootName)
    {
        for (Transform current = target.parent; current != null; current = current.parent)
            if (current.name == rootName) return true;
        return false;
    }

    private static string PathOf(Transform target)
    {
        string path = target.name;
        for (Transform current = target.parent; current != null; current = current.parent)
            path = current.name + "/" + path;
        return path;
    }
}
