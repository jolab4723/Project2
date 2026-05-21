using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class CustomNetworkManager : NetworkManager
{
    // 현재 접속 중인 플레이어들을 관리하는 딕셔너리 (데이터 드리븐 연동을 위해 세션과 플레이어 매핑)
    public Dictionary<NetworkConnectionToClient, GameObject> ConnectedPlayers = new Dictionary<NetworkConnectionToClient, GameObject>();

    #region Server Ovrreides (서버에서만 실행되는 이벤트)

    // 1. 플레이어가 서버에 성공적으로 '연결(Join)' 되었을 때 호출
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Log.Print($"[Server] 새로운 외부 플레이어 접속 시도! 커넥션 ID: {conn.connectionId}");

        // 여기서 로컬 DB나 JSON 데이터에서 유저 정보를 검증하는 데이터 드리븐 로직을 실행할 수 있습니다.
    }

    // 2. 플레이어 연결 후, 서버에 실제 '캐릭터 오브젝트'가 생성(Spawn)되어 완전히 조인했을 때 호출
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // base.OnServerAddPlayer를 호출하면 설정해둔 플레이어 프리팹이 자동 생성됩니다.
        base.OnServerAddPlayer(conn);

        // 생성된 플레이어 오브젝트 참조 가져오기
        GameObject playerObj = conn.identity.gameObject;

        // 딕셔너리에 등록하여 서버에서 통합 관리
        ConnectedPlayers[conn] = playerObj;

        // 예시: 플레이어 전용 컴포넌트에 데이터 세팅 (데이터 드리븐 예시)
        PlayerController player = playerObj.GetComponent<PlayerController>();
        if (player != null)
        {
            player.PlayerName = $"User_{conn.connectionId}";
            // 데이터 매니저에서 기본 스탯 테이블을 제네リック하게 읽어와 세팅하는 타이밍입니다.
            // DataManager.Instance.GetStat<CharacterStat>(1); 
        }

        Log.Print($"[Server] 플레이어 {conn.connectionId} 조인 완료. 현재 동접자 수: {ConnectedPlayers.Count}명");
    }

    // 3. 플레이어가 연결을 끊거나 '서버를 나갔을 때(Leave)' 호출
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        Log.Print($"[Server] 외부 플레이어 퇴장 감지. 커넥션 ID: {conn.connectionId}");

        // 관리 중이던 딕셔너리에서 제거 및 서버 데이터 저장 로직 연계
        if (ConnectedPlayers.TryGetValue(conn, out GameObject playerObj))
        {
            // [데이터 드리븐 연동 팁] 나가지 전에 플레이어의 최종 위치, 레벨 등의 데이터를 JSON/DB에 저장
            // SavePlayerData(playerObj.GetComponent<PlayerController>());

            ConnectedPlayers.Remove(conn);
        }

        // Mirror 내부적으로 클라이언트 오브젝트를 파괴하고 세션을 정리하도록 베이스 메서드 호출
        base.OnServerDisconnect(conn);

        Log.Print($"[Server] 플레이어 퇴장 처리 완료. 남은 동접자 수: {ConnectedPlayers.Count}명");
    }

    #endregion
}