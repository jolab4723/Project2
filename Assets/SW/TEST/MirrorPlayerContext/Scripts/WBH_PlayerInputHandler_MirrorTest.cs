using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// BH 원본 <c>WBH_PlayerInputHandler</c>의 Mirror 검증용 복제본이다.
/// <para>원본: <c>Assets/WBHTest/Scripts/Player/WBH_PlayerInputHandler.cs</c></para>
/// <para>원본의 <c>WorldItemPickupInteractor</c> 참조는 유지하되, 네트워크 픽업은 서버 판정을 위해 <c>PlayerInventorySync_MirrorTest</c>에 요청한다.</para>
/// <para>카메라·Controller·Combat 누락 시 입력을 무시하도록 방어하며, 로컬 플레이어 여부는 <c>MirrorSpawnedPlayerBinder</c>가 컴포넌트 활성화로 보장한다.</para>
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
        mainCamera ??= Camera.main;

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
        if (!Physics.Raycast(ray, out RaycastHit hit) || IsBlocked(hit.collider.gameObject.layer))
            return;

        combat.CancelChase();
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
