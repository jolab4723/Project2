using Mirror;
using UnityEngine;

/// <summary>서버에서 생성된 거너 스킬의 시각 복제본을 생성한다. 원본의 판정과 수명을 재사용한다.</summary>
public sealed class NetworkSkillPresentation_MirrorTest : NetworkBehaviour
{
    [SerializeField] private GunnerSkillController gunnerSkills;
    [SerializeField] private NetworkSkillVisual_MirrorTest visualPrefab;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (gunnerSkills != null) gunnerSkills.SkillObjectSpawned += HandleSpawned;
    }

    public override void OnStopServer()
    {
        if (gunnerSkills != null) gunnerSkills.SkillObjectSpawned -= HandleSpawned;
        base.OnStopServer();
    }

    [Server]
    private void HandleSpawned(GameObject instance, GameObject prefab)
    {
        if (visualPrefab == null)
        {
            Debug.LogError("[NetworkSkillPresentation] 스킬 외형 동기화 프리팹이 없습니다.", this);
            return;
        }
        var view = Instantiate(visualPrefab);
        if (!view.Initialize(instance, prefab))
        {
            Destroy(view.gameObject);
            Debug.LogError("[NetworkSkillPresentation] 등록되지 않은 원본 스킬 프리팹입니다.", this);
            return;
        }
        NetworkServer.Spawn(view.gameObject);
    }
}
