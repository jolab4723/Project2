using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal static class MirrorAct1RouteValidation_MirrorTest
{
    [MenuItem("SW/Mirror 테스트/Act1 맵 배정 규칙 검사")]
    private static void Validate()
    {
        MethodInfo reserve = typeof(MirrorTestNetworkManager).GetMethod("TryReserveAct1Scene", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo resolve = typeof(MirrorTestNetworkManager).GetMethod("ResolveAct1CombatScene", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo route = typeof(MirrorTestNetworkManager).GetMethod("GetRouteForScene", BindingFlags.Static | BindingFlags.NonPublic);
        int checks = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("[MirrorAct1Route] " + message);
            checks++;
        }
        bool Reserve(StageMapSaveData map, StageNodeSaveData node) => (bool)reserve.Invoke(null, new object[] { map, node });
        string Resolve(string name) => (string)resolve.Invoke(null, new object[] { name });

        var snapshot = new StageMapSaveData { act = StageActType.Act1, mapSeed = 382597156 };
        var visited = new HashSet<string>();
        for (int floor = 1; floor <= 6; floor++)
        {
            var node = new StageNodeSaveData { id = "node" + floor, floor = floor, type = floor % 2 == 0 ? StageNodeType.Elite : StageNodeType.Battle };
            Check(Reserve(snapshot, node) && visited.Add(node.sceneName), "여섯 전투 맵 중복 없는 배정");
            string scene = Resolve(node.sceneName);
            Check(!string.IsNullOrEmpty(scene) && (MirrorSessionRoute)route.Invoke(null, new object[] { scene }) == MirrorSessionRoute.Combat, "실제 전투 씬과 Ready 경로 일치");
            string assigned = node.sceneName;
            Check(Reserve(snapshot, node) && node.sceneName == assigned && snapshot.usedStageSceneNames.Count == floor, "재진입 배정 보존");
        }
        var repeat = new StageNodeSaveData { floor = 7, type = StageNodeType.Battle };
        Check(Reserve(snapshot, repeat) && visited.Contains(repeat.sceneName), "모두 사용한 뒤 재사용 허용");
        var boss = new StageNodeSaveData { floor = 11, type = StageNodeType.Boss };
        Check(Reserve(snapshot, boss) && Resolve(boss.sceneName) == MirrorTestNetworkManager.SessionBossScene, "실제 보스 맵 연결");
        Check(!Reserve(snapshot, new StageNodeSaveData { type = StageNodeType.Battle, sceneName = "Act1_BossStage" }), "일반 노드의 보스 맵 거절");
        Check(!Reserve(snapshot, new StageNodeSaveData { type = StageNodeType.Battle, sceneName = "../../Other.unity" }), "허용 목록 밖 맵 거절");
        Check(Resolve("Act1_Stage10") == string.Empty, "이름 접두사만으로 맵 허용 안 함");
        var restored = JsonUtility.FromJson<StageMapSaveData>(JsonUtility.ToJson(snapshot));
        Check(restored.usedStageSceneNames.Count == 6, "스냅샷 직렬화 맵 이력 보존");
        Check(!Reserve(new StageMapSaveData { act = StageActType.Act2 }, repeat), "Act1 밖 배정 거절");
        Debug.Log($"[MirrorAct1Route] PASS {checks} checks");
    }
}
