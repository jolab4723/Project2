using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// BH 원본 <c>WBH_PlayerInputHandler</c>의 Mirror 검증용 복제본이다.
/// <para>원본: <c>Assets/WBHTest/Scripts/Player/WBH_PlayerInputHandler.cs</c></para>
/// <para>원본의 <c>WorldItemPickupInteractor</c> 참조는 유지하되, 네트워크 픽업은 서버 판정을 위해 <c>PlayerInventorySync_MirrorTest</c>에 요청한다.</para>
/// <para>카메라·Controller·Combat 누락 시 입력을 무시하도록 방어하며, 로컬 플레이어 여부는 <c>MirrorSpawnedPlayerBinder</c>가 컴포넌트 활성화로 보장한다.</para>
/// <para>평타 도중 이동하면 서버 Authority에 먼저 같은 공격 번호의 취소를 알린다.
/// 타격 전이면 예약 피해도 취소되고, 타격 후면 이동만 허용되며 공격 대기시간은 유지된다.</para>
/// <para>6-C 전환 보완: Player는 Scene보다 오래 살아 있으므로 이전 Scene의 파괴된 Camera 참조를
/// C# null 병합 연산자로 검사하지 않고, Unity의 null 판정으로 새 Scene의 Main Camera를 다시 찾는다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class WBH_PlayerInputHandler_MirrorTest : MonoBehaviour
{
    [SerializeField] private LayerMask inputBlockLayer;

    private Camera mainCamera;
    private T_PlayerController controller;
    private T_PlayerCombat combat;
    private PlayerCombatAuthority_MirrorTest combatAuthority;
    private WorldItemPickupInteractor pickupItem;
    private PlayerInventorySync_MirrorTest inventorySync;

    private void Awake()
    {
        mainCamera = Camera.main;
        controller = GetComponent<T_PlayerController>();
        combat = GetComponent<T_PlayerCombat>();
        combatAuthority = GetComponent<PlayerCombatAuthority_MirrorTest>();
        pickupItem = FindFirstObjectByType<WorldItemPickupInteractor>();
        inventorySync = GetComponent<PlayerInventorySync_MirrorTest>();
    }

    private void Update()
    {
        // Unity에서 파괴된 Camera는 C# 참조 자체는 남아 있어 ??=로는 교체되지 않는다.
        // Scene 전환 뒤 이동·공격 레이캐스트가 모두 멈추지 않도록 Unity의 == null 판정을 사용한다.
        if (mainCamera == null)
            mainCamera = Camera.main;

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

        if (Input.GetMouseButtonDown(1))
        {
            // 최초 이동 입력에서만 추격과 예약 공격을 취소한다.
            // 누르는 동안 매 프레임 CancelChase를 호출하면 새 NavMesh 경로도 계속 초기화된다.
            combatAuthority?.TryCancelLocalAttackForMove();
            combat.CancelChase();
        }

        controller.MoveCommand(hit.point);
    }

    private void HandleAttackInput()
    {
        if (mainCamera == null || !Input.GetMouseButtonDown(0) || IsPointerOverUI())
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (inventorySync != null && inventorySync.TryRequestPickup(ray))
            return;

        if (Physics.Raycast(ray, out RaycastHit hit) && IsBlocked(hit.collider.gameObject.layer))
            return;

        Plane plane = new(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float distance))
            combatAuthority?.TryBeginLocalAttack(ray.GetPoint(distance));
    }

    private void HandleDodgeInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            controller.TryDodge();
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
