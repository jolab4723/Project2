# 클라이언트·Linux 전용 서버 빌드 전 리뷰

- 조사일: 2026-09-28
- 기준: `codex/unity-6000-3-22-test`의 현재 미커밋 변경 포함
- 목적: 약 3시간 뒤 빌드를 앞두고 우선 수정·검증할 항목 선정
- 조사: Astra low 서브에이전트 4개가 빌드, 세션, 저장·경제, 전투를 나누어 읽기 전용 조사. 주 에이전트가 주요 결함의 실제 코드를 대조하고 Unity CLI로 현재 Editor와 기존 테스트를 확인했다.
- 코드·씬·프리팹·설정은 수정하지 않았다. 이 보고서만 추가하며 개인 구현 로그는 갱신하지 않는다.
- 정적 코드에서 확인한 결함과 실제 실행으로 재현한 결과를 구분한다. 아래 결함의 원격 장애 주입 재현은 아직 수행하지 않았다.

## 우선 조치

### 1. P1 — 정식 빌더에 Linux 서버 진입점이 없다

`Assets/Editor/MirrorProductionBuilder.cs:38`은 Windows64로 강제 전환하고, `:62`도 Windows64 target을 지정한다. `:30`의 서버 메뉴와 `:51`의 실행파일명도 Windows 전용이다. 현재 메뉴로는 Linux 서버를 만들 수 없다. Unity Build Profiles를 수동으로 사용하는 모든 방법이 불가능하다는 뜻은 아니다.

- 기존 Server subtarget 분리는 유지하고 Linux target·출력 경로·실행파일명을 지원하는 진입점을 추가한다.
- Unity 6000.3.22f1의 Linux Server 모듈은 CLI에서 설치 확인했다. Editor도 Linux target 지원을 반환했다.
- 활성 빌드 씬 34개는 모두 존재한다. 빌더의 `.Where(File.Exists)`는 향후 누락 씬을 조용히 제외하므로 명시적 실패 처리로 바꾸면 좋다. 현재 누락 결함은 아니다.
- 서버는 Addressables 콘텐츠 생성을 생략한다(`:44`). 조사한 적 스폰·전투·파괴 경로에는 Addressables 로드가 없었으므로, 서버 콘텐츠 누락을 현시점의 확정 결함으로 판단하지 않는다. Linux 전환 시 실제 서버 의존성을 확인한다.
- 완료 기준: Linux/Server target의 산출물, Lobby 첫 씬, 오류 없는 실제 Linux 기동, 클라이언트 접속. 빌드 성공만으로 완료하지 않는다.

### 2. P1 — 마지막 미준비 참가자가 이탈하면 전투 시작 재평가가 누락된다

발생 순서: 두 명이 전투씬에 진입 → A만 준비 완료 → B가 준비 완료 메시지 전 연결 종료.

- `Assets/SW/Scripts/Network/Player/MirrorGameplayReadiness.cs:100`: 준비 메시지에서 전원 준비 여부를 검사한다. A의 메시지 시점에는 B 때문에 시작하지 않는다.
- `Assets/SW/Scripts/Network/Player/MirrorNetworkManager.cs:293`: 연결 종료는 준비 집합·명부·플레이어를 정리하고 로비 상태를 전송하지만 전투 시작을 다시 검사하지 않는다.
- `Assets/SW/Scripts/Network/Player/MirrorSessionLifecycle.cs:368` 및 `Assets/SW/Scripts/Network/Combat/NetworkEnemyWaveSpawner.cs:110`: Update에 대기 중인 최초 전투 시작을 복구하는 처리가 없다.

참가자 이탈 처리가 끝난 공통 경계에서 기존 `TryStartCombatWhenPartyReady()`를 재평가하도록 보완하는 범위가 적절하다. 전원 이탈·명시적 Leave·재접속도 함께 확인한다. 실제 원격 재현은 미실시다.

### 3. P1 — 정산 요청과 저장 확인 사이에 같은 골드를 강화에 사용할 수 있다

- `Assets/SW/Scripts/Network/Player/NetworkShopPlayerState.cs:191`: 서버 지갑의 잔액을 정산 금액으로 고정하여 클라이언트에 저장 요청한다.
- `Assets/SW/Scripts/Network/Player/PlayerInventorySync.cs:579`, `:890`, `:1197`: 강화의 서버 진입점은 요청 번호·revision·임시이탈·소유 아이템 등을 검사하지만 정산 중 경제 변경을 막지 않는다.
- `Assets/SW/Scripts/Network/Player/NetworkShopPlayerState.cs:237`: 저장 ACK 뒤 현재 지갑에서 고정 정산액을 차감하고 0으로 보정한다.
- `Assets/SW/Scripts/Network/Player/MirrorNetworkManager.cs:624`: Act 정산이 세우는 전환 플래그도 강화 Command에서 검사하지 않는다.

