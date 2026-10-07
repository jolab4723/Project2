# Mirror 네트워크 통합 및 검증 인계 문서 — 2026-09-11

이 문서는 2026년 9월 11일 진행된 **미러(Mirror) 네트워크 테스트 환경 개선, 공격/스킬 입력 불발 및 동기화 결함 해결, 다회차 런 진행도 오염 버그 수정, 그리고 Unity CLI 및 MPPM(Multiplayer Play Mode)을 이용한 1인/2인 풀 런(1~11층 보스 클리어) 실기 검증 결과**를 다른 개발자나 팀원에게 인계하기 위해 작성되었습니다.

---

## 1. 작업 개요 및 환경

- **작업 브랜치**: `codex/unity-6000-3-22-test`
- **Unity 버전**: Unity 6000.3.22f1 (URP, Mirror Networking, Unity Multiplayer Play Mode 1.1.1)
- **주요 목적**:
  1. 미러 테스트 플레이 시 적 공격에 플레이어가 즉사하지 않도록 최고 체력 방어구 및 클래스별 최고 공격력 무기 기본 자동 지급.
  2. 공격/스킬 입력이 서버에서 무시되거나 나가지 않던 네트워크 시퀀스 동기화 결함 분석 및 해결.
  3. 스킬 마나 소모 미동작 및 스킬 UI 아이콘 증발 문제 원인 분석 및 데이터/코드 방어 처리.
  4. 보스 클리어 후 로비로 돌아와 새 런을 시작할 때 이전 런의 진행도가 남아있던 다회차 진행도 오염 버그 해결.
  5. Unity CLI 및 MPPM 2인 가상 플레이어 연동 환경을 구성하여 1층부터 11층 보스(SpiderX) 격파 및 로비 복귀까지 전 과정 실기 검증 완료.

---

## 2. 변경 및 추가 파일 전체 목록

### 2-1. 게임플레이 및 네트워크 권한 스크립트
- `Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerInventorySync_MirrorTest.cs`
  - 테스트용 최고 체력 아이템(우주 괴물 두개골, HP +230) 및 클래스별 최고 공격력 무기(파이터: 데브리스 심장 도끼 ATK 75, 거너: 용암 파쇄포 ATK 78) 서버 스폰 시 자동 지급 및 장착 로직 추가.
  - 상단 및 메서드에 `// 임시 지급용:` 주석을 명시하여 향후 롤백/제거가 용이하도록 구성.
- `Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerCombatAuthority_MirrorTest.cs`
  - 공격 요청 시퀀스 식별자인 `lastServerRequestId`를 `[SyncVar]`로 전환.
  - 클라이언트 로컬 공격 요청 번호(`nextLocalRequestId`)가 서버 승인 번호보다 뒤처질 경우 자동 보정하도록 개선하여 공격 불발 현상 해결.
- `Assets/SW/TEST/MirrorPlayerContext/Scripts/FighterSkillAuthority_MirrorTest.cs`
  - 스킬 요청 시퀀스 식별자인 `lastServerRequestId`를 `[SyncVar]`로 전환 및 로컬 요청 시퀀스 보정.
  - 서버 시작/정지 시 모션 락 해제 및 시퀀스 리셋 로직 보강.
- `Assets/SW/TEST/MirrorPlayerContext/Scripts/WBH_PlayerAnimation_MirrorTest.cs`
  - 거너 기본 공격 애니메이션 클립에서 호출하는 `AniEvent_PlayGunnerAttackSfx` 이벤트 수신 핸들러 추가 (사운드 큐 연동).

### 2-2. 스테이지 및 UI 스크립트
- `Assets/SW/TEST/MirrorCombat/Scripts/MirrorStageSelectRouteAdapter_MirrorTest.cs`
  - 새 런 맵을 생성하고 서버 스냅샷을 퍼블리시할 때, `YJ_StageSelectManager`의 내부 캐시(`clearedFloor`, `lastClearedNodeId`, `selectedNode`)를 명시적으로 리셋하여 다회차 런 시 11층 클리어 상태가 오염되던 치명적 버그 해결.
- `Assets/SW/TEST/MirrorCombat/Scripts/MirrorCooldownHud_MirrorTest.cs`
  - 스킬 정의(`SkillDefinitionSO`)의 `icon`이 `null`이 아닐 때만 UI 스프라이트를 덮어쓰도록 방어 코드 추가 (스킬 사용 시 기존 아이콘이 빈 이미지로 날아가는 문제 방지).

