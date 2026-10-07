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
    private System.Action cancelAttackRequest;
    private System.Func<Ray, bool> pickupRequest;
    private System.Action<Ray> pingRequest;

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

    /// <summary>SW 수정: 입력 카메라를 이동·회피를 처리하는 컨트롤러에도 연결한다.</summary>
    private void Awake()
    {
        mainCamera = Camera.main;

        controller = GetComponent<T_PlayerController>();
        combat = GetComponent<T_PlayerCombat>();
        BindInputCamera(mainCamera);
    }

    /// <summary>SW 수정: 외부 획득 요청이 연결되지 않은 경우에만 로컬 획득 참조를 찾는다.</summary>
    private void Start()
    {
        if (pickupRequest == null)
            ResolvePickupInteractor(); // UI 실행 순서 보장을 위한 Start 에서 호출
    }

    /// <summary>SW 수정: 카메라 연결을 재시도하고 조작 불가 또는 UI 입력 소비 중에는 입력을 처리하지 않는다.</summary>
    private void Update()
    {
        if (mainCamera == null)
            BindInputCamera(Camera.main);
        if (mainCamera == null || controller == null || combat == null || !controller.IsControlEnabled ||
            (isInputConsumed != null && isInputConsumed()))
            return;

        HandleMoveInput();
        HandleAttackInput();
        HandleDodgeInput();

        UpdateItemChase();
        //HandlePortionInput();
        //HandleOpenUIInput();
        //HandleSkillInput(); // 스킬 연결 시, 활성화
    }

    /// <summary>SW 수정: 입력과 회피에 같은 카메라를 연결한다.</summary>
    public void BindInputCamera(Camera camera)
    {
        mainCamera = camera;
        controller?.BindInputCamera(camera);
    }

    /// <summary>SW 수정: 입력 해석은 유지하고 공격·획득·핑의 확정만 외부 권한에 전달한다.</summary>
    public void BindExternalActions(System.Func<Vector3, bool> attack, System.Action cancelAttack,
        System.Func<Ray, bool> pickup, System.Action<Ray> ping)
    {
        combat.BindAttackRequest(attack);
        cancelAttackRequest = cancelAttack;
        pickupRequest = pickup;
        pingRequest = ping;
    }

    private System.Func<bool> isInputConsumed;

    /// <summary>SW 수정: UI에서 입력을 소비하는지 확인할 조건을 연결한다.</summary>
    public void BindInputConsumption(System.Func<bool> inputConsumptionCheck) => isInputConsumed = inputConsumptionCheck;

    /// <summary>SW 수정: 입력 처리가 비활성화되면 아이템과 적 추적 상태를 정리한다.</summary>
    private void OnDisable()
    {
        CancelItemChase();
        combat?.CancelChase();
    }

    // 이동. 아이템 우클릭 시, collider 무시하고 아이템이 있던 위치로 이동.
    /// <summary>SW 수정: 이동 명령을 내리기 전에 외부 공격 요청을 취소한다.</summary>
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

        cancelAttackRequest?.Invoke();

        // 이동 멈추지 않고 아이템과 적 추적 정보만 초기화.
        if (Input.GetMouseButtonDown(1))
        {
            CancelItemChase();
            combat.CancelChase(stopMovement:false);
        }
            
        controller.MoveCommand(hit.point);
    }

    // 공격 및 아이템 획득
    /// <summary>SW 수정: 빈 지점 공격도 외부 권한에 연결할 수 있는 공격 요청 경로로 전달한다.</summary>
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
            combat.RequestAttack(ray.GetPoint(distance));
        }
    }

    // 회피
    /// <summary>SW 수정: 지정된 회피 입력을 사용하고 회피 성공 후 공격과 추적을 취소한다.</summary>
    private void HandleDodgeInput()
    {
        if(KeyBindingService.InputActions.Player.Dodge.triggered && controller.CanDodge)
        {
            if (!controller.TryDodgeFromInput())
                return;
            cancelAttackRequest?.Invoke();
            CancelItemChase();
            combat.CancelChase(); // 회피가 성공한 뒤 적 추적을 중지한다.
        }
    }

    // 아이템 위치 추적
    /// <summary>SW 수정: 외부 획득 요청을 사용하고 해당 경로에서는 즉시 실패 경고를 생략한다.</summary>
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

        RequestPickup(new Vector2(screenPos.x, screenPos.y));

        bool acquired = attemptedItem == null || !attemptedItem.gameObject.activeInHierarchy; // 아이템 획득 가능 여부 판단.

        // 획득 완료 시, 임시로 저장된 아이템 데이터 초기화
        pendingItem = null;
        hasItemDestination = false;

        if (pickupRequest == null && !acquired)
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
    /// <summary>SW 수정: 외부 획득 요청이 있으면 로컬 참조 탐색을 생략하고 공격 취소 후 획득 또는 추적한다.</summary>
    private bool TryHandleWorldItemClick(Vector2 screenPos, ItemDataStorage clickedItem)
    {
        if(clickedItem == null || clickedItem.Item?.definition == null)
            return false;

        if (pickupRequest == null && !ResolvePickupInteractor())
            return true;

        CancelItemChase();
        cancelAttackRequest?.Invoke();
        combat.CancelChase();

        float pickupDistanceSqr = itemPickupDistance * itemPickupDistance;

        if(GetItemSqrDistance(clickedItem) <= pickupDistanceSqr)
        {
            RequestPickup(screenPos);

            return true;
        }

        pendingItem = clickedItem;
        hasItemDestination = false;

        RefreshItemDestination();

        return true;
    }

    /// <summary>SW 수정: 획득 클릭을 외부 요청으로 전달하고 연결되지 않은 경우 기존 로컬 획득을 사용한다.</summary>
    private void RequestPickup(Vector2 screenPosition)
    {
        if (pickupRequest != null)
            pickupRequest(mainCamera.ScreenPointToRay(screenPosition));
        else if (ResolvePickupInteractor())
            pickupInteractor.TryHandleClick(screenPosition);
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

    /// <summary>SW 수정: 외부 핑 요청이 연결되어 있으면 클릭 광선을 전달하고 로컬 핑 생성을 생략한다.</summary>
    private void TrySpawnPing(Vector2 screenPosition)
    {
        if (pingRequest != null)
        {
            pingRequest(mainCamera.ScreenPointToRay(screenPosition));
            return;
        }
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
