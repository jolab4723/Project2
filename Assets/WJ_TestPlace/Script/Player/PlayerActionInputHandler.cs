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
    private readonly bool[] heldSkills = new bool[4];
    private Action potionUseRequest;
    private Action<int> skillPressedRequest;
    private Action<int> skillReleasedRequest;
    private T_PlayerController playerController;

    /// <summary>SW 수정: 공유 입력과 플레이어의 제어 가능 상태를 확인할 컴포넌트를 가져온다.</summary>
    private void Awake()
    {
        inputActions = KeyBindingService.InputActions;
        playerController = GetComponent<T_PlayerController>();
    }

    /// <summary>SW 수정: 공통 키 해석을 유지하고 포션·스킬의 실행을 소유자의 권한에 연결한다.</summary>
    public void BindExternalActions(Action potionUseAction, Action<int> skillPressedAction, Action<int> skillReleasedAction)
    {
        ReleaseHeldSkills();
        potionUseRequest = potionUseAction;
        skillPressedRequest = skillPressedAction;
        skillReleasedRequest = skillReleasedAction;
    }

    /// <summary>SW 수정: 다시 활성화될 때 공유 키 입력을 가져온다.</summary>
    private void OnEnable() => inputActions = KeyBindingService.InputActions;

    /// <summary>SW 수정: 비활성화될 때 눌린 스킬을 해제한다.</summary>
    private void OnDisable() => ReleaseHeldSkills();

    private Func<bool> inputConsumptionCheck;

    /// <summary>SW 수정: 채팅 등 다른 화면이 입력을 사용하는지 확인할 함수를 연결한다.</summary>
    public void BindInputConsumption(Func<bool> isInputConsumed) => inputConsumptionCheck = isInputConsumed;

    /// <summary>SW 수정: 입력 잠금 시 스킬을 해제하고, 사용 가능한 포션·스킬 입력을 연결된 실행 요청에 전달한다.</summary>
    private void Update()
    {
        if ((inputConsumptionCheck != null && inputConsumptionCheck()) || (playerController != null && !playerController.IsControlEnabled))
        {
            ReleaseHeldSkills();
            return;
        }
        if (inputActions.Player.Potion.triggered)
        {
            if (potionUseRequest != null) potionUseRequest();
            else PotionUseManager.Instance?.TryUsePotion();
        }

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

    /// <summary>SW 수정: 눌린 스킬을 기록하고 연결된 실행 요청 또는 기존 입력 이벤트에 전달한다.</summary>
    private void PressSkill(int skillIndex)
    {
        heldSkills[skillIndex] = true;
        if (skillPressedRequest != null) skillPressedRequest(skillIndex);
        else OnSkillKeyPressed?.Invoke(skillIndex);
    }

    /// <summary>SW 수정: 눌린 스킬만 한 번 해제하고 연결된 실행 요청 또는 기존 입력 이벤트에 전달한다.</summary>
    private void ReleaseSkill(int skillIndex)
    {
        if (!heldSkills[skillIndex]) return;
        heldSkills[skillIndex] = false;
        if (skillReleasedRequest != null) skillReleasedRequest(skillIndex);
        else OnSkillKeyReleased?.Invoke(skillIndex);
    }

    /// <summary>SW 수정: 입력 잠금·비활성화 전에 눌린 스킬을 한 번 해제해 차징 상태가 남지 않게 한다.</summary>
    public void ReleaseHeldSkills()
    {
        for (int skillIndex = 0; skillIndex < heldSkills.Length; skillIndex++) ReleaseSkill(skillIndex);
    }
}
