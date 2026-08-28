using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 포션/스킬 키 입력을 감지해서 필요한 곳에 전달한다.
///
/// 이동/공격/회피(WBH_PlayerInputHandler)는 레거시 Input을 그대로 쓰고 있어서 건들지 않고,
/// 포션/스킬은 새로 GameInputActions(Input System)를 써서 별도 스크립트로 분리했다 - 키를
/// 고정하지 않고 설정 화면에서 리바인딩할 수 있어야 하기 때문이다. 실제 키 매핑과 리바인딩
/// 자체는 이미 있는 GameInputActions.inputactions 에셋(Potion/Skill1~4 액션)이 담당하고,
/// 이 스크립트는 "지금 그 액션이 눌렸는지"만 감지한다.
///
/// 스킬 시스템 자체는 아직 없어서, 스킬 키는 바로 실행하지 않고 OnSkillKeyPressed 이벤트로만
/// 알린다 - 나중에 스킬 시스템이 이 이벤트를 구독하면 된다.
/// </summary>
public class PlayerActionInputHandler : MonoBehaviour
{
    /// <summary>스킬 키(1~4)가 눌렸을 때 발행. 인자는 0~3 (Skill1~4).</summary>
    public event Action<int> OnSkillKeyPressed;

    /// <summary>스킬 키(1~4)에서 손을 뗐을 때 발행. 인자는 0~3 (Skill1~4). 차지형 스킬(누르고 있다가
    /// 떼면 발동)에서 사용 - 차지가 아닌 스킬은 이 이벤트를 무시하면 됨.</summary>
    public event Action<int> OnSkillKeyReleased;

    // 예전엔 여기서 자체 GameInputActions를 만들고 PlayerPrefs("KeyBindings")를 직접 로드했다 -
    // 당시엔 KY_RebindManager 인스턴스가 씬마다 없을 수 있어서 그걸 우회하려고 중복 구현한 것이었다.
    // KeyBindingService(순수 static, 앱 전체 공유)로 이관하면서 이 중복이 사라졌고, 설정에서 리바인드한
    // 값도 같은 인스턴스를 보므로 씬과 무관하게 항상 반영된다. 공유 인스턴스라 더 이상 이 컴포넌트가
    // Enable/Disable/Dispose하지 않는다(그러면 이 인스턴스를 같이 쓰는 다른 화면에도 영향을 준다).
    private GameInputActions inputActions;

    private void Awake()
    {
        inputActions = KeyBindingService.InputActions;
    }

    private void Update()
    {
        if (inputActions.Player.Potion.triggered)
            PotionUseManager.Instance?.TryUsePotion();

        if (inputActions.Player.Skill1.triggered)
            OnSkillKeyPressed?.Invoke(0);
        if (inputActions.Player.Skill2.triggered)
            OnSkillKeyPressed?.Invoke(1);
        if (inputActions.Player.Skill3.triggered)
            OnSkillKeyPressed?.Invoke(2);
        if (inputActions.Player.Skill4.triggered)
            OnSkillKeyPressed?.Invoke(3);

        if (inputActions.Player.Skill1.WasReleasedThisFrame())
            OnSkillKeyReleased?.Invoke(0);
        if (inputActions.Player.Skill2.WasReleasedThisFrame())
            OnSkillKeyReleased?.Invoke(1);
        if (inputActions.Player.Skill3.WasReleasedThisFrame())
            OnSkillKeyReleased?.Invoke(2);
        if (inputActions.Player.Skill4.WasReleasedThisFrame())
            OnSkillKeyReleased?.Invoke(3);
    }
}
