using TMPro;
using UnityEngine;

/// <summary>SW 수정 : 적 위 냉각 단계와 지원 표식을 표시하며 판정·소유권은 변경하지 않는다.</summary>
/// <remarks>
/// 전용 VFX(Resources/UniqueEffectVFX)로 지원 표식·냉각 누적·방어 감소를 표시하고, 프리팹이 없을 때만 기존 글자 표시를 쓴다.
/// 프리팹 안의 "Head"(머리 위), "Body"(몸 중심), "Ground"(발밑) 자식은 적의 실제 크기에 맞춰 배치한다.
/// </remarks>
public sealed class EnemyEffectIndicator : MonoBehaviour
{
    private const string SupportMarkVfx = "UEVFX_SupportMark";
    private const string CoolingVfx = "UEVFX_CoolingStack";
    private const string ArmorBreakVfx = "UEVFX_ArmorBreak";
    private const int MaxCoolingPips = 3;

    private TMP_Text label;
    private int cooling;
    private bool marked;
    private bool defenseDown;
    private float coolingUntil, markUntil;
    private GameObject markVisual, coolingVisual, armorVisual;
    private GameObject[] coolingPips;
    private bool boundsMeasured;
    private float headHeight = 2.2f, bodyHeight = 1.1f, bodyRadius = 0.6f;
    private WBH_StatusEffectController offlineStatus;

    public void SetCooling(int count, float seconds)
    {
        cooling = count;
        coolingUntil = Time.unscaledTime + seconds;
        Refresh();
    }

    public void SetSupportMark(bool active, float seconds)
    {
        marked = active;
        markUntil = Time.unscaledTime + seconds;
        Refresh();
    }

    /// <summary>방어 감소 상태 표시. 네트워크는 서버의 상태 표시 마스크로, 오프라인은 실제 상태이상 목록으로 갱신한다.</summary>
    public void SetDefenseDown(bool active)
    {
        if (defenseDown == active) return;
        defenseDown = active;
        Refresh();
    }

    /// <summary>오프라인에서 실제 상태이상 목록을 따라 방어 감소 표시를 유지하도록 연결한다.</summary>
    public void TrackOfflineStatus(WBH_StatusEffectController status)
    {
        offlineStatus = status;
        if (status != null) SetDefenseDown(status.HasStatusEffect(WBH_StatusEffectType.DefenseDown));
    }

    private void Refresh()
    {
        MeasureBounds();
        bool needLabel = false;
        needLabel |= !Toggle(ref markVisual, SupportMarkVfx, marked);
        needLabel |= !Toggle(ref coolingVisual, CoolingVfx, cooling > 0);
        needLabel |= !Toggle(ref armorVisual, ArmorBreakVfx, defenseDown);
        if (coolingVisual != null && coolingVisual.activeSelf) ApplyCoolingPips();
        RefreshFallbackLabel(needLabel);
    }

    /// <summary>VFX를 켜거나 끈다. 켜야 하는데 프리팹이 없으면 false를 반환해 글자 표시로 대체한다.</summary>
    private bool Toggle(ref GameObject visual, string prefabName, bool active)
    {
        if (!active)
        {
            if (visual != null && visual.activeSelf) visual.SetActive(false);
            return true;
        }
        if (visual == null)
        {
            if (!UniqueEffectPresentation.TryGetVfx(prefabName, out GameObject prefab)) return false;
            visual = Instantiate(prefab, transform, false);
            visual.name = prefabName;
            visual.hideFlags = HideFlags.DontSave;
            PlaceAnchors(visual.transform);
        }
        if (!visual.activeSelf)
        {
            visual.SetActive(true);
            Restart(visual);
        }
        return true;
    }

    /// <summary>다시 켤 때 이전 생애의 고정 입자와 1회 등장 연출이 겹치지 않도록 처음부터 재생한다.</summary>
    private static void Restart(GameObject visual)
    {
        foreach (ParticleSystem system in visual.GetComponentsInChildren<ParticleSystem>())
        {
            system.Clear(false);
            system.Play(false);
        }
    }

    private void ApplyCoolingPips()
    {
        if (coolingPips == null)
        {
            coolingPips = new GameObject[MaxCoolingPips];
            for (int i = 0; i < MaxCoolingPips; i++)
                coolingPips[i] = FindDeep(coolingVisual.transform, "Pip" + (i + 1))?.gameObject;
        }
        for (int i = 0; i < MaxCoolingPips; i++)
            if (coolingPips[i] != null && coolingPips[i].activeSelf != i < cooling)
            {
                coolingPips[i].SetActive(i < cooling);
                if (i < cooling) Restart(coolingPips[i]);
            }
    }