### 2-3. 스킬 데이터 에셋 (`SkillDefinitionSO`)
- `Assets/Resources/DataFiles/CharData/SkillData/3. GeneratedAssets/Skill_Fighter_CursorDash.asset`: 마나 소모량 10 설정, 아이콘 스프라이트 연결.
- `Assets/Resources/DataFiles/CharData/SkillData/3. GeneratedAssets/Skill_Fighter_HalfCircleSlash.asset`: 마나 소모량 25 설정, 아이콘 스프라이트 연결.
- `Assets/Resources/DataFiles/CharData/SkillData/3. GeneratedAssets/Skill_Fighter_LineSlam.asset`: 마나 소모량 40 설정, 아이콘 스프라이트 연결.
- `Assets/Resources/DataFiles/CharData/SkillData/3. GeneratedAssets/Skill_GunnerArcBuster.asset`: 마나 소모량 15 설정.
- `Assets/Resources/DataFiles/CharData/SkillData/3. GeneratedAssets/Skill_GunnerBackstepShot.asset`: 마나 소모량 10 설정.
- `Assets/Resources/DataFiles/CharData/SkillData/3. GeneratedAssets/Skill_GunnerBombThrow.asset`: 마나 소모량 40 설정.

### 2-4. 테스트 드라이버 및 에디터 도구
- `Assets/SW/TEST/MirrorPlayerContext/Scripts/MirrorSessionSmokeDriver_MirrorTest.cs`
  - MPPM 가상 플레이어 태그 및 명령줄 인자 기반의 자동 스모크 기능(일반 플레이 모드 시 비활성화 상태).
  - 11층 런 동안 6개 일반 전투 스테이지(`Act1_Stage1`~`Stage6`)가 재순환될 때 중복 예외로 단언 실패가 발생하던 검증 로직 완화.
  - 보스 결과 화면에서 비방장(Client)의 로비 이동 거절 권한 검증 및 방장(Host)의 로비 복귀 버튼 클릭 자동화.
- *(정리 완료)* `MirrorSessionEndToEndTestRunner.cs` & `MirrorMppmRunner_MirrorTest.cs`:
  - 1인 및 2인 자동 풀 런 검증을 위해 임시 사용되었으며, 실기 검증 완료 후 사용자가 직접 수동 테스트할 수 있도록 프로젝트에서 깔끔하게 제거됨.
- `ProjectSettings/EditorBuildSettings.asset`
  - 미러 테스트 씬 목록 빌드 세팅 등록 유지.

---

## 3. 핵심 수정 사항 상세 및 기술 분석

### 3-1. 임시 고체력/고공격력 장비 자동 지급
- **배경**: 미러 테스트 중 적의 공격력이 높아 1~2방에 캐릭터가 사망하여 전투 및 씬 전환 흐름 검증이 끊기는 문제 발생.
- **구현 방식**:
  - `PlayerInventorySync_MirrorTest.cs`의 `OnStartServer()` 콜백에서 플레이어 생성 즉시 두 메서드를 호출:
    1. `ServerGrantDefaultHighHealthItem()`: 최고 체력 헬멧인 `item.armor.helmet.alienskullcrown`(우주 괴물 두개골, HP +230)을 생성하여 인벤토리에 넣고 투구 슬롯(`EquipSlotType.Helmet`)에 장착한 뒤 `Health.RefreshMaxHealth()` 및 `FillHealth()` 호출.
    2. `ServerGrantDefaultHighAttackWeapon()`: 플레이어 이름 및 Equipment의 활성 클래스를 판별하여 파이터에게는 `item.weapon.axe.heartofdebris`(데브리스 심장 도끼, ATK 75), 거너에게는 `item.weapon.shotgun.magmacrusher`(용암 파쇄포, ATK 78)를 지급하고 무기 슬롯(`EquipSlotType.Weapon`)에 장착.
- **롤백 가이드**: 정식 빌드 전환 시 `PlayerInventorySync_MirrorTest.cs`의 `OnStartServer()` 내 두 메서드 호출부와 하단의 `// 임시 지급용:` 주석이 달린 메서드 2개를 제거하면 됩니다.

