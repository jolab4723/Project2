using System;

/// <summary>네트워크 동기화 전에도 로비 UI를 표시할 수 있도록 플레이어 선택 상태를 담는 데이터다.</summary>
[Serializable]
public class KY_LobbyPlayerData
{
    public string playerId;
    public string displayName;
    public KY_CharacterId selectedCharacterId;
    public KY_LobbyReadyState readyState = KY_LobbyReadyState.NotReady;
    public bool isHost;
}
