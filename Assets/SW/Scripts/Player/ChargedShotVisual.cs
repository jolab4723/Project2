using UnityEngine;

/// <summary>SW 수정: 충전 탄의 작은 황금 고리만 표시하고 풀 반환 시 생성 머터리얼을 함께 제거한다.</summary>
public sealed class ChargedShotVisual : MonoBehaviour
{
    private Material ownedMaterial;
    public static GameObject Create(Transform parent)
    {
        var visual = new GameObject("Charged Shot Ring");
        visual.transform.SetParent(parent, false);
        visual.AddComponent<ChargedShotVisual>();
        return visual;
    }
    private void Awake()
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        ownedMaterial = new Material(shader);
        var line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = ownedMaterial; line.useWorldSpace = false; line.loop = true;
        line.positionCount = 16; line.widthMultiplier = 0.05f;
        line.startColor = line.endColor = new Color(1f, 0.7f, 0.1f);
        float scale = Mathf.Max(0.001f, transform.lossyScale.x);
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (0.2f / scale));
        }
        line.widthMultiplier /= scale;
    }
    private void OnDestroy() { if (ownedMaterial != null) Destroy(ownedMaterial); }
}
