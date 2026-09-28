# Mirror 정식 통합 — 병합 이후 싱글·멀티(Host) 전체 검증 및 수정 기록 (2026-09-27 ~ 09-28)

- 브랜치: `unity-6000-3-22-test` (김성우/SW 테스트 브랜치)
- 담당: 김성우 요청, Claude Code(Opus 5.5) 수행
- Unity: 6000.3.22f1 Editor (Pipeline `unity-cli`로 Play/검사 자동화)
- 상태: **수정·Editor 검증 완료, 미커밋**(작업 트리). 새 Player/Server 빌드는 만들지 않았다.
- 선행 문서: [Mirror_Production_Integration_Execution.md §9.6](Mirror_Production_Integration_Execution.md#96-재개-구현-후-집-pc-인계--2026-09-23-최신-상태)

## 0. 요약

1. 기존 9/24 클라이언트 빌드를 실행해 두 가지 문제를 발견했다: 고해상도에서 UI가 FHD 크기에 고정되어 Unity 기본 배경(Skybox)이 보이는 현상, 싱글 StageSelect에 스테이지 노드가 전혀 표시되지 않는 현상.
2. `main` ↔ 브랜치 체리픽/병합(`67913ea46`)을 양쪽 변경을 모두 보존해 해결했다(§1).
3. 병합 이후 Editor에서 싱글(Act1 보스 → Act2 보스 → Act3 맵)과 멀티 Host(Act1 → Act2 보스 → 결과 GAME CLEAR)를 끝까지 진행하면서 발견한 문제 **16건**을 수정했다(§2).
4. 사용자 지시("팀원 스크립트 수정은 승인 처리한 것으로 간주")에 따라 팀원 파일도 필요한 최소 범위로 수정했다. 수정한 팀원 파일은 §3에 모두 기록한다.

## 1. Git 병합 처리

| 단계 | 내용 |
|---|---|
| 체리픽(main 대상) | 브랜치의 Mirror 2~6단계·Firebase 2/3차 커밋을 `main`에 체리픽. 충돌: `FighterSkillController`/`GunnerSkillController`(main의 `visibleSkillArea` 조건 + 브랜치의 `ShowSkillRange` 원격 표시 이벤트를 결합), `WBH_EnemyCombat`(브랜치 `externalSkill` + main `soundCue` 결합), `Fighter.prefab`·`StageSelect.unity`(3-way 텍스트 병합, 중복 fileID/끊어진 참조 0 확인). UnityYAMLMerge 드라이버가 `Don't know how to merge`로 실패해 git 3-way 병합 후 결과를 검사했다. |
| 병합(브랜치 대상) | `main`(`0f374ec0c`)을 브랜치로 병합 → `67913ea46`. `DU` 5건(main에 새로 생긴 에셋의 `.meta`)은 GUID 보존을 위해 main 쪽 채택, `google-services-desktop.json.meta` GUID 충돌은 main 값 채택(GUID 참조 없음). 교차 검증: main 전용 변경 누락 0, 브랜치 전용 변경 누락 0. |
| 누락 커밋 확인 | 브랜치 전용 11커밋 중 거너 이펙트/모델링 8건은 main에 이미 반영(파일별 main=브랜치최신 확인). 나머지(테스트 코드·3DS Max MCP·6000.3.22 버전 커밋)는 사용자 판단으로 제외. main에만 남은 임시 Editor 스크립트 5개(`RootR12ColorProbe.cs`, `TempProduction49*`)는 동작 무관, 정리 여부 미결. |
| 잠금 파일 | 실행 중 git 프로세스 없이 남은 0바이트 `.git/index.lock` 2회를 사용자 허락 하에 제거. |
| 작업 보존 | 브랜치 전환 중 사라진 작업 트리 변경을 dangling stash `918f94e9d`에서 찾아 `refs/backup/claude-editor-fixes-20260928`로 고정 후 재적용. 필요 없어지면 `git update-ref -d refs/backup/claude-editor-fixes-20260928`로 삭제. |

## 2. 발견·수정한 문제

| # | 증상 | 원인 | 수정 (파일) |
|---|---|---|---|
| 1 | QHD/4K에서 로그인·타이틀·로딩·전투 HUD가 1920×1080 크기로 고정, 뒤에 Skybox 노출 | 해당 씬 루트 Canvas가 `ConstantPixelSize` | 빌드 씬 23개(Basic 3, Act1~3 전투·보스)의 루트 CanvasScaler를 다른 씬과 같은 `ScaleWithScreenSize 1920×1080`(메뉴 match 0.5, 스테이지 match 0)으로 통일 |
| 2 | 싱글 StageSelect에 노드/연결선이 전혀 없음 | `StageSelectManager` 오브젝트에 멀티 전용 `MirrorStageSelectRouteAdapter`(+필수 `NetworkIdentity`)가 붙어 있고, Mirror `NetworkScenePostProcess`가 씬 NetworkIdentity 객체를 비활성화 → 싱글에서 매니저 `Start` 미실행 | `MirrorSceneMode`가 싱글일 때 멀티 전용 루트 밖의 씬 NetworkIdentity를 다시 활성화(Awake+Start, Editor는 후처리가 Awake 뒤에 다시 끄므로 Start에서도 수행). 멀티 컴포넌트는 기존 `multiplayerBehaviours`로 꺼진 상태 유지 |
| 3 | Act1_Stage5 엘리베이터가 싱글에서 통째로 꺼질 수 있음 | #2와 동일(엘리베이터 발판이 NetworkIdentity 보유) | #2 수정으로 해결. 싱글에서 발판·`YJ_PointMove` 활성, 네트워크 스크립트 비활성 확인 |
| 4 | GAME OVER → 다시 시작 → 게임 시작 시 이전 런의 대기 노드로 곧장 진입 | 새 게임이 세션 임시 맵을 초기화하지 않음(부팅 시에만 `BeginTemporaryRun`) | `YJ_SinglePlayerStartFlow`: `BeginNewGame` 성공 후 세션 전용 모드면 `BeginTemporaryRun()` 호출 |
| 5 | `RedShockwaveImpact` AudioSource 누락 예외 반복 | 프로젝트 이펙트 프리팹에 AudioSource 없는 `SciFiPitchRandomizer` 잔존(9/24 수정이 병합 중 소실) | `E_Normal_Melee_Attack_Effect.prefab`에서 해당 컴포넌트 제거(외부 원본 에셋 미수정) |
| 6 | 멀티 접속 화면 버튼 겹침(최근 세션/타이틀 버튼이 Host/서버 버튼을 가림) | 레이아웃 좌표 중첩 | `Assets/SW/Scenes/Network/Lobby.unity` 버튼·안내문 Y 좌표 재배치 |
| 7 | 멀티 결과의 "최종 도달 스테이지"가 `Act1 · Act1_Stage2`(씬 이름) | 싱글과 다른 표기 생성 | `MirrorRunResult.FormatReachedStage` — 싱글과 같은 `ACT n · FLOOR m`(대기 노드 층, 없으면 clearedFloor) |
| 8 | **멀티 전투가 시작되지 않음**(웨이브 Waiting, 조작 잠김, 준비 실패) + 싱글 일반 웨이브에 보스 소환병이 섞이고 EnemyPool ID 중복 오류 | main의 BH 커밋 `f5a2b75c5`에서 일반 자폭 드론 SO의 `.meta`(GUID `f4a6…`)가 새 `act2boss_explosion_drone.asset`으로 옮겨가고 원 데이터는 새 GUID를 받음 → 전 게임의 "일반 자폭 드론" 참조가 Act2 보스 소환병을 가리킴 | GUID 원복: 일반 드론 SO에 `f4a6…`, 소환병 SO에 `ba92…`. BH가 의도적으로 소환병을 가리킨 4곳(AllEnemies 추가 항목, `Manager.prefab` act2Boss_Spawn 풀, `Boss_Act_02.prefab`·네트워크 `enemy.boss.boss.GoliathT.prefab`의 `selfDestructEnemy`)만 새 GUID로 변경 |
| 9 | 멀티 Act2 보스 소환병 생성 실패 가능 | 소환병 전용 네트워크 프리팹 없음 | `NetworkEnemyWaveSpawner.ServerSpawnMinion`: 같은 등급·유형 계열(`enemy.normal.selfdestruct.`) 네트워크 외형 재사용, 데이터(보상 없음)는 소환병 SO 사용. `ponytail:` 주석으로 전용 프리팹 추가 시 제거 조건 명시 |
| 10 | 결과 씬에서 `There are 2 event systems` 매 프레임 경고 | 세션(DontDestroyOnLoad) EventSystem + 결과 씬 EventSystem | `MirrorNetworkManager`: 세션이 살아 있는 동안 새로 로드된 씬의 EventSystem 컴포넌트를 비활성화 |
| 11 | 멀티 캠프 상점 창이 비어 있음(서버 재고 6개) | `NetworkShopState`가 공용 씬에서 먼저 발견되는 **싱글** ShopController(비활성)를 바인딩 | 공통 `MirrorSceneMode.FindInActiveMode<T>()`(부모가 활성인 현재 모드 UI 우선) 추가, `NetworkShopState`·`MirrorLocalPlayerUIBinder` 검색에 적용 |
| 12 | 멀티 상태창(L)이 싱글 UI에 바인딩 | #11과 동일 | #11 적용 |
| 13 | 멀티 캠프 재방문 시 상점/강화 NPC 클릭 무반응 | 바인더 `OnEnable` 시점에 멀티 NPC가 아직 비활성이라 리스너 미연결 | `MirrorLocalPlayerUIBinder.Start`에서 NPC 연결 재실행(멱등) |
| 14 | 버프가 있는 상태로 씬 진입 시 `BuffIconUIContainer.Rebuild` NullReference | 멀티 HUD가 컴포넌트 `Awake` 전에 Bind → `slotParent` null | `BuffIconUIContainer.Rebuild`에서 기본 부모 보장. `PlayerHudEventBridge`는 비활성 사본 대신 활성 컨테이너 우선 바인딩 |
| 15 | 멀티 세션 종료 → 타이틀 → **싱글 플레이 불가**(MissingReferenceException) | 파괴된 세션 매니저를 Mirror 정적 `singleton`이 계속 가리켜 `OwnsGameplay`가 참 | `OwnsGameplay`에 Unity null 판정 추가. (Mirror `ResetStatics`를 OnDestroy에서 부르는 방안은 **세션 종료 후 재Host가 실패**해 폐기) |
| 16 | 멀티 상태·스킬·퀘스트·팝업 UI가 옛 레이아웃(속성 행/아이콘/알림 루트 없음, 캠프 중복 상태창의 빈 컴포넌트로 NullReference, 키 안내 P 누락, 미니맵 Ping 누락) | 5·6단계 공용 씬 통합 때 전투 씬 14곳의 `Multiplayer/UI`가 예전 MirrorTest 사본으로 들어옴(캠프 2곳만 최신 사본) | ① 캠프 2곳의 중복 `KY_StatusPopup`(빈 참조) 제거·PopupManager 재연결 ② 14개 전투 씬 + 캠프 2곳의 멀티 `Canvas_Popup`을 같은 씬 싱글 `Canvas_Popup` 복제본으로 교체(역참조 3~4개 재연결, 싱글 인벤토리 항목은 캠프와 동일하게 null) ③ 멀티 HUD `TopRight`(미니맵)·`BottomRight`(키 안내)·`TopLeft`(초상화) 동일 방식 교체, Act1 보스·Act2_Stage2 키 안내 버튼 대상을 멀티 PopupManager로 재연결 ④ `KY_StatusPopup.Open`에서 스크롤을 항상 맨 위로 초기화(씬마다 저장된 스크롤 값 차이) |

## 3. 수정 파일

### 코드
- SW: `MirrorSceneMode.cs`, `MirrorNetworkManager.cs`, `MirrorRunResult.cs`, `MirrorLocalPlayerUIBinder.cs`, `NetworkShopState.cs`, `NetworkEnemyWaveSpawner.cs`, `PlayerHudEventBridge.cs`
- **팀원(사용자 일괄 승인)**:
  - YJ `Assets/Scripts/Scene/YJ_SinglePlayerStartFlow.cs` — #4 (한 줄 호출, 사전 개별 승인도 받음)
  - WJ `Assets/WJ_TestPlace/Script/Player/BuffIconUIContainer.cs` — #14 (`slotParent` 기본값 보장 3줄)
  - KY `Assets/Scripts/UI/Popup/Status/KY_StatusPopup.cs` — #16④ (열 때 스크롤 맨 위)
  - 기존 주석은 모두 보존하고 `SW 수정:` 설명을 추가했다.

### 에셋 (모두 Unity Editor API로 수정·저장)
- 씬: Basic 3(Loading/Login/Title), Act1 Stage1~6·Boss·Camp, Act2 Stage1~6·Boss·Camp, Act3 Stage1~6·Boss(스케일러만), `SW/Scenes/Network/Lobby.unity`
- 프리팹: `E_Normal_Melee_Attack_Effect.prefab`, `WBHTest/Prefabs/Etc/Manager.prefab`(BH), `WBHTest/Prefabs/Enemy/Boss_Act_02.prefab`(BH), `SW/Prefabs/Network/Enemy/enemy.boss.boss.GoliathT.prefab`
- 데이터: `EnemyData/.../AllEnemies.asset`(BH), 자폭 드론 SO `.meta` 2개(GUID 원복)
- 씬 저장 시 Unity가 함께 정리한 항목: 더 이상 없는 필드(`clearRoot` 등) 제거, 여러 씬 동시 로드로 중복된 Mirror `sceneId` 재발급(빌드 시 씬 해시가 합쳐지므로 동작 영향 없음), Stage1 레이아웃 구동 값.
- 최초 씬 diff가 컸던 이유: 16개 씬에 최신 팝업 UI 사본을 넣으며 프리팹 연결이 풀렸다. 이후 아래 §6에서 기존 공용 프리팹 연결을 복구했다. 직렬화 구조 변경으로 Scene diff는 여전히 크므로 병합 시 함께 확인한다.

## 4. 검증 (Editor Play, 계정 t2 — 사용자가 직접 로그인, 세션 유지로 자동 로그인)

### 싱글
- Start → 로그인 세션 복원 → Title → 싱글 로비 → Fighter → StageSelect(노드 30+) → Act1 전투·미지 이벤트(선택 효과: 골드 0, 최대 HP -30% 반영)·캠프·엘리트 → Act1 보스(HP 800→200→처치) → ACT2 맵 → Act2 전 구간 → Act2 보스(골리앗 T 1200→300→처치) → ACT3 맵 생성.
- 사망 → GAME OVER(ACT 1 · FLOOR n) → 다시 시작 → 새 Act1 맵(수정 #4 확인). Gunner 전투 클리어.
- 2560×1440에서 Title·전투 HUD가 1.333배 스케일로 전체 화면 표시(수정 #1). 로그인 화면은 로그인 상태에서 즉시 전환되어 캡처 불가 — 새 빌드에서 로그아웃 상태 확인 필요.
- Console Error 0(보스 셰이더 컴파일 오류·MCP 설정 메시지 등 기존 기준선 제외).

### 멀티 Host (Editor 단일 Host, 원격 Client 없음)
- 접속 화면(버튼 배치) → Host → 캐릭터 선택 → READY → 게임 시작 → StageSelect 투표 → 전투(5웨이브 Completed → 포탈 → 복귀).
- 스킬: 실제 키 입력(A) 서버 수락, Fighter/Gunner 4슬롯 `TryUseLocalSkill` 수락·적중·쿨타임(궁극기 55~60초), 쿨타임 HUD fill 반영, 진화/강화 선택 서버 반영. 기본 공격 `TryBeginLocalAttack` 적중.
- 인벤토리: 월드 드롭 줍기(네트워크 요청) → 장착(방어 24→34, 이동속도 5.08→5.88, 무기 공격력 229→255) → I키 인벤토리 UI 표시.
- 상점: 구매(골드 차감)·판매·무료/유료 리롤(100골드), 서버 재고와 UI 일치. 강화 +0→+3(공격력·골드 반영).
- 포션: 버프형 물약 장착 → 사용 요청 서버 처리(충전 3→2), 버프 아이콘 HUD 표시.
- 팝업: 상태창(속성 행·아이콘·맨 위 스크롤)·스킬·퀘스트·버프, ESC 일시정지(시간 정지 없음, "호스트 세션 종료").
- 미지 이벤트 방장 선택 → 서버 데이터 반영. 캠프 NPC 첫 방문부터 상점/강화 연결.
- Act1 보스(HP 바) → Act2 전 구간(엘리베이터 Stage5 포함) → Act2 보스 → 소환병 서버 생성 → **GAME CLEAR(ACT 2 · FLOOR 12, 처치 599)**, 전멸 GAME OVER, 로비 복귀 → 새 런 초기화, 세션 종료 → 재Host, 세션 종료 → 타이틀 → 싱글.
- 수정 이후 재검증 회귀 1회(Act1 보스·Act2 진입) Console Error 0.
- 빌드 씬 34개 Missing Script 0.

### 검증하지 못한 항목 / 남은 위험
- GUID 재검토: `AllEnemies` 11개 및 공용 Manager·정식 Act2_BossStage의 싱글/멀티 풀은 참조 누락·중복 0이다. 다만 빌드 미포함 `Assets/WBHTest/Act2_BossStage _WBHTest.unity`의 `enemyPools.Array.data[10].enemyDef` 씬 override가 일반 드론 GUID `f4a6…`를 유지하여 풀의 일반 드론 참조가 2개가 된다. 보스 소환병 `ba925…`로 맞추는 후속 수정이 필요하다. 이번 GUID 검토에서는 팀원 테스트 씬을 수정하지 않았다. 근거: `RunValidation/UIPrefabReconnect_20260928/guid-audit.json`.
- **새 Player/Server 빌드 미생성.** 9/24 빌드는 이번 수정 이전 버전이다. 다음 순서: Windows Player 빌드 → 로그아웃 상태 로그인 화면 QHD 확인 → 실제 2인 이상 Host+Client(원격 클라이언트 UI 교체분 포함)·전용 서버 혼합 확인.
- 원격 Client 관점(교체된 멀티 UI, 상점/강화 요청 권한, 소환병 표시)은 Host 로컬에서만 확인.
- 적 처치는 실제 피해 함수(`WBH_EnemyController.TakeDamage`) 호출로 진행했고 무적(`ApplyInvincibility`)을 테스트에 사용했다. 사람의 실제 조작 난이도·밸런스는 별도(예: 대기 중 자폭 드론 연속 폭발로 수 초 내 사망).
- Q/I/L 등 일부 키는 Unity가 포그라운드를 잃어 Input System 키보드가 꺼지는 환경 제약으로 동일 이벤트/요청 함수를 직접 호출해 확인했다(A키는 실제 키 이벤트로 서버 수락 확인).
- 멀티 결과 화면 상태창 등에 옛 사본 흔적(`YJ_HUDInformationView` 등 컴포넌트 중복)은 싱글 원본에도 동일하게 있어 유지했다.
- 보스 연출 경고(`Boss_Act1_Shoot/Death Binding 없음`), 파괴 연출 미준비 경고, BossName 폰트 대체 문자 경고, `RunPlaybackSpeed` Animator 경고는 기존 기준선.
- 사용자 저장 데이터(`LocalLow/DefaultCompany/Project2`, 105개)는 착수 전 백업과 바이트 동일하게 복원했다. 테스트 계정의 로컬 캐시(`PlayerSaves/<uid>`, `FirebaseMigration/...`)는 제거되어 다음 로그인 시 재생성된다. Firestore의 서버 측 계정 데이터(크레딧 증가 등)는 되돌리지 않았다.

## 5. 증거
- 캡처: `RunValidation/EditorVerify_20260927/*.png` (Git 제외)
- 임시 검증 스크립트는 세션 scratchpad에만 두었고 프로젝트에 남기지 않았다(한 번 `Assets/RunValidation`에 생긴 캡처 폴더는 즉시 삭제).

## 6. 멀티 UI 프리팹 연결 복구 및 빠른 재검증 (2026-09-28)

- Act1/Act2의 전투·보스·캠프 16씬에서 Multiplayer의 `Canvas_Popup`·`Canvas_HUD`를 기존 공용 프리팹 인스턴스로 연결했다. 기존 연결 2개를 유지하고 30개를 변환했으며, 별도 프리팹·런타임 코드는 추가하지 않았다.
- Unity `ConvertToPrefabInstance`의 계층 매칭과 override를 사용했다. 기존 멀티 전용 컴포넌트·참조·비활성 상태를 보존하고, 원본 HUD에서 새로 따라오는 불필요한 컴포넌트는 removed-component override로 제외했다. 언팩하지 않았다.
- 16씬 전체의 변환 전후 논리 객체·컴포넌트 수와 직렬화 속성·참조 차이 0. 저장 후 재로드에서 프리팹 루트 32개 연결 및 Missing Script 0. 저장 전후 해시 비교는 14씬 수행(최초 Camp·Stage2는 변환 직후 전체 속성 비교와 재로드 연결 검사).
- 대상 씬 파일 합계 82,785,400 → 27,973,364바이트(66.21% 감소). 이는 씬 직렬화 용량 감소이며 런타임 메모리·프레임 성능 측정값은 아니다.
- Editor 싱글 1회: Act1_Stage1 직접 진입, 실제 Fighter·게임플레이 준비 완료, HUD·스탯·스킬 화면 표시 및 활성 EventSystem 1개 확인.
- Editor Host 1회: Network Lobby → Fighter/READY → StageSelect의 첫 전투 노드 요청 → Act1_Stage1. 준비 완료, 로컬 PlayerContext 완성·UI Binder 동일 참조, HUD·스탯·스킬 화면 표시 및 활성 EventSystem 1개 확인. UI 공개 API로 열었으며 물리 키 입력·원격 Client·새 빌드는 이번 검증 범위에 포함하지 않았다.
- C# 컴파일 성공. Console에는 싱글 직행 검사 중 사망으로 `YJ_PlayerDead`의 결과 집계기 누락 오류 1건이 남았다(07:17:38 KST). Start 씬의 런 집계기를 거치지 않은 검사 진입 조건과 일치하며, 이번 프리팹 재연결에 따른 UI 예외는 관찰되지 않았다. 이번 짧은 검사에서 사망→결과 흐름은 통과로 기록하지 않는다.
- 사용자 허용에 따라 Act1_Stage1의 미저장 레이아웃 소수점 차이 1건만 폐기했고 사본은 보존했다. 테스트 전 로컬 저장 JSON/BAK 100개를 종료 후 복원하여 SHA256 일치 100/100을 확인했다. Editor는 Play 종료 후 저장된 Act1_Stage1로 복귀했다.
- 근거: `RunValidation/UIPrefabReconnect_20260928/`의 변환·재로드 보고서, 저장 복원 보고서 및 싱글/Host 캡처(Git 제외). 일회성 스크립트와 Assets에 생성된 캡처 임시 폴더는 제거했다.
- 커밋 전 해상도 설정 재확인: 활성 Build Settings 34씬을 Preview Scene으로 읽어 화면용 최상위 Canvas 159개를 검사했다. 142개는 `ScaleWithScreenSize 1920×1080`, StageSelect 1개는 기존 `3840×2160` 높이 기준이다. 나머지 16개는 기존 월드 드롭 전용 Canvas로 Scaler가 없으며 이번 화면 해상도 변경 대상이 아니다. 변경된 메뉴 루트 match0.5·스테이지 루트 match0을 확인했다. FHD/QHD/4K처럼 동일한 16:9 해상도에서 비례 스케일하는 설정이며, 전 씬·전 화면비의 실제 렌더 검증을 수행했다는 의미는 아니다. 근거: `resolution-audit.json`.

## 7. Act 정산·Firebase 및 보스 연출 재검증 (2026-09-28)

### 수정

- 사용자 결정에 따라 멀티도 싱글처럼 **Act 종료마다** 런 골드를 영구 크레딧으로 정산한다. Act1 종료 시 기존 정산 ID/ACK 경로를 사용하고, 미포기 참가자의 로컬 저장 확인을 기다린 뒤 지갑이 비워진 체크포인트를 발행하고 다음 Act로 전환한다. 대기 중 세션 종료도 저장 확인을 거친다. ACK는 원자적 로컬 저장·업로드 대기 기록 확인이며 Firestore 반영 완료와 동일하지 않다.
- 최종 결과의 정산 ACK 뒤 체크포인트를 다시 발행해 이미 지급한 금액이 체크포인트 지갑에 남지 않게 했다.
- 로컬 체크포인트 캐시가 없는 상태에서 기존 서버 문서를 읽지 않고 저장해 revision 충돌이 반복되는 문제를 재현했다. 최초 저장 전에 기존 SaveDataService로 서버 버전을 읽고, 연속 체크포인트 저장은 직렬 실행한다. 세션 세대·계정 검사를 유지하며 기존 미전송 데이터의 충돌 보호를 우회하지 않는다.
- 멀티 Act1 보스 소개를 구형 `TimeLine_Act1_Boss_test`(5.833초)에서 싱글의 `TimeLine_Act1_Boss_Intro`(8.233초)로 연결했다. 카메라·보스 모션·착지 효과·이름·HUD·오디오 바인딩을 맞추고, 서버 시간 재생 및 4인 연출용 플레이어 구조는 유지했다. DropEffect는 원본 프리팹 연결을 유지한다. 보스 이름도 싱글과 같은 공용 언어 DB를 사용한다.
- Act1 실제 스폰 경로는 authored wave의 `enemy.boss.boss.SpiderX.prefab`이다. 이 프리팹과 fallback `Boss_Act_01.prefab`의 원형·직사각형 경고를 싱글의 `CircleIndicator.asset`·`SquareIndicator.asset`으로 교체했다. fallback만 수정해서는 실제 전투에 반영되지 않음을 런타임에서 확인했다. Act2 GoliathT의 경고 참조는 이미 싱글과 같아 유지했다.
- 변경 코드: SW의 `MirrorNetworkManager`, `MirrorRunResult`, `MirrorSessionLifecycle`, `MirrorPlayerCheckpoint`, `NetworkShopPlayerState`, `MirrorBossIntro`. 변경 자산: SW 보스 연출 프리팹·Act1 네트워크 보스 프리팹 2개·공용 `Act1_BossStage`의 멀티 바인딩. 팀원 스크립트·원본 Timeline·인디케이터 데이터는 수정하지 않았다.

### 실제 Editor 및 서버 저장 결과

| 검증 | 관찰 결과 |
|---|---|
| 싱글 Fighter Act1 | Start→로그인→싱글 로비→맵·전투·캠프→보스→Act2. 크레딧 16,140→18,895(+2,755), Firestore 프로필 revision 8 및 Gameplay 서버 저장 확인 |
| 멀티 Gunner 사망 | 실제 전투 중 HP 0→GAME OVER, 크레딧 +10 서버 반영 |
| 멀티 Fighter 피격 | 테스트 무적 적용 전 실제 피해 처리로 HP 600→590. 무적은 검증 중 런타임에만 적용했고 플레이어 기본 체력/피해 코드는 변경하지 않음 |
| 체크포인트 수정 재현 | 로컬 캐시 없는 새 세션에서 서버 revision 81→83, 업로드 대기 false. Act1 정산 뒤 revision 94·gold 0도 확인 |
| 최종 자산 적용 Host Act1 | 크레딧 36,040→40,390(+4,350), 프로필 revision 13. 체크포인트 revision 105·act 1·gold 0·runEnded false |
| 같은 Host Act2 최종 결과 | GAME CLEAR, ACT 2 · FLOOR 12, 처치 448, 획득 크레딧 5,030. 프로필 40,390→45,420(revision 14). 체크포인트 revision 119·act 2·gold 0·runEnded true. 모두 Firestore 서버 직접 조회에서 업로드 대기 false |
| 중복 정산 요청 | 최종 지갑 0에서 재요청해도 크레딧 45,420 유지 |
| 보스 시각 확인 | 새 Timeline 0.5/2/3.4/5.5/7.8초 구간과 완료 후 HUD 복귀, 실제 네트워크 SpiderX의 새 원형·직사각형 인디케이터 캡처 확인 |
| 이름 현지화 회귀 | 정상 Host Act1 노드 진행으로 재진입, 연출의 `스파이더 X` 한글 표시·연출 완료 확인. 마지막 코드 컴파일 성공 |
| 자산 검사 | 빌드 씬 34개 Missing Script 0. 변경 씬·프리팹 재로드 검사 Missing Script 0, 새 Timeline·ControlTrack 효과·AudioTrack 연결 확인. PlayerDodge 트랙은 기존 런타임 동적 바인딩 |

서버 저장 수치는 `FirestoreSaveDataStore.LoadAsync`의 서버 조회로 확인했다. 멀티의 개인 Gameplay/Quest 스냅샷은 MultiplayerCheckpoint에 포함된다. 별도 Quest 문서는 테스트 중 수락·완료한 퀘스트가 없어 NotFound였으며, 퀘스트 저장 검증 통과로 간주하지 않는다.

### 남은 차이와 검증 한계

- **종료 Act는 동일하지 않다.** 현재 멀티는 Act2 보스를 최종 클리어로 처리한다(`MirrorNetworkManager.IsRunCompleted`). 싱글은 Act2 다음 Act3로 넘어가 Act3 보스에서 클리어한다(`YJ_PortalSceneLoader`). 이번 변경은 정산 시점을 맞춘 것이며 종료 Act 정책은 변경하지 않았다.
- 이번은 Editor 단일 Host 검증이다. 수정 후 원격 Client·전용 서버·다중 PC·새 Player 빌드는 검증하지 않았다. 새 연출의 원격 시간 동기화·여러 플레이어 배치·원격 인디케이터는 후속 범위다.
- 적 처치는 실제 피해 함수 호출, 진행은 기존 노드 선택·포탈·UI API로 가속했다. 테스트 무적을 사용했으므로 사람의 조작 난이도·밸런스·전 스킬 시각 품질·파괴 성능을 보증하지 않는다.
- 오프라인·강제 종료·재접속 중 정산/업로드 장애 주입은 수행하지 않았다. 오프라인 로컬 fallback 이후 서버 revision 충돌 가능성은 기존 충돌 보호로 남으며, 별도 복구 정책 검증이 필요하다.
- 기존 보스 셰이더 오류와 MPPM 초기화 오류, 보스 Shoot/Death 바인딩·NavMesh 풀 배치 경고는 별도 기준선이다. 마지막 전체 Host 진행 중 조회한 Console에는 Error가 없었다. 이 결과가 프로젝트의 기존 모든 셰이더/에디터 오류 해결을 의미하지 않는다.
- 작업 중 Editor 재시작으로 초기 컴파일 연결 문제를 복구했고, 첫 시각 복제 자동화는 네이티브 크래시로 중단됐다. 해당 시도는 저장되지 않았음을 확인 후 Unity 객체 복제와 기존 연출용 보스 재사용으로 다시 구성했다.
- 이름만 확인하려고 대기 노드 없이 보스 씬으로 직접 이동한 진단에서는 정상적인 전투 준비 실패 화면이 표시됐다. 그 캡처는 통과 증거에서 제외하고 정상 맵 경로로 다시 진행해 한글 이름을 확인했다. `invalid-direct-scene-preview.png`는 이 실패한 진단의 기록이다.

### 증거와 정리

- `RunValidation/EditorAudit_20260928/`(Git 제외): `final-assets-act1-cloud.json`, `final-assets-act2-cloud.json`, `checkpoint-fix-smoke-cloud.json`, `fighter-damage.json`, `boss-intro-runtime.json`, `final-asset-check.json`, `host-result-final.png`, `boss-intro-*.png`, `boss-circle-fixed.png`, `boss-rect-fixed.png`.
- 테스트 전 로컬 JSON/BAK 56개를 복원하여 SHA256 56/56 일치를 확인했다. 전체 런 검사에서 생성된 17개, 후속 이름 검사에서 다시 생성된 12개 파일은 각각 결과 사본을 보존한 뒤 로컬 캐시에서 제거했다. 실제 지급된 테스트 계정의 서버 크레딧은 되돌리지 않았다.
- 최종 이름 검사는 Act1 보스 도착에서 중단했다. 따라서 위 revision119의 최종 결과는 전체 런 검사 시점의 서버 조회 증거이며, 테스트 계정의 가장 최근 체크포인트가 항상 그 결과라는 의미는 아니다.
- Play 종료로 테스트 무적을 해제하고 원래 Network Lobby 씬으로 복귀했다. 임시 검증 코드는 Assets 밖에서 실행했으며 검증 종료 후 제거했다. 시작 시 존재한 Addressables link 삭제·렌더 텍스처·URP 설정 변경은 보존했다. Commit·Push 없음.
