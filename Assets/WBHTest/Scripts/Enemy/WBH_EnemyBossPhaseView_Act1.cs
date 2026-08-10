using UnityEngine;

public class WBH_EnemyBossPhaseView_Act1 : MonoBehaviour
{
    // 기존 분리용 갑옷 파츠 분리를 위한 클래스
    private class ArmorPieceSlot
    {
        public readonly Transform transform;
        public readonly MeshFilter meshFilter;
        public readonly MeshRenderer meshRenderer;
        public readonly Mesh bakedMesh;

        public Vector3 velocity;
        public Vector3 angularVelocity;
        public float remainingLifetime;
        public bool isAcitve;

        public ArmorPieceSlot(Transform transform, MeshFilter meshFilter, MeshRenderer meshRenderer, Mesh bakedMesh)
        {
            this.transform = transform;
            this.meshFilter = meshFilter;
            this.meshRenderer = meshRenderer;
            this.bakedMesh = bakedMesh;
        }
    }

    [SerializeField] private Renderer[] phaseOneRenderers;
    [SerializeField] private Transform armorPiecePoolRoot;

    [Header("Transition Motion")]
    [SerializeField] private float horizontalSpeed = 7f;
    [SerializeField] private float upwardSpeed = 5f;
    [SerializeField] private float randomVelocity = 2f;
    [SerializeField] private float angularSpeed = 360f;
    [SerializeField] private float gravity = 12f;
    [SerializeField] private float pieceLifetime = 2.5f;

    private bool[] initialEnabledStates;
    private ArmorPieceSlot[] armorPieceSlots;
    private bool isPhaseTwo;

    void Awake()
    {
        initialEnabledStates = new bool[phaseOneRenderers.Length];

        armorPieceSlots = new ArmorPieceSlot[phaseOneRenderers.Length];

        for (int i = 0; i < phaseOneRenderers.Length; i++)
        {
            Renderer source = phaseOneRenderers[i];

            initialEnabledStates[i] = source != null && source.enabled;

            if(source != null)
            {
                armorPieceSlots[i] = CreateArmorPieceSlot(source);
            }
        }
    }

    // 매 프레임 복제된 갑옷 파편 튕겨나감, 일정 시간 후 복제본 비활성화
    private void Update()
    {
       float deltaTime = Time.deltaTime;
       
        foreach (ArmorPieceSlot slot in armorPieceSlots)
        {
            if (slot == null || !slot.isAcitve)
                continue;

            slot.velocity += Vector3.down * gravity * deltaTime;
            slot.transform.position += slot.velocity * deltaTime;
            slot.transform.Rotate(slot.angularVelocity * deltaTime, Space.World);

            slot.remainingLifetime -= deltaTime;

            if(slot.remainingLifetime <= 0f)
            {
                ReturnPiece(slot);
            }
        }
    }

    // 2페이즈 진입 시, 기존 렌더끄기 + 복제본 튕겨나감
    public void SetPhaseTwo()
    {
        if (isPhaseTwo)
            return;

        isPhaseTwo = true;

        for (int i = 0; i < phaseOneRenderers.Length; i++)
        {
            Renderer source = phaseOneRenderers[i];

            if (source == null || !source.enabled)
                continue;

            ArmorPieceSlot slot = armorPieceSlots[i];

            if (slot != null)
                ReleasePiece(source, slot);

            source.enabled = false;
        }
    }

    // 기존 렌더로 초기화
    public void SetPhaseOne()
    {
        isPhaseTwo = false;

        ReturnAllPiece();

        for(int i = 0; i < phaseOneRenderers.Length; i++)
        {
            Renderer renderer = phaseOneRenderers[i];

            if(renderer != null)
            {
                renderer.enabled = initialEnabledStates[i];
            }
        }    
    }

