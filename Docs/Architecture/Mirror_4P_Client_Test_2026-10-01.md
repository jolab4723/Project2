# Mirror 4인 클라이언트 테스트 기록 (2026-10-01)

## 1. 환경

- 빌드: `MirrorProductionBuilder.Build(false, true)` Windows 개발 Player. 개발 빌드에서만 Pipeline 런타임을 켰고, 빌드 후 `enableInBuilds=false`로 되돌렸다.
- 실행: 한 PC에서 `Builds/MP4/P1~P4`(하드링크 복제) 4개를 1280x720 창 모드로 실행. 인스턴스마다 `--test-login p1~p4`, `--mirror-profile p1~p4`, `-logFile`을 따로 지정했다.
- 구성: P1 Host(Fighter, A1 `item.weapon.axe.phaseharvester`), P2 Client(Gunner, A2 `item.weapon.shotgun.starforgebreach`), P3 Client(Fighter, A3 `item.weapon.greatsword.wasteheatcleaver`), P4 Client(Gunner, A2).
- 조작: 각 Player의 Pipeline `eval`로 실제 진입점(로비 브리지, `RequestStageNodeSelection`, `TryRequestEquipmentChange`, `TryBeginLocalAttack`, `MoveCommand`)을 호출했다. 무기 지급은 Host의 `ServerGrantQuestReward`를 사용했다.
- 테스트용 조치: 준비 중 전멸을 막기 위해 Host 서버에서 4명에게 `ApplyInvincibility(1800)`을 걸었고, 준비하는 동안 Host `timeScale=0`을 사용했다. 운영 코드·데이터는 바꾸지 않았다.

## 2. 확인된 동작

| 항목 | 결과 |
| --- | --- |
| `--test-login` 로컬 테스트 계정 | 4개 인스턴스가 서로 다른 `local-test-p1~p4`로 로그인했고 튕김이 없었다. 저장은 `PlayerSaves/local-test-*`의 로컬 캐시에만 남는다. |
| 로비~진행 | 로비 → Ready → StageSelect → 전투 6회(일반·엘리트) → 포털 → 캠프 2회를 4명이 함께 진행했다. |
| 장착·외형 | 각 Client의 실제 장착 요청으로 A1~A3를 장착했고, 4개 화면 모두 무기 외형이 일치했다. |
| A1·A2 | 실제 전투에서 발동했다. 서버가 복제한 쿨다운 기록(`phase-harvester`, Gunner 2명 각각 `star-breacher`)이 4개 Client에서 같게 보였다. |
| A3 | P3의 열이 0→4로 충전되고 준비·발밑 오라가 켜진 뒤 방출하는 주기가 반복됐다. 열 값·준비 상태·오라 표시가 4개 Client에서 같았다. 방출 부채꼴 VFX(1.3초)는 샘플 간격 때문에 화면으로는 잡지 못했다. |
| 엘리트 HP바(R05) | 4개 Client 모두 엘리트를 연결·표시했고, 처치 뒤 정상적으로 닫혔다. |
| 카메라 추적 대상 | 4개 Client 모두 자기 로컬 캐릭터를 따라갔다(캠프 진입 직후 뷰포트 0.50, 0.38). |

## 3. 발견한 문제