예를 들어 1,000골드 정산 요청 후 ACK 전에 100골드 강화가 처리되면, 영구 크레딧 +1,000과 강화 효과를 모두 얻고 지갑은 0이 된다. 수정 클라이언트 또는 지연된 유효 Command 순서에서 성립하는 코드 경로이며, 일반 UI 조작으로의 재현 및 원격 장애 주입은 미실시다.

정산 중 경제 변경을 서버의 공통 검증 경계에서 차단하거나 정산액을 별도로 예약해야 한다. 이번 시간 범위에서는 공통 차단을 우선 검토한다. 상점·판매·리롤 등 다른 지갑 변경도 같은 기준으로 확인하되 저장 ACK 자체는 막지 않는다.

### 4. P2 — 퇴장이 거절돼도 원격 클라이언트의 재접속 정보가 삭제된다

`Assets/SW/Scripts/Network/Player/MirrorSessionLifecycle.cs:112`는 재접속 정보를 지운 다음 `:119`에서 Leave를 요청한다. 서버는 같은 파일 `:179`에서 정산 미완료를 이유로 Leave를 거절할 수 있다. Host에는 앞선 로컬 검사가 있지만 원격 Client에는 없다.

이후 연결이 끊기면 서버에 플레이어가 예약 보존돼도 클라이언트가 해당 런의 복귀 정보를 잃는다. 서버가 퇴장을 승인한 시점에 삭제를 확정하고, 거절 또는 단순 연결 유실에는 보존해야 한다. 검증은 저장 ACK 지연 → Leave 거절 → 단절 → 재접속 순서로 수행한다.

## 빌드 후보에서 필요한 실제 검증

오늘의 기존 검증 문서 `Mirror_Integration_Verification_2026-09-28.md:142`는 최신 수정 이후 Editor 단일 Host만 검증했고 원격 Client·전용 서버·새 Player는 미검증이라고 명시한다. 이번 리뷰도 이를 대신하는 새 Player 빌드나 다중 접속 실행을 수행하지 않았다.

1. 같은 코드·자산 후보의 Linux 서버와 서로 다른 계정의 Windows 클라이언트 2개를 사용한다. Fighter와 Gunner를 각각 선택한다.
2. 서버 기동 → 첫 참가자 방장 지정 → 준비 → StageSelect → 전투를 확인한다. 현 설정은 KCP UDP 7777이다. 서버는 모든 인터페이스에 바인드하며 `networkAddress: localhost`는 서버 수신을 localhost로 제한하는 설정이 아니다.
3. 마지막 미준비 참가자 이탈 시 남은 참가자의 전투가 시작되는지 확인한다.
4. Act1 보스 소개의 원격 시간·위치, 인디케이터, 보스 스폰·사망·Act 전환을 확인한다. `MirrorBossIntro.cs:48`의 서버 시작은 Timeline·Animator·Transform·duration에 의존하므로 Linux에서 실제 확인할 가치가 크다.
5. 정산 중 강화 요청 차단, 저장 실패 중 Leave 거절 후 복귀, ACK 중복 전송 시 중복 지급 방지를 확인한다. 테스트 계정을 사용하고 저장 데이터 변화를 기록한다.
6. Act2 최종 결과와 로비 복귀·다음 런 시작을 확인한다. 현재 멀티는 Act2 종료, 싱글은 Act3 종료이며 기존 문서에 명시된 정책 차이다. 이번 리뷰에서 신규 버그로 판정하거나 임의로 통일하지 않는다.

최종 빌드를 3시간 뒤 시작할 경우 실제 Linux 검증은 그 빌드 이후의 별도 완료 조건이다. 가능하면 최종 빌드 전에 테스트 후보를 먼저 만들어 원격 검증 시간을 확보한다.

## 측정 후 판단할 성능·표현 개선

