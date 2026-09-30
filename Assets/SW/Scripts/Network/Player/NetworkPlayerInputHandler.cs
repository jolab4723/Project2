using UnityEngine;

/// <summary>
/// 공통 WBH 입력의 공격·획득·핑 의도를 같은 플레이어의 서버 권한에 연결한다.
/// 이동·고층 조준·아이템과 적 추적·회피는 원본 입력과 Controller가 소유한다.
/// 평타 타격 전 이동 취소와 서버의 공격 대기시간 검사는 Authority에 유지한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WBH_PlayerInputHandler))]
public sealed class NetworkPlayerInputHandler : MonoBehaviour
{
    private WBH_PlayerInputHandler input;
    private PlayerCombatAuthority combat;
    private PlayerInventorySync inventory;
    private NetworkPlayerPing ping;

    private void Awake()
    {
        input = GetComponent<WBH_PlayerInputHandler>();
        combat = GetComponent<PlayerCombatAuthority>();
        inventory = GetComponent<PlayerInventorySync>();
        ping = GetComponent<NetworkPlayerPing>();
    }

    private void OnEnable()
    {
        input.BindExternalActions(RequestAttack, CancelAttack, RequestPickup, RequestPing);
    }

    private void OnDisable()
    {
        input?.BindExternalActions(null, null, null, null);
    }

    private bool RequestAttack(Vector3 point) => combat != null && combat.TryBeginLocalAttack(point);
    private void CancelAttack() => combat?.TryCancelLocalAttackForMove();
    private bool RequestPickup(Ray ray) => inventory != null && inventory.TryRequestPickup(ray);
    private void RequestPing(Ray ray) => ping?.TryRequest(ray);
}
