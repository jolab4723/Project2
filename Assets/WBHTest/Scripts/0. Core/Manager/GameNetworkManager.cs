using Mirror;

namespace Core
{
    /// <summary>
    /// 이 프로젝트 전용 NetworkManager. SceneLoader가 나중에 씬 전환을 구현할 때
    /// 싱글플레이(SceneManager.LoadSceneAsync)와 멀티플레이(ServerChangeScene)를
    /// 분기할 수 있도록, Mirror의 씬 동기화 완료 시점을 이벤트로 알려주는 역할만 우선 담당한다.
    ///
    /// !! 접속/로비/캐릭터 스폰 등 실제 네트워크 플로우는 아직 없음.
    ///    SceneLoader 작업의 선행 작업으로 최소 뼈대만 만들어둔 상태.
    /// </summary>
    public class GameNetworkManager : NetworkManager
    {
        /// <summary>NetworkManager.singleton을 캐스팅 없이 바로 쓰기 위한 편의 접근자.</summary>
        public static GameNetworkManager Game => singleton as GameNetworkManager;

        /// <summary>서버(호스트)에서 씬 전환이 완료됐을 때 발행. SceneLoader가 구독해서 로딩 화면을 내리는 데 쓴다.</summary>
        public event System.Action<string> OnServerSceneChangedEvent;

        /// <summary>클라이언트에서 씬 전환이 완료됐을 때 발행. SceneLoader가 구독해서 로딩 화면을 내리는 데 쓴다.</summary>
        public event System.Action OnClientSceneChangedEvent;

        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            OnServerSceneChangedEvent?.Invoke(sceneName);
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            OnClientSceneChangedEvent?.Invoke();
        }
    }
}
