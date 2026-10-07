using System;
using UnityEngine;

/// <summary>
/// 공통 액션 입력의 의도를 같은 플레이어의 포션 상태와 스킬 권한에 연결한다.
/// 키 해석·리바인딩·차징 해제는 PlayerActionInputHandler 한 곳에서 수행한다.
/// 공유 입력의 수명은 KeyBindingService가 관리하고 소유자 활성화는 Binder가 결정한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerActionInputHandler))]
public sealed class NetworkPlayerActionInputHandler : MonoBehaviour
{
    [SerializeField] private PlayerRuntimeStateSync runtimeState;
    public event Action<int> OnSkillKeyPressed;
    public event Action<int> OnSkillKeyReleased;
    private PlayerActionInputHandler input;

    private void Awake()
    {
        runtimeState ??= GetComponent<PlayerRuntimeStateSync>();
        input = GetComponent<PlayerActionInputHandler>();
    }

    private void OnEnable() => input.BindExternalActions(RequestPotion, PressSkill, ReleaseSkill);
    private void OnDisable()
    {
        ReleaseHeldSkills();
        input?.BindExternalActions(null, null, null);
    }

    private void RequestPotion() => runtimeState?.RequestUsePotion();
    private void PressSkill(int index) => OnSkillKeyPressed?.Invoke(index);
    private void ReleaseSkill(int index) => OnSkillKeyReleased?.Invoke(index);
    public void ReleaseHeldSkills() => input?.ReleaseHeldSkills();
}
