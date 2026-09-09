using System;
using System.Collections.Generic;

public sealed partial class MirrorTestNetworkManager
{
    public const string SessionStage2Scene = "Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage2_MirrorSessionTest.unity";
    public const string SessionStage3Scene = "Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage3_MirrorSessionTest.unity";
    public const string SessionStage4Scene = "Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage4_MirrorSessionTest.unity";
    public const string SessionStage5Scene = "Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage5_MirrorSessionTest.unity";
    public const string SessionStage6Scene = "Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage6_MirrorSessionTest.unity";
    public const string SessionBossScene = "Assets/SW/TEST/MirrorCombat/Scenes/Act1_BossStage_MirrorSessionTest.unity";

    // 서버 스냅샷의 맵 이름만 이 허용 목록으로 해석한다. 클라이언트의 임의 경로는 받지 않는다.
    private static string ResolveAct1CombatScene(string sceneName)
    {
        return sceneName switch
        {
            "Act1_Stage1" => SessionCombatScene,
            "Act1_Stage2" => SessionStage2Scene,
            "Act1_Stage3" => SessionStage3Scene,
            "Act1_Stage4" => SessionStage4Scene,
            "Act1_Stage5" => SessionStage5Scene,
            "Act1_Stage6" => SessionStage6Scene,
            "Act1_BossStage" => SessionBossScene,
            _ => string.Empty,
        };
    }

    private static bool IsAct1CombatScene(string scenePath)
    {
        return scenePath == SessionCombatScene || scenePath == SessionStage2Scene ||
               scenePath == SessionStage3Scene || scenePath == SessionStage4Scene ||
               scenePath == SessionStage5Scene || scenePath == SessionStage6Scene ||
               scenePath == SessionBossScene;
    }

    /// <summary>선택이 확정된 노드에만 맵을 배정하고 같은 런 스냅샷에 보존한다.</summary>
    private static bool TryReserveAct1Scene(StageMapSaveData snapshot, StageNodeSaveData node)
    {
        if (snapshot == null || snapshot.act != StageActType.Act1 || node == null)
            return false;

        switch (node.type)
        {
            case StageNodeType.Camp:
                node.sceneName = "Act1_Camp";
                return true;
            case StageNodeType.Event:
                node.sceneName = "Unknown_Stage";
                return true;
            case StageNodeType.Boss:
                node.sceneName = "Act1_BossStage";
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
            if (node.sceneName == "Act1_BossStage" || string.IsNullOrEmpty(ResolveAct1CombatScene(node.sceneName)))
                return false;
        }
        else
        {
            var remaining = new List<string>(6);
            for (int stage = 1; stage <= 6; stage++)
            {
                string name = "Act1_Stage" + stage;
                if (!snapshot.usedStageSceneNames.Contains(name)) remaining.Add(name);
            }
            // ponytail: 여섯 맵을 모두 방문한 뒤에만 재사용한다. 맵별 가중치 계층은 필요할 때 추가한다.
            if (remaining.Count == 0)
                for (int stage = 1; stage <= 6; stage++) remaining.Add("Act1_Stage" + stage);

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
            ? ResolveAct1CombatScene(node.sceneName)
            : string.Empty;
    }
}