| # | 문제 | 원인(확인 수준) | 담당·수정 위치 후보 |
| --- | --- | --- | --- |
| 1 | 4인 캠프 진입 시 4번째 참가자만 "플레이를 준비하고 있습니다..." 무한로딩 | **확인.** `Act1_Camp`의 `Network Start Position 4`가 `(-10, 0, 9)`이고 반경 4m 안에 NavMesh가 없다. 슬롯 3 참가자의 NavMesh 연결이 실패해 준비 코루틴이 60초 뒤 시간 초과로 끝나고 다시 시도하지 않는다. 테스트에서 P4를 `(-10, 0.1, 5)`로 옮기고 준비 요청을 다시 보내면 정상 진행됐다. 두 번째 캠프에서도 같은 증상이 재현됐다. 나머지 시작점 3개도 NavMesh에서 2m 떨어져 있어 4m 보정에 의존한다. | YJ 씬 `Act1_Camp` 시작점 이동 + SW 검사 도구에 "관리 씬 시작점이 NavMesh 위인지" 확인 추가 |
| 2 | 캠프 NPC·기능에 마우스를 올려도 툴팁이 위치에 뜨지 않고 화면 가운데에 "Text"로 고정 | **확인.** 공유 씬에 싱글용·멀티용 `NameTag1`이 하나씩 있다. NPC 4명의 `YJ_OutlineOnMouseHover.nameTag`는 싱글 UI 루트(멀티에서 꺼짐)의 `NameTag1`을 가리켜, `Awake`가 실행되지 않은 채 `YJ_NameTag.ChangeText`에서 NullReference가 난다. 멀티용 `Multiplayer/.../Canvas/NameTag1`은 켜진 채 아무도 갱신하지 않아 `(0, 0)`에 기본 문구로 남는다. 10/1 미러 통합에서 UI를 모드별로 나눌 때 이 참조가 빠졌다. | YJ `YJ_OutlineOnMouseHover`/`YJ_NameTag` 또는 씬 연결, SW `MirrorSceneMode` |
| 3 | 캠프에서 캐릭터가 앞쪽(z≤5)이나 오른쪽(x>-6)으로 가면 카메라가 따라가지 못해 자기 캐릭터가 화면 밖으로 나감 | **확인.** `CampCinemachine`의 `CinemachineConfiner3D` 범위(`CameraBounds`)가 x −14~−6, z −7~23이다. 카메라는 캐릭터보다 z로 12 뒤에 서므로 이동 가능 영역보다 좁다. 같은 씬·카메라라 싱글에서도 재현될 가능성이 높다. `YJ_SearchTrackingTarget`(첫 `Player` 태그 검색)도 같이 붙어 있으나 이번 실행에서는 바인더가 이겼다. | YJ 씬 `Act1_Camp` 카메라 범위 |
| 4 | 캠프에서 화면 좌우로 움직일 때 하단 그림자와 상점 캐노피가 검게 깨졌다 돌아옴 | **확인.** `CameraOcclusionFader`가 카메라→캐릭터 시선을 가리는 캐노피를 `Project2/P2_Unlit` 템플릿 머티리얼로 바꾸는데, 이 셰이더에는 ShadowCaster 패스가 없다. 그래서 페이드되는 동안 캐노피가 조명 없이 어둡게 보이고 바닥의 큰 그림자도 사라진다. 카메라가 3번의 범위 경계에서 밀리는 순간(z −6.84~−6.90)에만 시선이 캐노피를 지나 순간적인 깜빡임으로 보인다. P4에서 페이드만 끄자 캐노피 어두워짐(밝기 57→36)과 하단 그림자 소실(123→141~148)이 사라졌다. 캐릭터 조명 그림자·오클루전 컬링은 각각 꺼도 재현돼 원인이 아니다. | YJ `Assets/Scripts/Camera/CameraOcclusionFader.cs`와 페이드 템플릿 머티리얼(그림자 유지), 3번 카메라 범위와 함께 검토 |
| 5 | 채팅 보낸 사람이 닉네임이 아니라 "플레이어 1"처럼 번호로 표시 | **확인.** `Assets/SW/Scripts/Chat/ChatSession.cs:191`이 `$"플레이어 {message.PlayerNumber}"`로 표시한다. | SW `ChatSession` |
| 6 | 한 PC 다중 실행 시 로비 화면이 접속 창에 멈춤(P4) | **확인.** `MirrorLobbyBridge.Awake` → `SettingManager.Activate` → `Load` → `Apply` → `Save`가 매번 `settings.json`을 다시 써서, 동시 진입 시 `IOException: Sharing violation`이 나고 로비 브리지 초기화가 중단된다. 실제 다른 PC 환경에서는 충돌하지 않는다. 수정은 보류(사용자 결정). | BH `SettingManager` |
| 7 | 전투 씬 로드 직후 `Failed to create agent because there is no valid NavMesh` 111회 | 진행에는 영향이 없었다. 적 생성 직후 Agent 초기화 순서로 추정한다. | 미조사 |
| 8 | Client가 StageSelect 진입 때 `Scene 전환 뒤 로컬 PlayerContext를 다시 연결하지 못했습니다` 오류 | 캐릭터가 없는 StageSelect에서 120프레임 대기 후 남기는 로그다. 전투 씬에서는 정상 연결됐다. | SW `MirrorNetworkManager.OnClientSceneChanged` 대상 씬 조건 |