### 3-2. 공격/스킬 불발 원인 및 시퀀스 ID 동기화 해결
- **문제 원인**:
  - 플레이어가 공격이나 스킬을 사용할 때 클라이언트는 로컬 예측 요청 번호(`nextLocalRequestId`)를 올리며 서버에 `CmdRequestAttack(requestId, ...)` / `CmdRequestSkill(requestId, ...)`을 전송합니다.
  - 기존 코드에서는 `lastServerRequestId`가 단순 비동기화 멤버 변수였기 때문에, 씬 전환이나 플레이어 스폰 직후 클라이언트가 알고 있는 `lastServerRequestId`와 서버의 `lastServerRequestId` 간에 차이가 발생했습니다.
  - 특히 클라이언트의 `nextLocalRequestId`가 0으로 초기화된 반면 서버에는 이전 연결이나 이전 씬의 요청 카운터가 남아있어, 서버의 유효성 검사(`if (requestId <= lastServerRequestId) return;`)에서 모든 공격/스킬 명령이 "과거의 요청"으로 판정되어 무시되던 결함이 있었습니다.
- **해결 조치**:
  - `lastServerRequestId`에 `[SyncVar]` 속성을 부여하여 서버가 확정한 최신 요청 번호를 소유 클라이언트가 항상 수신하도록 수정했습니다.
  - `OnStartLocalPlayer()` 및 공격/스킬 시작 진입점에서:
    ```csharp
    if (nextLocalRequestId <= lastServerRequestId)
        nextLocalRequestId = lastServerRequestId;
    ```
    위와 같이 로컬 카운터를 서버 최신 번호 이상으로 즉시 동기화하여 요청이 거절당하지 않도록 안전장치를 구축했습니다.

### 3-3. 다회차 런 진행도 오염 버그 해결
- **문제 원인**:
  - 11층 보스를 클리어한 뒤 로비로 돌아와서 곧바로 2번째 게임을 시작할 때, `YJ_StageSelectManager`는 씬이 언로드되지 않거나 재사용되면서 내부 필드인 `clearedFloor`가 여전히 `11`을 가리키고 `selectedNode`가 보스 노드로 남아있었습니다.
  - 이 상태에서 `CaptureSaveData()`를 호출하여 서버 스냅샷을 생성하니, 1층을 시작하기도 전에 `snapshot.clearedFloor == 11`, `IsRunCompleted == true`로 전송되어 스테이지 노드 선택이 불가능해지는 치명적 버그가 발생했습니다.
- **해결 조치**:
  - `MirrorStageSelectRouteAdapter_MirrorTest.cs`의 `TrySetMapSeed` 및 `PublishServerRunSnapshotNextFrame`에서 리플렉션을 통해 `YJ_StageSelectManager`의 `clearedFloor = 0`, `lastClearedNodeId = string.Empty`, `selectedNode = null`, `clearedNodeIds.Clear()`, `visitedNodeIds.Clear()`를 명시적으로 초기화하도록 수정했습니다.

---

## 4. 실기 검증 결과 (Verification Report)

> [!NOTE]
> 본 실기 검증에 활용된 자동화 러너 스크립트(`MirrorSessionEndToEndTestRunner`, `MirrorMppmRunner_MirrorTest` 등)는 검증 완료 후 사용자가 직접 수동으로 플레이 및 테스트할 수 있도록 에디터 프로젝트에서 안전하게 제거되었습니다.

### 4-1. 1인 플레이 풀 런 (Single Player Run)
- **방식**: 에디터 자동 러너를 통한 1~11층 풀 E2E 검증
- **결과**: **100% 클리어 성공 (PASS)**
- **진행 내역**:
  - 로비 진입 ➔ Fighter 선택 ➔ 준비 및 게임 시작 ➔ `StageSelect` 진입
  - 1층 ~ 10층 전투/캠프/이벤트 순차 클리어 및 포탈 이동 정상
  - 11층 보스 SpiderX 조우 ➔ 1200 HP 격파 ➔ `IsRunCompleted` 달성 ➔ 처치 골드 보상 정상 수령

### 4-2. MPPM 2인 멀티플레이어 풀 런 (Host Fighter + Client Gunner)
- **방식**: MPPM 연동 자동 러너 + `MirrorSessionSmokeDriver_MirrorTest`
- **인스턴스 구성**:
  - Main Editor (PID: 39728): Host, Fighter (`mirror-smoke-main`)
  - Clone Player 2 (PID: 17180): Client, Gunner (`mirror-smoke-p2-gunner`)
