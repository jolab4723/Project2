using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WJ 원본 <c>PlayerActionInputHandler</c>의 PlayerContext 전환 검증용 복제본이다.
/// <para>원본: <c>Assets/WJ_TestPlace/Script/Player/PlayerActionInputHandler.cs</c></para>
/// <para>포션 입력은 같은 플레이어의 <c>PlayerRuntimeStateSync_MirrorTest</c>를 통해 서버에 요청하고,
/// 서버가 <c>PotionUseManager_MirrorTest</c>를 실행한다.</para>
/// <para>리바인딩 로드는 원본과 동일하며, 입력 인스턴스 생성은 중복 생성을 막도록 한 경로로 모았다.</para>
/// <para>로컬 플레이어 여부는 여기서 전역 조회하지 않고 <c>MirrorSpawnedPlayerBinder</c>가 이 컴포넌트의 활성화를 제어한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerActionInputHandler_MirrorTest : MonoBehaviour
{
    private const string KeyBindingsPrefKey = "KeyBindings";

    [SerializeField] private PlayerRuntimeStateSync_MirrorTest runtimeState;

    public event Action<int> OnSkillKeyPressed;
    public event Action<int> OnSkillKeyReleased;

    private GameInputActions inputActions;

    private void Awake()
    {
        runtimeState ??= GetComponent<PlayerRuntimeStateSync_MirrorTest>();
        CreateInputActions();
    }

    private void OnEnable()
    {
        CreateInputActions();
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions?.Disable();
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void Update()
    {
        if (inputActions.Player.Potion.triggered)
            runtimeState?.RequestUsePotion();

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

    private void CreateInputActions()
    {
        if (inputActions != null)
            return;

        inputActions = new GameInputActions();

        if (PlayerPrefs.HasKey(KeyBindingsPrefKey))
            inputActions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(KeyBindingsPrefKey));
    }
}