## 4. 테스트 중 확인한 조작 주의

- 전투 포털은 플레이어가 포털 영역 위에 서 있으면 열리지 않는다(`YJ_PortalActive`). 자동 진행에서는 포털에서 떨어진 뒤 열림을 확인하고 진입해야 한다.
- 캠프 포털은 4명이 모두 도착해야 열린다.

## 5. 수정 후 재검증 (2026-10-01)

기존 작업 트리에 반영되어 있던 수정도 포함해 같은 8개 항목을 재검증했다. 추가로 원본 적 풀의 불필요한 선생성, 원본 Lit 재질을 잃는 페이드 경로, Act3 캠프 카메라 범위를 보완했다.

| # | 반영 내용 | 재검증 결과 |
| --- | --- | --- |
| 1 | Act1 네 시작점을 NavMesh 위로 이동하고 캠프 시작점 검사 추가 | Act1·2의 시작점 8개 검사 통과. 실제 4인 Act1·2 모두 `IsLocalGameplayReady=true`, 이동 가능, NavMesh 연결. P4 Act1 시작 위치는 `(-6, 0.14, 5)`. |
| 2 | 활성 모드의 NameTag·카메라를 다시 찾고, 비활성 텍스트도 안전하게 초기화 | Act1·2 각각 4개 Player에서 상점·휴식·강화·의뢰 이름표가 올바른 문구·활성 카메라·NPC별 위치로 표시됨. Act3 싱글도 4종 통과. |
| 3 | Act1 카메라 범위 보완, Act3 범위를 전체 NavMesh와 FollowOffset에 맞춤 | Act1 4인 이동 샘플 433회, Act2 451회에서 화면 이탈 0회. 각 Player의 경계 이동 목표 5개 모두 도달. Act3 싱글 이동 273샘플 이탈 0회, 전체 삼각형 중심 투영 373개 이탈 0개. Act2는 기존 범위로 통과하여 씬 변경 없음. |
| 4 | 원본 재질 복사·단일 슬롯 그림자 보존, URP/Lit 및 Synty/Generic_Basic의 조명 속성을 유지하는 투명화 | Act1 Editor Play에서 두 캐노피의 가림을 재현해 동일 구도 전후 캡처 확인. 알파 0.15에서도 원본 셰이더·텍스처와 바닥 그림자 유지. 가림 해제 뒤 임시 재질 0개, 원본 에셋/슬롯 1개로 복구. Synty 보완은 아래 Player 빌드 이후 적용했으며 재빌드하지 않음. |
| 5 | 서버 참가 명부의 닉네임을 채팅 전달에 포함 | 4명 모두 송신하고 모든 화면에 `검증-p1`~`검증-p4`가 동일하게 표시됨. |
| 6 | 로컬 테스트 계정별 설정 파일 분리 | 4개 계정이 서로 다른 `settings_local-test-closeout-pN.json`을 사용. 로비 초기화·재접속 정상, Sharing violation 0건. |
| 7 | `WBH_EnemyPoolManager.Awake`의 적 선생성을 실제 첫 `Get`으로 지연 | 기존 로그의 스택은 `MirrorSceneMode` 활성화 → `Awake` → `CreatePools` → `Instantiate`였다. 멀티에서는 쓰지 않는 원본 풀이 111개 Agent를 만들던 것이 원인. 새 4인 전투 진입 로그 모두 `Failed to create agent` 0건(기존 인스턴스당 씬 진입 111건). |
| 8 | StageSelect에서 불필요한 로컬 전투 Context 재연결 오류를 남기지 않도록 조건 정리 | 4명 모두 StageSelect 준비 완료. 반복 로비→StageSelect 진입을 포함해 해당 오류 0건. |

