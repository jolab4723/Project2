using UnityEngine;

/// <summary>
/// 플레이어 인벤토리에서 유물의 소유 상태가 바뀔 때 고유 효과를 적용/해제한다.
/// 유물은 장비처럼 별도의 메인/서브 스탯(장비 스펙)을 갖지 않는다.
/// SW 수정: SO의 전역 OnEquip/OnUnequip 대신 공통 실행기에 소유 플레이어를 전달한다.
/// 버프 매니저가 스탯 재계산을 담당하므로 이 클래스는 스탯 계산에 관여하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRelicEffectProvider : PlayerRelicEffectRuntime
{
    // 기존 Inspector 필드를 보존한다. 실제 소유 인벤토리는 PlayerContext에서 명시적으로 연결한다.
    [SerializeField] private InventoryController inventoryController;
}
