using UnityEngine;

namespace Core
{
    public class GameManager : Singleton<GameManager>
    {
        [Header("매니저 로드 순서 (배열 순서대로 Activate 호출)")]
        [Tooltip("IManagerModule을 구현한 컴포넌트를 순서대로 넣으면 그 순서대로 로드/활성화됨")]
        [SerializeField] private MonoBehaviour[] orderedManagers;

        /// <summary>orderedManagers에 등록된 모든 매니저의 Activate 호출이 끝났는지 나타냅니다.</summary>
        public bool IsInitialized { get; private set; }

        private void Start()
        {
            ActivateManagersInOrder();
        }

        /// <summary>orderedManagers에 등록된 순서대로 각 매니저의 Activate()를 호출한다.</summary>
        [ContextMenu("매니저 순서대로 활성화 테스트")]
        public void ActivateManagersInOrder()
        {
            IsInitialized = false;

            if (orderedManagers == null || orderedManagers.Length == 0)
            {
                Debug.LogWarning("[GameManager] orderedManagers가 비어있습니다.");
                IsInitialized = true;
                return;
            }

            foreach (var mb in orderedManagers)
            {
                if (mb == null)
                {
                    Debug.LogWarning("[GameManager] 매니저 목록에 비어있는 항목이 있습니다.");
                    continue;
                }

                var module = mb as IManagerModule;
                if (module == null)
                {
                    Debug.LogWarning("[GameManager] " + mb.GetType().Name + "은(는) IManagerModule을 구현하지 않았습니다.");
                    continue;
                }

                Debug.Log("[GameManager] " + module.ModuleName + " 로드 및 활성화 시작...");
                module.Activate();
            }

            IsInitialized = true;
            Debug.Log("[GameManager] 모든 매니저 활성화 완료.");
        }

        public void Game()
        {
            Debug.Log("게임 매니저 싱글톤화 완료");
        }
    }
}
