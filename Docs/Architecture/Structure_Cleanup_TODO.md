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
| 2026-08-26 | 설정에서 언어 변경 시 이미 떠 있는 텍스트 레이블 즉시 변환 | `Assets/WJ_TestPlace/Script/Player/Skill/SkillPopupController.cs`, `Assets/WJ_TestPlace/Script/Player/PlayerStatUIManager.cs` | `LanguageChanged` 이벤트는 이미 있고 `KY_StatusPopup`/`YJ_LocalizedFont`/`YJ_UnknownStageManager` 등 일부만 구독 중이다. WJ가 만든 스킬/아이템/스탯 라벨 기반 UI(스킬 팝업, 툴팁 등)는 아직 이 이벤트를 구독하지 않아서, 화면에 이미 떠 있는 상태에서 설정 언어를 바꾸면 즉시 반영되지 않고 다음에 새로 열 때만 바뀔 가능성이 높다(`LanguageTestKeyTrigger.cs`에 이미 같은 우려가 주석으로 남아있음). | **WJ 소유 파일 완료(이우진.md 148번)** - `SkillPopupController`/`PlayerStatUIManager`가 `YJ_LanguageManager.LanguageChanged`를 구독해 팝업이 열려있는 동안 언어를 바꿔도 즉시 재갱신되도록 수정, Play 모드에서 구독 목록 직접 확인해 검증함. **아이템 툴팁(`Assets/SW/Scripts/ItemTooltip/TooltipUI.cs`, `Assets/SW/Scripts/WorldItem/WorldItemTooltipView.cs`)은 SW님 소유라 이번엔 손대지 않기로 함(사용자 확인) - 아직 미착수로 남아있음.** |
| 2026-08-26 | `KY_RebindManager`(키 리바인드) 기능을 다른 스크립트로 이관하고 이 매니저의 인스턴스는 없앤다 | `Assets/Scripts/UI/Popup/Setting/Controll/KeyBindingService.cs`(신규), `KY_RebindManager.cs`(비움), `KY_SettingsPopup.cs`, `KY_RebindSlot.cs` | 사용자(WJ) 요청으로 등록. 지금은 씬의 `-------------------------------Managers` 하위에 독립 매니저로 떠 있는데, 이 형태를 유지할 필요가 없다고 판단해 기능만 다른 스크립트로 옮기고 이 매니저 인스턴스 자체는 없애기로 함. | **코드 이관 완료(이우진.md 143번)** - 입력 액션 보유/저장은 `KeyBindingService`(순수 static)로, 리바인드 UI 플로우는 `KY_SettingsPopup`으로 이관, `KY_RebindManager.cs`는 삭제 대신 내용만 비움(다른 담당자 씬에 missing script 안 생기게). WJ 소유 씬 2개(`WJ_Act1_Camp`/`PM_Act1_Camp_MergeTest`)만 GameObject 정리 완료, **SW/BH 등 나머지 담당자 씬 6곳은 아직 GameObject가 안 지워진 채로 남아있음 - 각 담당자 확인 후 정리 필요**. |
| 2026-08-26 | `PlayerWallet`(재화) 기능을 다른 스크립트로 이관하고 이 매니저의 인스턴스는 없앤다 | `Assets/SW/Scripts/Player/PlayerWallet.cs` | 사용자(WJ) 요청으로 등록. `KY_RebindManager`와 동일한 맥락 - 씬의 `Managers` 하위 독립 매니저 형태를 없애고 기능만 옮긴다. SW님 소유 스크립트라 실제 이관 시 대상/방식을 SW님과 맞춰야 함. | 미착수 |
| 2026-08-26 | 모든 씬에 있어야 하는 전역 매니저(`GameManager`/`ItemManager`/`DataManager`/`SceneLoader`)와 스테이지별 고유 데이터를 갖는 매니저(`StageManager` 등)를 구조적으로 분리 | `Assets/WJ_TestPlace/Script/Core/Singleton.cs` | 이번에 "다른 씬으로 넘어가면 이벤트 시스템이 중복되는 것 같다"는 사용자 제보를 조사해 발견함. 전역 싱글톤 4개(`GameManager`/`ItemManager`/`DataManager`/`SceneLoader`)가 `EventSystem`/`StageManager` 등과 같은 부모(`-------------------------------Managers`) 밑에 있어서, `Singleton<T>.Awake()`의 `DontDestroyOnLoad(transform.root.gameObject)`가 형제 오브젝트들까지 전부 다음 씬으로 끌고 가 씬 전환마다 중복이 쌓였다. | **해결 완료(이우진.md 145번)** - 처음 검토했던 "루트 전체를 지우는" 방식 대신, `Singleton<T>.Awake()`에서 부모를 통째로 살리는 대신 **자기 자신을 `SetParent(null)`로 분리해 독립 루트로 만든 뒤 자기 자신만 `DontDestroyOnLoad`**하도록 수정 - 씬 파일을 하나도 안 건드리는 코드 한 줄짜리 해결책이라 팀 논의 없이 바로 적용함. Play 모드에서 `WJ_Act1_Camp → Act1_Camp → Act1_Stage1` 2연속 실제 씬 전환으로 전역 싱글톤 4개/`EventSystem`/`StageManager` 전부 정확히 1개씩만 남는 것을 확인. |

## 3. 참고

- 항목을 추가할 때는 관련 코드 경로와 "왜 문제인지/왜 미룬 건지"를 짧게 남긴다 - 나중에 재검토할 때 코드를 처음부터 다시 읽지 않아도 되게 하기 위함.
- 이 목록에 있다고 해서 지금 당장 고쳐야 하는 것은 아니다.
