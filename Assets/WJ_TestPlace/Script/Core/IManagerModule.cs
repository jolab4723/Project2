using UnityEngine;

namespace Core
{
    /// <summary>
    /// GameManager가 순서대로 로드/활성화시킬 수 있는 매니저가 구현하는 공통 인터페이스.
    /// MonoBehaviour는 인터페이스를 직접 SerializeField로 못 받아서, GameManager 쪽에서는
    /// MonoBehaviour 필드로 받은 뒤 이 인터페이스로 캐스팅해서 사용한다.
    /// </summary>
    public interface IManagerModule
    {
        /// <summary>로그 등에 표시할 이름.</summary>
        string ModuleName { get; }

        /// <summary>로드/활성화 처리. 지금은 동기 호출만 지원 (비동기 필요해지면 나중에 확장).</summary>
        void Activate();
    }
}
