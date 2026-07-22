using UnityEngine;

/// <summary>
/// 제네릭 싱글턴 베이스 클래스. 
/// 상속받는 쪽에서 Awake가 더 필요하면 반드시 override 후 base.Awake()를 호출해야
/// 싱글턴 등록이 정상 동작한다.
/// </summary>
/// 
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;

    /// <summary>
    /// 앱 종료/Play 모드 종료가 시작됐는지. OnApplicationQuit은 실제 빌드 종료뿐 아니라
    /// 에디터에서 Play 모드를 끌 때도 호출된다.
    /// !! 이게 없으면: 씬을 닫는 도중 다른 오브젝트의 OnDisable/OnDestroy가 이미 파괴된 이
    ///    싱글턴의 Instance를 참조하는 순간, 죽은 참조를 null로 오인해서 새 GameObject를
    ///    즉석에서 또 만들어버린다. 그 오브젝트는 씬이 닫히는 도중에 생겨서 정리가 안 되고,
    ///    "Some objects were not cleaned up when closing the scene" 경고로 나타난다.
    /// </summary>
    private static bool isShuttingDown;

    public static T Instance
    {
        get
        {
            if (isShuttingDown)
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
        isShuttingDown = true;
    }
}

