using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using ItemSystem;

/// <summary>
/// 테스트용: 키 입력으로 플레이어가 바라보는 방향으로 실제 공격을 실행시킨다.
///
/// SW(성우님) 검증용 - WBH_CombatManager.ProcessDamage에 새로 연결한
/// ItemTriggerManager.Fire(OnDamageDealt/OnCrit) 배선이 실제 공격 경로에서 동작하는지 확인하는 용도.
/// 마우스 조준 없이 T_PlayerCombat.TryAttack()을 그대로 호출해서, 실제 입력(WBH_PlayerInputHandler)과
/// 동일한 프로덕션 경로(상태 전환 -> 애니메이션 -> AniEvent_FighterAttackEvent -> ExecuteAttack ->
/// FighterAttack/GunnerAttack -> WBH_CombatManager.ProcessDamage)를 그대로 탄다.
///
/// 확인 방법: 적을 사거리 안에 두고(파이터는 정면 부채꼴, 거너는 조준 방향) 테스트 키를 누른 뒤,
/// OnDamageDealt로 설정된 고유 효과(예: 양산형 코어의 "쾌속 발놀림" - 이동 속도 20% 증가)가
/// 실제로 걸리는지 콘솔 로그와 스탯 변화로 확인하면 된다.
/// </summary>
public class AttackTriggerTestKey : MonoBehaviour
{
    [Tooltip("이 키를 누르면 플레이어가 바라보는 방향으로 공격을 실행한다.")]
    [SerializeField] private Key testKey = Key.G;

    private T_PlayerCombat combat;

    private void Awake()
    {
        combat = GetComponent<T_PlayerCombat>();
        if (combat == null)
            Debug.LogWarning("[AttackTriggerTestKey] 같은 오브젝트에 T_PlayerCombat이 없습니다.", this);
    }

    private void Update()
    {
        if (Keyboard.current == null || combat == null)
            return;

        if (Keyboard.current[testKey].wasPressedThisFrame)
            TriggerAttack();
    }

    private void TriggerAttack()
    {
        // targetPos는 바라보는 방향으로만 쓰이므로(T_PlayerCombat.TryAttack 참고),
        // 현재 forward 방향으로 한 걸음 앞을 지정해 마우스 조준 없이도 실제 공격 경로를 그대로 탄다.
        Vector3 targetPos = transform.position + transform.forward;
        combat.TryAttack(targetPos);

        LogTriggerCandidates();
    }

    /// <summary>
    /// 지금 OnDamageDealt/OnCrit로 설정된 고유 효과를 장착/보유 중인지 미리 알려준다.
    /// 아무것도 안 뜨면 공격 자체는 실행됐어도 확인할 효과가 없다는 뜻이라 헷갈리지 않게 하기 위함.
    /// </summary>
    private void LogTriggerCandidates()
    {
        if (InventoryController.Instance == null)
        {
            Debug.LogWarning("[AttackTriggerTestKey] InventoryController.Instance가 없어 장착/보유 아이템을 확인할 수 없습니다.");
            return;
        }

        var candidates = GetEquippedAndHeldEffects()
            .Where(e => e is TriggeredBuffUniqueEffectSO triggered
                && (triggered.triggerCondition == TriggerCondition.OnDamageDealt
                    || triggered.triggerCondition == TriggerCondition.OnCrit))
            .Cast<TriggeredBuffUniqueEffectSO>()
            .ToList();

        if (candidates.Count == 0)
        {
            Debug.Log("[AttackTriggerTestKey] 공격 실행함. 단, OnDamageDealt/OnCrit 고유 효과를 가진 아이템이 없습니다 " +
                      "(예: '양산형 코어'를 장착/보유하면 확인 가능).");
            return;
        }

        foreach (var effect in candidates)
            Debug.Log($"[AttackTriggerTestKey] 공격 실행함. 대상 효과: '{effect.effectName}' ({effect.name}, 조건={effect.triggerCondition})");
    }

    private System.Collections.Generic.IEnumerable<UniqueEffectSO> GetEquippedAndHeldEffects()
    {
        if (InventoryController.Instance.EquipmentSystem != null)
        {
            foreach (var pair in InventoryController.Instance.EquipmentSystem.GetEquippedItems())
            {
                var effect = pair.Value?.itemData?.definition?.uniqueEffect;
                if (effect != null)
                    yield return effect;
            }
        }

        InventoryGrid playerGrid = InventoryController.Instance.PlayerGrid;
        if (playerGrid == null)
            yield break;

        foreach (InventoryItem inventoryItem in playerGrid.GetAllItems())
        {
            var effect = inventoryItem?.itemData?.definition?.uniqueEffect;
            if (effect != null && inventoryItem.itemData.definition.category == ItemCategory.Relic)
                yield return effect;
        }
    }
}