- **결과**: **100% 클리어 성공 (PASS)**
- **주요 검증 포인트**:
  1. **로비 2인 동기화**: 방장과 참가자가 각각 Fighter와 Gunner를 선택하고 준비(Ready) 완료 후 방장 클릭으로 동시 씬 전환 성공.
  2. **노드 투표(Vote)**: `StageSelect_MirrorSessionTest`에서 두 플레이어가 투표하고 서버가 과반/방장 룰로 확정(`ResolveStageVoteIfDue`)하여 전원 동일 씬으로 동시 진입 성공.
  3. **전투/웨이브 동기화**: 일반 적(`working_machine`, `patrol_drone`), 엘리트, 보스(`SpiderX`) 스폰, 피격, 체력 동기화 무결점 동작.
  4. **보스 격파 및 보상**: SpiderX(1200 HP) 격파 즉시 두 플레이어 모두에게 500 골드 보상 지급 확인 (`PASS kill-credit id=enemy.boss.boss.SpiderX amount=500 gold=5535`).
  5. **결과 화면 및 보안**: Client의 임의 로비 복귀 거절 확인 및 Host의 `ReturnToLobbyButton` 클릭으로 2인 모두 `Lobby_MirrorTest` 복귀 완료.

---

## 5. 인계 및 후속 작업 안내 (TODO & Handoff Notes)

### 5-1. 플레이어 조작감 및 마나 소모 (팀원 인계 보류 사항)
- 사용자의 요청에 따라 `USEMANA` 추가 이후의 `T_PlayerController.cs` 수정은 **더 이상 진행하지 않고 팀원에게 인계하기로 결정**되었습니다.
- **참고 사항**:
  - `SkillDefinitionSO`의 `manaCost`와 아이콘은 데이터상 정상 복구되었습니다.
  - 향후 조작감 개선 시 `Assets/SW/TEST/MirrorPlayerContext/Scripts/T_PlayerController.cs`에서 스페이스바 회피(Dodge/Dash) 입력 처리부 및 스킬 시전 시 마나 차감(`context.Mana.TryConsumeMana`) 연동 상태를 확인하시면 됩니다.

### 5-2. 직접 수동 테스트 방법 안내
1. **1인 수동 플레이 테스트**:
   - Unity Editor에서 `Assets/SW/TEST/MirrorPlayerContext/Scenes/Lobby_MirrorTest.unity` 씬을 엽니다.
   - 에디터 상단의 **Play** 버튼을 누릅니다.
   - 로비 UI에서 `Host (Server + Client)` 버튼을 클릭합니다.
   - 캐릭터 선택 UI에서 Fighter 또는 Gunner를 선택하고 `Ready` 버튼을 누른 후, `Start Game` 버튼을 누릅니다.
   - `StageSelect_MirrorSessionTest` 씬으로 전환되면 원하는 1층 노드를 클릭하여 전투 스테이지로 진입하고 수동으로 플레이합니다.
2. **2인 MPPM 멀티플레이어 수동 테스트**:
   - Unity 상단 메뉴 `Multiplayer` ➔ `Play Mode` 창을 엽니다 (또는 `Window` ➔ `Multiplayer Play Mode`).
   - 가상 플레이어(`Player 2`)가 활성화되어 있는지 확인합니다.
   - Main Editor에서 `Lobby_MirrorTest` 씬을 열고 **Play** 버튼을 누릅니다 (Main Editor와 Player 2 창이 함께 실행됨).
   - Main Editor 창에서 `Host (Server + Client)` 버튼을 누르고, Player 2 창에서는 `Client` 버튼을 눌러 접속합니다 (127.0.0.1:7777).
   - 각 창에서 원하는 캐릭터(예: Main은 Fighter, Player 2는 Gunner)를 선택하고 `Ready`를 완료한 뒤, Main Editor(Host)에서 `Start Game`을 누릅니다.
   - 스테이지 선택 화면에서 양쪽 플레이어가 노드를 투표/선택하고 게임을 함께 진행합니다.

### 5-3. 정식 빌드 전환 시 체크리스트
1. **임시 지급 로직 롤백**:
   - `PlayerInventorySync_MirrorTest.cs`에서 `ServerGrantDefaultHighHealthItem()` 및 `ServerGrantDefaultHighAttackWeapon()` 호출 주석 처리.
2. **테스트 드라이버 비활성화**:
   - 로비 씬의 `MirrorSessionSmokeDriver_MirrorTest`는 자동화 테스트용 컴포넌트이므로, 실제 유저 테스트 시 비활성화하거나 빌드에서 제외.