    /// <summary>적의 실제 Collider/Renderer 크기로 머리 위·몸 중심·발밑 위치를 정한다(드론과 보스의 크기 차이 대응).</summary>
    private void MeasureBounds()
    {
        if (boundsMeasured) return;
        // Renderer 경계는 매 프레임 갱신되므로 우선 쓰고(방금 생성·이동한 적의 Collider는 물리 동기화 전일 수 있음),
        // 표시 메시가 없을 때만 Collider로 대체한다. 이펙트·글자 렌더러는 크기 측정에서 뺀다.
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer item in GetComponentsInChildren<Renderer>())
        {
            if (item is not (MeshRenderer or SkinnedMeshRenderer) || !item.enabled ||
                item.GetComponentInParent<ParticleSystem>() != null || item.GetComponent<TMP_Text>() != null) continue;
            if (!found) { bounds = item.bounds; found = true; }
            else bounds.Encapsulate(item.bounds);
        }
        if (!found)
            foreach (Collider item in GetComponentsInChildren<Collider>())
            {
                if (item.isTrigger || !item.enabled) continue;
                if (!found) { bounds = item.bounds; found = true; }
                else bounds.Encapsulate(item.bounds);
            }
        if (!found) return;
        boundsMeasured = true;
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        // 대형 적의 렌더러 경계는 실제 실루엣보다 높게 잡히기 쉬워 탑뷰에서 표식이 몸과 멀어지지 않도록 높이를 제한한다.
        float top = Mathf.Clamp(bounds.max.y - transform.position.y, 0.8f, 3.6f);
        headHeight = (top + 0.45f) / scaleY;
        bodyHeight = Mathf.Clamp(bounds.center.y - transform.position.y, 0.4f, 1.8f) / scaleY;
        bodyRadius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.35f, 3f); // 월드 m
    }

    private void PlaceAnchors(Transform root)
    {
        root.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        // 몸·발밑 배율은 궤도 반경과 배치 간격에만 쓰이고(입자 크기는 프리팹에서 고정), 머리 표식은 대형 적에서도 과하게 커지지 않게 제한한다.
        // 적 루트가 확대된 프리팹(보스 등)도 같은 월드 크기가 되도록 부모 배율을 상쇄한다.
        float sizeScale = Mathf.Clamp(bodyRadius / 0.6f, 0.75f, 3f);
        float inverseParent = 1f / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        Transform head = root.Find("Head");
        if (head != null) { head.localPosition = Vector3.up * headHeight; head.localScale = Vector3.one * (Mathf.Clamp(sizeScale, 0.85f, 1.25f) * inverseParent); }
        Transform body = root.Find("Body");
        if (body != null) { body.localPosition = Vector3.up * bodyHeight; body.localScale = Vector3.one * (sizeScale * inverseParent); }
        Transform ground = root.Find("Ground");
        if (ground != null) { ground.localPosition = Vector3.up * (0.05f * inverseParent); ground.localScale = Vector3.one * (Mathf.Clamp(sizeScale, 0.85f, 2f) * inverseParent); }
    }

    private static Transform FindDeep(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == childName) return child;
        return null;
    }

    private void RefreshFallbackLabel(bool needLabel)
    {
        bool show = needLabel && (cooling > 0 || marked || defenseDown);
        if (label == null && show)
        {
            var child = new GameObject("Effect Indicator");
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.up * 2.5f;
            label = child.AddComponent<TextMeshPro>();
            label.fontSize = 4f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
        }

        if (label == null)
            return;

        label.gameObject.SetActive(show);
        label.text = marked ? "+" : defenseDown ? "-" : cooling.ToString();
        label.color = marked ? new Color(1f, 0.9f, 0.2f) : defenseDown ? new Color(1f, 0.35f, 0.2f) : new Color(0.25f, 0.8f, 1f);
    }

    private void LateUpdate()
    {
        bool changed = false;
        if (cooling > 0 && Time.unscaledTime >= coolingUntil)
        {
            cooling = 0;
            changed = true;
        }
        if (marked && Time.unscaledTime >= markUntil)
        {
            marked = false;
            changed = true;
        }
        if (offlineStatus != null && offlineStatus.HasStatusEffect(WBH_StatusEffectType.DefenseDown) != defenseDown)
        {
            defenseDown = !defenseDown;
            changed = true;
        }

        if (changed)
            Refresh();
        if (label != null && label.gameObject.activeSelf && Camera.main != null)
            label.transform.rotation = Camera.main.transform.rotation;
    }

    private void OnDisable()
    {
        // 풀 반환·사망 시 이전 생애의 표시를 남기지 않는다.
        cooling = 0;
        marked = false;
        defenseDown = false;
        offlineStatus = null;
        foreach (GameObject visual in new[] { markVisual, coolingVisual, armorVisual })
            if (visual != null) visual.SetActive(false);
        if (label != null) label.gameObject.SetActive(false);
    }
}
