using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WJ 원본 <c>PlayerActionInputHandler</c>의 PlayerContext 전환 검증용 복제본이다.
/// <para>원본: <c>Assets/WJ_TestPlace/Script/Player/PlayerActionInputHandler.cs</c></para>
/// <para>포션 입력은 같은 플레이어의 <c>PlayerRuntimeStateSync</c>를 통해 서버에 요청하고,
/// 서버가 <c>NetworkPotionUseManager</c>를 실행한다.</para>
/// <para>설정 UI와 같은 입력 인스턴스를 사용해 키 변경을 즉시 반영한다. 공유 입력의 수명은 KeyBindingService가 관리한다.</para>
/// <para>로컬 플레이어 여부는 여기서 전역 조회하지 않고 <c>MirrorSpawnedPlayerBinder</c>가 이 컴포넌트의 활성화를 제어한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkPlayerActionInputHandler : MonoBehaviour
{
    [SerializeField] private PlayerRuntimeStateSync runtimeState;

    public event Action<int> OnSkillKeyPressed;
    public event Action<int> OnSkillKeyReleased;

    private GameInputActions inputActions;
    private readonly bool[] heldSkills = new bool[4];

    private void Awake()
    {
        runtimeState ??= GetComponent<PlayerRuntimeStateSync>();
    }

    private void OnEnable()
    {
        inputActions = KeyBindingService.InputActions;
    }

    private void OnDisable()
    {
        ReleaseHeldSkills();
    }

    private void Update()
    {
        if (inputActions.Player.Potion.triggered)
            runtimeState?.RequestUsePotion();

        if (inputActions.Player.Skill1.triggered)
            PressSkill(0);
        if (inputActions.Player.Skill2.triggered)
            PressSkill(1);
        if (inputActions.Player.Skill3.triggered)
            PressSkill(2);
        if (inputActions.Player.Skill4.triggered)
            PressSkill(3);

        if (inputActions.Player.Skill1.WasReleasedThisFrame())
            ReleaseSkill(0);
        if (inputActions.Player.Skill2.WasReleasedThisFrame())
            ReleaseSkill(1);
        if (inputActions.Player.Skill3.WasReleasedThisFrame())
            ReleaseSkill(2);
        if (inputActions.Player.Skill4.WasReleasedThisFrame())
            ReleaseSkill(3);
    }

    private void PressSkill(int index)
    {
        heldSkills[index] = true;
        OnSkillKeyPressed?.Invoke(index);
    }

    private void ReleaseSkill(int index)
    {
        if (!heldSkills[index]) return;
        heldSkills[index] = false;
        OnSkillKeyReleased?.Invoke(index);
    }

    public void ReleaseHeldSkills()
    {
        for (int i = 0; i < heldSkills.Length; i++) ReleaseSkill(i);
    }

}
