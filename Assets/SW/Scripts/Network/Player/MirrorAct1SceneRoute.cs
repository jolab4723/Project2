using System;
using System.Collections.Generic;

public sealed partial class MirrorNetworkManager
{
    public const string SessionStage2Scene = "Assets/Scenes/Maps/Act1_Maps/Act1_Stage2/Act1_Stage2.unity";
    public const string SessionStage3Scene = "Assets/Scenes/Maps/Act1_Maps/Act1_Stage3/Act1_Stage3.unity";
    public const string SessionStage4Scene = "Assets/Scenes/Maps/Act1_Maps/Act1_Stage4/Act1_Stage4.unity";
    public const string SessionStage5Scene = "Assets/Scenes/Maps/Act1_Maps/Act1_Stage5/Act1_Stage5.unity";
    public const string SessionStage6Scene = "Assets/Scenes/Maps/Act1_Maps/Act1_Stage6/Act1_Stage6.unity";
    public const string SessionBossScene = "Assets/Scenes/Maps/Act1_Maps/Act1_BossStage/Act1_BossStage.unity";

    // 서버 스냅샷의 맵 이름만 이 허용 목록으로 해석한다. 클라이언트의 임의 경로는 받지 않는다.
    private static string ResolveCombatScene(string sceneName)
    {
        for (int act = 1; act <= 2; act++)
        {
            string prefix = "Act" + act;
            if (sceneName == prefix + "_BossStage")
                return "Assets/Scenes/Maps/" + prefix + "_Maps/" + sceneName + "/" + sceneName + ".unity";
            for (int stage = 1; stage <= 6; stage++)
                if (sceneName == prefix + "_Stage" + stage)
                    return "Assets/Scenes/Maps/" + prefix + "_Maps/" + sceneName + "/" + sceneName + ".unity";
        }
        return string.Empty;
    }

    private static bool IsCombatScene(string scenePath)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(scenePath);
        return !string.IsNullOrEmpty(scenePath) && ResolveCombatScene(name) == scenePath;
    }

    /// <summary>선택이 확정된 노드에만 맵을 배정하고 같은 런 스냅샷에 보존한다.</summary>
    private static bool TryReserveStageScene(StageMapSaveData snapshot, StageNodeSaveData node)
    {
        if (snapshot == null || (snapshot.act != StageActType.Act1 && snapshot.act != StageActType.Act2) || node == null)
            return false;
        string act = "Act" + (int)snapshot.act;

        switch (node.type)
        {
            case StageNodeType.Camp:
                node.sceneName = act + "_Camp";
                return true;
            case StageNodeType.Event:
                node.sceneName = "Unknown_Stage";
                return true;
            case StageNodeType.Boss:
                node.sceneName = act + "_BossStage";
                return true;
            case StageNodeType.Battle:
            case StageNodeType.Elite:
                break;
            default:
                return false;
        }

        snapshot.usedStageSceneNames ??= new List<string>();
        if (!string.IsNullOrWhiteSpace(node.sceneName))
        {
            if (!node.sceneName.StartsWith(act + "_Stage", StringComparison.Ordinal) || string.IsNullOrEmpty(ResolveCombatScene(node.sceneName)))
                return false;
        }
        else
        {
            var remaining = new List<string>(6);
            for (int stage = 1; stage <= 6; stage++)
            {
                string name = act + "_Stage" + stage;
                if (!snapshot.usedStageSceneNames.Contains(name)) remaining.Add(name);
            }
            // 여섯 맵을 모두 방문한 뒤에만 재사용한다. 맵별 가중치 계층은 필요할 때 추가한다.
            if (remaining.Count == 0)
                for (int stage = 1; stage <= 6; stage++) remaining.Add(act + "_Stage" + stage);

            var random = new Random(unchecked(snapshot.mapSeed * 397 ^ node.floor));
            node.sceneName = remaining[random.Next(remaining.Count)];
        }

        if (!snapshot.usedStageSceneNames.Contains(node.sceneName))
            snapshot.usedStageSceneNames.Add(node.sceneName);
        return true;
    }

    private string GetPendingCombatScene()
    {
        return TryGetPendingStageNode(out StageNodeSaveData node)
            ? ResolveCombatScene(node.sceneName)
            : string.Empty;
    }

    private const string Act2CampScene = "Assets/Scenes/Maps/Act2_Maps/Act2_Camp/Act2_Camp.unity";
    private string GetCurrentCampScene() => TryGetRunSnapshot(out var snapshot) && snapshot.act == StageActType.Act2
        ? Act2CampScene : SessionCampGameplayScene;
}
