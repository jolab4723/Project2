# 구조 정리 백로그

> 생성일: 2026-08-26

## 1. 이 문서의 목적

- 지금 당장 고칠 범위는 아니지만 나중에 반드시 재검토해야 할 정리 항목을 모아두는 백로그다.
- `DecisionLog.md`와 달리 팀이 합의한 결정이 아니다. 팀이 실제로 방향을 합의하면 그때 `DecisionLog.md`로 옮긴다.
- `Docs/Architecture/Multiplayer_Readiness_TODO.md`와 성격은 같고(재검토용 백로그), 대상이 멀티플레이/`PlayerContext` 전환 한정이 아니라 구조 정리 전반이라 별도 문서로 둔다.
- 새 항목은 발견 즉시 추가하고, 기존 항목은 지우지 말고 상태만 갱신한다.

## 2. 작업 목록

| 발견일 | 항목 | 관련 코드 | 문제/이유 | 상태 |
|---|---|---|---|---|
| 2026-08-26 | 설정에서 언어 변경 시 이미 떠 있는 텍스트 레이블 즉시 변환 | `Assets/Scripts/System/YJ_LanguageManager.cs`(`LanguageChanged` 이벤트), WJ 라벨 파이프라인(`StatLabelDatabaseSO`/`ItemLabelDatabaseSO`/`EnemyLabelDatabaseSO`) | `LanguageChanged` 이벤트는 이미 있고 `KY_StatusPopup`/`YJ_LocalizedFont`/`YJ_UnknownStageManager` 등 일부만 구독 중이다. WJ가 만든 스킬/아이템/스탯 라벨 기반 UI(스킬 팝업, 툴팁 등)는 아직 이 이벤트를 구독하지 않아서, 화면에 이미 떠 있는 상태에서 설정 언어를 바꾸면 즉시 반영되지 않고 다음에 새로 열 때만 바뀔 가능성이 높다(`LanguageTestKeyTrigger.cs`에 이미 같은 우려가 주석으로 남아있음). 실제로 어떤 UI가 안 바뀌는지 재확인 후 구독 추가 필요. | 미착수 |
| 2026-08-26 | `KY_RebindManager`(키 리바인드) 기능을 다른 스크립트로 이관하고 이 매니저의 인스턴스는 없앤다 | `Assets/Scripts/UI/Popup/Setting/Controll/KY_RebindManager.cs` | 사용자(WJ) 요청으로 등록. 지금은 씬의 `-------------------------------Managers` 하위에 독립 매니저로 떠 있는데, 이 형태를 유지할 필요가 없다고 판단해 기능만 다른 스크립트로 옮기고 이 매니저 인스턴스 자체는 없애기로 함. 이관 대상 스크립트와 구체적 방식은 아직 미정. | 미착수 |
| 2026-08-26 | `PlayerWallet`(재화) 기능을 다른 스크립트로 이관하고 이 매니저의 인스턴스는 없앤다 | `Assets/SW/Scripts/Player/PlayerWallet.cs` | 사용자(WJ) 요청으로 등록. `KY_RebindManager`와 동일한 맥락 - 씬의 `Managers` 하위 독립 매니저 형태를 없애고 기능만 옮긴다. SW님 소유 스크립트라 실제 이관 시 대상/방식을 SW님과 맞춰야 함. | 미착수 |
| 2026-08-26 | 모든 씬에 있어야 하는 전역 매니저(`GameManager`/`ItemManager`/`DataManager`/`SceneLoader`)와 스테이지별 고유 데이터를 갖는 매니저(`StageManager` 등)를 구조적으로 분리 | `Assets/WJ_TestPlace/Script/Core/Singleton.cs`, `Assets/WJ_TestPlace/Script/Core/Manager/GameManager.cs`, 각 씬의 `-------------------------------Managers` 계층(`Act1_Camp.unity`/`Act1_Stage1.unity` 등 스테이지·캠프 씬 전체 + WJ 미러 씬) | 이번에 "다른 씬으로 넘어가면 이벤트 시스템이 중복되는 것 같다"는 사용자 제보를 조사해 발견함. 전역 싱글톤 4개(`GameManager`/`ItemManager`/`DataManager`/`SceneLoader`)가 `EventSystem`/`StageManager`/`ItemSystemController`/`KY_UIInputManager`/`PlayerWallet`/`KY_SettingsManager`/`KY_RebindManager`와 같은 부모(`-------------------------------Managers`) 밑에 있어서, `Singleton<T>.Awake()`의 `DontDestroyOnLoad(transform.root.gameObject)`가 이 형제 오브젝트들까지 전부 다음 씬으로 끌고 간다. 그런데 중복 감지·제거는 `Destroy(gameObject)`(자기 자신만) 뿐이라, 씬 전환마다 `EventSystem`뿐 아니라 `StageManager`/`KY_UIInputManager`/`PlayerWallet`/`KY_SettingsManager`/`KY_RebindManager`까지 형제 전체가 새 씬의 사본과 중복돼서 쌓인다. `GameManager.Awake()`에서 중복 시 자기 자신 대신 `transform.root.gameObject` 전체를 지우는 방식도 검토했지만, 그러면 `YJ_StageManager.Start()`(`LoadGameplayData()` 호출)처럼 씬마다 새로 실행돼야 하는 로컬 매니저의 `Start()`가 씬 전환 이후로는 아예 안 돌게 되는 더 큰 문제가 생겨서 보류함. 근본 해결은 코드 한 곳이 아니라 전역 싱글톤 4개를 별도 루트로 분리하는 씬 구조 변경이고, 이 계층이 JYJ님 소유 스테이지/캠프 씬 전부에 동일하게 있어서 여러 담당자와 조율 필요. | 미착수 |

## 3. 참고

- 항목을 추가할 때는 관련 코드 경로와 "왜 문제인지/왜 미룬 건지"를 짧게 남긴다 - 나중에 재검토할 때 코드를 처음부터 다시 읽지 않아도 되게 하기 위함.
- 이 목록에 있다고 해서 지금 당장 고쳐야 하는 것은 아니다.
- `EventSystem 중복` 항목(4번째 행)은 여러 담당자의 씬에 걸친 구조 변경이라, 실제 착수 전 팀 논의가 먼저 필요하다.
