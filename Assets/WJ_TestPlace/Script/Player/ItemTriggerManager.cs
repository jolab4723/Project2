using System.Collections.Generic;
using UnityEngine;
using ItemSystem;

/// <summary>
/// 장착 중인 아이템의 발동형 고유효과(TriggeredBuffUniqueEffectSO)를, 실제 게임 이벤트가 발생했을 때
/// 자동으로 발동시켜주는 중앙 관리자.
///
/// 지금은 PlayerHealthManager.OnDamageTaken(피격 시)만 실제로 연결되어 있다.
/// 나머지 조건(치명타/처치/공격 적중)은 전투 시스템이 생기면, 해당 이벤트가 발생하는 지점에서
/// Fire(TriggerCondition.OnCrit) 같은 식으로 이 매니저의 Fire()만 불러주면 된다 (조건 판정 로직은 여기 없음).
///
/// !! 멀티플레이 대비: 이 매니저는 "내가 맞은 데미지로 내가 장착한 아이템을 발동"시키는,
///    본질적으로 로컬 플레이어 전용 편의 매니저다. (다른 플레이어가 맞은 데미지로 그 플레이어의
///    아이템이 발동하는 건 그 플레이어의 클라이언트에서 처리될 일이라 여기서 다룰 필요가 없음.)
///    그래서 All 목록 없이, 로컬 인스턴스일 때만 실제로 동작(구독/발동)하도록 막아뒀다.
///    InventoryController.Instance/PlayerHealthManager.Instance도 "로컬 플레이어의 것"이라는
///    보장 하에 그대로 쓴다.
/// </summary>
public class ItemTriggerManager : MonoBehaviour
{
    public static ItemTriggerManager Instance { get; private set; }

    private void Awake()
    {
        var identity = GetComponent<Mirror.NetworkIdentity>();
        if (identity != null && !identity.isLocalPlayer)
            return; // 로컬이 아니면 이 매니저는 아무 것도 안 함 (Instance 등록도 안 함)

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ItemTriggerManager] 이미 인스턴스가 존재해서 중복 오브젝트를 제거합니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>회피 감지를 위해 구독 중인 상태머신. 중복 구독/해제 누락을 막으려고 들고 있는다.</summary>
    private WBH_PlayerStateMachine subscribedStateMachine;

    private void OnEnable()
    {
        // OnDisable과 짝을 이루는 재등록 - Awake는 생애 한 번만 돌아서, 한 번 비활성화됐다가 다시
        // 활성화되는 캐릭터는 Awake가 재실행 안 되므로 여기서 다시 등록해줘야 Instance가 null로
        // 안 남는다(125번).
        if (Instance != this)
        {
            var identity = GetComponent<Mirror.NetworkIdentity>();
            bool isLocal = identity == null || identity.isLocalPlayer;

            if (isLocal && Instance == null)
                Instance = this;
        }

        // 로컬 인스턴스일 때만 실제로 구독한다 (Awake가 아직 안 돌았을 수도 있어 Instance==this로 체크).
        if (Instance != this)
            return;

        if (PlayerHealthManager.Instance != null)
            PlayerHealthManager.Instance.OnDamageTaken += HandleHitTaken;

        // 회피는 별도 이벤트가 없어서 상태머신의 상태 진입을 보고 판단한다.
        // 이렇게 하면 회피 로직(T_PlayerController, BH 담당)을 건드리지 않아도 된다.
        subscribedStateMachine = GetComponent<WBH_PlayerStateMachine>();
        if (subscribedStateMachine != null)
            subscribedStateMachine.OnEnterState += HandleStateEntered;
    }

    private void OnDisable()
    {
        if (PlayerHealthManager.Instance != null)
            PlayerHealthManager.Instance.OnDamageTaken -= HandleHitTaken;

        if (subscribedStateMachine != null)
        {
            subscribedStateMachine.OnEnterState -= HandleStateEntered;
            subscribedStateMachine = null;
        }

        // 기존엔 OnDestroy에서만 Instance를 비워서, SetActive(false)로 비활성화만 해도(파괴 아님)
        // Instance가 계속 이 캐릭터를 가리키고 있었다 - 이후 다른 캐릭터가 Awake될 때 "이미 인스턴스가
        // 있다"고 오판해서 새 캐릭터를 통째로 Destroy하는 문제가 있었다(PlayerStatManager에서 실측 확인, 125번).
        if (Instance == this)
            Instance = null;
    }

    private void HandleHitTaken(float amount)
    {
        Fire(TriggerCondition.OnHitTaken);
    }

    private void HandleStateEntered(PlayerState state)
    {
        if (state == PlayerState.Dodge)
            Fire(TriggerCondition.OnDodge);
    }

    /// <summary>
    /// 지정한 조건과 일치하는 발동형 고유효과를 가진 (로컬 플레이어의) 장착 아이템과
    /// 인벤토리에 보유 중인 유물을 전부 발동시킨다.
    /// 전투 시스템이 생기면 치명타/처치/공격적중 등에서도 이 메서드를 그대로 호출하면 됨.
    ///
    /// !! 유물은 장착 슬롯이 아니라 인벤토리 보유 개념이라 EquipmentSystem.GetEquippedItems()에
    ///    잡히지 않는다. 그래서 PlayerGrid를 별도로 훑어 Relic 카테고리만 추가로 확인한다.
    /// </summary>
    public void Fire(TriggerCondition condition)
    {
        if (InventoryController.Instance == null)
            return;

        if (InventoryController.Instance.EquipmentSystem != null)
        {
            foreach (var pair in InventoryController.Instance.EquipmentSystem.GetEquippedItems())
                FireIfMatches(pair.Value != null ? pair.Value.itemData : null, condition);
        }

        FireRelics(condition);
    }

    /// <summary>인벤토리에 있는 유물(Relic 카테고리) 중 조건이 일치하는 발동형 고유효과를 발동시킨다.</summary>
    private void FireRelics(TriggerCondition condition)
    {
        InventoryGrid playerGrid = InventoryController.Instance.PlayerGrid;
        if (playerGrid == null)
            return;

        foreach (InventoryItem inventoryItem in playerGrid.GetAllItems())
        {
            ItemInstance itemData = inventoryItem?.itemData;
            if (itemData?.definition == null || itemData.definition.category != ItemCategory.Relic)
                continue;

            FireIfMatches(itemData, condition);
        }
    }

    private static void FireIfMatches(ItemInstance itemInstance, TriggerCondition condition)
    {
        var uniqueEffect = itemInstance != null && itemInstance.definition != null ? itemInstance.definition.uniqueEffect : null;

        if (uniqueEffect is TriggeredBuffUniqueEffectSO triggered && triggered.triggerCondition == condition)
            triggered.OnTrigger(itemInstance);
    }
}
