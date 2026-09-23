# Mirror 정식 통합 실행 기록

> 계획: [기존 싱글을 보존하는 Mirror 단계별 정식 통합](Mirror_Production_Integration_Plan.md)
> 이 문서 하나에 단계별 조사·구현·검증을 누적한다. 회차별 인계 문서를 새로 만들지 않는다.

## 1. 갱신 규칙

- 단계 착수 시 상태표와 해당 단계 기록을 갱신하고, 종료 시 실제 결과·미검증·다음 작업을 기록한다.
- 상태는 `대기 / 진행 중 / 조사 완료 / 구현 완료·검증 대기 / 완료`로 구분한다. 0단계의 조사 완료는 실전 플레이 재검증 완료를 뜻하지 않는다.
- 코드 확인, 현재 Editor 확인, 과거 실행 증거, 이번 실행 검증을 구분한다. 기존 PASS를 이번 실행 결과로 복사하지 않는다.
- 팀원 스크립트 수정 전 대상·이유·싱글 영향을 제시하고 승인을 기록한다. 승인된 변경에 `SW 수정`과 쉬운 XML `<summary>`를 적용한다.
- 기존 팀원이 작성한 주석은 변경 내용과 직접 충돌하지 않는 한 삭제하지 않는다. 코드 이동·공통화 시에도 적합한 위치에 설명과 작성 의도를 보존하며, 실제 동작과 충돌하는 부분만 필요한 범위로 수정한다.
- 서브에이전트는 사용자 지정에 따라 **`gpt-5.6-sol` / `medium`만 사용**한다. 역할에 고정된 다른 모델을 호출하지 않는다.
- 구현 완료 시 변경 내용 / 싱글 유지 방식 / 멀티 동작 / 검증 결과를 요약한다. 필요한 개인 구현 로그는 실제 구현·검증 후 별도로 갱신한다.
- 계획의 변경이 필요하면 변경 이유와 사용자 결정을 먼저 이 문서에 기록한다. 승인 없이 원래 계획을 축소하거나 단계를 완료로 바꾸지 않는다.

## 2. 단계 상태

| 단계 | 작업 | 상태 | 최근 기록 |
|---|---|---|---|
| 0 | 현재 동작·차이·상태 소유자 기준선 | 조사 완료 | 2026-09-20 소스·실제 프리팹·Editor 기준선 기록; Play 재실행은 하지 않음 |
| 1 | PlayerContext와 플레이어 소유 상태 | 완료 | 2026-09-20 싱글 실제 UI·장착, 별도 서버+4클라이언트 인벤토리·Q1 107단계 검증 |
| 2 | 포션·장비·인벤토리·경제 | 완료 | 2026-09-23 싱글 실제 플레이어·Host+Client·전용 서버+4 Client 규칙/동시 구매/중복·버전 거절/재접속 보존 PASS; 실행 환경 제한은 §5 |
| 3 | 기본 전투·상태이상 | 대기 | — |
| 4 | 완료 고유효과 6종 공통화 | 대기 | — |
| 5 | 로비·진행·Act1 멀티 씬 | 대기 | — |
| 6 | 결과·복귀·정식 자산 승격 | 대기 | — |
| 7 | Act1 이후 확대 | 대기 | — |

## 3. 0단계 — 2026-09-20

### 3.1 착수 및 보존 범위

- 사용자 요청: 확정 계획을 문서로 보존하고, 실행 기록 한 문서를 매 단계 갱신하며 0단계부터 진행한다.
- 브랜치: `unity-6000-3-22-test` — SW 테스트 브랜치. 시작 HEAD는 `5673c8616d92d3c8f6625cb7a2f017ca9d01f7af`. 이번 작업의 커밋이 아니다. Commit·Push·원격 갱신은 하지 않는다.
- 기존 미커밋 변경에는 고유효과·상태이상 코드, 적 프리팹, 데이터 표·SO·아이콘, 기존 문서 변경/삭제 등이 있다. 시작 시 상태와 변경 파일 SHA-256을 수집해 이번 문서 작업의 영향과 구분한다.
- 게임 코드·Scene·Prefab·Packages·ProjectSettings는 수정하지 않는다. 사용자 Dirty Scene을 저장하거나 폐기하지 않는다.
- 조사 분담: Sol medium 2개가 전투·효과와 씬·세션·직렬화 참조를 각각 읽고, 주 에이전트가 플레이어 상태·포션·경제와 Editor 및 증거를 확인한다.

### 3.2 이번에 직접 확인한 Editor·자산 기준선

Unity CLI로 연결 프로젝트 `I:/git/Project2-test/Project2`, Unity `6000.3.22f1`, Edit Mode, 컴파일 중 아님을 확인했다. 열린 씬은 `Assets/Scenes/Maps/Act1_Maps/Act1_Stage1/Act1_Stage1.unity` 하나이며 Dirty 상태다. Prefab Stage는 없다. 씬 저장·폐기·전환·Play를 실행하지 않았다. 현재 Console 오류는 0건이다. 이것은 새 빌드나 Play 테스트 통과를 의미하지 않는다.

`AssetDatabase.LoadAssetAtPath`로 실제 프리팹을 읽고, 자식 전체 Missing Script와 직렬화된 컴포넌트를 검사했다. 생성·저장은 하지 않았다.

| 실제 플레이어 프리팹 | GUID | NetworkIdentity / PlayerContext | 프리팹 자식 InventoryController | Missing Script | 추가 확인 |
|---|---|---|---|---|---|
| `Assets/Resources/Prefabs/Character/Player/Fighter.prefab` | `ac0352209e99eb446babcad427ac4372` | 없음 / 없음 | 0 | 0 | `PotionUseManager.useCooldownSeconds=1`; FighterSkillController 활성 |
| `Assets/Resources/Prefabs/Character/Player/Gunner.prefab` | `52343f8796454ed4d99aef6897d6ab9a` | 없음 / 없음 | 0 | 0 | 포션 1초; GunnerSkillController 활성 |
| `Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab` | `6b0d6bb0a29f43f45bcc12e78ff3415a` | 있음 / 있음 | 1 | 0 | Context.IsComplete=true, 14개 필수 참조 모두 같은 루트/자식 소유; 원본 SkillController 비활성 |
| `Assets/SW/TEST/MirrorPlayerContext/Prefabs/GunnerNetworkPlayer_MirrorTest.prefab` | `f24ea0b3fc4a19f41bade479062908b8` | 있음 / 있음 | 1 | 0 | Context.IsComplete=true, 14개 참조 모두 자기 플레이어 소유; 원본 SkillController 비활성 |

**1단계에 직접 영향을 주는 차이:** 열린 싱글 Stage1의 `InventoryController`는 `Assets/SW/Prefabs/Inventory/InventoryCommon.prefab` 인스턴스의 `InventoryCommon/Runtime`에 있고 EquipmentSystem과 PlayerWallet도 같은 Runtime에 있다. 싱글 플레이어 프리팹 안에는 이 세 상태가 없다. 따라서 Context에서 네트워크 필수 조건만 빼고 싱글 프리팹에 붙이는 것으로 전환이 끝나지 않는다. 현재 Context의 `ValidateOwnedReference`는 플레이어 바깥 참조를 오류로 처리한다. 싱글의 씬 인벤토리를 유지한 채 연결할 범위와 실제 소유 이전 범위를 1단계에서 구분하고, 경고만 끄는 방식으로 넘기지 않는다. 이 씬 정보는 사용자가 열어 둔 Dirty 상태 기준이며 디스크의 정식 씬 전체에 일반화하지 않는다.

