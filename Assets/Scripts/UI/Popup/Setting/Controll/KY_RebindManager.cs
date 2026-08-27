using UnityEngine;

/// <summary>
/// [2026-08-26 WJ 임시 비활성화] 기존 3가지 책임을 아래로 이관하고 이 클래스는 내용을 비웠다.
///   1) 입력 액션(GameInputActions) 보유 + Enable      -> KeyBindingService.InputActions
///   2) 바인딩 오버라이드 PlayerPrefs 저장/로드          -> KeyBindingService.Save() / (내부 자동 로드)
///   3) 리바인드 오버레이 UI 플로우(StartRebind)         -> KY_SettingsPopup.StartRebind()
/// 자세한 배경은 Docs/Architecture/Structure_Cleanup_TODO.md 2번 항목 참고.
///
/// 파일/클래스 자체는 아직 지우지 않았다 - 이 컴포넌트가 씬 여러 개(WJ 소유 2개 외 SW/BH/JYJ 쪽
/// 포함 8곳)에 GameObject로 남아있는데, 그 씬들을 전부 지금 건드리는 대신 클래스만 비워서
/// missing script 경고 없이 그대로 안전하게 남아있게 했다. WJ 소유 씬 2곳(WJ_Act1_Camp,
/// PM_Act1_Camp_MergeTest)은 이 GameObject 자체를 정리했다. 남은 씬들은 각 담당자가 확인 후
/// GameObject를 지우면 되고, 그때 이 파일도 완전히 삭제하면 된다.
/// </summary>
public class KY_RebindManager : MonoBehaviour
{
}
