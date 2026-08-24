using System.Collections.Generic;
using UnityEngine;

public class CameraOcclusionFader : MonoBehaviour
{
    [Header("설정")]
    public Transform target;
    public LayerMask obstacleLayer;
    [Range(0f, 1f)] public float fadedAlpha = 0.3f;
    public float fadeSpeed = 5f;

    [Header("투명화 전용 머티리얼")]
    [Tooltip("에디터에서 미리 생성한 Surface Type: Transparent 머티리얼을 넣어주세요.")]
    public Material transparentMaterialTemplate;

    private Dictionary<Renderer, OcclusionData> currentlyOccluding = new Dictionary<Renderer, OcclusionData>();
    private List<Renderer> toRestore = new List<Renderer>();

    // 최적화: RaycastNonAlloc을 위한 배열 (최대 10개의 장애물 동시 감지)
    private RaycastHit[] hitResults = new RaycastHit[10];
    
    // 최적화: 셰이더 프로퍼티 ID 캐싱 (매 프레임 무거운 문자열 검색 방지)
    private readonly int baseColorID = Shader.PropertyToID("_BaseColor");

    private class OcclusionData
    {
        public Material originalMaterial;
        public Material fadeMaterial;
        public float currentAlpha = 1f;
    }

    void Start()
    {
        if (target != null)
            return;

        target = GameObject.FindGameObjectWithTag("Player").transform;
    }

    // 최적화: FixedUpdate(물리 프레임) 대신 LateUpdate(렌더링 직전) 사용
    void LateUpdate()
    {
        if (target == null || transparentMaterialTemplate == null) return;

        Vector3 direction = target.position - transform.position;
        float distance = direction.magnitude;

        // 최적화: 매 프레임 가비지를 생성하는 RaycastAll 대신 RaycastNonAlloc 사용
        int hitCount = Physics.RaycastNonAlloc(transform.position, direction.normalized, hitResults, distance, obstacleLayer);
        
        List<Renderer> currentHits = new List<Renderer>();

        for (int i = 0; i < hitCount; i++)
        {
            Renderer hitRenderer = hitResults[i].collider.GetComponent<Renderer>();
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

                // 알파값 조절
                OcclusionData occData = currentlyOccluding[hitRenderer];
                occData.currentAlpha = Mathf.Lerp(occData.currentAlpha, fadedAlpha, Time.deltaTime * fadeSpeed);

                if (occData.fadeMaterial.HasProperty(baseColorID))
                {
                    Color color = occData.fadeMaterial.GetColor(baseColorID);
                    color.a = occData.currentAlpha;
                    occData.fadeMaterial.SetColor(baseColorID, color);
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

                if (pair.Value.fadeMaterial.HasProperty(baseColorID))
                {
                    Color color = pair.Value.fadeMaterial.GetColor(baseColorID);
                    color.a = pair.Value.currentAlpha;
                    pair.Value.fadeMaterial.SetColor(baseColorID, color);
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