using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class YJ_PortalEffect : MonoBehaviour
{
    private const string TeleportEffectResourcePath = "Effects/PortalTeleport";

    [Header("Effect")]
    [SerializeField] private GameObject teleportEffectPrefab;
    [SerializeField] private Vector3 effectPositionOffset;
    [SerializeField, Min(0f)] private float fallbackDuration = 1f;

    // SW 수정: 멀티는 여러 플레이어가 같은 포탈에 들어가므로 재생 중 여부를 플레이어마다 기록한다.
    private readonly System.Collections.Generic.HashSet<GameObject> playingPlayers = new();

    /// <summary>
    /// 플레이어를 숨기고 텔레포트 이펙트를 한 번 재생합니다.
    /// 이펙트가 끝나면 호출 측에서 씬 전환을 이어갈 수 있습니다.
    /// </summary>
    public IEnumerator PlayOnce(GameObject player)
    {
        if (player == null || !playingPlayers.Add(player))
            yield break;

        HidePlayer(player);

        GameObject effectPrefab = ResolveEffectPrefab();
        if (effectPrefab == null)
        {
            playingPlayers.Remove(player);
            yield break;
        }

        Vector3 effectPosition = player.transform.position + effectPositionOffset;
        GameObject effectInstance = Instantiate(
            effectPrefab,
            effectPosition,
            Quaternion.identity);

        ParticleSystem[] particleSystems =
            effectInstance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(false);
        }

        float effectDuration = CalculateEffectDuration(particleSystems);
        if (effectDuration > 0f)
            yield return new WaitForSeconds(effectDuration);

        if (effectInstance != null)
            Destroy(effectInstance);

        playingPlayers.Remove(player);
    }

    private GameObject ResolveEffectPrefab()
    {
        if (teleportEffectPrefab == null)
            teleportEffectPrefab = Resources.Load<GameObject>(TeleportEffectResourcePath);

        if (teleportEffectPrefab == null)
        {
            Debug.LogError(
                $"[YJ_PortalEffect] Resources/{TeleportEffectResourcePath}.prefab을 찾을 수 없습니다.",
                this);
        }

        return teleportEffectPrefab;
    }

    private static void HidePlayer(GameObject player)
    {
        Renderer[] playerRenderers = player.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer playerRenderer in playerRenderers)
        {
            if (playerRenderer != null)
                playerRenderer.enabled = false;
        }
    }

    private float CalculateEffectDuration(ParticleSystem[] particleSystems)
    {
        float longestDuration = fallbackDuration;

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            float particleDuration =
                main.startDelay.constantMax +
                main.duration +
                main.startLifetime.constantMax;
            longestDuration = Mathf.Max(longestDuration, particleDuration);
        }

        return longestDuration;
    }
}
