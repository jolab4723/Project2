using UnityEngine;
using Mirror;

public class PlayerController : NetworkBehaviour
{
    // SyncVar를 사용하면 서버에서 데이터가 바뀔 때 클라이언트들에게 자동 동기화됩니다.
    [SyncVar(hook = nameof(OnNameChanged))]
    public string PlayerName;

    // 클라이언트가 서버에 조인할 때 본인 화면에만 띄울 초기화 로직
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Log.Print("[Client] 내가 서버에 정상적으로 조인되었습니다!");

        // 메인 카메라가 나를 따라오도록 설정하는 등의 로직을 여기에 작성합니다.
        // Camera.main.GetComponent<FollowCamera>().Target = this.transform;
    }

    // 다른 외부 플레이어가 조인해서 내 화면에 보이기 시작할 때 호출
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isLocalPlayer)
        {
            Log.Print($"[Client] 외부 플레이어({PlayerName})가 내 시야에 조인했습니다.");
        }
    }

    // 다른 플레이어가 나가서 내 시야에서 사라질 때 호출
    protected void OnDestroy()
    {
        // 네트워크 연결이 끊겨 파괴될 때 클라이언트 단의 처리
        if (isClient && !isLocalPlayer)
        {
            Log.Print($"[Client] 외부 플레이어({PlayerName})가 나갔거나 시야에서 벗어났습니다.");
        }
    }

    // 훅(Hook) 함수: SyncVar 변수가 변경되면 모든 클라이언트에서 실행됨
    void OnNameChanged(string oldName, string newName)
    {
        // 캐릭터 머리 위의 네임텍 UI 등을 업데이트
        gameObject.name = newName;
    }
}