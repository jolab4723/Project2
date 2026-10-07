using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트용: F1~F4로 표시 언어를 즉시 전환한다.
///   F1 = 한국어 / F2 = 영어 / F3 = 일본어 / F4 = 중국어
///
/// 실제 언어 변경은 YJ_LanguageManager가 담당하고 이 스크립트는 호출만 한다.
/// 언어 선택 UI(설정 화면 드롭다운 등)가 만들어지면 이 스크립트는 지워도 된다.
/// (드롭다운은 YJ_LanguageManager.SetLanguageByIndex(int)에 바로 연결하면 됨)
///
/// !! 이미 화면에 떠 있는 UI가 즉시 바뀌는지는 그 UI가 YJ_LanguageManager.LanguageChanged를
///    구독하고 있는지에 달려 있다. 구독하지 않는 UI는 다시 열었을 때 새 언어로 표시된다.
/// </summary>
public class LanguageTestKeyTrigger : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.f1Key.wasPressedThisFrame)
            Apply(GameLanguage.KOR);
        else if (Keyboard.current.f2Key.wasPressedThisFrame)
            Apply(GameLanguage.ENG);
        else if (Keyboard.current.f3Key.wasPressedThisFrame)
            Apply(GameLanguage.JPN);
        else if (Keyboard.current.f4Key.wasPressedThisFrame)
            Apply(GameLanguage.CHN);
    }

    private void Apply(GameLanguage language)
    {
        if (YJ_LanguageManager.Instance == null)
        {
            Debug.LogWarning("[LanguageTestKeyTrigger] YJ_LanguageManager가 씬에 없습니다. " +
                             "언어를 바꾸려면 씬에 배치해야 합니다.");
            return;
        }

        if (YJ_LanguageManager.Instance.CurrentLanguage == language)
        {
            Debug.Log($"[LanguageTestKeyTrigger] 이미 {language} 입니다.");
            return;
        }

        YJ_LanguageManager.Instance.SetLanguage(language);
        Debug.Log($"[LanguageTestKeyTrigger] 표시 언어를 {language}로 변경했습니다.");
    }
}
