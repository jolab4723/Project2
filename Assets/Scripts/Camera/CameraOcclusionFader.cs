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
    private static readonly int BaseMapID = Shader.PropertyToID("_BaseMap");

    // SW 수정: 그림자만 그리는 보조 머티리얼에서 끌 화면 출력 패스(LightMode)들이다.
    private static readonly string[] NonShadowPasses =
        { "UniversalForward", "UniversalForwardOnly", "SRPDefaultUnlit", "UniversalGBuffer", "DepthOnly", "DepthNormals", "DepthNormalsOnly", "Meta", "MotionVectors" };

    private class OcclusionData
    {
        // SW 수정: 슬롯 전체를 공유 머티리얼로 보존해 복구 시 인스턴스를 만들지 않는다.
        public Material[] originalMaterials;
        public Material fadeMaterial;
        public Material shadowMaterial;
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
                    data.originalMaterials = hitRenderer.sharedMaterials;
                    Material original = data.originalMaterials.Length > 0 ? data.originalMaterials[0] : null;

                    // 원본 Lit 재질 또는 Inspector의 투명 전용 머티리얼을 복사한다.
                    data.fadeMaterial = CreateFadeMaterial(original);
                    // SW 수정: 템플릿 색으로 바뀌어 검게 보이지 않도록 원래 텍스처와 색을 옮긴다.
                    CopyAppearance(original, data.fadeMaterial);

                    // SW 수정: 투명 템플릿에는 그림자 패스가 없어 페이드 중 그림자가 사라진다.
                    // 단일 메시면 원래 머티리얼에서 그림자 패스만 남긴 머티리얼을 덧붙여 같은 메시의 그림자를 유지한다.
                    // ponytail: 서브메시가 여러 개인 물체는 그림자를 유지하지 않는다. 필요해지면 슬롯별 그림자 전용 렌더러를 둔다.
                    if (data.originalMaterials.Length == 1 && original != null && original.FindPass("ShadowCaster") >= 0)
                    {
                        data.shadowMaterial = new Material(original);
                        foreach (string pass in NonShadowPasses)
                            data.shadowMaterial.SetShaderPassEnabled(pass, false);
                        hitRenderer.sharedMaterials = new[] { data.fadeMaterial, data.shadowMaterial };
                    }
                    else
                    {
                        Material[] faded = (Material[])data.originalMaterials.Clone();
                        if (faded.Length > 0) faded[0] = data.fadeMaterial;
                        hitRenderer.sharedMaterials = faded;
                    }
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
                    if (pair.Key != null)
                        pair.Key.sharedMaterials = pair.Value.originalMaterials;
                    Destroy(pair.Value.fadeMaterial); // 임시 투명 머티리얼 제거
                    if (pair.Value.shadowMaterial != null)
                        Destroy(pair.Value.shadowMaterial);
                    toRestore.Add(pair.Key);
                }
            }
        }

        foreach (var renderer in toRestore)
        {
            currentlyOccluding.Remove(renderer);
        }
    }

    /// <summary>SW 수정: Lit 재질은 조명 반응을 보존한 채 알파 블렌딩으로 전환한다.</summary>
    private Material CreateFadeMaterial(Material original)
    {
        // SW 수정: Lit 표면은 셰이더와 노멀·금속성·발광 등 원본 속성을 보존하고 투명 상태만 바꾼다.
        // 다른 셰이더는 기존 Inspector 템플릿을 사용한다.
        if (original == null || (original.shader.name != "Universal Render Pipeline/Lit" &&
            original.shader.name != "Synty/Generic_Basic"))
            return new Material(transparentMaterialTemplate);

        Material fade = new Material(original);
        fade.SetFloat("_Surface", 1f);
        fade.SetFloat("_Blend", 0f);
        fade.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        fade.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        fade.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        fade.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        fade.SetFloat("_ZWrite", 0f);
        fade.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        fade.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        fade.DisableKeyword("_ALPHAMODULATE_ON");
        fade.SetOverrideTag("RenderType", "Transparent");
        fade.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        fade.SetShaderPassEnabled("ShadowCaster", false);
        fade.SetShaderPassEnabled("DepthOnly", false);
        fade.SetShaderPassEnabled("DepthNormals", false);
        return fade;
    }

    /// <summary>SW 수정: 원래 머티리얼의 기본 텍스처와 색(알파 제외)을 페이드 머티리얼에 옮긴다.</summary>
    private void CopyAppearance(Material source, Material fade)
    {
        if (source == null)
            return;

        if (source.HasProperty(BaseMapID) && fade.HasProperty(BaseMapID))
        {
            fade.SetTexture(BaseMapID, source.GetTexture(BaseMapID));
            fade.SetTextureScale(BaseMapID, source.GetTextureScale(BaseMapID));
            fade.SetTextureOffset(BaseMapID, source.GetTextureOffset(BaseMapID));
        }

        if (source.HasProperty(baseColorID) && fade.HasProperty(baseColorID))
        {
            Color color = source.GetColor(baseColorID);
            color.a = fade.GetColor(baseColorID).a;
            fade.SetColor(baseColorID, color);
        }
    }

    /// <summary>SW 수정: 비활성화·파괴 시 바꿔 둔 머티리얼을 원래대로 돌리고 임시 머티리얼을 정리한다.</summary>
    private void OnDisable()
    {
        foreach (var pair in currentlyOccluding)
        {
            if (pair.Key != null)
                pair.Key.sharedMaterials = pair.Value.originalMaterials;
            Destroy(pair.Value.fadeMaterial);
            if (pair.Value.shadowMaterial != null)
                Destroy(pair.Value.shadowMaterial);
        }
        currentlyOccluding.Clear();
    }
}
