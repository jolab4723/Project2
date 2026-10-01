using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// BH 원본 <c>WBH_PlayerInputHandler</c>의 Mirror 검증용 복제본이다.
/// <para>원본: <c>Assets/WBHTest/Scripts/Player/WBH_PlayerInputHandler.cs</c></para>
/// <para>원본의 <c>WorldItemPickupInteractor</c> 참조는 유지하되, 네트워크 픽업은 서버 판정을 위해 <c>PlayerInventorySync</c>에 요청한다.</para>
/// <para>카메라·Controller·Combat 누락 시 입력을 무시하도록 방어하며, 로컬 플레이어 여부는 <c>MirrorSpawnedPlayerBinder</c>가 컴포넌트 활성화로 보장한다.</para>
/// <para>평타 도중 이동하면 서버 Authority에 먼저 같은 공격 번호의 취소를 알린다.
/// 타격 전이면 예약 피해도 취소되고, 타격 후면 이동만 허용되며 공격 대기시간은 유지된다.</para>
/// <para>6-C 전환 보완: Player는 Scene보다 오래 살아 있으므로 이전 Scene의 파괴된 Camera 참조를
/// C# null 병합 연산자로 검사하지 않고, Unity의 null 판정으로 새 Scene의 Main Camera를 다시 찾는다.</para>
/// <para>미러테스트 회피 보완: T_PlayerController의 Awake() 1회 카메라 캐싱으로 인해 씬 전환 후 카메라 참조가 끊어지는 현상을
/// 리플렉션을 통해 유효한 Camera.main으로 동기화하고, 바닥 Raycast 실패 시 수평 평면(Plane) 기반 fallback 회피를 보장한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkPlayerInputHandler : MonoBehaviour
{
    [SerializeField] private LayerMask inputBlockLayer;

    private Camera mainCamera;
    private T_PlayerController controller;
    private T_PlayerCombat combat;
    private PlayerCombatAuthority combatAuthority;
    private WorldItemPickupInteractor pickupItem;
    private PlayerInventorySync inventorySync;
    private WBH_PlayerStateMachine stateMachine;
    private WBH_PlayerStatus status;
    private NetworkPlayerPing ping;

    private static readonly FieldInfo ControllerCameraField =
        typeof(T_PlayerController).GetField("mainCamera", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ControllerDodgeDirField =
        typeof(T_PlayerController).GetField("dodgeDir", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ControllerDodgeCooltimeBackingField =
        typeof(T_PlayerController).GetField("<currentDodgeCooltime>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo ControllerSetDodgeCooltimeMethod =
        typeof(T_PlayerController).GetProperty("currentDodgeCooltime", BindingFlags.Instance | BindingFlags.Public)?.GetSetMethod(true);
    private static readonly FieldInfo ControllerMeshTrailField =
        typeof(T_PlayerController).GetField("meshTrailTut", BindingFlags.Instance | BindingFlags.NonPublic);

    private void Awake()
    {
        mainCamera = Camera.main;
        controller = GetComponent<T_PlayerController>();
        combat = GetComponent<T_PlayerCombat>();
        combatAuthority = GetComponent<PlayerCombatAuthority>();
        pickupItem = FindFirstObjectByType<WorldItemPickupInteractor>();
        inventorySync = GetComponent<PlayerInventorySync>();
        stateMachine = GetComponent<WBH_PlayerStateMachine>();
        status = GetComponent<WBH_PlayerStatus>();
        ping = GetComponent<NetworkPlayerPing>();

        SyncControllerCamera();
        EnsureControllerMeshTrail();
    }

    private void Update()
    {
        // Unity에서 파괴된 Camera는 C# 참조 자체는 남아 있어 ??=로는 교체되지 않는다.
        // Scene 전환 뒤 이동·공격 레이캐스트가 모두 멈추지 않도록 Unity의 == null 판정을 사용한다.
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            SyncControllerCamera();
        }

        if (controller == null || combat == null || !controller.IsControlEnabled)
            return;

        HandleMoveInput();
        HandleAttackInput();
        HandleDodgeInput();
    }

    private void HandleMoveInput()
    {
        if (mainCamera == null || !Input.GetMouseButton(1) || IsPointerOverUI())
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                500f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) ||
            IsBlocked(hit.collider.gameObject.layer))
            return;

        // 이동을 누른 채 다시 공격해도 미확정 타격을 취소한다. 확정된 공격의 대기시간은 유지된다.
        combatAuthority?.TryCancelLocalAttackForMove();
        if (Input.GetMouseButtonDown(1))
        {
            // 최초 이동 입력에서만 추격을 취소한다.
            // 누르는 동안 매 프레임 CancelChase를 호출하면 새 NavMesh 경로도 계속 초기화된다.
            combat.CancelChase();
        }

        controller.MoveCommand(hit.point);
    }

    private void HandleAttackInput()
    {
        if (mainCamera == null || !Input.GetMouseButtonDown(0) || IsPointerOverUI())
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // 원본과 같이 Alt+좌클릭은 핑 전용이며 공격·획득으로 이어지지 않는다.
        if (Input.GetKey(KeyCode.LeftAlt))
        {
            ping?.TryRequest(ray);
            return;
        }

        if (inventorySync != null && inventorySync.TryRequestPickup(ray))
            return;

        Vector3 targetPoint = Vector3.zero;
        bool hasTarget = false;

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            if (IsBlocked(hit.collider.gameObject.layer))
                return;

            WBH_EnemyController enemy = hit.collider.GetComponentInParent<WBH_EnemyController>();
            if (enemy != null)
            {
                targetPoint = enemy.transform.position;
                hasTarget = true;
            }
            else
            {
                targetPoint = hit.point;
                hasTarget = true;
            }
        }

        if (!hasTarget)
        {
            Plane plane = new(Vector3.up, transform.position);
            if (plane.Raycast(ray, out float distance))
            {
                targetPoint = ray.GetPoint(distance);
                hasTarget = true;
            }
        }

        if (hasTarget)
        {
            combatAuthority?.TryBeginLocalAttack(targetPoint);
        }
    }

    private void HandleDodgeInput()
    {
        bool dodgePressed = Input.GetKeyDown(KeyCode.Space);
        try
        {
            if (KeyBindingService.InputActions.Player.Dodge.triggered)
                dodgePressed = true;
        }
        catch
        {
            // KeyBindingService 미초기화 시 예외 방어
        }

        if (!dodgePressed)
            return;

        if (controller == null || !controller.IsControlEnabled || !controller.CanDodge)
            return;

        if (stateMachine != null && stateMachine.IsAnyState(PlayerState.Dodge, PlayerState.Dead))
            return;

        SyncControllerCamera();
        EnsureControllerMeshTrail();

        combatAuthority?.TryCancelLocalAttackForMove();
        combat?.CancelChase();

        controller.TryDodge();

        // T_PlayerController.TryDodge()는 Physics.Raycast로만 방향을 계산하므로,
        // 마우스 커서가 바닥 콜라이더 밖이나 공중, UI를 가리키면 dodgeDir이 zero가 되어 회피가 취소된다.
        // 이때 수평 평면 Raycast 또는 플레이어 전방 방향을 보조 방향으로 사용하여 확실하게 회피를 발동시킨다.
        if (stateMachine != null && !stateMachine.Is(PlayerState.Dodge))
        {
            TriggerFallbackDodge();
        }
    }

    private void TriggerFallbackDodge()
    {
        if (controller == null || !controller.CanDodge || !controller.IsControlEnabled)
            return;

        Vector3 dir = Vector3.zero;
        if (mainCamera != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new(Vector3.up, transform.position);
            if (groundPlane.Raycast(ray, out float distance))
            {
                dir = ray.GetPoint(distance) - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    dir.Normalize();
                else
                    dir = Vector3.zero;
            }
        }

        if (dir == Vector3.zero)
            dir = transform.forward;

        if (dir == Vector3.zero)
            return;

        ControllerDodgeDirField?.SetValue(controller, dir);

        float cooltime = status != null ? status.DodgeCooltime : 1f;
        SetControllerDodgeCooltime(cooltime);

        stateMachine?.ChangeState(PlayerState.Dodge);
    }

    private void SetControllerDodgeCooltime(float cooltime)
    {
        if (controller == null)
            return;

        ControllerDodgeCooltimeBackingField?.SetValue(controller, cooltime);
        ControllerSetDodgeCooltimeMethod?.Invoke(controller, new object[] { cooltime });
    }

    private void EnsureControllerMeshTrail()
    {
        if (controller == null || ControllerMeshTrailField == null)
            return;

        object trail = ControllerMeshTrailField.GetValue(controller);
        if (trail == null)
        {
            var comp = GetComponentInChildren<YJ_MeshTrailTut>();
            if (comp == null)
                comp = gameObject.AddComponent<YJ_MeshTrailTut>();

            ControllerMeshTrailField.SetValue(controller, comp);
        }
    }

    private void SyncControllerCamera()
    {
        if (controller == null)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null && ControllerCameraField != null)
        {
            ControllerCameraField.SetValue(controller, mainCamera);
        }
    }

    private bool IsBlocked(int layer)
    {
        return ((1 << layer) & inputBlockLayer) != 0;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
