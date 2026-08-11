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
    // KY_RebindManager가 저장하는 PlayerPrefs 키와 동일하다 - 설정에서 바꾼 키를 그대로 반영하려고
    // 직접 로드한다(다른 화면에 KY_RebindManager 인스턴스가 없어도 리바인딩이 항상 적용되게).
    private const string KeyBindingsPrefKey = "KeyBindings";

    /// <summary>스킬 키(1~4)가 눌렸을 때 발행. 인자는 0~3 (Skill1~4).</summary>
    public event Action<int> OnSkillKeyPressed;

    private GameInputActions inputActions;

    private void Awake()
    {
        inputActions = new GameInputActions();

        if (PlayerPrefs.HasKey(KeyBindingsPrefKey))
            inputActions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(KeyBindingsPrefKey));
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void OnDestroy()
    {
        inputActions.Dispose();
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
    }
}
