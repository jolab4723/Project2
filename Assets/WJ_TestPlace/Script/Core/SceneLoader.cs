using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core
{
    /// <summary>
    /// 씬 전환을 전담하는 매니저. 지금은 싱글플레이 전용(SceneManager.LoadSceneAsync)만 지원한다.
    ///
    /// !! 멀티플레이(Mirror의 ServerChangeScene) 분기는 나중에 LoadScene 내부 구현에 추가할 예정.
    ///    호출부(LoadScene(sceneName))의 시그니처는 그대로 유지되므로, 나중에 붙여도 호출부는 안 바뀐다.
    ///
    /// !! 로딩 화면 자체는 여기서 직접 띄우지 않는다. OnLoadProgress/OnBeforeSceneUnload/OnSceneLoaded
    ///    이벤트만 발행하고, 실제 로딩 UI(로딩바, 로딩 씬 표시 등)는 이 이벤트를 구독하는 쪽에서 담당한다.
    /// </summary>
    public class SceneLoader : Singleton<SceneLoader>, IManagerModule
    {
        [Tooltip("로딩 화면이 최소 이 시간(초) 동안은 유지되도록 한다. 로드가 순식간에 끝나도 화면이 깜빡이지 않게.")]
        [SerializeField] private float minLoadingScreenSeconds = 0.3f;

        public string ModuleName => "SceneLoader";

        /// <summary>지금 씬을 불러오는 중인지. true인 동안 LoadScene 재호출은 무시된다.</summary>
        public bool IsLoading { get; private set; }

        /// <summary>현재 활성화된 씬 이름.</summary>
        public string CurrentSceneName { get; private set; }

        /// <summary>로딩 진행률(0~1)이 바뀔 때마다 발행. 로딩 화면 UI가 프로그레스바 갱신에 사용.</summary>
        public event Action<float> OnLoadProgress;

        /// <summary>씬 언로드를 시작하기 직전에 발행(인자: 떠나는 씬 이름). 저장 등 "씬을 떠나기 전에 할 일"을 구독시킨다.</summary>
        public event Action<string> OnBeforeSceneUnload;

        /// <summary>새 씬이 완전히 로드/활성화된 직후 발행(인자: 새 씬 이름). 불러오기 등 "씬 진입 후 할 일"을 구독시킨다.</summary>
        public event Action<string> OnSceneLoaded;

        public void Activate()
        {
            CurrentSceneName = SceneManager.GetActiveScene().name;
            Debug.Log("[SceneLoader] 활성화 완료 (현재 씬: " + CurrentSceneName + ")");
        }

        /// <summary>sceneName으로 전환한다. 이미 로딩 중이면 무시한다.</summary>
        public void LoadScene(string sceneName)
        {
            if (IsLoading)
            {
                Debug.LogWarning("[SceneLoader] 이미 씬을 불러오는 중이라 " + sceneName + " 요청을 무시합니다.");
                return;
            }

            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            IsLoading = true;
            string previousSceneName = CurrentSceneName;

            OnBeforeSceneUnload?.Invoke(previousSceneName);
            OnLoadProgress?.Invoke(0f);

            float startTime = Time.unscaledTime;

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                Debug.LogError("[SceneLoader] " + sceneName + " 씬을 찾을 수 없습니다 (Build Settings에 등록되어 있는지 확인).");
                IsLoading = false;
                yield break;
            }

            operation.allowSceneActivation = false;

            // Unity는 activation을 허용하기 전까지 progress를 0.9에서 멈춰두므로, 0~1로 다시 정규화한다.
            while (operation.progress < 0.9f)
            {
                OnLoadProgress?.Invoke(operation.progress / 0.9f);
                yield return null;
            }

            OnLoadProgress?.Invoke(1f);

            float elapsed = Time.unscaledTime - startTime;
            if (elapsed < minLoadingScreenSeconds)
                yield return new WaitForSecondsRealtime(minLoadingScreenSeconds - elapsed);

            operation.allowSceneActivation = true;

            while (!operation.isDone)
                yield return null;

            CurrentSceneName = sceneName;
            IsLoading = false;

            OnSceneLoaded?.Invoke(sceneName);
        }
    }
}
