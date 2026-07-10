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

    public static T Instance
    {
        get
        {
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
}