### 3.3 진입 씬·플레이어 생성·저장 경계

| 구분 | 현재 원본과 호출 경로 | 상태 소유자 / 보존할 경계 |
|---|---|---|
| 싱글 시작 | `YJ_SinglePlayerStartFlow` → `DataManager.BeginNewGame` → SceneLoader → StageSelect | 기존 오프라인 프로필·시작 흐름 유지 |
| 싱글 플레이어 생성 | `YJ_PlayerSpawner.Start` → 저장 캐릭터 선택 → Fighter/Gunner Instantiate | `PlayerSpawnPoint.prefab`의 두 직렬화 참조가 위 싱글 GUID를 가리킴. 기존 T_PlayerController가 있으면 생성 거부 |
| 미러 참가·스폰 | 로비 Bridge → Authenticator → SessionLifecycle → 클래스별 Instantiate → `NetworkServer.AddPlayerForConnection` | 서버 명부·참가자/연결 소유. 로비 `autoCreatePlayer=0`이며 기존 NetworkManager+Lifecycle 사용 |
| 노드 선택 | 싱글 `YJ_StageSelectManager.SelectNode` / 멀티 `MirrorStageSelectRouteAdapter` → StageVoting → 서버 Scene 전환 | 멀티는 서버 runSnapshotJson/revision이 원본. 단일 로컬 SceneLoader가 동시에 실행되면 안 됨 |
| 진행 저장 | 싱글 `YJ_StageSaveService` / 멀티 `MirrorTestNetworkManager` | 싱글 `stage_map_save.json` 또는 임시 런의 static sessionJson과 서버 스냅샷을 분리. 재접속 프로필은 `MirrorReconnect`의 별도 자격 저장이며 전체 게임 저장이 아님 |
| 결과 | 싱글 `KY_RunStatsTracker.FinishRun` → `KY_ResultPayload.SetResult` → KY_ResultScreen | 멀티는 서버 결과를 같은 표시 API로 전달할 경계가 필요. `SetResult`는 메모리 표시 데이터이며 영구 크레딧 지급 API가 아님 |

근거: [PlayerSpawnPoint.prefab](../../Assets/Resources/Prefabs/Manager/PlayerSpawnPoint.prefab) 44–48행, [YJ_PlayerSpawner](../../Assets/Scripts/Scene/YJ_PlayerSpawner.cs) 19–48행, [MirrorSessionLifecycle](../../Assets/SW/TEST/MirrorPlayerContext/Scripts/MirrorSessionLifecycle_MirrorTest.cs) 263–307행, [MirrorTestNetworkManager](../../Assets/SW/TEST/MirrorPlayerContext/Scripts/MirrorTestNetworkManager.cs) 416–459행, [YJ_StageSaveService](../../Assets/Scripts/StageSelect/YJ_StageSaveService.cs), [KY_RunStatsTracker](../../Assets/Scripts/UI/Result/KY_RunStatsTracker.cs) 66–127행.

현재 멀티 운영 대상은 아래 **11개 씬**이다. 로비를 포함해 모두 `Assets/SW/TEST/MirrorCombat/Scenes`에 있다. `Assets/SW/TEST/MirrorPlayerContext/Scenes/MirrorPlayerContextTest.unity`는 별도 Context 시험 씬이다.

- `Lobby_MirrorTest`
- `StageSelect_MirrorSessionTest`
- `Act1_Camp_MirrorSessionTest`
- `Unknown_Stage_MirrorSessionTest`
- `Act1_Stage1_MirrorSessionTest` ~ `Act1_Stage6_MirrorSessionTest`
- `Act1_BossStage_MirrorSessionTest`

`MirrorSessionCampTest`, `MirrorSessionCampRouteTest`, `Act1_Stage1_MirrorCombatTest`는 위 운영 route와 구분하는 시험 씬이다. 현재 `EditorBuildSettings.asset`에는 정식 싱글 씬이 등록돼 있고 위 Mirror 11개는 없다. 기존 빌더가 시험 빌드 때 별도로 넣었던 사실을 정식 등록 완료로 해석하지 않는다.

경로 변경은 `MirrorAct1SceneRoute_MirrorTest`의 매핑뿐 아니라 NetworkManager의 `GetRouteForScene/GetSceneForRoute/IsManagedSessionScene` 및 `MirrorAct1SceneSetup_MirrorTest`, `MirrorLobbySceneSetup_MirrorTest`의 하드코딩 경로와 함께 검토해야 한다. 미러 Camp의 `PlayerSpawnPoint`라는 이름은 현재 NetworkStartPosition이며 이름만 보고 싱글 스포너로 판정하지 않는다. YAML의 직접 참조 검색에서 미러 11개에 YJ_PlayerSpawner script GUID가 없었지만, 이것을 모든 중첩 프리팹의 런타임 중복 검사 통과로 확대하지 않는다.

### 3.4 기능별 상태 소유자와 실제 호출 경로

경로 표기는 클래스/메서드 이름이다. `*_MirrorTest` 접미사는 현재 타입을 뜻하며 아직 이름을 바꾸지 않았다.

| 기능 | 싱글 실전 경로 / 상태 원본 | 미러 경로 / 상태 원본 | 보존할 규칙 |
|---|---|---|---|
| 공격 입력·실행 | WBH_PlayerInputHandler → T_PlayerCombat.TryAttack → 상태머신·AnimationEvent.ExecuteAttack → Fighter 근접 / Gunner 총기별 판정 | PlayerCombatAuthority의 소유자 입력 → Command 예약 → 애니메이션 이벤트 확인 → 서버 근접·산탄·네트워크 투사체 | 원본 무기별 범위·타이밍·탄종과 서버의 공격 중복·권한 검사 모두 보존 |
| 피해 계산 | WBH_CombatManager.ProcessDamage → WBH_DamageResult → 대상 TakeDamage | WBH_CombatResolver.TryProcessPlayerDamage → NetworkEnemyAuthority. 적→플레이어는 원본 CombatManager도 사용 | 공격력·속성·치명·방어·받는 피해 배율·최소 피해. 공격 출처·AttackId·후속 큐를 잃지 않음 |
| 장착→Stat | ItemUI/InventoryController → EquipmentSystem → 장비 변경 이벤트 → PlayerStatManager.Recalculate | NetworkInventoryInput → PlayerInventorySync Command → 서버 EquipmentSystem → 소유자 스냅샷 | 인벤토리 배치와 장비 슬롯은 같은 아이템 인스턴스를 유지. Stat 원본은 해당 플레이어 |
| HP·MP·버프 | 해당 플레이어 PlayerHealthManager/PlayerManaManager/PlayerBuffManager; 레거시 UI는 Instance 활용 | 서버의 같은 Manager가 계산. PlayerRuntimeStateSync가 복제하고 원격 클라이언트 Mana/Buff 자동 계산은 정지 | 체력 반올림·상한 보정, 버프 수명, 자원 소비·회복 중복 금지 |
| 포션 | PlayerActionInputHandler → PotionUseManager.TryUsePotion → 로컬 Health/Buff, CurrentCharges·nextUsableTime | PlayerActionInputHandler_MirrorTest → RuntimeStateSync.CmdUsePotion → 해당 Context.Potions | 원본 1초 제한과 서버 요청 검증을 함께 보존; 현재 미러는 쿨다운 미구현 |
| 상점 | ShopController → ShopTradeService → ShopStockService·PlayerWallet·그리드 이동 | NetworkShopPlayerState Command → 공유 NetworkShopState.ServerTryBuy/Sell → 서버 인벤토리·지갑 | 재고 원본과 개인 지갑 분리, 실패 시 환불·재고/아이템 복구, 동시 구매 단일 성공 |
| 강화 | UpgradeController → UpgradeService.TryUpgrade | PlayerInventorySync.ServerUpgradeItem → 공통 TryGetUpgradeCost → 서버 결제·레벨 변경·스냅샷/실패 복구 | 비용 계산은 이미 재사용. 원격 스냅샷 복구를 제거하며 단순 호출로 치환하지 않음 |
| 고유효과 | ItemTriggerManager·PlayerRelicEffectProvider·StatThresholdRunner | 미러 대응 컴포넌트 + ArmorEffectProvider·연쇄/추가타 실행·서버 큐·표시 | 직접타/Skill/Effect/DoT 자격, 아이템별 쿨다운과 소유자, 효과 재진입·연쇄 증식 제한 |
| 적 상태·사망 | 원본 StatusEffectController 틱 → EnemyStatus HP/OnDead → EnemyController 사망·풀 | 원본 상태이상 컴포넌트 재사용 + NetworkEnemyAuthority 서버 적용·사망·보상·표시 | 풀 반환 즉시 상태 정리, 처치자 귀속, 보상 1회. 실제 본체와 파괴 연출 풀 분리 |
| 플레이어 사망·부활 | Health.OnDeath → WBH_PlayerStatus.OnDead → Controller.Die. WBH_PlayerAnimation.AniEvent_EndDead → TryRevive → Revive 상태 → CompleteRevive | RuntimeStateSync가 서버 HP/사망 스냅샷으로 입력·시각 제어. Mirror 애니메이션의 EndDead/EndRevive는 원본 부활을 호출하지 않음 | 싱글 패시브 부활과 미러 개발 부활은 서로 다른 기능. 아래 차이표 참조 |
| UI·씬 복귀 | 싱글 Scene/UI 참조·일부 Instance | LocalPlayerUIBinder가 로컬 Context를 Bind/Unbind, 명부·투표·서버 결과 표시 | 재접속과 씬 이동의 구독 해제/재연결; 다른 플레이어의 상태를 로컬 HUD로 읽지 않음 |

