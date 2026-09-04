using TMPro;
using UnityEngine;

/// <summary>멀티플레이 로비의 한 플레이어 칸에 이름, 캐릭터, 준비 상태를 표시한다.</summary>
public class KY_LobbyPlayerSlot : MonoBehaviour
{
    [Header("상태 오브젝트")]
    [SerializeField] private GameObject emptyStateRoot;
    [SerializeField] private GameObject playerStateRoot;
    [SerializeField] private GameObject hostBadge;
    [SerializeField] private GameObject localPlayerFrame;
    [Header("텍스트")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text readyStateText;

    /// <summary>비어 있는 플레이어 칸으로 표시한다.</summary>
    public void ShowEmpty()
    {
        SetActive(emptyStateRoot, true); SetActive(playerStateRoot, false);
        SetActive(hostBadge, false); SetActive(localPlayerFrame, false);
    }

    /// <summary>전달받은 플레이어의 현재 선택과 준비 상태를 표시한다.</summary>
    public void ShowPlayer(KY_LobbyPlayerData player, bool isLocalPlayer)
    {
        if (player == null) { ShowEmpty(); return; }
        KY_CharacterInfoData character = KY_CharacterDatabase.GetById(player.selectedCharacterId);
        SetActive(emptyStateRoot, false); SetActive(playerStateRoot, true);
        SetActive(hostBadge, player.isHost); SetActive(localPlayerFrame, isLocalPlayer);
        SetText(playerNameText, string.IsNullOrWhiteSpace(player.displayName) ? "Player" : player.displayName);
        SetText(characterNameText, character != null ? character.characterName : "캐릭터 선택 중");
        SetText(readyStateText, GetReadyStateText(player.readyState));
    }

    /// <summary>준비 상태에 맞는 UI 문구를 반환한다.</summary>
    private static string GetReadyStateText(KY_LobbyReadyState readyState) => readyState switch
    {
        KY_LobbyReadyState.Ready => "READY", KY_LobbyReadyState.NotReady => "NOT READY", _ => "SELECTING"
    };
    /// <summary>선택적으로 연결한 오브젝트의 활성 상태를 안전하게 바꾼다.</summary>
    private static void SetActive(GameObject target, bool isActive) { if (target != null) target.SetActive(isActive); }
    /// <summary>선택적으로 연결한 텍스트에 문구를 표시한다.</summary>
    private static void SetText(TMP_Text target, string value) { if (target != null) target.text = value; }
}