- 전용 서버의 표시용 효과: `NetworkEnemyAuthority.cs:224`가 공용 effect spawner를 주입하고 `WBH_EnemyEffect.cs:93` 및 `WBH_IndicatorSpawner.cs:32`는 원격 알림 뒤 로컬 효과도 실행한다. 해당 경로에 headless 가드가 없다. 실제 서버의 활성 객체 수·CPU·GC를 측정한 뒤 원격 이벤트는 유지하며 표시용 처리를 분리한다. WBH 스크립트를 직접 수정하려면 담당 영역 승인 규칙이 적용된다.
- 파괴 prewarm: `EnemyDestructionService.cs:153`과 `:325`는 서버 구분 없이 사전 준비와 실제 Visual.Play를 수행한다. 사망 재생 자체는 `NetworkEnemyAuthority.cs:1002`의 Client 가드가 있다. 상위 씬에서 서비스가 비활성화되는지와 서버 비용은 미검증이다.
- 동시 사망 품질: `EnemyDestructionService.cs:109`는 준비된 연출이 없으면 요청을 생략하고, `NetworkEnemyAuthority.cs:1058`은 실패해도 본체 Renderer를 숨긴다. 정식 씬 설정에서 일반 적 여러 마리와 보스 사망을 각각 측정한다. 코드 기본 prewarm 4/최대 12가 실제 모든 씬의 설정이라는 뜻은 아니다.
- 보스 소개의 매 프레임 Renderer 조회·본 탐색은 개선 후보지만 실행 시간이 제한되고 비용을 측정하지 않았다. 빌드 직전 리팩터링의 우선순위는 낮다.

성능 수치나 시각 품질을 이번 리뷰에서 측정하지 않았으므로 성능 개선 완료나 특정 프레임 저하를 주장하지 않는다.

## 이번 리뷰에서 직접 확인한 결과

| 확인 | 결과 |
| --- | --- |
| 연결된 Editor | Project2, Unity 6000.3.22f1, ready |
| 시작 상태 | Edit Mode, 컴파일·임포트 진행 없음, Prefab Stage 없음 |
| 열린 씬 | Network Lobby 한 개, dirty=false, 종료 시 동일 |
| 현재 target | Windows64 / Player, 전환하지 않음 |
| Linux Server 모듈 | 설치됨; Linux target 지원 true |
| 활성 빌드 씬 | 34개 경로 모두 존재 |
| 로비 네트워크 등록 | spawnPrefabs 7개 null 없음, 기본 Fighter 포함 8개 검사 |
| 위 8개 프리팹 | Missing Script 0, NetworkIdentity 존재, assetId 모두 nonzero·서로 다름 |
| 프로젝트 Edit Mode 테스트 | Assembly-CSharp-Editor 10/10 통과, 실패·스킵 0 |
| 테스트 내용 | 임시 폴더 기반 로컬 저장·백업 복구·사용자 분리·잘못된 ID·크레딧 경계 |
| Console | 시작 전 MPPM 초기화 예외·기존 진단 RenderTexture 오류 존재. 기준 cursor 9157 이후 새 Error 0 |

이번에는 34개 씬 전체를 다시 열어 Missing Script를 검사하지 않았다. 전체 34씬 Missing Script 0은 오늘의 기존 검증 문서에 기록된 결과다. Linux 컴파일·플레이·원격 지연·실제 성능은 위 결과에 포함하지 않는다.

## 남은 3시간 배분 제안

- 약 30분: Linux 빌더·출력·서버 기동 조건 정리.
- 약 75분: 전투 준비 이탈, 정산 경제 변경, 퇴장 승인과 재접속 정보 처리 보완.
- 약 45분: 해당 경계 회귀 검증. 원격 검증에는 같은 후보의 Player가 필요하다.
- 마지막 30분: 변경 동결, 씬/프리팹·Console·빌드 설정 확인, 배포 및 빌드 후 시험 순서 확정.

시간은 계획 배분이며 구현 완료 예상치를 보증하지 않는다. 지연되면 측정 전 성능 리팩터링·외형 수정·플랫폼 최적화 설정 변경부터 뒤로 미루고, 기능 결함과 실제 원격 검증을 우선한다.

## 후속 구현 및 Editor 검증 (2026-09-28)

사용자가 빌드 경로·세션·정산 보완과 Editor 검증을 승인했다. 위 본문은 수정 전 리뷰이며, 우선 조치 1~4는 아래와 같이 보완했다. 실제 Player/Server 빌드는 실행하지 않았다.

