using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 앱 전체가 공유하는 입력 액션(키 바인딩) 하나만 보유·저장하는 순수 C# 서비스.
///
/// 예전엔 KY_RebindManager라는 MonoBehaviour 싱글톤(Instance)이 이 역할을 맡았는데, 씬의
/// "-------------------------------Managers" 루트에 얹혀 GameManager 등과 함께 DontDestroyOnLoad로
/// 다음 씬까지 넘어가면서도, 정작 자기 자신의 중복 방지 로직은 없어서 씬 전환마다 오브젝트가
/// 그대로 쌓이는 문제가 있었다(Docs/Architecture/Structure_Cleanup_TODO.md 4번 항목과 같은 근본 원인).
/// 이 역할은 GameObject나 Awake 생명주기가 필요 없는 순수 데이터 보유+저장이라 MonoBehaviour
/// 없이 static으로 옮겼다. 리바인드 오버레이 UI 플로우(예전 KY_RebindManager.StartRebind)는
/// KY_SettingsPopup으로 이관됐다 - 그쪽에서 리바인드 완료 시 Save()를 호출한다.
/// </summary>
public static class KeyBindingService
{
    private const string KeyBindingsPrefKey = "KeyBindings";

    private static GameInputActions inputActions;

    /// <summary>앱 전체가 공유하는 입력 액션 인스턴스. 최초 접근 시 생성되고, 저장된 바인딩
    /// 오버라이드가 있으면 그대로 불러온다 - 여러 컴포넌트가 같은 인스턴스를 나눠 쓰므로, 더 이상
    /// 개별 컴포넌트가 이 인스턴스를 Disable/Dispose하면 안 된다(그러면 이 인스턴스를 참조하는
    /// 다른 컴포넌트도 같이 영향을 받는다).
    /// 접근할 때마다 Enable()을 매번 호출한다(이미 Enable된 상태에서 호출해도 안전한 멱등 연산) -
    /// 에디터에서 "Enter Play Mode Options: Disable Domain Reload"가 켜져 있으면 static 필드는
    /// Play 진입/종료 사이에 그대로 남아있는데, Input System 쪽 네이티브 상태는 Play 모드 전환 때
    /// 리셋되면서 실제로는 Disable 상태가 될 수 있다(직접 겪어서 발견 - WJ). 생성 시 한 번만 Enable하면
    /// 이 경우 다음 Play 진입부터 조용히 입력이 안 먹는 문제가 생겨서, 매번 Enable을 보장한다.</summary>
    public static GameInputActions InputActions
    {
        get
        {
            if (inputActions == null)
            {
                inputActions = new GameInputActions();

                if (PlayerPrefs.HasKey(KeyBindingsPrefKey))
                    inputActions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(KeyBindingsPrefKey));
            }

            inputActions.Enable();
            return inputActions;
        }
    }

    /// <summary>현재 바인딩 오버라이드를 PlayerPrefs에 저장한다. 리바인드 완료 시(KY_SettingsPopup.StartRebind
    /// 의 OnComplete)에서 호출한다.</summary>
    public static void Save()
    {
        string json = InputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(KeyBindingsPrefKey, json);
        PlayerPrefs.Save();
    }
}
