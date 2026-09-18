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

        // 저장 실패 시 씬을 이동하지 않습니다.
        if ( ! dataManager.BeginNewGame(character))
            return;

        Debug.Log($"[YJ_SinglePlayerStartFlow] 새 게임 캐릭터: {character}", this);

        KY_RunStatsTracker.Instance?.BeginRun(); // 결과창 데이터 집계용으로 추가
        loader.LoadScene(stageSelectSceneName);
    }
}