핵심 소스: [T_PlayerCombat](../../Assets/WBHTest/Scripts/Player/T_PlayerCombat.cs) 83–303, 465–499행; [PlayerCombatAuthority](../../Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerCombatAuthority_MirrorTest.cs) 288–336, 591–810행; [CombatManager](../../Assets/WBHTest/Scripts/Combat/WBH_CombatManager.cs) 9–80행; [CombatResolver](../../Assets/SW/TEST/MirrorPlayerContext/Scripts/WBH_CombatResolver_MirrorTest.cs) 65–120, 177–217행; [InventoryView](../../Assets/SW/Scripts/Inventory/UI/InventoryView.cs) 36–118행; [RuntimeStateSync](../../Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerRuntimeStateSync_MirrorTest.cs) 85–177행.

### 3.5 공유 규칙 / 네트워크 처리 / 시험 도구 분류

| 분류 | 현재 대상 | 통합 때의 취급 |
|---|---|---|
| 이미 공유하는 게임 데이터·규칙 | ItemDefinitionSO·ItemInstance·EquipmentSystem·Stat/Health/Mana/Buff·상태이상 컨트롤러·UpgradeService 비용 계산·StageMapSaveData | 기존 구현을 기준으로 유지하고 실제 중복 부분만 공통화 |
| 양쪽에 복제된 게임 규칙 | 피해 계산, PotionUseManager, ItemTriggerManager, PlayerRelicEffectProvider, StatThresholdRunner, 상점 가격/거래 일부 | 같은 이름이라는 이유로 덮어쓰지 않음. 싱글 최신 기능과 미러 최신 기능을 각각 보존하며 기능별로 합침 |
| 운영에 필요한 네트워크 코드 | NetworkManager·Authenticator·SessionLifecycle·Roster·StageVoting·각 Authority/Sync·NetworkWorldItem·원격 표시 | TEST 폴더에 있어도 삭제할 시험 코드가 아님. 권한·스냅샷·재접속·실패 복구 유지 후 승격 |
| 화면 연결 | InventoryView·HUD Bridge·LocalPlayerUIBinder·StageSelectRouteAdapter·NPC 연결 | 기존 Bind/Unbind/RestoreMap/UnityEvent 우선. 필요한 외부 제어만 최소 API로 노출 |
| 명시적 시험 도구 | Smoke/Mppm runner·Editor Validation/Setup/Builder·상황실 개발 명령·ServerReviveForTest·시험용 지급/치명 피해 | 게임 규칙 원본으로 채택하지 않음. 검사 도구는 유지하되 운영 입력/보상/부활과 분리 |
| 실전 자산에 남은 시험 컴포넌트 | 싱글·미러 Gunner의 HealthTestKeyTrigger·ManaTestKeyTrigger | Editor 직렬화 `enabled=true` 확인. 정식 승격 단계에서 시험 입력 제거/분리 대상. 이번에는 수정하지 않음 |

**시험 키 추가 근거:** 두 컴포넌트는 H/J/M 입력으로 `PlayerHealthManager.Instance`·`PlayerManaManager.Instance`를 직접 변경하며 코드에 개발 빌드/서버 권한 가드가 없다. 미러 Gunner Binder의 직렬화 `localOnlyBehaviours` 목록은 `WBH_PlayerInputHandler_MirrorTest`, `PlayerActionInputHandler_MirrorTest` 두 개뿐이다. 이 자료로 현재 프리팹의 시험 입력 잔존은 확인했지만, 다중 Player 실행에서의 실제 부작용까지 재현한 결과는 아니다.

### 3.6 양방향 차이와 정식 통합 전에 보존할 동작

