using Mirror;
using UnityEngine;

/// <summary>서버에서 생성된 거너 스킬의 시각 복제본을 생성한다. 원본의 판정과 수명을 재사용한다.</summary>
public sealed class NetworkSkillPresentation : NetworkBehaviour
{
    [SerializeField] private GunnerSkillController gunnerSkills;
    [SerializeField] private NetworkSkillVisual visualPrefab;
    private WBH_PlayerEffect playerEffect;
    private WBH_EffectSpawner effectSpawner;
    private FighterSkillController fighterSkills;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (gunnerSkills != null)
        {
            gunnerSkills.SkillObjectSpawned += HandleSpawned;
            gunnerSkills.SkillRangePresented += HandleSkillRange;
        }
        fighterSkills = GetComponent<FighterSkillController>();
        if (fighterSkills != null) fighterSkills.SkillRangePresented += HandleSkillRange;
        playerEffect = GetComponent<WBH_PlayerEffect>();
        if (playerEffect != null) playerEffect.WorldEffectRequested += HandleWorldEffect;
    }

    public override void OnStopServer()
    {
        if (gunnerSkills != null)
        {
            gunnerSkills.SkillObjectSpawned -= HandleSpawned;
            gunnerSkills.SkillRangePresented -= HandleSkillRange;
        }
        if (fighterSkills != null) fighterSkills.SkillRangePresented -= HandleSkillRange;
        if (playerEffect != null) playerEffect.WorldEffectRequested -= HandleWorldEffect;
        base.OnStopServer();
    }

    [Server]
    private void HandleWorldEffect(WBH_PlayerEffectCue cue, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        RpcPlayWorldEffect(cue, position, rotation, scale);
    }

    [ClientRpc]
    private void RpcPlayWorldEffect(WBH_PlayerEffectCue cue, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (isServer) return; // Host의 원본은 이미 같은 VFX/SFX를 재생한다.
        playerEffect ??= GetComponent<WBH_PlayerEffect>();
        if (playerEffect == null) return;
        effectSpawner ??= FindFirstObjectByType<WBH_EffectPoolManager>()?.GetComponent<WBH_EffectSpawner>();
        if (effectSpawner != null) playerEffect.Initialize(effectSpawner);
        playerEffect.PlayWorldEffect(cue, position, rotation, scale);
    }

    private void HandleExplosionRange(Vector3 position, float radius, Color color, float duration)
    {
        HandleSkillRange(position, Vector3.forward, radius, 360f, color, duration);
    }

    private void HandleSkillRange(Vector3 position, Vector3 direction, float radius, float angle, Color color, float duration)
    {
        if (this != null && isServer && NetworkServer.active)
            RpcShowSkillRange(position, direction, radius, angle, color, duration);
    }

    [ClientRpc]
    private void RpcShowSkillRange(Vector3 position, Vector3 direction, float radius, float angle, Color color, float duration)
    {
        if (!isServer) SkillRangeVisual.ShowSector(position, direction, radius, angle, color, duration);
    }

    [Server]
    private void HandleSpawned(GameObject instance, GameObject prefab)
    {
        if (instance != null && instance.TryGetComponent<GunnerArcProjectile>(out var projectile))
            projectile.ExplosionRangePresented += HandleExplosionRange;
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
