using ItemSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public class WBH_PlayerInputHandler : MonoBehaviour
{
    public event System.Action<Vector3, float> PingCreated;

    //[SerializeField] private PlayerSkillSystem skillSystem; // 우진님 스킬시스템 연결

    [SerializeField] private LayerMask inputBlockLayer; // 입력 방지 레이어. 
    [SerializeField] private LayerMask worldItemLayer; // 아이템 레이어

    private Camera mainCamera;

    private T_PlayerController controller;
    private T_PlayerCombat combat;

    private WorldItemPickupInteractor pickupInteractor;
    private ItemDataStorage pendingItem; // 아이템 정보 임시저장. 추적해서 획득하면 초기화.
    private Vector3 lastItemPos;
    private bool hasItemDestination;
    private float itemPickupDistance = 2.3f; // 아이템 픽업 가능 거리.
    private float itemDestinationRefreshDistance = 0.25f; // 드랍된 아이템 움직일 때 경로 갱신 조건.

    [Header("맵 핑")]
    [SerializeField] private GameObject pingMarkerPrefab;
    [SerializeField] private LayerMask pingGroundLayer;
    [SerializeField, Min(0.1f)] private float pingLifetime = 3f;
    [SerializeField, Min(0f)] private float pingCooldown = 0.3f;
    [SerializeField, Min(0f)] private float pingSurfaceOffset = 0.03f;
    private float nextPingTime;

    private void Awake()
    {
        mainCamera = Camera.main;

        controller = GetComponent<T_PlayerController>();
        combat = GetComponent<T_PlayerCombat>();
    }

    private void Start()
    {
        ResolvePickupInteractor(); // UI 실행 순서 보장을 위한 Start 에서 호출
    }

    private void Update()
    {
        if (!controller.IsControlEnabled)
            return;

        HandleMoveInput();
        HandleAttackInput();
        HandleDodgeInput();

        UpdateItemChase();
        //HandlePortionInput();
        //HandleOpenUIInput();
        //HandleSkillInput(); // 스킬 연결 시, 활성화
    }

    // 이동. 아이템 우클릭 시, collider 무시하고 아이템이 있던 위치로 이동.
    private void HandleMoveInput()
    {
        if (!Input.GetMouseButton(1) || IsPointerOverUI() || !controller.IsControlEnabled)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        int moveRaycastMask = Physics.DefaultRaycastLayers & ~worldItemLayer.value;

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, moveRaycastMask, QueryTriggerInteraction.Ignore))
            return;

        if (IsInLayerMask(hit.collider.gameObject.layer, inputBlockLayer))
            return;

        // 이동 멈추지 않고 아이템과 적 추적 정보만 초기화.
        if (Input.GetMouseButtonDown(1))
        {
            CancelItemChase();
            combat.CancelChase(stopMovement:false);
        }
            
        controller.MoveCommand(hit.point);
    }

    // 공격 및 아이템 획득
    private void HandleAttackInput()
    {
        if ( ! Input.GetMouseButtonDown(0) || IsPointerOverUI())
            return;

        Vector2 screenPos = Input.mousePosition;

        // Alt+좌클릭은 핑 전용으로 사용합니다.
        if (Input.GetKey(KeyCode.LeftAlt))
        {
            TrySpawnPing(screenPos);
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // 아이템을 먼저 검사하여 장판 등의 Collider가 획득 클릭을 가리지 않도록 합니다.
        if (Physics.Raycast(ray, out RaycastHit itemHit, 500f, worldItemLayer, QueryTriggerInteraction.Collide))
        {
            ItemDataStorage item = itemHit.collider.GetComponentInParent<ItemDataStorage>();
            if (item != null)
            {
                TryHandleWorldItemClick(screenPos, item);
                return;
            }
        }

        if(Physics.Raycast(ray, out RaycastHit hit, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            if (IsInLayerMask(hit.collider.gameObject.layer, inputBlockLayer))
                return;

            WBH_EnemyController enemy = hit.collider.GetComponentInParent<WBH_EnemyController>();

            if (enemy != null)
            {
                CancelItemChase();
                combat.TryAtkTarget(enemy);
                return;
            }
        }

        // 고층맵에서도 조준점이 바닥 아래로 밀리지 않도록 플레이어 높이의 평면을 사용한다.
        Plane plane = new Plane(Vector3.up, transform.position);

        if(plane.Raycast(ray, out float distance))
        {
            CancelItemChase();
            combat.TryAttack(ray.GetPoint(distance));
        }
    }

    // 회피
    private void HandleDodgeInput()
    {
        if(Input.GetKeyDown(KeyCode.Space) && controller.CanDodge)
        {
            CancelItemChase();
            combat.CancelChase(); // 회피 쿨타임이어도 추적은 중지됨.
            controller.TryDodge();
        }
    }

    // 아이템 위치 추적
    private void UpdateItemChase()
    {
        if(pendingItem == null || !pendingItem.gameObject.activeInHierarchy)
        {
            CancelItemChase(true); // 아이템 소실 (다른 플레이어가 먼저 습득 등)하면 이동정지
            return;
        }

        float pickupDistanceSqr = itemPickupDistance * itemPickupDistance;

        if(GetItemSqrDistance(pendingItem) > pickupDistanceSqr)
        {
            RefreshItemDestination();
            return;
        }

        controller.StopMovement();

        if (IsPointerOverUI())
            return;

        Vector3 screenPos = mainCamera.WorldToScreenPoint(pendingItem.transform.position);

        if(screenPos.z <= 0f )
        {
            CancelItemChase();
            return;
        }

        ItemDataStorage attemptedItem = pendingItem;

        pickupInteractor.TryHandleClick(new Vector2(screenPos.x, screenPos.y));

        bool acquired = attemptedItem == null || !attemptedItem.gameObject.activeInHierarchy; // 아이템 획득 가능 여부 판단.

        // 획득 완료 시, 임시로 저장된 아이템 데이터 초기화
        pendingItem = null;
        hasItemDestination = false;

        if(!acquired)
        {
            Log.Print("아이템 위치까지 이동했지만 획득하지 못하였습니다.");
        }
    }

    // 마우스 포인터가 UI위에 올라가있는지 체크하는 메서드
    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    // 아이템 획득을 위한 참조
    private bool ResolvePickupInteractor()
    {
        if (pickupInteractor != null)
            return true;

        pickupInteractor = FindFirstObjectByType<WorldItemPickupInteractor>();

        if(pickupInteractor == null)
        {
            Log.Warning($"[{name}] WorldItemPickupInteractor 를 찾지 못했습니다.");
            return false;
        }
        return true;
    }

    // 좌클릭 시, 참조가 없다면 다시 탐색. 있다면 아이템 획득 메서드 호출
    private bool TryHandleWorldItemClick(Vector2 screenPos, ItemDataStorage clickedItem)
    {
        if(clickedItem == null || clickedItem.Item?.definition == null)
            return false;

        if (!ResolvePickupInteractor())
            return true;

        CancelItemChase();
        combat.CancelChase();

        float pickupDistanceSqr = itemPickupDistance * itemPickupDistance;

        if(GetItemSqrDistance(clickedItem) <= pickupDistanceSqr)
        {
            pickupInteractor.TryHandleClick(screenPos);

            return true;
        }

        pendingItem = clickedItem;
        hasItemDestination = false;

        RefreshItemDestination();

        return true;
    }

    // 아이템이 이동할 경우 플레이어 이동경로 갱신
    // (8/14 기준 아이템은 Layer 로 스킬 등의 효과에서 예외처리하나 혹시 모를 예외처리 실수로 아이템이 움직일 때 방지. 혹은 차후 미지스테이지 등에서 움직이는 아이템을 구현할 때 사용.)
    private void RefreshItemDestination()
    {
        if (pendingItem == null)
            return;

        Vector3 destination = pendingItem.transform.position;

        float refreshDistanceSqr = itemDestinationRefreshDistance * itemDestinationRefreshDistance;

        bool shouldRefresh = !hasItemDestination || (destination - lastItemPos).sqrMagnitude >= refreshDistanceSqr;

        if (!shouldRefresh)
            return;

        float stoppingDistance = itemPickupDistance * 0.8f;

        if(!controller.ChaseCommand(destination, stoppingDistance))
        {
            CancelItemChase();
            return;
        }
        lastItemPos = destination;
        hasItemDestination = true;
    }

    // 아이템과의 거리 계산
    private float GetItemSqrDistance(ItemDataStorage item)
    {
        if (item == null)
            return float.PositiveInfinity; // 예외처리.

        return (item.transform.position - transform.position).sqrMagnitude;
    }

    // 아이템 추적 취소 (아이템 데이터 및 위치 초기화)
    private void CancelItemChase(bool stopMovement = false)
    {
        bool wasChasing = hasItemDestination;

        pendingItem = null;
        hasItemDestination = false;

        if(stopMovement && wasChasing)
        {
            controller.StopMovement();
        }
    }

    // 레이어가 layerMask 와 일치하는지 체크
    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer) ) != 0;
    }

    //// 포션 사용
    //private void HandlePortionInput()
    //{
    //    if (Input.GetKeyDown(KeyCode.Q))
    //    {

    //    }
    //}

    //// UI 단축키
    //private void HandleOpenUIInput()
    //{
    //    // 환경설정 
    //    if (Input.GetKeyDown(KeyCode.Escape))
    //    {

    //    }
    //    // 스킬트리
    //    if (Input.GetKeyDown(KeyCode.K))
    //    {

    //    }
    //    // 인벤토리
    //    if (Input.GetKeyDown(KeyCode.I))
    //    {

    //    }
    //    // 스탯패널
    //    if (Input.GetKeyDown(KeyCode.P))
    //    {

    //    }
    //    // 퀘스트 패널
    //    if (Input.GetKeyDown(KeyCode.O))
    //    {

    //    }
    //}




    // --- 우진님 스킬 즉각 사용 필요시, 활성화.  skillSystem.cs 에서 HandleSkillInput 메서드 public 화 필요
    //private void HandleSkillInput()
    //{
    //    if (skillSystem == null)
    //        return;

    //    skillSystem.HandleSkillInput(0, KeyCode.A);
    //    skillSystem.HandleSkillInput(1, KeyCode.S);
    //    skillSystem.HandleSkillInput(2, KeyCode.D);
    //}



    // --- 우진님 스킬 리팩토링 시, 활성화
    //private void HandleSkillInput(int index, KeyCode key)
    //{
    //    SkillBase skill = skillSystem.GetSkill(index);

    //    if (skill == null)
    //        return;

    //    if (skill.IsChargeSkill)
    //    {
    //        if (Input.GetKeyDown(key))
    //            skill.OnPress(gameObject);

    //        if (Input.GetKey(key))
    //            skill.OnHold(gameObject, Time.deltaTime);

    //        if (Input.GetKeyUp(key))
    //            skill.OnRelease(gameObject);
    //    }
    //    else
    //    {
    //        if (Input.GetKeyDown(key))
    //            skill.Excute(gameObject);
    //    }
    //}

    private void TrySpawnPing(Vector2 screenPosition)
    {
        if (pingMarkerPrefab == null || mainCamera == null)
            return;

        if (Time.time < nextPingTime)
            return;

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        if ( ! Physics.Raycast(ray, out RaycastHit hit, 500f,pingGroundLayer, QueryTriggerInteraction.Ignore))
            return;

        nextPingTime = Time.time + pingCooldown;
        Vector3 position = hit.point + hit.normal * pingSurfaceOffset;

        // 프리팹에 설정된 회전을 유지합니다.
        GameObject marker = Instantiate(pingMarkerPrefab, position, pingMarkerPrefab.transform.rotation);

        // 현재 프리팹은 Play On Awake가 꺼져 있습니다.
        foreach (ParticleSystem particle in marker.GetComponentsInChildren<ParticleSystem>())
        {
            particle.Play(false);
        }

        Destroy(marker, pingLifetime);
        PingCreated?.Invoke(hit.point, pingLifetime);
    }
}