| ID | 확인한 차이 | 처리 단계 / 수용 기준 |
|---|---|---|
| B01 | 싱글의 씬 소유 InventoryCommon/Runtime과 미러의 플레이어 자식 인벤토리 구조가 다름. Context는 테스트 타입과 자기 루트 소유를 요구 | 1단계. 싱글 장비/UI 참조를 끊지 않는 소유 연결 설계 후 변경. Context 검사 무력화로 대체하지 않음 |
| B02 | 싱글 포션은 두 실제 프리팹 모두 1초 쿨다운. 미러 PotionUseManager에 nextUsableTime/쿨다운이 없고 CmdUsePotion은 부재 검사 후 실행 | 2단계. 성공한 사용 간격·충전·효과를 공통 규칙으로. 사망/조작 불가 시 서버 사용 조건도 명시해 검사 |
| B03 | 싱글 정상 부활은 패시브 해금 시 시작 횟수 1, TryRevive에서 횟수 차감·패시브 체력 비율·10초 무적. ReviveForStageClear는 10%·3초 무적 API지만 이번 제한 검색에서 실제 호출부는 확인하지 못함 | 3단계. 정상 부활과 개발 부활을 구분. 무호출 API를 이미 쓰는 게임 규칙이라고 주장하지 않음 |
| B04 | 미러 애니메이션은 정상 TryRevive/CompleteRevive 호출을 차단. 확인한 서버 부활 API는 개발 명령 조건의 FillHealth+입력 복구. ReviveHealthFraction은 검증 프로필에 있지만 조사한 미러 런타임의 소비 호출은 없음 | 3단계. 기존 싱글 패시브 부활을 서버 권한으로 연결해야 함. Q1 개발 부활 PASS로 대체 불가 |
| B05 | 싱글 YJ_PlayerDead는 부활 횟수가 없을 때 사망 화면 후 Title로 이동하며 네트워크 활성/Identity가 있으면 실행하지 않음 | 3·5단계. 싱글 사망 복귀 유지. 미러 전멸/런 종료가 이 로컬 Title 경로를 우회한다는 사실을 검증 |
| B06 | 싱글 CombatManager와 미러 Resolver의 계산·트리거 실행이 분리. 원본에도 NetworkEnemyAuthority_MirrorTest 중복 트리거 방지 참조가 있음 | 3단계. 공통 계산과 권한/후속 큐 경계 정리. 테스트 타입 삭제·이름 변경 전에 역참조 갱신 |
| B07 | 미러의 AttackId·출처 snapshot·직접/Effect/DoT 구분·6종 효과·원격 회피 사건·아이템별 상태는 단순 싱글 복제로 되돌릴 수 없음 | 3·4단계. 최신 Q1 보존. 클라이언트 ItemTriggers를 일괄 끄면 원격 회피 전달이 다시 끊길 수 있음 |
| B08 | 미러 DamageResult의 일부 피격 정보는 null이나 별도 Gunner impact RPC/EnemyView가 존재 | 3단계. 원본 hitEffect와 네트워크 연출의 실제 대응 대조. 전부 소실 또는 전부 동일로 단정하지 않음 |
| B09 | 상점 싱글 ShopPricing은 로컬 PassiveSkillManager 할인율을 읽고, 미러는 참가자별 검증 할인율을 사용. 강화 비용은 공용이나 확정/복구는 별도 | 2단계. 가격 수식 재사용 시 서버가 Host 프로필을 원격 참가자에 적용하지 않도록 보존 |
| B10 | StageSelect는 private 필드 reflection 및 다음 프레임 선택/reticle 보정으로 원본 완료·씬 이동을 우회 | 5단계. 현재 시험 씬의 선완료를 재현 버그로 단정하지 않음. 정식화 시 명시적 외부 제어로 교체 |
| B11 | 결과 UI의 지갑·진행 조회는 싱글 소스이며 Mirror 11개 route에 ClearResultScene은 없음 | 6단계. 서버 결과→기존 Payload 표시 연결. 영구 크레딧 정산 완료로 표현하지 않음 |
| B12 | Gunner 실제 프리팹에 시험 자원 키가 남아 있고 미러 산탄 direct target 기록은 Fighter와 다름 | 시험 키는 6단계 운영 자산 점검 대상. 산탄 Arc는 현행 Fighter 전용 제약상 확정 버그가 아니며 조합 확대 때 재검토 |

부활 근거: [T_PlayerController](../../Assets/WBHTest/Scripts/Player/T_PlayerController.cs) 82–83, 409–449행; [원본 Animation](../../Assets/WBHTest/Scripts/Player/WBH_PlayerAnimation.cs) 270–276행; [미러 Animation](../../Assets/SW/TEST/MirrorPlayerContext/Scripts/WBH_PlayerAnimation_MirrorTest.cs) 274–282행; [YJ_PlayerDead](../../Assets/Scripts/Scene/YJ_PlayerDead.cs) 20, 62–66행; [미러 패시브](../../Assets/SW/TEST/MirrorPlayerContext/Scripts/MirrorPassiveProfile_MirrorTest.cs) 14, 67행.

### 3.7 플레이 검증 기준과 증거 수준

아래는 다음 변경의 전후 비교에 사용할 기준이다. **이번 0단계에서 실제 Play를 재실행한 결과는 없다.** 현재는 소스·실제 프리팹·Editor 상태로 경로와 차이를 확정하고, 과거 실행 기록의 범위를 함께 보존했다.

| 실제 흐름 | 이후 같은 조건으로 확인할 기준 | 현재 근거 수준 |
|---|---|---|
| Fighter/Gunner 기본 공격 | 실제 입력→AnimationEvent→근접/라이플/산탄/유탄→적 HP·피격 연출. 같은 Stat/적 조건으로 수치 비교 | 이번 소스·프리팹 확인. 9/13 전투 및 9/20 Q1은 아래 과거 기록 |
| 장착·해제 | 동일 instanceId의 무기/방어구 장착→Stat/HUD 변화→해제 후 복원. 남의 플레이어 불변 | 과거 싱글 인벤토리/전용4인 거래 기록. 이번 재실행 아님 |
| 포션 | 1회 성공 시 충전 1 감소, 1초 내 추가 사용 실패, 이후 재사용. 해당 플레이어만 회복/버프 | 원본 코드 및 두 싱글 프리팹 1초 확인. 미러 동작 차이 B02 미수정 |
| 사망·부활 | 패시브 유무·횟수·HP 비율·무적·입력 복구·죽은 상태 재접속을 따로 검사 | 싱글 정상 경로 소스 확인. Q1은 개발 부활이므로 정상 미러 부활 검증 아님 |
| 캠프 거래 | 구매·판매·강화·드롭·획득·장착, 실패 복구, 공유 재고 경쟁, 원격 복귀 후 일치 | 9/13 전용4인 검사 기록. 이번 현재 코드 재실행 아님 |
| 고유효과 6종 | 실제 공격·회피·저마나·아이템 소유·Burn 처치·원격 관찰, 재접속·새 런 | 9/20 Q1: 서버 전용 Editor+Windows Player4, KCP loopback, 107단계 기록 |
| Act1 전체 | 웨이브·전투·캠프·이벤트·보스 패턴·결과·새 런. 치명 피해로 강제 진행한 검사는 따로 표시 | 9/13 전체 런은 개발 치명 피해를 사용한 진행 검증. 일반 난이도 플레이 완료 아님 |

과거 근거:

