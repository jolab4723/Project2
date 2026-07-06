using UnityEngine;
using UnityEngine.InputSystem;
using ItemSystem;

/// <summary>
/// 테스트용: T/Y키로 버프를 즉시 발동시키는 트리거.
/// T - 공격력 +50 (고정치)
/// Y - 공격력 +20% (퍼센트)
/// 실제 게임 트리거(포션 소비, 스킬 등)가 생기면 이 스크립트는 지워도 됨.
/// </summary>
public class BuffTestKeyTrigger : MonoBehaviour
{
    [Tooltip("비워두면 PlayerStatManager를 통해 자동으로 찾음")]
    [SerializeField] private PlayerBuffManager buffManager;

    [Header("T - 공격력 +50 (고정치)")]
    [SerializeField] private BuffDefinitionSO attackFlatBuff;

    [Header("Y - 공격력 +20% (퍼센트)")]
    [SerializeField] private BuffDefinitionSO attackPercentBuff;

    private PlayerBuffManager BuffManager
    {
        get
        {
            if (buffManager != null)
                return buffManager;

            // PlayerStatManager가 buffManagerBehaviour로 들고 있는 걸 그대로 재사용
            return PlayerStatManager.Instance != null
                ? PlayerStatManager.Instance.GetComponent<PlayerBuffManager>()
                : null;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.tKey.wasPressedThisFrame)
            ApplyBuff(attackFlatBuff);

        if (Keyboard.current.yKey.wasPressedThisFrame)
            ApplyBuff(attackPercentBuff);
    }

    private void ApplyBuff(BuffDefinitionSO def)
    {
        if (def == null)
        {
            Debug.LogWarning("[BuffTestKeyTrigger] 버프 정의가 연결되지 않았습니다.");
            return;
        }

        if (BuffManager == null)
        {
            Debug.LogWarning("[BuffTestKeyTrigger] PlayerBuffManager를 찾을 수 없습니다.");
            return;
        }

        BuffManager.ApplyBuff(def);
        Debug.Log($"[BuffTestKeyTrigger] {def.buffName} 적용됨.");
    }
}
