using UnityEngine;

public class YJ_MeshTrail : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private YJ_MeshTrailTut meshTrailTut;
    [SerializeField] private Material mat;
    [SerializeField] private float activeTime = 2f;
    [SerializeField] private float meshRefreshRate = 0.1f;
    [SerializeField] private float meshDestroyDelay = 0.5f;

    private bool pendingPlay;

    // SW 수정: 이펙트 풀은 켠 뒤에 시전자를 넘겨 Play하므로, 같은 프레임의 LateUpdate에서 대상을 정하고 재생한다.
    private void OnEnable()
    {
        pendingPlay = true;
    }

    private void LateUpdate()
    {
        if (!pendingPlay)
            return;

        pendingPlay = false;
        PlayTrail();
    }

    private void PlayTrail()
    {
        FindPlayer();

        meshTrailTut?.Trail(mat, activeTime, meshRefreshRate, meshDestroyDelay);
    }

    // SW 수정: 첫 번째 T_PlayerController는 멀티에서 원격 플레이어일 수 있고 풀링된 이펙트에 계속 남으므로,
    // 재생할 때마다 이 이펙트를 재생한 시전자(WBH_Effect.Owner)에게 잔상을 붙인다. 시전자가 없으면 재생하지 않는다.
    private void FindPlayer()
    {
        Transform owner = GetComponentInParent<WBH_Effect>()?.Owner;
        player = owner != null ? owner.gameObject : null;
        meshTrailTut = owner != null ? owner.GetComponentInChildren<YJ_MeshTrailTut>(true) : null;
    }
}
