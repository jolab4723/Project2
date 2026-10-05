using ItemSystem;
using UnityEngine;

/// <summary>실제 오라 SO의 반경을 표시하고 타인 범위 옵션은 렌더링에만 적용합니다.</summary>
/// <remarks>SW 수정 : 범위 안 플레이어의 버프 오라는 각 플레이어의 실제 버프 목록을 따라 UniqueEffectPresentation이 표시한다.
/// 이 컴포넌트는 소지자 중심의 판정 범위 링만 담당한다.</remarks>
public sealed class PlayerAuraVisual : MonoBehaviour
{
    private Transform follow;
    private GameObject visuals;
    private Material ringMaterial;
    private bool isLocal;

    public void Bind(Transform owner, FieldAuraUniqueEffectSO aura, bool local)
    {
        follow = owner;
        isLocal = local;
        visuals = new GameObject("Range");
        visuals.transform.SetParent(transform, false);
        var ring = visuals.AddComponent<AreaRingVisual>();
        ring.SetColor(aura.areaVisualColor);
        ring.SetRadius(aura.radius);
        ringMaterial = visuals.GetComponent<LineRenderer>().sharedMaterial;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (follow == null) { Destroy(gameObject); return; }
        transform.position = follow.position;
        bool visible = isLocal || Core.SettingManager.Instance == null ||
            Core.SettingManager.Instance.GetData().showAlliedBuffRanges;
        if (visuals != null && visuals.activeSelf != visible) visuals.SetActive(visible);
    }

    private void OnDestroy()
    {
        // AreaRingVisual가 이 표시 인스턴스에 만든 머티리얼의 수명도 함께 끝낸다.
        if (ringMaterial != null) Destroy(ringMaterial);
    }
}
