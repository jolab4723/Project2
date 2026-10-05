using ItemSystem;
using TMPro;
using UnityEngine;

/// <summary>실제 오라 SO의 반경을 표시하고 타인 범위 옵션은 렌더링에만 적용합니다.</summary>
public sealed class PlayerAuraVisual : MonoBehaviour
{
    private Transform follow;
    private GameObject visuals;
    private Material ringMaterial;
    private bool isLocal;
    private Transform rotatingText;

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
        if (aura.name == "UE_HelloWorldBeacon")
        {
            rotatingText = new GameObject("Hello World").transform;
            rotatingText.SetParent(visuals.transform, false);
            for (int index = 0; index < 4; index++)
            {
                float angle = index * 90f;
                var label = new GameObject("Hello world", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                label.transform.SetParent(rotatingText, false);
                label.transform.localPosition = Quaternion.Euler(0, angle, 0) * new Vector3(0, .12f, aura.radius);
                label.transform.localRotation = Quaternion.Euler(90, angle, 0);
                label.rectTransform.sizeDelta = new Vector2(5, 1);
                label.text = "Hello world";
                label.fontSize = 5;
                label.alignment = TextAlignmentOptions.Center;
                label.color = aura.areaVisualColor;
            }
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
        if (visible && rotatingText != null)
            rotatingText.localRotation = Quaternion.Euler(0, Time.time * 12f, 0);
    }

    private void OnDestroy()
    {
        // AreaRingVisual가 이 표시 인스턴스에 만든 머티리얼의 수명도 함께 끝낸다.
        if (ringMaterial != null) Destroy(ringMaterial);
    }
}
