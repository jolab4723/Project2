# Project2 Mirror 통합 마무리 계획

**기준일:** 2026-09-30
**저장소:** `jolab4723/Project2`
**브랜치:** `unity-6000-3-22-test` (김성우, `codex/` 접두사 동명 지침 포함)
**검토 기준 커밋:** `6318486b1c332fffcfb8bad87e9c839b3f7dfbf4`
**산출물 성격:** 1~9절은 읽기 기반 구조 검토 및 실행 계획의 원문이다. 9월 30일 중단 이력은 10절, 10월 1일 재개 결과는 11절 및 누적 실행 기록에 구분했다. Commit/Push는 수행하지 않았다.

**최신 실행 상태(2026-10-01):** 정식 Act1·2의 16씬 공용 HUD/입력/고등급 HP/포털과 두 캠프 NPC 통합, Lobby/Pause 배선 및 미사용 복제본 정리를 반영했다. 사용자 지정 **Editor Host 1인** 범위에서 Fighter/Gunner 각각 23개 검사를 통과했다. 계정 저장·복귀와 전체 진행·원격 검증은 미완료이며, 기존 자산의 끊어진 참조를 별도 기록했다. [누적 실행 기록 §9.9](Mirror_Production_Integration_Execution.md#99-10월-1일-재개--정식-씬-통합잔재-정리와-editor-host-1인-검증)를 확인한다.

## 1. 판정과 검토 범위

**기존 통합은 실제로 진행됐다. 그러나 게임 규칙의 단일 원본화와 동일 씬 내부의 중복 인스턴스 정리가 남아 있다.**

`Assets/SW/Scripts/Network/Player` 44개, `Network/Combat` 15개, `Scripts/Chat` 3개를 검토 목록으로 삼고 공통 PlayerContext 및 BH/JYJ/KY 원본과 관련 구현 로그를 대조했다. 대형 파일은 통합 관련 메서드와 호출 경계 중심으로 검토했다. 이 문서는 모든 Scene/Prefab의 직렬화 참조를 전수 검사했거나 최신 커밋의 실제 멀티플레이를 실행했다는 보고서가 아니다. 로컬 작업 트리와 Unity 실행 환경은 확인하지 못했다. 삭제 후보의 최종 확정에는 실제 GUID/Inspector/AnimationEvent/호출부 검사가 필요하다.

아래에서는 **코드에서 확인한 구조**, **로그에 기록된 과거 검증**, **추가 확인이 필요한 삭제/정책 후보**를 구분한다. 코드상 차이를 이번 검토에서 재현한 플레이 버그로 표시하지 않는다.

### 이미 공통화된 부분 — 다시 만들지 않는다

- PlayerContext 공통 필수 상태와 선택적 Mirror 참조 분리.
- PlayerDamageResolver를 통한 공통 피해 계산 및 기존 원본 Fighter/Gunner 스킬 호출.
- PotionUseState, 기존 장비/거래/가격/강화 규칙 재사용.
- PlayerItemEffectState 및 PlayerRelicEffectRuntime 등 플레이어별 효과 상태.
- 최신 A1 공허 파동, A2 스타 브리처, A3 폐열의 공통 계산/표시 연결.
- KY_SkillView, PotionSlotView, PlayerHudEventBridge의 멀티 로컬 Context 연결.
- 9월 30일 전투/보스 14씬의 상점 객체 제거. NetworkShopPlayerState는 지갑과 정산 때문에 남아야 한다.
- 경제 잠금, 마지막 미준비 참가자 이탈 후 재평가, 퇴장 승인 뒤 재접속 자격 삭제, 적 기본 공격의 원본 애니메이션 타격/종료 이벤트 연결.

9월 28일 초기 검토에서 지적했지만 이후 수정된 문제를 새 결함으로 재등록하지 않는다. 특히 적 기본 공격 0.45초 고정 타이머, 거너 기본 총 불일치, 정산 중 거래 허용을 현재 미수정으로 쓰지 않는다.

### 남은 구조적 부채

1. NetworkPlayerInputHandler가 원본 private 상태와 자동 프로퍼티 backing field를 리플렉션으로 변경한다.
2. 입력/회피/애니메이션 및 UI 단축키가 원본과 네트워크 코드에 분산돼 있다.
3. NPC private UnityEvent 탐색, 이름 기반 UI 연결, 원본 버튼의 persistent listener 비활성화가 남아 있다.
4. 적/보스 UI가 원본 뷰를 끄고 별도 표시 경로를 유지한다.
5. Act1 보스 patternID=101의 패턴 타이머·코루틴이 원본과 따로 존재한다.
6. 정식 웨이브 설정이 없을 때 구형 시험 웨이브로 진입할 수 있다.
7. 공용 씬이어도 HUD/NPC 인스턴스가 두 벌이며, 의뢰의 Act1 캠프 고정 조건도 남아 있다.
8. 검증용 복제 보상 컴포넌트, 빈 호환 래퍼, 테스트 ContextMenu·진단용 상태가 정식 실행 코드와 섞여 있다. 단, 각 삭제는 실제 참조 확인을 조건으로 한다.

## 2. 목표 구조와 하지 않을 일

```text
기존 입력 / 기존 UI
  ├─ 오프라인 싱글: 같은 원본 실행 메서드 호출
  └─ 멀티 로컬 소유자: 기존 Authority에 요청
                         ↓ 서버 검증
                   같은 원본 게임 규칙 실행
                         ↓
            상태 복제 / 개별 확정 사건 전달
                         ↓
                  기존 공통 View 표시
```

통합 완료란 **같은 규칙을 고칠 때 두 구현을 동시에 수정하지 않아도 되는 상태**다. Network 파일이 모두 사라지거나 NetworkBehaviour를 원본에 모두 붙인 상태가 아니다.

- 싱글을 항상 StartHost로 실행하도록 바꾸지 않는다.
- 새 UnifiedGameManager, GameModeService, 공통 서비스 로케이터, 단일 구현용 Interface/Factory를 만들지 않는다.
- 기존 ISkillController, 기존 바인딩 API 등 이미 쓰이는 추상화는 유지한다.
- 공용 원본 프리팹 + 네트워크 컴포넌트만 다른 얇은 Variant는 허용한다. 물리적 프리팹 하나를 완료 지표로 삼지 않는다.
- NetworkIdentity가 붙었다는 이유만으로 멀티로 판별하는 현재 소비 코드가 존재한다. 이를 해결하기 전에 플레이어 원본/네트워크 프리팹을 무조건 합치지 않는다.
- 원본 스킬 생성물과 피해/Collider 없는 원격 표시 프리팹은 서로 다른 역할이다. 중복 외형이라는 이유로 합치지 않는다.
- 기존 싱글 Act3는 보존하고 현재 멀티 Act1/Act2 범위를 임의로 Act3까지 확장하지 않는다.
- 신규 효과 설계를 이번 구조 작업에 추가하지 않는다. 이미 구현된 A1~A3는 보존하고 회귀 검사에 포함한다.

## 3. 핵심 문제별 처리 방향

### I-01. 회피 리플렉션과 실행 분기 — 우선 처리

`NetworkPlayerInputHandler`의 `mainCamera`, `dodgeDir`, `meshTrailTut`, `currentDodgeCooltime` setter 및 `<currentDodgeCooltime>k__BackingField` 접근을 없앤다. private 필드를 전부 public으로 바꾸는 식으로 끝내지 않는다.

원본 T_PlayerController가 카메라/회피 방향/쿨다운/상태 전환을 소유한다. 기존 TryDodge 호출 계약을 유지하면서 필요한 명시적 방향 입력과 성공 결과만 최소 확장하는 방법을 먼저 검토한다. 현재 네트워크 fallback의 지면 미검출·카메라 갱신·trail 없는 구성 대응을 무작정 삭제하지 않고 원본에서 처리할지 실제 구성 오류로 차단할지 결정한다. 이 문서의 새 API 표현은 제안이며 이미 존재하는 메서드라고 간주하지 않는다.

WBH 원본 입력에 존재하는 고층 조준, 아이템 접근/획득, 적 접근 공격, 핑 동작을 기능별로 비교한다. 별도 네트워크 경로에서 확인되지 않는 기능은 다른 호출부/씬 연결까지 확인한 뒤 누락 여부를 결정한다. 반대로 멀티의 서버 검증과 입력 잠금도 원본 재사용으로 사라지면 안 된다.

**통과 기준:** private/backing-field 리플렉션 0, 회피 수락 시 쿨다운 1회, 거부 시 상태 불변, 높이 다른 맵/카메라 교체/잡기/승강기/사망/채팅 중 올바른 입력 차단.

### I-02. 입력과 애니메이션의 단일 진입점

NetworkPlayerInputHandler/NetworkPlayerActionInputHandler의 입력 의도 해석은 BH/WJ 원본으로 모으고, 서버 Command와 권한 검증은 기존 Authority에 남긴다. WBH_PlayerAnimation과 NetworkPlayerAnimation의 공통 Animator 파라미터·cue 선택·로컬 연출은 한 구현으로 모으되, 소유자 입력·서버 스킬 확정·원격 표시의 사건 수신 권한을 구분한다.

MirrorSpawnedPlayerBinder에는 원본 입력/애니메이션 등의 부착 자체를 금지하는 구형 검사와 구성 목록이 있다. 재사용할 원본을 추가하면서 이 검사를 그대로 두면 새로운 구성도 실패한다. 코드, Variant override, local-only 목록, 구성 검사, AnimationEvent 수신자를 같은 단계에서 전환한다.

**통과 기준:** 플레이어별 실제 입력 소비자 1개, 공격/스킬 타격 사건 1회, 원격 AnimationEvent의 게임 상태 변경 0, Host의 연출 1회.

### I-03. UI/NPC 연결은 Inspector·기존 API를 먼저 쓴다

YJ_ClickNPC는 이미 직렬화된 onClicked UnityEvent를 가진다. 정적인 씬 NPC 연결은 우선 Unity Editor에서 기존 UnityEvent를 공용 UI의 기존 진입점에 연결한다. 이것으로 충분하면 NPC 원본 코드는 수정하지 않는다. 실제 동적 구독이 필요한 경우에만 좁은 Bind/Unbind API를 추가하며 필드 공개나 Reflection helper는 만들지 않는다.

MirrorLocalPlayerUIBinder는 로컬 Context 연결/해제를 담당하는 얇은 바인더로 남긴다. I/O/U/L/K/Escape 입력과 이름 기반 버튼 검색을 여기에서 제거한다. KY_UIInputManager는 현재 독립 GameInputActions를 생성하므로 기존 KeyBindingService와 실제 액션 소유/수명을 맞춘다. 공유 액션을 빌려 쓰면서 Dispose하거나 전체 ActionMap을 꺼 다른 소비자를 차단하지 않도록 한다. 채팅의 ConsumesInputThisFrame/IME/ESC, 모달 우선 닫기, 인벤토리와 사이드 팝업 상호 배제를 보존한다.

NetworkUpgradeButton의 persistent listener Off 우회는 기존 UpgradeController의 공용 요청 연결로 대체한다. 기존 선택·가격·메시지·다국어는 유지한다. 서버 응답 전에 아이템/골드를 먼저 적용하지 않는다.

스킬/포션 HUD는 다시 만들지 않는다. 남아 있는 고유효과 슬롯만 기존 CooldownIcon 계열의 명시적 데이터 입력으로 공통화한다. 슬롯 코드가 전역 인벤토리를 읽지 않게 하며 A1/A2 남은 시간과 A3 충전/준비 표시를 유지한다.

### I-04. 적 HP 상태와 개별 피해 표시 사건은 다르다

NetworkEnemyCombatView/NetworkBossHealthBar가 원본 WBH 뷰를 전역 비활성화하고 별도 Slider/텍스트를 조작하는 구조를 정리한다. 공통 뷰가 Bind된 대상의 HP/페이즈를 표시하고, 서버에서 확정된 개별 피해는 기존 RPC가 공통 표시 API에 전달한다.

같은 프레임 Direct + Effect 두 피해를 마지막 SyncVar 값 하나로 압축하지 않는다. LastDamage polling으로 되돌아가지 않는다. 원본 Controller/View를 단순히 다시 켜면 로컬 풀 반환·사망 연출·보상 사건까지 중복될 수 있으므로 수명과 사건 소유부터 분리한다.

### I-05. Act1 보스의 단일 패턴 구현

NetworkEnemyPattern은 일반 적에서는 이미 원본 패턴을 호출하지만 patternID=101에서 별도의 TickBoss/코루틴/타이머를 유지한다. WBH_EnemyBossPattern_Act1과 비교하면 원본의 주기적 타깃 교체, Phase2 사거리 밖 추적, 변신 연출 완료 콜백과 네트워크의 별도 흐름이 다르다. 이는 정적 코드 차이이며 이번 검토의 실제 플레이 재현 결과는 아니다.

원본 보스 패턴이 패턴 선택·쿨다운·페이즈·타깃 규칙을 소유하도록 한다. 기존 BindExternalTargets/BindExternalActions 등 연결을 우선 확장하고 서버 타깃 공급, 서버 투사체 생성, 확정 페이즈/연출 전달만 네트워크 측에 남긴다. 새 BossPatternService나 패턴 프레임워크를 만들지 않는다. Dedicated에서 로컬 렌더 완료 콜백을 기다리지 않도록 시뮬레이션 완료와 화면 재생 완료도 구분한다.

**통과 기준:** 동일 입력/시각/타깃 조건에서 싱글과 서버의 패턴 선택·페이즈·쿨다운 규칙이 같고, 각 Client의 연출은 1회, 보스 사망/씬 종료 후 늦은 공격 콜백은 피해를 만들지 않는다.

### I-06. 시험 웨이브 fallback과 사망 보상 정리

NetworkEnemyWaveSpawner의 stageManager 미연결 경로는 기존 시험용 melee/ranged/waveCount 설정으로 진입한다. ServerPrepareConfiguration도 stageManager가 없으면 준비를 통과시키는 구조다. 실제 정식 Scene/Prefab과 외부 검증의 사용처를 확인한 뒤 이 시험 경로를 제거한다. 정식 설정이 빠지면 누락 원인을 표시하고 출발을 막아야 한다.

공용 웨이브 정의/적 선택/진행 정책은 기존 YJ_StageManager와 WBH_EnemySpawnManager/Area를 재사용한다. 생성·등록·제거만 로컬 풀 또는 서버 Spawn/Destroy로 나눈다. 필요한 확장은 기존 스포너의 최소 생성/완료 연결이며 범용 스폰 추상 계층은 만들지 않는다. Act2 자폭병처럼 전용 프리팹이 아직 없어 같은 계열 외형을 재사용하는 명시적 예외는 구형 시험 fallback과 구분한다.

EnemyKillExpReward/NetworkEnemyItemDropAdapter는 현재 부착 여부를 조사한 뒤 공통 사망 경로로 흡수하거나 제거한다. 즉시 이중 보상이 발생한다고 단정하지 않는다. 로그 #327의 싱글 화상 처치 보상 누락 가능성은 미수정 기록이므로, 싱글을 무조건 정답으로 삼아 복사하지 않는다. 직접/스킬/Effect/DoT 모두 확정 사망 전이에서 경험치·크레딧·드롭·의뢰·OnKill이 각각 1회인지 확인한다.

### I-07. 공용 씬 내부의 중복 인스턴스와 경로 조건

로그 #328은 HUD 뷰 공통화 후에도 싱글/멀티 Canvas 인스턴스가 분리됨을 명시한다. #329는 Act1 캠프 NPC 복제 위치가 원본 변경을 따라가지 못한 실제 수정 이력이다. 코드 공통화가 끝난 뒤 한 씬씩 공용 HUD/NPC/포탈을 단일 소스로 정리한다. 로비 명부, 투표, 네트워크 준비 표시처럼 실제로 다른 기능만 모드 전용으로 남긴다.

MirrorQuestSession의 BindQuestBoard/BeginQuestVisit/RequestQuest/서버 요청 검사에는 Act1 캠프 경로가 고정돼 있다. 실제 Act2 NPC 구성과 기획상 의뢰 범위를 먼저 확인한다. 공통 캠프에서 동일 의뢰를 제공하는 의도라면 기존 현재 캠프 해석 함수를 재사용한다. 문자열 Act1→Act2 치환이나 임의 Act3 확장으로 처리하지 않는다.

### I-08. 마지막에 삭제하는 잔재

빈 네트워크 Relic 래퍼, 과거 참조 필드, 무시되는 인자의 호환 overload, 빈 override, 시험 ContextMenu, 진단 전용 카운터를 조사한다. RuntimeInitializeOnLoadMethod를 문자열로 일괄 삭제하지 않는다. 정적 상태 초기화는 정상 생명주기 코드일 수 있다.

진단 전용 SyncVar를 없애더라도 실제 표시/준비/쿨다운/피해 사건 식별에 필요한 값과 구분한다. wire 형태나 SyncVar 순서를 바꾸면 기존 CompatibilityVersion 정책을 적용하고 같은 버전의 서버·Client로 다시 검증한다. Editor/Player에서 서로 다른 필드 배치를 만드는 조건부 컴파일로 얼버무리지 않는다.

테스트라는 주석만 낡은 정상 어댑터는 주석만 현실에 맞게 수정한다. 삭제와 이름 변경을 첫 단계로 하지 않는다.

## 4. 실행 순서 — 작은 기능 단위로 닫는다

| 단계 | 구현 범위 | 통과해야 다음 단계 진행 |
|---|---|---|
| 0. 기준 고정 | 최신 HEAD·git status·진행 중 A1~A3 변경 확인, 실제 프리팹/Scene/호출부 목록, 원본 수정 승인 범위 | 기존 정상/기존 결함/미검증을 분리한 기준표와 핵심 실제 프리팹 재현 확보 |
| 1. 입력·회피 | T_PlayerController, BH/WJ 입력, NetworkPlayerInput/Action, Binder 구성 검사 | 싱글/Host/원격 입력·리바인딩·회피·채팅·잠금 1회 동작 |
| 2. UI·NPC | KY 입력, NPC 기존 UnityEvent, UI Binder, 강화/리롤 버튼, 남은 고유효과 슬롯 | 기존 HUD 재사용, 구독/버튼 요청 1회, 모달/ESC/씬 재바인딩 정상 |
| 3. 애니메이션·적 표시 | 원본/NetworkPlayerAnimation, 원본 Enemy View/HP bar, 개별 피해 표시 API | AnimationEvent 권한 분리, 동일 프레임 두 피해 표시, Host VFX 1회 |
| 4A. Act1 보스 | 기존 원본 보스 패턴 + 최소 서버 action/target 연결 | Phase1/2·타깃 변경·추적·돌진/점프/미사일·사망/취소 일치 |
| 4B. 웨이브·사망 보상 | 구형 시험 fallback 제거, 기존 웨이브 실행, 원본 사망 보상 공통 진입 | 설정 누락 시 출발 거절, 실제 웨이브/소환/보상/DoT 사망 1회 |
| 5. 씬·잔재 정리 | 공용 HUD/NPC 인스턴스, Act 조건 대조, 실제 미사용 컴포넌트와 테스트 의존성 제거 | GUID/Inspector/AnimationEvent 참조 정상, Missing Script/신규 Broken Reference 0 |
| 6. 통합 승인 | 같은 커밋의 싱글·Host+원격·4인·Dedicated·재접속·정산 전체 경로 | 아래 실제 검증표 통과. Editor assertion만으로 대체하지 않음 |

각 단계는 호출자 → 원본 실행 → 네트워크 경계 → View → 프리팹/Scene 구성 → 최소 재현 검사까지 묶는다. 파일명별 대량 수정이나 새 구현을 남기고 구형 구현도 계속 활성화하는 방식으로 진행하지 않는다.

## 5. 수정 원칙과 승인 경계

ponytail full의 우선순위는 삭제 필요성 확인 → 기존 코드/Inspector/API 재사용 → Unity 기본 기능 → 최소 추가다. 간결화 때문에 권한/입력 검증/저장 실패 처리/생명주기/오류 로그를 없애지 않는다.

| 담당 영역 | 승인 대상 후보 | 이유/조건 |
|---|---|---|
| BH | T_PlayerController, WBH_PlayerInputHandler, WBH_PlayerAnimation | 회피 소유, 입력 공통화, 애니메이션 사건/표시 경계 |
| BH | WBH_EnemyBossPattern_Act1, WBH_EnemyCombat, WBH_EnemyController, WBH_EnemyView, WBH_HighEnemyHpbarView, WBH_EnemySpawnManager | 보스/표시/풀 반환/생성 연결. 실제 변경할 파일만 확정하여 승인 요청 |
| WJ | PlayerActionInputHandler, 기존 CooldownIconUIContainer/Slot, 사망 보상 원본 등 | 공통 입력/슬롯/보상에 실제 변경이 필요한 경우 |
| KY | KY_UIInputManager 및 실제 수정이 필요한 HUD/팝업 파일 | 공용 액션·명시적 대상 바인딩·입력 소비 정책 |
| JYJ | 실제 통합 대상 Scene, 필요한 Stage/NPC 코드 | 동일 씬 중복 인스턴스 제거. YJ_ClickNPC는 Inspector 연결만으로 해결되면 스크립트 수정 불필요 |

AGENTS.md에 따라 다른 담당자 스크립트는 실제 구현 전 파일명과 이유를 제시하고 **“이 스크립트를 수정할까요?”**라는 명시적 승인을 받는다. 이번 문서는 계획이며 승인을 대신하지 않는다.

기존 팀원 주석을 보존하고 변경과 충돌하는 설명만 갱신한다. 공통화로 이동하는 설명도 따라 옮긴다. 필요한 public API에는 실제 역할을 설명하는 쉬운 한국어 Summary를 둔다. Scene/Prefab은 Unity API/Editor로 수정하고 GUID/.meta, 기존 UnityEvent/AnimationEvent 계약을 보존한다. 외부 에셋·Packages·무관한 작업 변경은 손대지 않는다.

## 6. 반드시 보존할 경계

- 서버 소유권/요청 입력값/체력·게임 준비/현재 씬·대상 유효성 검증.
- AttackId, 피해 원인, 공격 시점 스냅샷, 후속 Effect FIFO, 사망 전이 1회.
- 아이템 소유자/instanceId/장비 세대, 요청 번호, 인벤토리·상점 revision, 실패 복원.
- 서버 정산 금액 고정·개인 경제 잠금·같은 정산 ID 중복 방지·ACK 후 지갑 처리. 로컬 저장 ACK와 Firebase 업로드 완료를 같은 의미로 취급하지 않는다.
- 연결 참가자/재접속 예약/명시적 퇴장 구분, 계정 UID·세션 generation이 달라진 늦은 저장 콜백 차단.
- 승강기의 준비→탑승→이동→착지 ACK→스냅샷 정리→입력 해제. 잡기/스킬 이동 잠금과 함께 검증한다.
- Host에서 공통 사건과 RPC 양쪽이 동일 VFX/SFX/피해를 중복 실행하지 않는 경계.
- 씬/비활성화/사망에 따른 공격 수명 정리와 A1/A2 동일 생애 쿨다운 보존, A3 열/MPB 복원.

## 7. 검증표와 완료 조건

### 기존 검증 재사용

이미 존재하는 `Tools/Validation/MirrorPrebuildChecks.cs`, `MultiplayerUXChecks.cs`, `CombatParityFinalChecks.cs` 및 A1/A2/A3 검증 코드를 필요한 범위만 보강한다. 검증 코드는 Assets 밖에 두고 운영 런타임에 새 시험 Command, 자동 Runner, 우회 시작 기능을 넣지 않는다. 테스트 프레임워크를 새로 만들지 않는다.

### 실제 실행 게이트

| 환경/경계 | 필수 시나리오 | 확인할 결과 |
|---|---|---|
| 오프라인 싱글 F/G | 정상 시작→맵→캠프→보스→결과, 기존 Act3 포함 | Mirror 세션 불필요, 원본 입력·공유 View 정상 |
| Host + 별도 Client | 양 클래스 교대 조작, 공격/회피/스킬/포션/획득/강화 | 서버 확정·소유자별 UI, Host/원격 사건 각 1회 |
| 4인 혼합 클래스 | 동시 피해·보상·구매·스킬·승강기 | 상태 섞임/이중 지급/중복 실행 없음 |
| Dedicated + Client | 실제 Windows 또는 Linux 서버와 원격 연결, Linux 목표는 Linux에서 확인 | 로컬 카메라/렌더러 없이 진행, 같은 프로토콜 버전 |
| 생명주기 | 씬 로딩 중 이탈, 전투 재접속, 리더 변경, 결과→새 런, 멀티 종료→싱글→다시 Host | 잔존 구독/정적 상태/이전 위치/이전 UI/이전 요청 없음 |
| 피해/효과 | Direct+Effect 같은 프레임, DoT 막타, A1~A3, 사망/부활/탈착/씬 이동 | 사건·보상 1회, 출처/스냅샷/쿨다운/버프 수명 보존 |
| 경제/저장 | 동시 구매, 오래된 revision, 씬 전환 중 응답, 정산 ACK 전 이탈/재시도 | 유실·복제 없음, 기존 계정/세션 경계 유지 |

같은 Editor에서 모의 연결 또는 복제 객체로 통과한 assertion 수를 별도 Client 패킷·4인·Linux 검증 수로 표현하지 않는다. 실행 환경의 RAM 제약이 있으면 한 Editor와 별도 Player 조합부터 순차 검증하되, 4인/원격 게이트 자체를 생략해 완료 처리하지 않는다.

### 완료 판정

1. 프로젝트 소유 런타임의 private/backing-field 회피/NPC 리플렉션 제거.
2. 같은 입력·회피·패턴·표시 규칙의 수정 위치가 한 곳이며 활성 소비자가 1개.
3. 정식 설정 누락 시 시험 웨이브나 임의 대체 플레이어로 조용히 진입하지 않음.
4. 실제 참조가 없는 검증 복제본/호환 잔재만 제거, 필요한 네트워크 경계는 보존.
5. 원본 금지형 구형 구성 검사가 새 공통 구성을 정확하게 검사하도록 변경됨.
6. Scene/Prefab/GUID/Inspector/AnimationEvent 정상, 새 Missing Script와 Broken Reference 0.
7. 동일 커밋·동일 호환 버전의 실제 실행 게이트 통과. 미검증은 항목별로 그대로 남김.
8. 기존 실행 기록과 김성우 구현 로그에 변경 이유·검증 환경·결과·미검증을 기록. Commit/Push는 별도 요청 없이는 수행하지 않음.

## 8. 파일별 처리표

‘유지’는 보안/동시성/플레이 검증 전체 통과를 의미하지 않는다. ‘삭제/흡수’는 계획상 방향이며 코드·직렬화 참조 확인 전에 삭제 승인이 내려진 상태가 아니다.

### 8.1 Network/Player — 44개

기준 경로: `Assets/SW/Scripts/Network/Player`

| 파일 | 계획 | 처리 범위/보존 조건 |
|---|---|---|
| `EnemyKillExpReward.cs` | 참조 확인 후 삭제/흡수 | 예전 Context 검증 복제본. 코드·GUID·프리팹 참조 확인 전 미사용 또는 이중 지급으로 단정하지 않는다. 공통 사망 보상 경로로 귀속한다. |
| `FighterSkillAuthority.cs` | 유지·축소 | Fighter/Gunner 원본 스킬 호출, 서버 검증·스냅샷·시전/이동 잠금·ACK를 보존한다. 이름 정리는 마지막이다. |
| `GunnerCombatPresentation.cs` | 공통 표시 재사용 | 기존 무기 VFX/Effect 데이터와 Presenter 재사용을 유지한다. 애니메이션 정리 때 실제 중복 cue 선택만 합친다. |
| `MirrorAct1SceneRoute.cs` | 기존 경로 함수 재사용 | 실제로 Act1/Act2를 처리한다. Quest의 Act1 고정 조건 등과 대조한다. 허용 씬 검증은 없애지 않는다. |
| `MirrorGameplayReadiness.cs` | 유지 | epoch·씬·netId·전원 준비와 배치/외형 로딩 완료 조건을 보존한다. |
| `MirrorLobbyBridge.cs` | 입력만 공통화 | 기존 KY 로비 external-flow 연결은 유지한다. 독립 Escape 처리와 Pause 메뉴 중복 연결을 공용 UI 입력 경계에 귀속한다. |
| `MirrorLocalPlayerUIBinder.cs` | 축소 | 로컬 Context Bind/Unbind는 유지. NPC private 이벤트 리플렉션·키 입력·이름 기반 UI 수정을 원본 연결로 흡수한다. |
| `MirrorNetworkManager.cs` | 기존 세션 소유자로 유지 | 씬 전환·권한·경제 잠금·호환 버전·세션 수명은 보존한다. 새로운 통합 Manager를 추가하지 않는다. |
| `MirrorPassiveProfile.cs` | 유지·공통 계산 대조 | 비신뢰 JSON 검증·서버 DB 수치 계산은 필요하다. 원본 패시브의 효과 매핑과 중복이 확인되면 그 계산만 기존 코드로 모은다. |
| `MirrorPlayerCheckpoint.cs` | 유지 | 참가자/계정/세션/revision·generation 검사, 비동기 저장 직렬화와 정산 후 지갑 0 체크포인트를 보존한다. |
| `MirrorQuestSession.cs` | 범위 조건 정리 | 공통 QuestManager 진행도 계산은 유지. Act1 캠프 경로 고정 조건을 실제 Act2 의뢰 구성/의도와 대조하고 기존 캠프 해석 함수로 정리한다. |
| `MirrorReconnectProfile.cs` | 유지 | 프로필별 분리·원자적 파일 교체·실패 시 기존 자격 보존을 유지한다. |
| `MirrorRunResult.cs` | 유지·표시 공통화 유지 | 서버 확정 결과·부활 대기·개인 결과 재전송·계정 경계를 유지한다. KY 결과 화면을 새로 만들지 않는다. |
| `MirrorSceneMode.cs` | 공용 인스턴스 전환에 맞춰 축소 | 같은 씬의 모드별 실행 경계를 유지하되 공용 HUD/NPC까지 두 벌 관리하지 않게 한다. Identity 유무/활성 세션 판별을 혼동하지 않는다. |
| `MirrorSessionAuthenticator.cs` | 유지 | 빌드 버전·참가 자격·시간 제한·소유자 전용 재접속 토큰 응답은 삭제 대상이 아니다. |
| `MirrorSessionLifecycle.cs` | 유지 | 로스터·리더·퇴장 승인·재접속 재바인딩·런타임 보존 및 정산 대기를 보존한다. |
| `MirrorSessionRoster.cs` | 유지 | 최대 4인, 기존 참가자 예약, 명시적 포기, 리더 재선정은 멀티 전용 규칙이다. |
| `MirrorSpawnedPlayerBinder.cs` | 역할 축소·구성 검사 갱신 | 소유자 입력/UI와 씬 배치 수명을 유지한다. 원본 입력/애니메이션을 금지하는 구형 구성 검사와 프리팹 목록을 함께 바꾼다. |
| `MirrorStageVoting.cs` | 유지 | 연결된 참가자 분모·씬 로딩·투표 revision·동시 확정 방지를 보존한다. 싱글 선택으로 대체하지 않는다. |
| `MirrorUnknownStageSession.cs` | 기존 계산 재사용 유지 | DataManager의 기존 Unknown 계산과 전원 적용 전 검증·실패 복원·선택한 아이템 ID 보존을 유지한다. |
| `NetworkEnemyItemDropAdapter.cs` | 참조 확인 후 삭제/흡수 | 옛 검증 복제본으로 원본 ItemManager를 호출한다. 현재 Authority와 함께 붙는지는 직렬화 참조 확인이 선행돼야 한다. |
| `NetworkInventoryInput.cs` | 유지·UI 요청 연결 최소화 | 공용 InventoryView 외부 입력 연결을 유지한다. Host 즉시 응답과 드래그 미리보기 복원 순서, 양쪽 revision 대기를 보존한다. |
| `NetworkItemTriggerManager.cs` | 동기화로 축소 | 실제 규칙은 PlayerItemEffectState에 남긴다. A1/A2 쿨다운·A3 준비 상태·신뢰 RPC는 보존하며 중복 쿨다운 키 계산만 대조한다. |
| `NetworkPlayerActionInputHandler.cs` | 원본 입력으로 흡수 | 스킬/포션 의도 해석은 원본 입력과 하나로, 서버 요청 전달은 해당 Authority/상태 컴포넌트로 남긴다. |
| `NetworkPlayerAnimation.cs` | 원본 표시/이벤트와 공통화 | Animator 파라미터·cue 선택을 원본으로 모으되 원격 AnimationEvent가 피해·상태 전환을 실행하지 못하게 한다. |
| `NetworkPlayerInputHandler.cs` | 우선 흡수 | mainCamera/dodgeDir/쿨다운 backing field/meshTrailTut 리플렉션과 별도 회피 fallback을 원본 동작 API로 옮긴다. |
| `NetworkPlayerRelicEffectProvider.cs` | 마지막 호환 껍데기 정리 | 빈 상속 래퍼의 .meta/직렬화 참조를 이전한 뒤 제거 여부를 결정한다. 효과 계산 중복으로 과장하지 않는다. |
| `NetworkPotionUseManager.cs` | 유지·공통 상태 재사용 | PotionUseState와 실제 서버 자원 변경·충전 동기화를 유지한다. 이미 통합된 1초 재사용 제한을 다시 구현하지 않는다. |
| `NetworkShopPlayerState.cs` | 유지 | 개인 지갑·서버 요청·정산 ACK·요청/revision 추적을 보존한다. 상점 없는 전투 씬에서도 필요한 컴포넌트다. |
| `NetworkShopRerollButton.cs` | 공용 버튼 연결 검토 | 공용 리롤 UI에 요청 콜백만 연결하는 방향. 요청 중 잠금·실패 복원·구독 해제를 유지한다. |
| `NetworkShopState.cs` | 유지·계산만 공통 원본 | 파티 공유 재고·동시 구매·재고 revision은 필요하다. 가격/추첨/거래 규칙은 기존 구현을 재사용한다. |
| `NetworkSkillPresentation.cs` | 유지 | 원본 스킬의 생성/범위/효과 사건만 전달한다. Host 이중 표시 방지와 구독 해제를 유지한다. |
| `NetworkUpgradeButton.cs` | 공용 요청 연결로 대체 | PersistentListenerState를 런타임에서 끄는 우회를 제거한다. 기존 UpgradeController 선택/메시지와 서버 완료 응답을 연결한다. |
| `NetworkWorldItem.cs` | 유지 | ItemSaveData·공용 pickup claim/presentation을 재사용하고 서버의 선점·Spawn/Destroy 권한을 유지한다. |
| `PlayerArmorEffectProvider.cs` | 동기화로 축소 | 공통 ArmorRuntime의 보호막 복제는 유지. 과거 직렬화 필드와 런타임 추가가 실제로 필요한지만 확인한다. |
| `PlayerCombatAuthority.cs` | 유지·중복 선택 규칙만 대조 | 요청 검증·공격 ID·공격 스냅샷·타격 확정은 유지한다. 공통 Resolver를 별도로 재작성하지 않는다. |
| `PlayerEquipmentVisualSync.cs` | 유지 | 서버 itemId를 기존 PlayerWeaponVisualPresenter로 전달하는 정상 어댑터다. 낡은 테스트 설명만 갱신한다. |
| `PlayerInventorySync.cs` | 유지 | 개인 소유 모델·원자적 거래·아이템/장비 revision·실패 복구·정산 경계를 보존한다. |
| `PlayerNameplate.cs` | 유지 | 플레이어별 명부 표시다. 새 공용 UI 프레임워크를 만들 명분이 되지 않는다. |
| `PlayerNetworkTransform.cs` | 유지 | 서버 스킬/잡기/승강기 이동 중 소유자 위치 덮어쓰기 방지, 소유자 재연결 기준값·스냅샷 처리를 보존한다. |
| `PlayerRuntimeStateSync.cs` | 유지 | 서버 확정 상태의 전달 및 공통 원본 상태로의 반영을 유지한다. 게임 규칙을 Sync hook에 새로 복제하지 않는다. |
| `SessionUIMessageLocalizer.cs` | 현재 계약 유지 | 기존 서버 문구→공용 라벨 변환. 이번 통합을 이유로 전 메시지 프로토콜을 새 enum 체계로 교체하지 않는다. |
| `UniqueEffectPresentation.cs` | 공통 표시로 유지 | 싱글 사건/RPC의 입력 경계만 구분한다. A1~A3 표시·임시 MaterialPropertyBlock 복원·수명 정리를 유지한다. |
| `WBH_CombatResolver.cs` | 유지 또는 마지막 얇은 래퍼 정리 | 공통 PlayerDamageResolver 앞의 서버·출처 검사 경계다. 별도 피해 공식으로 오인해 삭제하지 않는다. |

### 8.2 Network/Combat — 15개

기준 경로: `Assets/SW/Scripts/Network/Combat`

| 파일 | 계획 | 처리 범위/보존 조건 |
|---|---|---|
| `MirrorBossIntro.cs` | 유지·표시 중복만 정리 | 서버 시각/완료/보스 생성 순서·원격 재접속 시각·HUD/렌더러/입력 복원을 유지한다. Timeline 연출 배우와 실제 전투 객체는 구분한다. |
| `MirrorCooldownHud.cs` | 공통 쿨다운 슬롯으로 흡수 | 스킬/포션은 이미 공통화됐다. 남은 고유효과 슬롯 생성·쿨다운 판독·아이콘 표시를 기존 슬롯 API에 연결한다. |
| `MirrorFourPlayerElevator.cs` | 유지·원본 경계만 대조 | 4인 준비→탑승 이동→착지 ACK→위치 스냅샷 정리→해제 순서를 보존한다. 강제 착지 timeout으로 줄이지 않는다. |
| `MirrorLocalPlayerCameraBinder.cs` | 유지·명시적 카메라 주입 | 공통 Cinemachine 설정·가림 처리에 로컬 소유자만 연결한다. 원본 컨트롤러의 private Camera에 리플렉션으로 쓰지 않는다. |
| `MirrorQuestUIBinder.cs` | 표시 계산만 공통화 | 기존 QuestBoardNPC/QuestUI 외부 요청 연결 유지. 파티 진행·개인 보상 보류 표시를 삭제하지 않는다. |
| `MirrorStagePortalAdapter.cs` | 유지·공용 포탈 연결 | 서버 전환·전원 캠프 도착·다중 Collider 개수·노드 완료 1회는 유지한다. 테스트 ContextMenu는 외부 검증으로 이전한다. |
| `MirrorStageSelectRouteAdapter.cs` | 기존 맵 UI 연결 유지 | 공용 StageSelect/Unknown 표시와 서버 선택 요청 사이의 역할을 유지하고 원본과 중복된 표시/경로 규칙만 합친다. |
| `NetworkBossHealthBar.cs` | 원본 뷰로 흡수 | 전체 원본 체력바 비활성화/경로 이름 검색 대신 공통 보스 체력바 Bind를 사용한다. |
| `NetworkEnemyAuthority.cs` | 권한 계층 유지·원본 수명 연결 | 서버 HP/사망/보상/드롭 1회 및 RPC는 유지한다. 원본 Controller/View 일괄 비활성화를 줄이려면 원본 풀 반환/사건 소유를 먼저 분리한다. |
| `NetworkEnemyCombatView.cs` | 원본 표시로 흡수 | HP 상태 표시와 피해 사건 표시를 구분한다. 개별 피해 RPC를 LastDamage polling으로 되돌리지 않는다. |
| `NetworkEnemyPattern.cs` | Act1 보스 규칙 원본 흡수 | 일반 적은 원본 패턴을 이미 호출한다. patternID=101의 타이머·패턴 코루틴·페이즈 기준을 원본 보스 구현 하나로 모은다. |
| `NetworkEnemyProjectile.cs` | 권한/표시 유지·조건부 잔재 제거 | 서버 투사체 판정·고정 스냅샷·중복 피격 방지·씬 종료 정리는 유지한다. 무시하는 인자의 호환 overload와 빈 override를 참조 확인 후 제거한다. |
| `NetworkEnemyWaveSpawner.cs` | 시험 fallback 제거·공통 웨이브 실행 정리 | StageManager 미연결을 구형 시험 웨이브 성공으로 처리하지 않는다. 공통 웨이브 정의/진행과 네트워크 생성 권한의 경계를 정리한다. |
| `NetworkSkillVisual.cs` | 유지 | 원본 생성물의 외형·위치·수명만 복제한다. 피해/Collider 없는 표시 프리팹을 판정 원본과 합치지 않는다. |
| `NetworkWorldItemSpawnService.cs` | 유지·공통 위치 계산 대조 | 네트워크 Spawn 경계를 유지한다. 드롭 지면/벽/절벽 위치 계산의 실제 중복이 있으면 기존 공통 드롭 코드로 모은다. |

### 8.3 Chat — 3개

기준 경로: `Assets/SW/Scripts/Chat`

| 파일 | 계획 | 처리 범위/보존 조건 |
|---|---|---|
| `ChatSession.cs` | 유지 | 입력 소비 프레임·소유자 재바인딩·서버 검증/전송 제한·요청 중복 방지를 보존한다. |
| `ChatPanel.cs` | 공용 UI 유지 | 이미 싱글/멀티 공유 뷰다. 공통 UI 입력 전환 때 Enter/IME/Escape 소비와 포커스 수명을 함께 검증한다. |
| `ChatMessageMapper.cs` | 유지 | 기존 거래/인벤토리 결과 매퍼 재사용을 유지한다. 통합과 무관한 새 메시지 프레임워크는 만들지 않는다. |

## 9. 주요 근거 문서와 원본 연결

아래 경로는 모두 문서 상단의 고정 커밋을 기준으로 한다. 작업 시작 시 최신 HEAD와 다시 대조한다.

- `.agents/skills/ponytail/SKILL.md`: full 모드, 최소 해법 순서, 호출 흐름 이해, 검증·권한·오류 처리 보존.
- `AGENTS.md`: 담당 영역, 원본 수정 승인, 주석 보존, 직렬화 참조 검증, Unity 자산 수정, 작업 로그 기준.
- `Docs/Architecture/Mirror_Production_Integration_Plan.md`: 오프라인 싱글과 공용 콘텐츠 씬, 단순 이름 변경으로 완료하지 않는 목표.
- `Docs/Architecture/Mirror_Production_Integration_Execution.md`: 단계별 구현/검증 이력. 과거 중단 시점과 최신 후속 기록을 구분한다.
- `Docs/Architecture/Mirror_Integration_Verification_2026-09-28.md`: 공용 씬, 초기 준비, 정산 및 실행 범위.
- `Docs/Architecture/Prebuild_Review_2026-09-28.md`: 최초 문제와 후속 수정/제한을 구분한다.
- `Docs/Architecture/UX_Multiplayer_MainMerge_Review_2026-09-28.md`: 원본 애니메이션/보스 효과/UI 일치 보완과 Editor/원격 검증 범위.
- `Docs/Architecture/ImplementationLogs/김성우.md`: 특히 #218~225, #321~335. #327 화상 보상 가능성, #328 HUD 인스턴스 분리, #329 NPC 배치 불일치, #331 전투 상점 제거, #332~335 신규 효과 현황을 현재 기준으로 반영한다.
- `Docs/Architecture/Structure_Cleanup_TODO.md`: 과거 정리 후보 문서이며 현재 미해결 또는 변경 승인 목록으로 간주하지 않는다.
- `Assets/WBHTest/Scripts/Player/T_PlayerController.cs`, `WBH_PlayerInputHandler.cs`, `WBH_PlayerAnimation.cs`: 입력·회피·애니메이션 원본 연결.
- `Assets/WBHTest/Scripts/Enemy/WBH_EnemyBossPattern_Act1.cs`: Act1 보스의 원본 규칙.
- `Assets/Scripts/UI/Test/KY_UIInputManager.cs`: 독립 입력 액션/팝업 닫기 순서.
- `Assets/Scripts/NPC/YJ_ClickNPC.cs`: 기존 직렬화 UnityEvent 및 모달/UI 클릭 차단.
- `Assets/SW/Scripts/Player/PlayerContext.cs`: 공통 소유 상태, 싱글 인벤토리 연결, 선택적 Mirror 참조 및 최신 효과 수명.

**최종 권고:** 입력/회피 → UI/NPC → 애니메이션/적 표시 → Act1 보스 → 웨이브/사망 보상 → 씬 인스턴스/잔재 정리 순서로 진행한다. 새 통합 시스템을 더 만드는 대신 이미 있는 원본이 공통 실행 경로가 되도록 바꾸고, Mirror에는 서버 권한·전달·동기화·세션 수명만 남긴다.

## 10. 후속 적용 상태 — 2026-09-30 사용자 요청으로 중단

이 절은 위의 원래 읽기 검토 이후 실제 프로젝트에서 수행한 작업을 요약한다. 검토 당시 관찰을 수정 후 현재 결함으로 다시 등록하거나, 과거 실행 결과를 이번 후보의 검증으로 표시하지 않는다. 상세 변경·승인·검증·인계는 [Mirror 정식 통합 실행 기록 §9.8](Mirror_Production_Integration_Execution.md#98-9월-30일-마무리-계획-적용--부분-반영-후-사용자-요청으로-중단)에 누적했다.

| 항목 | 중단 시점 상태 |
|---|---|
| I-01 회피·카메라·추적 공격 | 원본의 명시적 API와 공통 요청 경계로 코드 변경. private/backing-field 회피 Reflection 제거. F/G Prefab 연결 반영, 실제 입력·상태 검증 대기 |
| I-02 입력·AnimationEvent | 원본 입력/액션/애니메이션 + 얇은 서버 요청/권한 어댑터로 변경. 구성 검사/local-only 목록 및 F/G Prefab 변경. 원격/Host 사건 1회 검증 대기 |
| I-03 UI·NPC·쿨다운 | 공용 KY 입력·Pause 표시·강화/리롤 요청·쿨다운 데이터 API 작성, Binder 독립 키/NPC Reflection 제거. `InventoryCamp` Prefab 배선 반영. 정식 씬 신규 참조·NPC 이벤트·Lobby 배선은 미완료 |
| I-04 적·보스 표시 | 원본 공통 View에 HP 변화·개별 reliable 피해/화상 전달, 공격자 정보 보존. 적 12 Prefab 배선 반영. 씬 공용 고등급 HP 뷰와 실제 연속 피해/거리/화상 검증 대기 |
| I-05 Act1 보스 | 원본 101 패턴·Combat·PhaseView 시간/취소 재사용 및 서버 대상/투사체 연결. 실제 Animator Dash 상태 확인. 서버·Client·변신 취소 실행 검증 대기 |
| I-06 웨이브·보상·잔재 | 시험 웨이브 fallback 제거와 설정 누락 차단, 공통 경험치·크레딧 함수 연결, 빈 투사체 override/미사용 overload 제거. 복제 보상 파일·나머지 참조 정리는 미완료 |
| I-07 씬·의뢰·버전 | 현재 캠프 경로를 의뢰 검사에 사용, 공용 NPC 창 진입점 준비, 호환 버전 `2026093001`. 16씬 HUD/두 캠프 NPC 통합은 미완료. Act1 Camp 이전 명령 시간초과, 확인 시 Scene Diff 0 |

마지막 Unity `recompile_status`는 `completed/failed=false/errors=[]`였다. 그러나 씬 이전 명령은 30초, 후속 Editor 상태 조회는 주 스레드 5초 시간초과를 반환해 정식 씬 저장·현재 Editor 상태 확인을 완료하지 못했다. 기존 Dirty `Act1_Stage1`을 저장하지 않았다. 새 Edit/Play Mode 기능 검사·싱글 F/G/Act3·Host+별도 Client·4인·Dedicated/원격/Linux·A1~A3·경제/생명주기 최종 게이트는 수행하지 않았다. 전체 Diff check에는 Unity 직렬화의 빈 필드 및 원본 Combat 변경 줄의 trailing whitespace가 남아 있다.

사용자는 이후 수정 스크립트를 **워커 한 명당 한 파일씩 서로 다른 파일에 병렬 배정**하도록 지시했고, 다시 **구현을 일단 멈추고 진행 상황만 문서로 갱신**하도록 지시했다. 구현 변경은 보존했으며 개인 구현 완료 로그·Commit/Push는 수행하지 않았다. 재개 시 Editor 및 타임아웃 작업 상태 → 정식 씬의 필수 배선/참조 → 남은 잔재 정리 → 이번 후보의 실제 실행 게이트 순으로 진행한다. 통합 완료 판정은 보류한다.

## 11. 재개 결과 — 2026-10-01 Editor Host 1인 범위

정식 16씬의 공용 HUD·KY 입력·고등급 HP·포털을 통합하고 두 캠프의 중복 NPC를 정리했다. 모드별 인벤토리/팝업은 유지하며 원본 NPC 이벤트가 현재 모드의 창으로 연결된다. HUD의 원본 CanvasScaler와 포털 위치를 보존했고 Lobby/Pause 필수 참조를 연결했다. 두 플레이어 유물 실행기를 공통 구현으로 이전한 뒤 참조 없는 복제 스크립트 3개를 제거했다. 런타임 ContextMenu 검사는 Assets 밖 재현 도구로 이동했다.

Unity 컴파일, 규칙 20건, 프리팹 15개 필수 배선, AnimationEvent 59개 단일 수신자, 16씬 구조·모드·NPC 연결 검사를 통과했다. Missing Script는 0이다. 기존 자산의 끊어진 참조는 프리팹 60건·씬 13건으로 별도 보고하며 전체 참조 검사 성공으로 포장하지 않는다.

사용자 요청대로 최종 실행은 Editor Host 1인으로 한정했다. Fighter/Gunner 각각 정상 로비·캐릭터 선택·Ready·StageSelect·전투 진입 후 메뉴·회피·평타 23건을 통과했다. Play 종료와 테스트 전 저장 JSON 100개 해시 복원까지 마쳤다. 계정 미로그인으로 결과 저장·로비 복귀는 확인하지 못했으며, 전체 Act1~2 진행·보스·경제·A1~A3·싱글 Act3·원격/4인/Dedicated/Linux 완료를 주장하지 않는다. 상세 근거는 누적 실행 기록 §9.9, 개인 구현 로그 #337에 있다. Commit/Push는 수행하지 않았다.

검증 후 사용자 요청으로 이번 임시 validation 스크립트·결과·복원 완료 백업을 정리했다. 결과와 미검증 범위는 본 문서와 실행 기록에 보존하며 이번 통합 변경을 커밋한다. 다른 미완료 검증과 기존 테스트 씬/빌드 목록은 유지한다.