### 검증 방법과 범위

- Windows 개발 Player 4개는 `--test-login closeout-p1`~`closeout-p4`로 실행했다. 별도 싱글 Player 1개는 Act3 캠프 확인에만 사용했다. 운영 계정은 사용하지 않았다.
- 로비 참가·Ready·StageSelect·첫 전투는 실제 요청 API를 사용했다. 캠프 검증은 전체 Act 진행을 생략하는 **외부 검증 스크립트의 서버 씬 전환**과 기존 `PlaceServerPlayersAtSceneStarts`/`ServerConfirmSceneStart`/준비 응답 경계를 사용했다. 전체 런 완주 검증으로 보지 않는다.
- Act3는 현재 씬 구성에 Mirror 모드/4인 시작점이 없어 싱글로 검사했다. 도달 가능한 경계 5개 중 4개는 거리 기준으로 도달했고, 나머지 1개는 목적지와 x/z가 같지만 NavMesh 삼각형 중심과 실제 지면의 y 차이(0.81m) 때문에 거리 판정에서 실패했다. 화면 이탈은 없었다.
- 캠프 3씬의 Missing Script는 0개였다. 시작점 검사: `Tools/Validation/SceneStartNavMeshCheck.cs`의 `RunCamps`.
- 빌드는 성공했으며 C# 컴파일·Mirror Weaver는 통과했다. 다만 기존 BH 보스 `Boss_Act_01_Up.shader`·`Boss_Act_01_Leg.shader`의 `uv0` 관련 셰이더 오류 10건이 빌드 보고서에 남았다. 이를 무오류 빌드로 표현하지 않는다.
- 외부 검증 중 잘못된 씬 경로 1회, 직접 씬 진입의 진행 노드 누락, Pipeline 요청 시간 초과가 발생했다. 설정을 바로잡아 다시 검사한 결과와 구분하며, 해당 로그를 제품 오류가 없다는 근거로 숨기지 않는다.
- 사용자 요청에 따라 캐노피 후속 보완은 Editor에서 검증하고 추가 Player 빌드는 하지 않는다.
- 캐노피 검증에서는 실제 Fighter를 가림 재현 위치로 옮기고 Cinemachine을 잠시 멈춰 카메라 구도를 고정했다. 검증 후 Play를 종료했으며 씬에 저장하지 않았다. `editor-canopy-before/after.png`, `editor-synty-before/after.png`, `editor-restore.json`에 결과를 보관했다. 현재 그림자 보존은 기존 단일 슬롯 Renderer 범위이며, 다중 서브메시 전체 투명화·그림자 지원을 추가한 것은 아니다.
- 사용자가 표시한 상점 오른쪽 기둥·벽 구간을 추가로 검증했다. 정상 Cinemachine 추적과 실제 Fighter `MoveCommand`로 상점 앞 → 오른쪽 끝 → 상점 앞의 5개 지점을 왕복했다. 58샘플에서 `SM_Prop_Canopy_Preset_01`, `SM_Barrier_03`, `SM_Barrier_02 (1)`의 페이드를 관측했고, 원본 셰이더 유지 및 그림자 패스 소실 0건을 확인했다. 해당 위치의 Game 화면도 확인했다(`corridor/result.json`, `corridor/occlusion-*.png`).
- 재검증 자료: `RunValidation/MirrorBugCloseout_20261001/`의 각 Player 로그, `act1-final.json`, `act2-final.json`, `act1-hover.json`, `act2-hover.json`, `chat-received.json`, `Act*_Camp-walk.json`, 화면 PNG 및 빌드 오류 기록.