- [Mirror_Work_Closeout_2026-09-13.md](../Mirror_Work_Closeout_2026-09-13.md): 전용 서버+4클라이언트 전투71단계, 인벤토리 경쟁, 11노드·결과·새 런 검증 기록. `RunValidation/FinalValidation20260913/`의 당시 로그 경로를 안내한다. 이번에는 원본 로그를 다시 실행하거나 전체 재판독하지 않았다.
- [고유효과 상세 계획 §10.3](UniqueEffect_Detailed_Plan.md#103-q1--서버별도-player-4개-기능-검수-완료): 2026-09-20 Q1 완료 6종·107단계. 반복 관찰 단계이며 독립 테스트 107개가 아니다. 캠프 AI/HP 고정, 개발 부활, 빠른 보스 이동·AI 정지의 제한이 있다.
- 같은 기록의 전체 Windows 빌드는 기존 BH 보스 셰이더 오류 10건을 남겼다. 현재 Console 오류0을 이 셰이더 문제의 해결 근거로 사용하지 않는다.

재실행 도구는 새 검사 프레임워크 대신 기존 `MirrorLanTestBuilder`, `MirrorCombatSmoke_MirrorTest`+`MirrorUniqueEffectSmoke_MirrorTest`를 재사용한다. Q1 인자는 상세 계획 §10.3을 따른다. 실행 전 시험 프로필과 Build Settings·Dirty Scene 보호 범위를 확인하고 실행 후 복구한다. 정상 부활·일반 보스 패턴·다중 PC 지연/손실 검사는 기존 Q1 범위에 추가해서 별도 결과를 남겨야 한다.

### 3.8 0단계 판정 및 다음 착수점

**판정: 0단계 기준선 조사 완료.** 실제 플레이어 자산, 기능별 상태 소유자·호출 경로, 양방향 보존 항목, 운영 네트워크와 시험 도구, 기존 검증의 제한을 기록했다. 게임 기능의 정식 통합이나 현재 버전 Play 회귀검증을 완료한 것은 아니다.

- 변경 내용: 계획 원문 보존 문서와 이 실행 기록, 총 2개 Markdown 문서 생성·갱신.
- 싱글 유지 방식: 싱글 코드·프리팹·씬·저장 파일을 수정하지 않음. 열린 Dirty Stage1 보존.
- 멀티 동작: 기존 런타임 변경 없음. 포션·정상 부활·시험 키 차이는 해결할 항목으로 기록했으며 고쳤다고 주장하지 않음.
- 검증 결과: 실제 플레이어 프리팹4개 Missing Script0, 미러 Context2개 필수 참조 완전/동일 플레이어 소유, 싱글 포션 직렬화1초, Console 현재 오류0. 빌드·Play·LAN/WAN·성능은 이번 미실행.
- 변경 보존 검사: 시작 전 미커밋 경로 79개의 상태와 파일 SHA-256을 종료 시 다시 비교해 모두 동일함을 확인했다. 삭제 상태도 동일하다. HEAD 변경 없음, 새 경로는 이 실행 기록과 계획 문서 2개뿐이다. 문서 내부 파일 링크의 존재와 Markdown 코드 블록 짝을 검사했다.
- 다음 작업은 **1단계**다. 우선 SW PlayerContext·바인더·InventoryView와 InventoryCommon 소유 관계를 기준으로 필수 참조를 분리한다. WJ 포션/효과 원본 공통화나 BH 전투 변경을 1단계에 무작정 끌어오지 않는다.
- 이후 팀원 스크립트가 실제 수정 대상이 되면 파일·이유·싱글 영향을 제시하고 승인 후 작업한다. 이번에는 팀원 스크립트를 수정하지 않았다.
- 개인 구현 로그: 갱신하지 않음. 이번은 계획·조사·문서화이며 기능 구현·Play 검증 완료 작업이 아니다.

## 4. 1단계 — 2026-09-20

- 상태: 완료. `ponytail full`, `unity-cli` 적용. 읽기 조사 서브에이전트는 Sol medium 1개만 사용.
- 구현 범위: 공통 Context의 네트워크 필수 조건 분리, 기존 싱글 InventoryCommon의 명시적 연결과 중복 소유 차단, 미러 생성·등록·입력 복구의 필수 구성 검사, 기존 검증 보강.
- 싱글 보존: 씬 인벤토리와 장비·지갑을 옮기거나 복제하지 않는다. 기존 Instance API는 유지하고 공통 장비 조회에서 연결된 Context를 우선한다.
- 팀원 수정 승인: `Assets/Scripts/Scene/YJ_PlayerSpawner.cs`의 생성 직후 연결에 대해 사용자가 “수정해줘”라고 승인했다. `SW 수정`과 쉬운 XML `<summary>`를 붙였다.
- 대안 검토: SW 전용 연결 컴포넌트를 별도로 두면 스포너 수정은 피할 수 있지만 컴포넌트·Inspector 참조·실행 순서가 늘어난다. 승인된 스포너에서 직접 연결해 흐름을 짧게 유지했다.

### 4.1 변경 내용

| 대상 | 변경 요약 |
|---|---|
| `PlayerContext` | 공통 완전성에서 네트워크 전용 4개 타입을 제외한다. 기존 미러 직렬화 필드와 접근자는 보존한다. 싱글 씬 인벤토리는 같은 씬·네트워크 비활성·다른 소유자 없음 조건으로 명시적으로 연결한다. 비활성화 시 연결 소유를 해제하고 재활성화 시 다시 연결한다. |
| `InventoryController` | 인벤토리 하나에 연결된 싱글 소유자를 기록한다. 장비 조회는 원격 Identity 차단을 유지하고 연결된 Context를 사용하며, Context가 없는 기존 싱글 초기화에는 Instance 경로를 유지한다. |
| `YJ_PlayerSpawner` | 활성 원본 프리팹 생성 후 Context를 추가·연결한다. 프리팹에 미리 Context를 붙이지 않아 기존 Awake/OnEnable의 스탯·무기 외형 초기화 순서를 유지한다. 연결 실패는 오류를 남기며 기존 HUD 연결 흐름은 유지한다. |
| `MirrorSpawnedPlayerBinder`, 세션 Lifecycle·NetworkManager | 공통 Context와 미러 필수 컴포넌트 구성을 별도로 확인한다. 생성 후 상태 참조 사용 전, 서버·로컬 등록, 입력 복구 전에 검증한다. 잘못된 생성 구성은 접속을 거절한다. |
| `InventoryView`, Lobby 자산 검사 | 기존 Bind API에서 완전한 공통 Context를 받고, 미러 프리팹 검사에는 별도 미러 필수 조건을 사용한다. |
| Editor 검사기 | `PlayerContextStage1Validation`에 실제 프리팹 소유 분리와 싱글 실행 검사를 남겼다. 기존 인벤토리 검사기는 현재 정식 씬의 `KY_InventoryPopup`도 열도록 보강했다. |

### 4.2 이번 검증

- Unity 6000.3.22f1 컴파일 오류 0. Sol medium의 읽기 검토 후 주 에이전트가 실제 Editor에서 검사했다.
- `PlayerContextStage1Validation.ValidateAssets`: 실제 싱글 Fighter/Gunner 연결, 중복 소유 거절, 인벤토리 교체, 장비·Grid·골드 보존 PASS. 실제 미러 프리팹 4개 인스턴스의 독립 참조·골드, 원격 싱글 fallback 차단, 네트워크 필수 상태 누락 거절 PASS. Preview Scene만 사용하고 원본 자산은 저장하지 않았다. **이 검사는 네트워크 4접속 검증이 아니다.**
- `MirrorLobbySceneSetup_MirrorTest.ValidateLobbyAssets`, `MirrorInventoryRulesValidation_MirrorTest.ValidateAssetBindings` PASS.
- 열린 정식 Act1_Stage1의 실제 Fighter를 스포너로 생성해 `ValidateSingleRuntime` PASS. Mirror 세션 없이 Context 완전성, 기존 씬 인벤토리, 반복 연결·비활성화/재활성화·골드/장비/아이템 보존 확인.
- 실제 인벤토리 검사 첫 시도는 해당 씬에 구형 `InventoryPartView`가 없어 검사기 전제에서 중단됐다. `KY_InventoryPopup` 대응 후 재실행해 지급→실제 UI 드래그·회전·이동→우클릭 장착/최대체력 증가→해제/최대체력 복원→시험 아이템 정리 PASS. Console 오류 0. 검사 중 적 공격 진행을 막기 위해 재실행에서는 Time.timeScale=0을 사용했다.
- 실제 4클라이언트·씬 이동·재접속 검증용 Development Player는 C 드라이브 임시 작업 폴더에 출력한다. 첫 시도는 I 드라이브의 중간 빌드 공간 부족으로 실패했다(`Temp/__Backupscenes/0.backup` 저장 및 `Assembly-CSharp.dll` 복사 실패, 오류 3건). 코드 컴파일 실패와 구분한다.
- 빌드 전 미저장 씬 확인창에서 사용자 Escape로 자동 조작이 중단됐고, 이후 사용자가 빌드 중임을 알리며 계속 진행하도록 요청했다. 재개 시 Act1_Stage1은 저장된 상태(`isDirty=false`)였고 디스크 Scene 변경이 존재했다. 해당 변경은 되돌리지 않는다.
- 사용자 요청으로 미사용 이전 빌드 약 21.97GB를 삭제했다: I의 `Builds/MirrorFinalValidation_20260913` ZIP 5.25GB·Client 6.72GB·Server 3.06GB, C 임시 `Project2-D1Q1-20260920/Client` 6.94GB. 실행 중인 프로세스가 없는 이전 출력임을 확인했다. 소스·에셋·이전 검증 로그·미저장 씬 백업은 보존했다.
- 재빌드: `Succeeded`, 215.53초, 출력 약 6.94GB. BuildReport 오류 10건은 기존 `Boss_Act_01_Up/Leg.shader`의 `invalid subscript uv0`이며 이 단계에서 수정하지 않았다. 최종 Editor Console도 해당 셰이더 오류 10건이 남아 있으므로 “최종 Console 오류 0”으로 보고하지 않는다.
- **실제 네트워크 검증:** 같은 최신 Development 실행 파일로 별도 서버 1개와 클라이언트 4개(Fighter 2/Gunner 2)를 실행했다. 단일 PC loopback이며 서버는 batchmode/nographics, 클라이언트는 화면을 생성하는 일반 Player다.
  - 인벤토리 검사: 4명 모두 Camp 이동·실제 UI 이동/회전/교환/거절·장착/해제·판매/구매·강화·드롭/획득 PASS. 서버가 4개의 서로 다른 보유 아이템·개별 지갑을 확인했다. 공유 재고 경쟁은 성공자/소유자 1명, 네 클라이언트의 `server-confirmed` 모두 PASS. 기존 패시브 구독 검사도 네 명 모두 `callbacks=1` PASS.
  - 재접속·씬 이동 회귀: 기존 Q1 검사에서 서버의 최종 PASS와 네 클라이언트 각각 `steps=107` PASS. 재접속 뒤 같은 참가자·런타임·장비·버프 유지, 재접속 후 실제 공격, 보스 씬 이동, 로비 복귀·새 런 상태 초기화를 확인했다. 기존 UI 바인더는 연결 변경 시 먼저 Unbind하고 재연결하는 코드 경로를 유지했다. 모든 UI 이벤트의 호출 횟수를 별도로 계측한 검사는 아니다.
  - 이번 두 실행의 로그에서 FAIL·Exception·PlayerContext 오류·로컬 UI Bind 실패 없음. 각 검사기의 최종 PASS를 수집한 뒤 이번에 시작한 5개 프로세스만 종료했다. 1,200초 대기 타이머의 종료 코드로 통과 판정한 것은 아니다.
- 증거: `RunValidation/MirrorStage1_20260920/`의 `build.json`, `build-errors.json`, `inventory-server0.log`, `inventory-client1~4.log`, `q1-server0.log`, `q1-client1~4.log`. 현재 검증 빌드는 `C:/Users/firen/AppData/Local/Temp/Project2MirrorStage1_20260920/MirrorStage1.exe`에 남겨 둔다. 로그·실행 파일은 Git 관리 대상이 아니다.

### 4.3 완료 판단과 남은 범위

- 공통 Context를 싱글에 연결하고 네트워크 필수 구성을 분리했으며, 실제 싱글과 별도 4클라이언트의 소유 분리·씬 이동·재접속을 확인해 1단계를 완료한다. 기존 싱글 프리팹과 씬 인벤토리를 재배치하지 않았다.
- 빌드가 자동 생성한 ProjectSettings의 iOS 기본 필드 4개, URP GlobalSettings의 빌드용 런타임 목록, Addressables `link.xml`/`.meta`를 시작 상태로 돌렸다. 사용자가 저장한 Act1_Stage1 변경은 보존했고, 빌드 후 다시 Dirty가 된 열린 씬은 추가 저장하지 않았다.
- 보존 검사: 시작 당시 미커밋 경로 81개 중 이번에 갱신한 실행 문서·개인 로그 2개를 제외한 79개는 SHA-256/파일 존재 상태가 동일하다. 신규 변경은 1단계 코드·검사기와 사용자가 저장한 Scene 변경이며, 공용 ProjectSettings/Packages에 새 변경을 남기지 않았다. 수정 코드·문서의 Diff 공백 검사 통과, 검증용 Player 프로세스 잔류 없음.
- 남은 범위: 기존 보스 셰이더 오류, 다중 PC LAN/WAN, 전체 Act1 전투·성능은 이번 완료 범위 밖이다. 싱글은 실제 Fighter Play 및 Fighter/Gunner 자산 검사를 수행했으며 Gunner의 별도 오프라인 Play는 이번에 실행하지 않았다. Q1의 개발 부활 검증은 3단계 정상 패시브 부활 통합 완료를 뜻하지 않는다.
- 개인 구현 로그: `ImplementationLogs/김성우.md`의 Git 구현 이력과 기록 이력에 이번 구현·검증·제한을 추가했다. Commit·Push 없음.
- 다음 작업: **2단계 포션·장비·인벤토리·경제 규칙 통합**. 원본의 1초 포션 재사용 제한 등 0단계 차이표를 기준으로 진행한다.

### 4.4 Start부터 싱글 연결 후속 검증 — 2026-09-20

- 사용자 요청으로 실제 `Start`에서 Play를 시작했다. `IsSessionOnly=true`를 확인하고 타이틀의 싱글 버튼 → 거너 선택·시작 → StageSelect → 첫 전투 맵까지 기존 UI 이벤트로 진행했다. 거너 생성·Context 연결과 사망 후 타이틀 복귀를 확인했다.
- 이후 Act1 11층 → Act2 12층 → Act3 13층의 한 경로를 최종 보스 맵까지 추적했다. **전투 완료는 `CompleteStage` 호출로 보조하고, 플레이어를 활성 포탈 안으로 이동시켜 실제 Trigger·저장·씬 전환을 실행했다. 전체 전투를 정상 플레이로 클리어했다는 검증은 아니다.** 노드는 기존 클릭 이벤트를 사용했고 일반전·엘리트·캠프·이벤트 경로와 Act 전환을 거쳤다. 모든 분기·이벤트 선택지·전투 밸런스 검증은 포함하지 않는다.
- 거너가 생성된 구간의 Context 완전성 및 인벤토리 소유 연결 검사 35회 모두 통과(보스 재시도 포함), 연결 실패 0회. Act1→Act2, Act2→Act3 전환 후에도 기존 싱글 연결을 확인했다. 이 결과로 앞 절의 거너 오프라인 Play 미검증 항목을 보완한다.
- Act1 보스에서 검사 보조로 포탈에 일찍 진입했을 때 이동 조건 오류 1회를 관찰했다. 이후 조작 가능 상태에서 포탈 밖으로 나갔다가 재진입해 Act2로 이동했다. 재시도 시 조건은 정상이었으나 첫 실패 순간의 개별 조건을 계측하지 않아 원인을 확정하지 않는다.
- Act3 보스는 `WaveSet_Boss.asset`이 Boss 1개를 요구하지만 해당 SpawnArea가 Boss를 생성할 수 없어 웨이브 구성이 실패했다. **사용자가 Act3 보스는 아직 미완성이며 현재 상태가 맞다고 확인했다.** 이번 1단계 회귀 실패로 분류하지 않고 미완성 구간으로 남긴다.
- Act3 마무리 확인 항목: 최종 포탈의 `clearSceneName`은 `ClearScene`이고 실제 로드 가능한 결과 씬은 `ClearResultScene`이다. 완료 처리를 보조한 뒤에도 목적지 검사에서 이동이 차단됐다. 최종 결과 화면 도달은 미검증이며, 이번 요청에서는 스크립트·씬·Build Settings를 수정하지 않았다.
- 종료 후 Play를 중지하고 임시 Play 시작 씬 설정을 원래의 null로 복원했다. 원래 열려 있던 Dirty Act1_Stage1은 저장하지 않고 보존했다. 기존 `gamesave.json`, `settings.json`, `stage_map_save.json` 3개는 시작 전 SHA-256과 일치함을 확인했다. 테스트 중 생성된 저장·백업 파일은 검증 폴더에 보관했다.
- 증거: `RunValidation/SingleStart_20260920/route.jsonl`, `final-wave.json`, `final-portal.json`, `final-state.json`, `final-console-errors.json`, `saves-restored.json`, 화면 캡처 2개. 임시 실행 스크립트는 제거했다.
- 판단: **이번 PlayerContext 통합의 싱글 시작·재생성·소유 연결은 확인한 경로에서 정상.** Act3 보스 완성과 결과 씬 연결, 보조 없는 전체 전투 클리어는 별도 범위다. 검사·기록만 수행했으므로 개인 구현 로그는 추가하지 않았다.

## 5. 2단계 — 2026-09-23

- 상태: 완료. `unity-cli`, `ponytail full`을 적용했다. 단계 완료는 아래 단일 PC 검증 범위 기준이며 최종 Act1 채택 검증과 구분한다.
- 시작 브랜치: `codex/unity-6000-3-22-test`. 기존 `.codex/agents/unity-scout.toml`, `AGENTS.md` 수정과 Addressables `link.xml`/`.meta` 삭제 상태는 보존한다.
- Editor 기준선: Unity 6000.3.22f1, 저장된 `Assets/Scenes/Maps/Basic/LoginScene.unity`, Edit Mode, Prefab Stage 없음. Console에 Multiplayer Play Mode `ScenarioConfig.GetAllInstances`의 기존 AssertionException 1건이 있다.
- 승인: 사용자가 WJ `PotionUseManager.cs`의 공통 규칙 추출과 해당 플레이어 효과 참조 연결을 승인했다. 기존 공개 API·직렬화 필드를 유지하고 변경 메서드에 `SW 수정`과 XML 설명을 붙인다.
- 범위: 포션 충전·효과·고정 쿨타임 공통화, 서버 사용 조건 검증, 기존 장비·인벤토리·거래·강화 서비스 재사용과 원자적 복구 확인. 전투·고유효과·씬 승격은 다음 단계에 남긴다.
- 사용자 추가 지침: 기존 팀원 주석 보존 원칙을 `AGENTS.md`와 정식 전환 계획·실행 기록에 반영하고, 이번 포션 수정에도 적용한다.
- 사용자 추가 지침: 후속 단계에서도 계속 검증할 수 있도록 미러 테스트 씬·빌더 씬 목록·승인된 Build Settings 씬 등록은 정식 전환 완료까지 유지한다. 이번 빌드는 기존 `MirrorLanTestBuilder.TestScenes`를 BuildPipeline 인자로 사용했으며 EditorBuildSettings는 바꾸지 않았다. 복원 대상인 자동 생성 iOS 필드·URP 내부 목록과 씬 등록은 구분한다.
### 5.1 변경 내용과 유지한 경계

- `PotionUseState` 한 곳에 장착 포션 판정·효과·충전·고정 재사용 시간을 모았다. 싱글과 미러의 기존 컴포넌트·직렬화 필드·공개 API는 유지하고 플레이어별 상태를 갖는다. 회복과 버프는 해당 플레이어의 Health/Buff에 적용한다. 사망·없는 효과 대상은 충전을 소비하지 않는다.
- 미러에 원본과 같은 기본 1초 재사용 제한을 적용했다. 서버 Command는 부재·사망·조작 불가를 검사하고, 성공한 효과와 충전량만 기존 RuntimeStateSync로 전파한다.
- 구매 가격은 `ShopPricing`의 명시적 할인율 오버로드를 함께 사용한다. 서버는 검증된 참가자 할인율을 전달한다. 판매 가격, 할인 상한과 기존 올림 계산은 유지한다.
- 강화 실행도 기존 `UpgradeService`를 재사용하고 서버 Wallet 이벤트를 골드 복제에 연결했다. 요청 번호·버전·소유권 검사, 아이템 스냅샷 반영, 실패 시 이전 레벨·골드·스냅샷 복구는 유지한다.
- 이동·장착·해제·드롭·획득은 이미 공통 Inventory/Equipment 규칙을 사용하므로 새 계층을 만들지 않았다. 상점의 서버 SyncList 거래와 싱글 ShopStockService는 상태 소유 방식이 달라 각자의 원자적 이동·복구를 유지한다.
- WJ 주석은 공통화한 설명까지 적합한 위치에 보존했다. 제거된 메서드명을 가리키던 가격 주석만 현재 공통 API명으로 바꿨다.

### 5.2 이번 실행 검증

- Unity C# 재컴파일 오류 0. 유지할 작은 검사기 `SW/Mirror Test/Validate Stage2 Rules`에서 포션 회복·충전·쿨타임·사망·실패·소유 분리, 할인 범위·가격 올림, 강화 결제 성공/실패 PASS.
- 실제 정식 `Act1_Stage1`의 스포너가 생성한 싱글 Fighter에서 Context 검사와 기존 UI 드래그·회전·장착/해제·최대체력 반영 PASS. 실제 장착 회복 포션의 회복·3→2 충전·즉시 재사용 거절·1초 경과 후 2→1 사용 PASS. 임시 Play 시작 씬은 원래 null로 돌렸고 LoginScene으로 복귀했다.
- 같은 싱글 실제 Fighter의 Wallet·Inventory를 사용해 ShopTradeService 판매/구매/잔액 부족 보존, UpgradeService 강화, 실제 WorldItemDropService 드롭과 PickupInteractor 획득·반복 획득 거절 PASS. 아이템 ID를 유지하고 1개만 소유함을 확인했다. 거래 재고 Grid와 아이템은 임시 fixture이며 마우스 클릭으로 캠프 NPC를 여는 검사는 아니다. 정상 버프 포션의 소유자 Buff 목록 적용도 공통 규칙 검사에서 PASS.
- 최신 Development Windows Player의 Host+별도 Client 2명, 전용 서버+Fighter/Gunner 혼합 Client 4명에서 각각 서버·모든 클라이언트 최종 PASS. 실제 Command로 이동·장비 교환·해제·강화·다른 소유자 요청 거절·드롭/획득·잔액 부족 거절·포션 연속 사용/만료/조작 차단·동시 구매 단일 성공·판매를 실행했다. 각 응답에서 서버와 요청자 아이템 수·골드·충전량이 일치했다. 캠프 진입과 시험 아이템 지급은 검사 보조이며 전체 게임 진행 검증이 아니다.
- 첫 네트워크 실행에서 StageSelect/Camp 전환 구간의 기존 `RestoreClientSceneState`가 로컬 Context를 120프레임 내 찾지 못했다는 오류가 관찰됐다(Host 실행 원격 1개, 전용 서버 실행 Client 4개). 이후 소유자 연결과 거래 검사는 통과했다. 해당 씬 수명주기 코드는 이번에 수정하지 않았으며 무오류 실행으로 보고하지 않는다.
- 첫 빌드 결과는 `Succeeded`, 657.32초다. BuildReport 오류 16건은 수정하지 않은 `Boss_Act_01_Up.shader`/`Boss_Act_01_Leg.shader`의 필드·uv0 셰이더 오류다. C# 컴파일 성공과 구분한다.
- 재접속 검사 첫 시도는 로비 복귀 시 교체된 NetworkManager의 이전 참조를 검사 도구가 사용해 중단됐다. 검사 도구에서 로비 로드 완료 후 현재 singleton을 다시 받아 기존 재접속 자격을 보내도록 수정했다. 최종 검사 빌드는 `Succeeded`, 107.34초, BuildReport 오류 0이다. 증분 빌드에서 셰이더가 다시 컴파일되지 않았으므로 첫 빌드의 기존 셰이더 문제를 해결했다는 뜻은 아니다.
- 최종 Host+Client 및 전용 서버+4 Client에서 중복 강화 요청·오래된 버전 요청 거절과 골드/레벨 불변 PASS. Host 원격 1명 및 전용 서버 4명 모두 끊김→로비 복귀→기존 자격 재접속 후 같은 참가자, 같은 서버 플레이어, 모든 아이템 스냅샷·골드·포션 충전량 보존 PASS. 서버와 모든 Client의 최종 PASS를 각각 수집했다.
- 최종 검사 도구는 60fps로 실행했다. 이 실행에서는 앞선 로컬 Context 연결 오류가 재현되지 않았지만 프레임 기반 대기 원인을 고쳤다고 판단하지 않는다. 전용 서버 최종 실행의 Client 1·4 시작 시 공용 `settings.json`에 Sharing violation이 각각 관찰됐다. 해당 공용 설정 저장 경로는 이번 변경 대상이 아니며 거래·재접속 검사는 모두 완료됐다.
- 지침에 따라 일회성 네트워크 검사·메시지·등록 분기는 `MirrorSmokeConfiguration_MirrorTest.cs`에서 제거했다. 최종 C# 재컴파일 오류 0, 공통 규칙·실제 Fighter/Gunner 및 미러 4프리팹의 소유 분리·인벤토리 참조 검사 PASS. 변경된 Scene/Prefab은 없고 관련 자산 검사에서 Missing Script 없음.
- 검증 과정에서 시작 전에도 있던 MPPM ScenarioConfig Assertion 계열 오류, Play 전환 때의 같은 패키지 NullReference, Stage1 직접 시작에 따른 진행 노드 없음, 빌드 중 CLI 5초 응답 제한 오류를 관찰했다. 마지막 재컴파일·도메인 리로드와 규칙/자산 검사 후 Console 오류 조회는 0건이지만, 앞선 실행에 오류가 없었거나 기존 문제를 해결했다는 뜻은 아니다.
- 증거는 Git 제외 경로 `RunValidation/MirrorStage2_20260923/`에 보관한다. 다중 PC LAN/WAN, 인위적 지연·패킷 손실, 전체 전투는 이번 검사에 포함하지 않는다.

### 5.3 정리와 다음 범위

- 시작 전 저장 파일 9개와 빌드가 생성한 ProjectSettings의 iOS 필드·URP 내부 목록을 백업과 바이트 단위로 동일하게 복원했다. Addressables `link.xml`/`.meta`는 시작 당시의 삭제 상태를 유지한다. Scene·Prefab·EditorBuildSettings는 변경하지 않았고 미러 테스트 씬과 기존 빌더 목록은 유지했다. 원래 LoginScene은 clean, Prefab Stage 없음, Play 시작 씬은 null이다. 이번 검증 Player 프로세스는 모두 종료했다.
- 주석 보존과 미러 검증 씬 유지 방침을 AGENTS 및 전환 계획에 반영했다. 기존 `.codex/agents/unity-scout.toml`과 AGENTS의 에이전트 설정 변경은 보존했다.
- 개인 구현 로그 `ImplementationLogs/김성우.md`의 Git 구현 이력과 로그 이관 이력에 구현·검증·한계를 추가했다. Commit·Push 없음.
- 다음 작업은 3단계 기본 전투·상태이상 공통화다. 이번 완료에 오프라인 Gunner 별도 Play, 캠프 NPC의 마우스 클릭 경로, 강제 예외 주입을 통한 모든 복구 분기, 다중 PC LAN/WAN을 포함하지 않는다. 로컬 Context 재연결의 120프레임 제한과 같은 PC의 설정 파일 경합은 후속 세션 검증 시 확인할 항목으로 남긴다.

## 6. 후속 단계 기록 형식

각 단계에 다음 항목을 같은 문서의 새 절로 추가한다.

- 날짜 / 단계 / 상태 / 이번 실행 범위
- 수정 대상과 승인: 팀원 파일, 변경 이유, 승인 여부
- 변경 내용: 실제 수정한 동작과 파일
- 싱글 유지 방식 / 멀티 동작
- 검증: 실행 환경, 호출 경로, PASS·FAIL, 증거 경로
- 남은 위험·미검증 / 다음 작업
- 개인 구현 로그 갱신 여부

## 7. 실행 이력

| 날짜 | 단계 | 수행 | 검증·제한 | 다음 작업 |
|---|---|---|---|---|
| 2026-09-20 | 0 | 계획 원문·누적 실행 기록 문서 생성, 기준선 조사 착수 | 게임 자산 변경 없음; 현재 런타임 재검증 결과 아님 | 기준선과 근거 기록 |
| 2026-09-20 | 0 | Sol medium 2개 읽기 조사 + 주 에이전트 소스·Editor 자산 대조; 소유자/호출/차이 B01~B12 기록 | 프리팹4개 Missing0, 미러 Context2개 소유 참조 정상, 현재 Console 오류0. Play는 재실행하지 않음 | 1단계 Context와 싱글 InventoryCommon 소유 연결 |
| 2026-09-20 | 1 | 공통 Context·싱글 인벤토리 명시적 연결·미러 필수 구성 검사 구현. 승인된 YJ 스포너 수정. 사용자 요청으로 이전 빌드 약 21.97GB 정리 | 싱글 실제 UI/장착, 별도 서버+4 Player 인벤토리/소유 분리, Q1 서버·각 client 107단계 PASS. 기존 보스 셰이더 오류 10건·다중 PC/전체 전투 미검증은 별도 | 2단계 공통 게임 규칙 |
| 2026-09-20 | 1 후속 | Start→거너 싱글 진입·사망 복귀, 완료 보조로 Act1~3 최종 보스까지 경로 추적 | Context·인벤토리 소유 검사 35회 통과. Act3 보스 미완은 사용자 확인, 결과 씬 연결은 별도 확인 항목. 전체 전투 클리어 검증 아님. 저장·Editor 설정 복원 | 2단계 공통 게임 규칙 |
| 2026-09-23 | 2 | 승인된 WJ 포션 공통화, 서버 1초 제한·소유 검증, 가격·강화 서비스 공유와 지갑 동기화. 주석 보존·검증 씬 유지 지침 반영 | 실제 싱글 규칙/UI/거래/드롭·획득, Host+Client 및 서버+4 Client 동시 구매/중복·버전 거절/재접속 보존 PASS. 셰이더·MPPM·씬 연결·설정 파일 경합 및 다중 PC 미검증은 §5에 구분 | 3단계 기본 전투·상태이상 |
