using System.Collections.Generic;
using UnityEngine;

public class CameraOcclusionFader : MonoBehaviour
{
    [Header("설정")]
    public Transform target;
    public LayerMask obstacleLayer;
    [Range(0f, 1f)] public float fadedAlpha = 0.3f;
    public float fadeSpeed = 5f;

    [Header("투명화 전용 머티리얼 (Inspector에서 할당)")]
    [Tooltip("에디터에서 미리 생성한 Surface Type: Transparent 머티리얼을 넣어주세요.")]
    public Material transparentMaterialTemplate;

    private Dictionary<Renderer, OcclusionData> currentlyOccluding = new Dictionary<Renderer, OcclusionData>();
    private List<Renderer> toRestore = new List<Renderer>();

    private class OcclusionData
    {
        public Material originalMaterial;
        public Material fadeMaterial;
        public float currentAlpha = 1f;
    }

    void Update()
    {
        if (target == null || transparentMaterialTemplate == null) return;

        Vector3 direction = target.position - transform.position;
        float distance = direction.magnitude;

        RaycastHit[] hits = Physics.RaycastAll(transform.position, direction.normalized, distance, obstacleLayer);
        List<Renderer> currentHits = new List<Renderer>();

        foreach (RaycastHit hit in hits)
        {
            Renderer hitRenderer = hit.collider.GetComponent<Renderer>();
            if (hitRenderer != null)
            {
                currentHits.Add(hitRenderer);

                // 처음 가려지는 장애물이면 투명 머티리얼로 교체
                if (!currentlyOccluding.ContainsKey(hitRenderer))
                {
                    OcclusionData data = new OcclusionData();
                    data.originalMaterial = hitRenderer.material;

                    // Inspector에서 넣어둔 투명 전용 머티리얼의 복사본을 생성
                    data.fadeMaterial = new Material(transparentMaterialTemplate);

                    hitRenderer.material = data.fadeMaterial;
                    currentlyOccluding[hitRenderer] = data;
                }

                // 알파값 조절 (URP Lit 셰이더의 _BaseColor 사용)
                OcclusionData occData = currentlyOccluding[hitRenderer];
                occData.currentAlpha = Mathf.Lerp(occData.currentAlpha, fadedAlpha, Time.deltaTime * fadeSpeed);

                if (occData.fadeMaterial.HasProperty("_BaseColor"))
                {
                    Color color = occData.fadeMaterial.GetColor("_BaseColor");
                    color.a = occData.currentAlpha;
                    occData.fadeMaterial.SetColor("_BaseColor", color);
                }
            }
        }

        // 안 가려지게 된 장애물들 복구
        toRestore.Clear();
        foreach (var pair in currentlyOccluding)
        {
            if (!currentHits.Contains(pair.Key))
            {
                pair.Value.currentAlpha = Mathf.Lerp(pair.Value.currentAlpha, 1f, Time.deltaTime * fadeSpeed);

                if (pair.Value.fadeMaterial.HasProperty("_BaseColor"))
                {
                    Color color = pair.Value.fadeMaterial.GetColor("_BaseColor");
                    color.a = pair.Value.currentAlpha;
                    pair.Value.fadeMaterial.SetColor("_BaseColor", color);
                }

                // 복구가 끝나면 원래(Opaque) 머티리얼로 완벽 복귀
                if (pair.Value.currentAlpha >= 0.95f)
                {
                    pair.Key.material = pair.Value.originalMaterial;
                    Destroy(pair.Value.fadeMaterial); // 임시 투명 머티리얼 제거
                    toRestore.Add(pair.Key);
                }
            }
        }

        foreach (var renderer in toRestore)
        {
            currentlyOccluding.Remove(renderer);
        }
    }
}