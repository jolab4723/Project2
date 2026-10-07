using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>실제 오라 SO의 반경을 표시하고 타인 범위 옵션은 렌더링에만 적용합니다.</summary>
/// <remarks>SW 수정 : 범위 안 플레이어의 버프 오라는 각 플레이어의 실제 버프 목록을 따라 UniqueEffectPresentation이 표시한다.
/// 이 컴포넌트는 소지자 중심의 판정 범위 링만 담당한다.</remarks>
public sealed class PlayerAuraVisual : MonoBehaviour
{
    // 아군 대상 오라는 바깥으로 퍼지는 빛 장막, 적 대상 오라는 안쪽으로 빨아들이는 장막을 쓴다(반경 1m 기준 제작).
    private const string AllyRangeVfx = "UEVFX_AuraRange";
    private const string HostileRangeVfx = "UEVFX_AuraRangeHostile";
    private const float SlowScanInterval = 0.25f;
    // 4인이 범위 오라를 함께 켜도 화면을 덮지 않도록 다른 플레이어의 범위는 본인 범위보다 어둡게 그린다.
    private const float OtherPlayerRangeIntensity = 0.55f;

    private Transform follow;
    private GameObject visuals;
    private Material ringMaterial;
    private bool isLocal;
    private FieldAuraUniqueEffectSO aura;
    private bool presentHostileSlow;
    private float nextSlowScanAt;
    private readonly HashSet<WBH_EnemyStatusEffectController> slowedByAura = new();
    private readonly List<WBH_EnemyStatusEffectController> slowScratch = new();
    private static readonly Collider[] OverlapBuffer = new Collider[64];

    public void Bind(Transform owner, FieldAuraUniqueEffectSO aura, bool local)
    {
        follow = owner;
        isLocal = local;
        this.aura = aura;
        // 네트워크에서는 서버가 적 상태 표시 마스크로 둔화 연출을 복제하므로 오프라인 소지자 표시만 직접 갱신한다.
        presentHostileSlow = aura.targetEnemies && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active;

        if (UniqueEffectPresentation.TryGetVfx(aura.targetEnemies ? HostileRangeVfx : AllyRangeVfx, out GameObject prefab))
        {
            visuals = Instantiate(prefab, transform, false);
            visuals.name = "Range";
            visuals.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var fit = visuals.GetComponent<UniqueEffectRadiusFit>();
            if (fit != null)
            {
                fit.Fit(aura.radius, Mathf.Abs(transform.lossyScale.x));
                fit.SetTint(aura.areaVisualColor, local ? 1f : OtherPlayerRangeIntensity);
            }
            else visuals.transform.localScale = Vector3.one * aura.radius;
        }
        else
        {
            // 전용 VFX가 없을 때만 기존 원형 선을 쓴다.
            visuals = new GameObject("Range");
            visuals.transform.SetParent(transform, false);
            var ring = visuals.AddComponent<AreaRingVisual>();
            ring.SetColor(aura.areaVisualColor);
            ring.SetRadius(aura.radius);
            ringMaterial = visuals.GetComponent<LineRenderer>().sharedMaterial;
        }
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (follow == null) { Destroy(gameObject); return; }
        transform.position = follow.position;
        bool visible = isLocal || Core.SettingManager.Instance == null ||
            Core.SettingManager.Instance.GetData().showAlliedBuffRanges;
        if (visuals != null && visuals.activeSelf != visible) visuals.SetActive(visible);
        if (presentHostileSlow && Time.time >= nextSlowScanAt)
        {
            nextSlowScanAt = Time.time + SlowScanInterval;
            RefreshHostileSlowPresentation();
        }
    }

    /// <summary>
    /// 오프라인에서 이 오라의 둔화 버프를 실제로 받은 적에게 공용 둔화 연출을 재생한다.
    /// 범위를 다시 판정하지 않고 적 버프 목록에 이 오라가 들어 있는지만 확인한다.
    /// </summary>
    private void RefreshHostileSlowPresentation()
    {
        slowScratch.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position, aura.radius + 1.5f, OverlapBuffer,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var buffs = OverlapBuffer[i] != null ? OverlapBuffer[i].GetComponentInParent<EnemyBuffManager>() : null;
            if (buffs == null || !HasThisAura(buffs)) continue;
            var status = buffs.GetComponent<WBH_EnemyStatusEffectController>();
            if (status == null || slowScratch.Contains(status)) continue;
            slowScratch.Add(status);
            status.PlayStatusEffect(WBH_StatusEffectType.Slow);
        }

        foreach (var status in slowedByAura)
            if (status != null && !slowScratch.Contains(status) && !status.HasStatusEffect(WBH_StatusEffectType.Slow))
                status.StopStatusEffect(WBH_StatusEffectType.Slow);
        slowedByAura.Clear();
        foreach (var status in slowScratch) slowedByAura.Add(status);
    }

    private bool HasThisAura(EnemyBuffManager buffs)
    {
        foreach (var buff in buffs.ActiveBuffs)
            if (ReferenceEquals(buff?.source, aura)) return true;
        return false;
    }

    private void OnDestroy()
    {
        foreach (var status in slowedByAura)
            if (status != null && !status.HasStatusEffect(WBH_StatusEffectType.Slow))
                status.StopStatusEffect(WBH_StatusEffectType.Slow);
        slowedByAura.Clear();
        // AreaRingVisual가 이 표시 인스턴스에 만든 머티리얼의 수명도 함께 끝낸다.
        if (ringMaterial != null) Destroy(ringMaterial);
    }
}
