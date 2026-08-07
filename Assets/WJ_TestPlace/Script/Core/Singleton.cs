using UnityEngine;

/// <summary>
/// 모든 제네릭 싱글턴이 공유하는 Play 세션 종료 상태를 관리한다.
/// Domain Reload를 생략해도 새 Play 세션 시작 시 종료 상태를 초기화한다.
/// </summary>
internal static class SingletonLifecycleState
{
    /// <summary>
    /// 앱 종료/Play 모드 종료가 시작됐는지 나타낸다. OnApplicationQuit은 실제 빌드 종료뿐 아니라
    /// 에디터에서 Play 모드를 끌 때도 호출된다.
    /// 이 상태가 없으면 씬을 닫는 도중 다른 오브젝트의 OnDisable/OnDestroy가 이미 파괴된
    /// 싱글턴의 Instance를 참조할 때 새 GameObject를 즉석에서 다시 만들 수 있다.
    /// 그 오브젝트는 씬이 닫히는 중에 생성되어 정리되지 않고
    /// "Some objects were not cleaned up when closing the scene" 경고를 만들 수 있다.
    /// </summary>
    internal static bool IsShuttingDown { get; set; }

    /// <summary>
    /// 런타임 서브시스템 등록 시점에 이전 Play 세션의 종료 상태를 제거한다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        IsShuttingDown = false;
    }
}

/// <summary>
/// 제네릭 싱글턴 베이스 클래스. 
/// 상속받는 쪽에서 Awake가 더 필요하면 반드시 override 후 base.Awake()를 호출해야
/// 싱글턴 등록이 정상 동작한다.
/// </summary>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;

    public static T Instance
    {
        get
        {
            if (SingletonLifecycleState.IsShuttingDown)
            {
                Debug.LogWarning("[Singleton] 종료 중이라 " + typeof(T).Name + " 인스턴스를 새로 만들지 않고 null을 반환합니다.");
                return null;
            }

            if (instance == null)
            {
                instance = FindFirstObjectByType<T>();

                if (instance == null)
                {
                    GameObject obj = new GameObject(typeof(T).Name, typeof(T));
                    instance = obj.GetComponent<T>();
                }
            }
            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance != null && instance != this as T)
        {
            Debug.LogWarning("[Singleton] 이미 " + typeof(T).Name + " 인스턴스가 존재해서 중복 오브젝트를 제거합니다.");
            Destroy(gameObject);
            return;
        }

        instance = this as T;

        if (transform.parent != null && transform.root != null)
            DontDestroyOnLoad(transform.root.gameObject);
        else
            DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (instance == this as T)
            instance = null;
    }

    protected virtual void OnApplicationQuit()
    {
        SingletonLifecycleState.IsShuttingDown = true;
    }
}

