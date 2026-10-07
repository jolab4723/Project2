using Mirror;
using UnityEngine;

/// <summary>원본 스킬 생성물의 위치와 수명을 전달한다. 복제본에는 피해나 충돌 처리가 없다.</summary>
public sealed class NetworkSkillVisual : NetworkBehaviour
{
    [SerializeField] private GameObject[] sourcePrefabs;
    [SerializeField] private GameObject[] visualPrefabs;
    [SyncVar] private int prefabIndex = -1;
    private GameObject serverSource;

    [Server]
    public bool Initialize(GameObject instance, GameObject prefab)
    {
        prefabIndex = System.Array.IndexOf(sourcePrefabs, prefab);
        if (instance == null || prefabIndex < 0 || prefabIndex >= visualPrefabs.Length || visualPrefabs[prefabIndex] == null)
            return false;
        serverSource = instance;
        CopyPose();
        return true;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (isServer) return; // Host는 원본의 시각을 이미 재생하고 있다.
        if (prefabIndex < 0 || prefabIndex >= visualPrefabs.Length || visualPrefabs[prefabIndex] == null)
        {
            Debug.LogError("[NetworkSkillVisual] 등록되지 않은 스킬 외형입니다.", this);
            return;
        }
        GameObject view = Instantiate(visualPrefabs[prefabIndex], transform);
        view.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        view.transform.localScale = Vector3.one;
    }

    private void LateUpdate()
    {
        if (!isServer) return;
        if (serverSource == null)
        {
            NetworkServer.Destroy(gameObject);
            return;
        }
        CopyPose();
    }

    private void CopyPose()
    {
        transform.SetPositionAndRotation(serverSource.transform.position, serverSource.transform.rotation);
        transform.localScale = serverSource.transform.lossyScale;
    }
}
