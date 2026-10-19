using Core;
using ItemSystem;
using UnityEngine;

/// <summary>
/// 튜토리얼 진입·이탈 규칙. 캐릭터 선택 화면(YJ_SinglePlayerStartFlow)이 진입 여부를 정하고,
/// TutorialRunner가 끝날 때 진입 경로에 맞는 씬으로 돌아간다.
/// </summary>
public static class TutorialEntry
{
    public const string SceneName = "Tutorial";
    private const string NewRunSceneName = "StageSelect";

    // ponytail: 타이틀 메뉴 → 캐릭터 선택 → 튜토리얼로 넘기는 요청 하나뿐이라 정적 값으로 둔다.
    // 진입 경로가 더 늘거나 멀티에서 쓰게 되면 PlayerContext 쪽 세션 상태로 옮긴다.
    public static bool ReplayRequested { get; set; }
    private static string exitSceneName = NewRunSceneName;

    /// <summary>
    /// 최초 실행인지 판정한다. 새 게임을 만들기 전에 호출해야 한다.
    /// 완료·건너뛰기 기록, 런 정산 기록, 이어할 런 중 하나라도 있으면 기존 플레이어로 본다.
    /// </summary>
    public static bool IsFirstRun(DataManager data)
    {
        PlayerProfileData profile = data.LoadSinglePlayerSlot()?.profile;
        return !data.HasContinuableRun() &&
               (profile == null || (!profile.tutorialCompleted && string.IsNullOrEmpty(profile.lastRunSettlementId)));
    }

    /// <summary>격리 런을 열고 튜토리얼 씬을 불러온다. 끝나면 exitScene으로 이동한다. 멀티에서는 열지 않는다.</summary>
    public static bool TryEnter(DataManager data, SceneLoader loader, CharacterClass character, string exitScene)
    {
        if (MirrorNetworkManager.OwnsGameplay || !Application.CanStreamedLevelBeLoaded(SceneName) ||
            !data.BeginIsolatedRun(character))
            return false;

        ReplayRequested = false;
        exitSceneName = exitScene;
        loader.LoadScene(SceneName);
        return true;
    }

    /// <summary>진입 경로의 다음 씬으로 이동한다. 에디터에서 튜토리얼을 바로 실행했다면 StageSelect로 간다.</summary>
    public static void Exit(SceneLoader loader)
    {
        string destination = exitSceneName;
        exitSceneName = NewRunSceneName;
        // 새 런으로 이어질 때는 튜토리얼 처치 기록이 결과 화면 집계에 섞이지 않게 집계를 새로 시작한다.
        if (destination == NewRunSceneName)
            KY_RunStatsTracker.Instance?.BeginRun();
        loader.LoadScene(destination);
    }
}