- `MirrorProductionBuilder`: `SW/Mirror/Linux 전용 서버 빌드` 메뉴와 Linux64/Server 옵션, `Builds/Project2ServerLinux/Project2Server.x86_64` 출력 경로를 추가했다. 기존 Windows Player/Server 경로는 유지한다. 없는 활성 씬을 조용히 제외하지 않고 오류로 처리한다. 실제 빌드가 사용하는 `CreateBuildOptions`를 따로 검사하므로 플랫폼 전환이나 빌드 없이 설정을 검증할 수 있다.
- `MirrorNetworkManager`·`MirrorSessionLifecycle`: 연결 종료 및 명시적 Leave 후 기존 전투 준비 검사를 다시 수행한다. 퇴장 승인 응답을 받은 Client만 재접속 정보를 삭제하고 연결을 종료한다. 거절·응답 전 단절에는 정보를 보존한다. 서버는 명부·플레이어를 먼저 정리한 뒤 승인 응답을 보내며, 응답을 무시하는 연결도 기존 인증 거절과 같은 0.25초 지연 방식으로 종료한다. 응답 유실 시 로컬에 만료된 자격이 남을 수 있지만 서버의 포기 처리는 되돌리지 않는다.
- `MirrorSessionFeedback`에 승인 필드가 추가되어 호환 버전을 `2026092801`로 올렸다. 이전 빌드와 새 빌드는 함께 접속시키지 않는다.
- `NetworkShopPlayerState`의 개인 정산 ACK 대기와 Manager의 씬 전환·Act 정산·최종 결과 확정 상태를 하나의 경제 잠금 조건으로 제공한다. 상점 공통 요청 검사와 실제 강화 진입점에서 이 조건을 사용한다. 구매·판매·리롤·강화가 잠금 중 거절되고 저장 ACK는 계속 처리된다.
- 지속적으로 실행할 회귀 스크립트는 `Tools/Validation/MirrorPrebuildChecks.cs`에 두었다. Assets 밖의 Editor `run_script`용이며 Player에 임시 러너·명령을 추가하지 않는다.

### 실행 결과

| 검사 | 결과와 범위 |
| --- | --- |
| C# 컴파일 | 완료, 실행 중 호환 버전 2026092801 확인 |
| 빌드 옵션 | 7항목 통과: Windows 경로 유지, Linux target/Server/출력, Lobby 선두, 씬 존재, 개발 옵션, 지원하지 않는 조합 거절, target 변경 없음 |
| Editor Play 회귀 | 38개 assert 통과. 전용 서버 모드에서 실제 Fighter/Gunner 프리팹으로 로비→StageSelect 투표→실제 전투씬 진행. A 준비/B 미준비→B 단절 뒤 Waiting→Playing 및 실제 적 생성 확인 |
| 경제 처리 | 실제 아이템과 지갑으로 정상 강화 결제, 4종 잠금별 구매·판매·리롤·강화 거절, 잔액/강화 단계 불변, 잠금 해제 후 강화 재개 확인 |
| 정산·퇴장 | 서버의 정산 대기·Leave 거절, 잘못된 ACK 무시, 일치 ACK 차감, 중복 ACK가 새 골드를 차감하지 않음, 승인 응답 직렬화와 연결 정리 확인. Client 콜백은 승인/거절·무응답 시 로컬 자격 파일 동작 검사 |
| 기존 Edit Mode 테스트 | 프로젝트 테스트 10/10 통과, 실패·스킵 0 |
| 저장 보존 | 원본 JSON/BAK 56개 SHA256 동일, 추가 파일 0. 임시 재접속 파일은 try/finally로 바이트 복원. 실제 클라우드 클라이언트를 연결하지 않아 보상 저장 RPC의 클라우드 실행은 하지 않음 |
| Editor 종료 상태 | Play 종료, 원래 Network Lobby, dirty=false, Windows64/Player 유지 |

Play 연결은 실제 원격 프로세스가 아닌 모의 `NetworkConnectionToClient`다. 실제 게임 프리팹·씬·서버 핸들러를 실행하고 직렬화된 퇴장 응답을 확인했지만, 실제 TCP/UDP 전달·원격 지연·다중 PC·Linux 실행을 검증한 것은 아니다. 정산 ACK는 외부 검사 스크립트에서 해당 Command의 서버 구현을 호출했으며 Firebase 저장 성공을 새로 검증한 결과가 아니다. Console에는 기존 MPPM ScenarioConfig 초기화 오류가 반복됐고 검사 대상 게임 코드의 새 오류는 관찰되지 않았다.

재실행 명령(프로젝트 루트에서 실행):

```powershell
rtk proxy unity command run_script --caller plugin --skill unity-cli --file Tools/Validation/MirrorPrebuildChecks.cs --entry MirrorPrebuildChecks.BuildOptions
# Network Lobby에서 Play 시작, 아직 세션을 시작하지 않은 상태에서:
rtk proxy unity command run_script --caller plugin --skill unity-cli --file Tools/Validation/MirrorPrebuildChecks.cs --entry MirrorPrebuildChecks.Start
```

증거: `RunValidation/PrebuildFix_20260928/play-checks.json`, `save-integrity.json` 및 로컬 백업(Git 제외). 테스트 후 Play를 종료한다. 스크립트 첫 컴파일에서 검사 메서드와 Unity BuildOptions 타입의 이름 충돌이 있었으며 완전한 타입명으로 수정한 뒤 실행을 통과했다. 운영 스크립트 컴파일 오류는 발생하지 않았다.
