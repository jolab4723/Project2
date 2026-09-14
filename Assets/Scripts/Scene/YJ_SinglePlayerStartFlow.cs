using UnityEngine;
using Core;

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
            Debug.LogError(
                "[YJ_SinglePlayerStartFlow] Lobby Controller를 연결하세요.",
                this);
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
        if (string.IsNullOrWhiteSpace(stageSelectSceneName))
        {
            Debug.LogError(
                "[YJ_SinglePlayerStartFlow] 목적 씬 이름이 비어 있습니다.",
                this);
            return;
        }

        // Start 씬에서 준비된 기존 로더만 사용한다.
        // Singleton.Instance의 자동 생성으로 부트 누락을 숨기지 않는다.
        SceneLoader loader = FindFirstObjectByType<SceneLoader>();

        if (loader == null)
        {
            Debug.LogError(
                "[YJ_SinglePlayerStartFlow] SceneLoader가 없습니다. " +
                "Start 씬부터 실행하세요.",
                this);
            return;
        }

        if (loader.IsLoading)
            return;

        Debug.Log(
            $"[YJ_SinglePlayerStartFlow] 선택 캐릭터: {characterId}, " +
            $"이동 씬: {stageSelectSceneName}",
            this);

        // 현재 단계는 씬 이동만 처리한다.
        // 캐릭터 저장 및 새 게임/이어하기 처리는 추후 이 호출 전에 연결한다.
        loader.LoadScene(stageSelectSceneName);
    }
}
