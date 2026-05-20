using UnityEngine;
using UnityEngine.Pool;

public class ComponentPool<T> where T : Component
{
    private readonly T _prefab;
    private readonly Transform _rootParent;

    private readonly IObjectPool<T> _pool;

    public ComponentPool(T prefab, int defaultCapacity = 10, int maxPoolSize = 50, Transform parent = null)
    {
        _prefab = prefab;

        // 하이어라키 정리를 위한 루트 오브젝트 생성
        if (parent == null)
        {
            _rootParent = new GameObject($"{typeof(T).Name}_PoolRoot").transform;
        }
        else
        {
            _rootParent = parent;
        }

        // 유니티 내장 ObjectPool 초기화
        _pool = new ObjectPool<T>(
            createFunc: CreateInstance,          // 풀에 객체가 없을 때 새로 생성하는 함수
            actionOnGet: OnGetFromPool,          // 풀에서 꺼낼 때 실행할 함수
            actionOnRelease: OnReleaseToPool,    // 풀에 반납할 때 실행할 함수
            actionOnDestroy: OnDestroyPoolItem,  // 풀이 꽉 찬 상태에서 반납되어 오브젝트를 파괴할 때 실행할 함수
            collectionCheck: true,               // 중복 반납 검사 여부 (에디터 에러 방지용)
            defaultCapacity: defaultCapacity,    // 내부 리스트의 초기 용량
            maxSize: maxPoolSize                 // 풀이 가질 수 있는 최대 객체 개수
        );
    }

    // 1. 객체 생성 로직
    private T CreateInstance()
    {
        T instance = Object.Instantiate(_prefab, _rootParent);
        return instance;
    }

    // 2. 풀에서 꺼낼 때 (활성화)
    private void OnGetFromPool(T instance)
    {
        instance.gameObject.SetActive(true);
    }

    // 3. 풀에 반납할 때 (비활성화)
    private void OnReleaseToPool(T instance)
    {
        if (instance == null) return;

        instance.gameObject.SetActive(false);
        instance.transform.SetParent(_rootParent);
    }

    // 4. 최대 크기를 초과하여 반납된 객체를 파괴할 때
    private void OnDestroyPoolItem(T instance)
    {
        if (instance != null)
        {
            Object.Destroy(instance.gameObject);
        }
    }

    /// <summary>
    /// 풀에서 객체를 하나 꺼내옵니다.
    /// </summary>
    public T Get(Vector3 position, Quaternion rotation)
    {
        T instance = _pool.Get();
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    /// <summary>
    /// 사용이 끝난 객체를 풀에 반납합니다.
    /// </summary>
    public void Release(T instance)
    {
        _pool.Release(instance);
    }
}