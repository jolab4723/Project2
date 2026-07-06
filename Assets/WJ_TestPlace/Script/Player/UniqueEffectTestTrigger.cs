using UnityEngine;
using UnityEngine.InputSystem;
using ItemSystem;

/// <summary>
/// 테스트용: U키로 발동형 고유 효과(TriggeredBuffUniqueEffectSO)를 강제로 발동시킨다.
/// 실제 게임에서는 전투 시스템이 조건(치명타/피격 등) 판정 후 OnTrigger를 불러야 함 -
/// 지금은 그 자리를 U키로 대신 테스트하는 것.
/// </summary>
public class UniqueEffectTestTrigger : MonoBehaviour
{
    [Tooltip("발동형 고유 효과를 가진 아이템")]
    [SerializeField] private ItemDefinitionSO triggeredItem;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
            TriggerEffect();
    }

    private void TriggerEffect()
    {
        if (triggeredItem == null || triggeredItem.uniqueEffect == null)
        {
            Debug.LogWarning("[UniqueEffectTestTrigger] triggeredItem 또는 uniqueEffect가 연결되지 않았습니다.");
            return;
        }

        triggeredItem.uniqueEffect.OnTrigger(null);
        Debug.Log($"[UniqueEffectTestTrigger] '{triggeredItem.itemName}'의 고유 효과 발동.");
    }
}
