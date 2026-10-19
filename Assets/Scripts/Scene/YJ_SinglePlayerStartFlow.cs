using UnityEngine;
using Core;
using ItemSystem;

[DisallowMultipleComponent]
public class YJ_SinglePlayerStartFlow : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private KY_SinglePlayerLobbyController lobbyController;

    [Header("Destination")]
    [SerializeField]
    private string stageSelectSceneName = "StageSelect";

    private void OnEnable()
    {
        if (lobbyController == null)
        {
            Debug.LogError("[YJ_SinglePlayerStartFlow] Lobby Controller를 연결하세요.", this);
            return;
        }

        lobbyController.StartGameRequested += HandleStartGame;
    }

    private void OnDisable()
    {
        if (lobbyController != null)
            lobbyController.StartGameRequested -= HandleStartGame;
        // SW 수정: 튜토리얼 요청은 이번 캐릭터 선택 화면에서만 유효하다. 뒤로 나간 뒤 결과창의 다시 시작 등으로
        // 이 화면에 다시 와도 새 게임으로 시작하게 한다.
        TutorialEntry.ReplayRequested = false;
    }

    private void HandleStartGame(KY_CharacterId characterId)
    {
        if (string.IsNullOrWhiteSpace(stageSelectSceneName) ||
            !Application.CanStreamedLevelBeLoaded(stageSelectSceneName) ||
            !Application.CanStreamedLevelBeLoaded("LoadingScene"))
        {
            Debug.LogError(
                "[YJ_SinglePlayerStartFlow] StageSelect 또는 LoadingScene의 " +
                "Build Settings 등록을 확인하세요.", this);
            return;
        }

        // 설정된 부트씬 Manager를 사용합니다.
        SceneLoader loader = FindFirstObjectByType<SceneLoader>();
        DataManager dataManager = FindFirstObjectByType<DataManager>();

        if (loader == null || dataManager == null)
        {
            Debug.LogError("[YJ_SinglePlayerStartFlow] SceneLoader 또는 DataManager가 없습니다. " + "Start 씬부터 실행하세요.", this);
            return;
        }

        if (loader.IsLoading)
            return;

        // UI 선택값을 게임 데이터 타입으로 명시적으로 변환합니다.
        CharacterClass character;

        switch (characterId)
        {
            case KY_CharacterId.Fighter:
                character = CharacterClass.Fighter;
                break;

            case KY_CharacterId.Gunner:
                character = CharacterClass.Gunner;
                break;

            default:
                Debug.LogError($"지원하지 않는 캐릭터 선택입니다: {characterId}", this);
                return;
        }

        // SW 수정: 타이틀의 튜토리얼 메뉴에서 왔으면 새 게임을 만들지 않고 튜토리얼만 진행한 뒤 타이틀로 돌아간다.
        if (TutorialEntry.ReplayRequested)
        {
            if (!TutorialEntry.TryEnter(dataManager, loader, character, "TitleScene"))
                Debug.LogError("[YJ_SinglePlayerStartFlow] 튜토리얼을 시작하지 못했습니다. Tutorial 씬의 Build Settings 등록을 확인하세요.", this);
            return;
        }

        // SW 수정: 최초 실행 판정은 새 게임이 이어할 런을 만들기 전에 한다.
        bool firstRun = TutorialEntry.IsFirstRun(dataManager);

        // 저장 실패 시 씬을 이동하지 않습니다.
        if ( ! dataManager.BeginNewGame(character))
            return;

        // SW 수정: 결과창의 다시 시작도 새 게임이므로 이전 런의 임시 맵(대기 노드 포함)을 버리고 Act1 첫 층부터 시작한다.
        if (YJ_StageSaveService.IsSessionOnly)
            YJ_StageSaveService.BeginTemporaryRun();

        Debug.Log($"[YJ_SinglePlayerStartFlow] 새 게임 캐릭터: {character}", this);

        KY_RunStatsTracker.Instance?.BeginRun(); // 결과창 데이터 집계용으로 추가

        // SW 수정: 최초 실행이면 새 게임을 만든 뒤 튜토리얼을 거쳐 스테이지 선택으로 간다. 열지 못하면 바로 스테이지 선택으로 간다.
        if (firstRun && TutorialEntry.TryEnter(dataManager, loader, character, stageSelectSceneName))
            return;

        loader.LoadScene(stageSelectSceneName);
    }
}