    // 갑옷 파편 복제본 생성
    private ArmorPieceSlot CreateArmorPieceSlot(Renderer source)
    {
        GameObject pieceObject = new GameObject($"{source.name}_Detached");

        pieceObject.transform.SetParent(armorPiecePoolRoot, false);

        MeshFilter meshFilter = pieceObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = pieceObject.AddComponent<MeshRenderer>();

        Mesh bakedMesh = new Mesh { name = $"{source.name}_BakedMesh" };

        meshFilter.sharedMesh = bakedMesh;
        meshRenderer.sharedMaterials = source.sharedMaterials;

        pieceObject.SetActive(false);

        return new ArmorPieceSlot(pieceObject.transform, meshFilter, meshRenderer, bakedMesh);
    }

    // 복제본 활성화 및 튕겨나감
    private void ReleasePiece(Renderer source, ArmorPieceSlot slot)
    {
        if (!TryCopyMesh(source, slot))
            return;

        Transform sourceTransform = source.transform;

        slot.transform.SetParent(null, false);
        slot.transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);

        slot.transform.localScale = Vector3.one;
        slot.meshRenderer.sharedMaterials = source.sharedMaterials;

        Vector3 outward = slot.transform.position - transform.position;

        outward.y = 0f;

        if(outward.sqrMagnitude < 0.01f)
        {
            outward = transform.forward;
        }

        outward.Normalize();

        slot.velocity = outward * horizontalSpeed + Vector3.up * upwardSpeed + Random.insideUnitSphere * randomVelocity;

        slot.angularVelocity = Random.onUnitSphere * angularSpeed;

        slot.remainingLifetime = pieceLifetime;
        slot.isAcitve = true;

        slot.transform.gameObject.SetActive(true);
    }

    // 복제본 메쉬 생성 (기존 메쉬가 Skinned 로 Armature 에 transform 이 연동되어 있음)
    private bool TryCopyMesh(Renderer source, ArmorPieceSlot slot)
    {
        if(source is SkinnedMeshRenderer skinnedRenderer)
        {
            skinnedRenderer.BakeMesh(slot.bakedMesh, useScale : false); // 스케일은 복제본 transform 에서 적용

            slot.bakedMesh.RecalculateBounds();

            slot.meshFilter.sharedMesh = slot.bakedMesh;
            return true;
        }

        if(source.TryGetComponent<MeshFilter>(out MeshFilter sourceFilter) && sourceFilter.sharedMesh != null)
        {
            slot.meshFilter.sharedMesh = sourceFilter.sharedMesh;
            return true;
        }
        return false;
    }

    // 모든 복제본 풀로 돌려두기. 초기화용
    private void ReturnAllPiece()
    {
        foreach(ArmorPieceSlot slot in armorPieceSlots)
        {
            if (slot != null)
                ReturnPiece(slot);
        }
    }

    // 풀로 돌려놓기
    private void ReturnPiece(ArmorPieceSlot slot)
    {
        if (!slot.isAcitve)
            return;

        slot.isAcitve = false;
        slot.transform.gameObject.SetActive(false);
        slot.transform.SetParent(armorPiecePoolRoot, false);

        slot.transform.localPosition = Vector3.zero;
        slot.transform.localRotation = Quaternion.identity;
        slot.transform.localScale = Vector3.one;

        slot.velocity = Vector3.zero;
        slot.angularVelocity = Vector3.zero;
        slot.remainingLifetime = 0f;
    }

    // 보스가 삭제될 때, 복제본 삭제. 보스를 풀로 반환할 경우에는 동작하지 않음.
    private void OnDestroy()
    {
        if (armorPieceSlots == null)
            return;

        foreach(ArmorPieceSlot slot in armorPieceSlots)
        {
            if (slot == null)
                continue;

            if(slot.bakedMesh != null)
            {
                Destroy(slot.bakedMesh);
            }

            if(slot.transform != null)
            {
                Destroy(slot.transform.gameObject);
            }
        }
    }
}
