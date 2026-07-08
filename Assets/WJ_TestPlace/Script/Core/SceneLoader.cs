using UnityEngine;

namespace Core
{
    /// <summary>
    /// 씬 로드를 담당할 매니저. 지금은 GameManager의 "순서대로 로드/활성화" 기능을
    /// 테스트하기 위한 자리만 잡아둔 상태 - 실제 씬 전환 로직은 아직 없음.
    /// </summary>
    public class SceneLoader : Singleton<SceneLoader>, IManagerModule
    {
        public string ModuleName => "SceneLoader";

        public void Activate()
        {
            // TODO: 실제 씬 로드/전환 로직 구현 예정 (비동기 로드, 진행률 이벤트 등)
            Debug.Log("[SceneLoader] 활성화 완료 (자리만 잡아둠 - 실제 씬 로드 로직 없음)");
        }
    }
}
