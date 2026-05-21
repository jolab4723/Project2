using UnityEngine;

// T는 반드시 MonoBehaviour를 상속받은 클래스여야 합니다.
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object();
    private static bool _applicationIsQuitting = false;

    public static T Instance
    {
        get
        {
            // 게임 종료 중일 때 싱글톤을 호출하면 유니티 에디터가 멈추거나 
            // 유령 오브젝트가 남는 현상을 방지합니다.
            if (_applicationIsQuitting)
            {
                Log.Warning($"[Singleton] {typeof(T)} 인스턴스는 이미 앱이 종료되어 파괴되었습니다. null을 반환합니다.");
                return null;
            }

            // 멀티스레드 환경에서도 안전하게 싱글톤을 확보하기 위한 락(Lock)
            lock (_lock)
            {
                if (_instance == null)
                {
                    // 1. 씬에 이미 배치된 인스턴스가 있는지 확인
                    _instance = (T)FindFirstObjectByType(typeof(T));

                    // 2. 씬에 없다면 새로 생성
                    if (_instance == null)
                    {
                        GameObject singletonObject = new GameObject();
                        _instance = singletonObject.AddComponent<T>();
                        singletonObject.name = $"{typeof(T)} (Singleton)";

                        // 씬이 바뀌어도 파괴되지 않도록 설정
                        DontDestroyOnLoad(singletonObject);
                    }
                }

                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            // 중복 생성된 매니저가 있다면 파괴
            Destroy(gameObject);
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    protected virtual void OnDestroy()
    {
        // 씬이 전환되면서 파괴될 때 앱 종료 상태가 아니라면 락 해제용
        if (_instance == this)
        {
            _instance = null;
        }
    }
}