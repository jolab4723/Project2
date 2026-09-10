# Mirror 작업 중단·계정 변경 인계 — 2026-09-10

사용자가 사용량 제한으로 작업 중단과 문서 갱신을 요청했다. **추가 구현·검증은 중단했으며, 전체 완료가 아니다.** 새 계정에서는 이 문서를 먼저 읽고 현재 작업 트리에서 이어간다. 9월 9일 문서는 이전 구현 배경이며, 현재 진행 상태는 이 문서와 [9월 10일 검증 기록](Mirror_Validation_2026-09-10.md)이 우선한다.

## 작업 위치와 유지할 조건

- 작업 폴더: `C:\Users\user\Desktop\Project2_test\Project2`
- 브랜치: `codex/unity-6000-3-22-test`, Unity 6000.3.22f1. Commit·Push하지 않았다. 미커밋 변경과 신규 파일을 보존한다.
- 사용자 요청: 전날 남은 Mirror 통합·실제 검증 계속, WJ의 새 패시브 화면 연결, 팀원 설명 PDF, 사용하지 않는 이전 빌드 정리.
- **서브에이전트 사용 금지**, 주 에이전트가 직접 진행. 이번 이어지는 작업은 사용자가 지정한 Ponytail ultra 기준이다.
- **WJ 원본 씬 수정 금지.** 새 패시브 화면은 원본을 읽어 SW Mirror 로비에만 복사했다.
- 원본 `DataManager.cs`, `SettingManager.cs` 변경은 보류. 원본 `WBH_PlayerEffect.cs`의 월드 폭발 시각 전달도 보류. 기존 사운드와 컴포넌트를 보존한다.
- 이미 승인받아 변경한 KY 패시브 팝업·슬롯은 현재 작업 트리에 있다. 추가로 다른 담당자 스크립트를 수정해야 하면 AGENTS.md의 파일별 확인 규칙을 따른다.
- 다음 Act에서는 런 상태·미수령 보상을 유지하고 마지막 Act에서만 아이템을 정리하며 크레딧·패시브를 가지고 나온다는 요구를 유지한다. 영구 크레딧 정산·저장 실패 복구·중복 방지의 정식 저장 연결은 아직 구현하지 않았다.

## 완료된 작업과 실제 근거

| 작업 | 검증 결과 |
| --- | --- |
| 새 패시브 화면 | 실제 UI 레이캐스트·클릭 포함 75개 검사 통과. 선택, 단계 조절, 적용, 비용, 저장 재조회, 해금 유지, 초기화, 부족 잔액, 최대 단계, 준비 잠금. `Temp/MirrorValidation/PassiveUI.txt` |
| 구매 패시브 적용 | 실제 준비 요청의 공격 +15%가 서버에서 생성된 Fighter 공격력 5 → 6으로 반영. `Temp/MirrorValidation/PassiveRuntime.txt` |
| 전용 서버 + Client 4 | 모두 62단계 PASS. 기본 전투 10 + 영구 유물 관찰 24 + Fighter/Gunner 스킬 24 + 포션·버프·사망·부활 4. `Builds/MirrorValidation_2026-09-10/Logs2/server.log`, `Logs3/client0.log`~`client3.log` |
| 실제 영구 유물 3종 | `item.relic.alienheart` 최대 20, `scrapcompactor` 100, `scrapcubecore` 50. 두 소유자 독립 발동, 서버 JSON, 네 복제본, 소유자 아이템 인스턴스 유지, 실제 드롭·줍기·버프 제거/복원 검증 |
| 쿨다운 정책 | ShareCooldown / PerItem, 소유자 간 독립, 반복 발동 차단 PASS. `RemainingLogs2/server.log` |
| 차징 중 사망 | 실제 사망·부활·지연 피해 없음·다시 스킬 사용·네 복제본 일치 PASS |
| 차징 중 재접속 | 아래 좌표 오류 수정 후 Editor 서버 + 별도 Client 4에서 같은 참가자/런타임, 시작점 좌표 유지, 중단 결과, 새 공격과 네 복제본 일치 PASS. `EditorQuestLogs3`와 보존한 Editor 로그 |
| NPC 초기 제시 | 같은 실행에서 네 클라이언트 모두 실제 NPC 접근·제시 팝업 통과. 실패는 다음 리롤 단계(Phase 41)였음 |
| 실제 화면 | 별도 게임 창에서 최근 세션 재접속, 마우스 이동·공격, 의뢰 NPC 이름/외곽선과 제시 팝업 표시 확인. 뒤의 UI 버튼·단축키 전체 검증은 완료하지 못함 |
| PDF | Pretendard 6쪽, 여섯 페이지 시각 검증 완료. `output/pdf/팀원 코드 수정 내용.pdf` |
| 이전 빌드 정리 | 사용하지 않는 Windows 빌드와 PDF 중간 파일을 삭제하여 27.75 GiB 확보. 현재 빌드·기존 검사 로그·최종 PDF·Linux 빌드는 유지. `Builds/MirrorValidation_2026-09-10/StorageCleanup.txt` |

유물 검사는 실제 서버 아이템 트리거를 호출하는 검사이며 모든 처치 애니메이션의 전수 검사를 뜻하지 않는다. 기본 전투·스킬은 실제 소유자 요청과 원본 AnimationEvent 경로를 사용했다.

## 마지막으로 수정한 두 문제

### 재접속 좌표가 두 배가 되는 문제 — 수정 후 Editor 서버 4인 통과

보존된 플레이어에 새 연결을 붙일 때 Mirror의 `AddPlayerForConnection`은 일반 권한 변경 콜백을 실행하지 않는다. 새 클라이언트 송신 델타의 시작값은 0인데 서버에는 이전 연결의 `lastDeserializedPosition`이 남았다. 실제 서버 좌표와 내부 수신 기준값이 `(-10, 0.08, 7)`에서 `(-20, 0.16, 14)`로 변하는 것을 확인했다.

- `PlayerNetworkTransform_MirrorTest.cs`: `ServerResetOwnerReceiveState()`에서 수신 position/scale 기준값과 서버 수신 버퍼만 초기화.
- `MirrorSessionLifecycle_MirrorTest.cs`: 기존 런타임 재사용 시 `AddPlayerForConnection` 직전에 한 번 호출.
- 기존 관전자 세 명에게 보내는 송신 델타 기준값은 유지한다. Mirror 원본 또는 전체 `ResetState()`로 바꾸지 않는다.
- `MirrorCombatLifecycleSmoke_MirrorTest.cs`: 재접속 후 1초가 지나도 서버 시작점과 0.3m 이내인지 검사. 새 시작점 근처에 대상 적을 다시 배치한 뒤 실제 스킬 타격과 네 복제본까지 통과했다.

### 캠프 UI 버튼 입력 객체 소실 — 수정·컴파일·씬 저장 완료, 실행 재검증 전 중단

`EditorQuestLogs3`의 최종 실패는 Phase 41 리롤 버튼에서 `EventSystem.current`가 없는 문제다. Phase 40 NPC 제시 팝업은 네 명 모두 이미 통과했다. 실제 게임 창에서도 NPC 클릭으로 팝업은 열렸지만 UI 닫기 버튼이 동작하지 않았다. 로비의 EventSystem이 루트 씬 객체여서 캠프로 갈 때 제거되고, 세션 Manager 아래에는 컴포넌트가 없는 비활성 EventSystem 이름의 자식만 남아 있었다.

- `Assets/Editor/MirrorLobbySceneSetup_MirrorTest.cs`: 기존 Manager 자식 EventSystem을 우선 선택하고, 유지할 EventSystem을 Manager 아래에 배치하도록 생성기 보완.
- `Lobby_MirrorTest.unity`: Unity Editor에서 기존 정상 EventSystem을 `Mirror Session` 아래로 이동·활성화·저장. EventSystem 1개와 InputSystemUIInputModule 연결 확인. WJ 씬 변경 없음.
- `MirrorQuestSmoke_MirrorTest.cs`: EventSystem 누락을 명시적인 실패 문구로 검사.
- `MirrorCombatSmoke_MirrorTest.cs`: 검사 실패에 원래 예외 stack trace도 남기도록 보완.
- 마지막 Console 조회 컴파일 오류 0. **수정 후 UI·퀘스트 실행은 아직 하지 않았다.** K/I 키도 실제 게임 창에서 창이 열리지 않았으므로 EventSystem 수정으로 해결됐다고 단정하지 말고 다시 확인한다.

## 다음 계정의 재개 순서

1. 현재 브랜치·작업 트리·Unity 인스턴스/열린 씬을 확인한다. 현재 문서 마지막의 종료 상태를 확인하고, 전체 프로젝트를 다시 조사하지 않는다.
2. `PlayerRemainingBuild5.txt`의 결과를 확인한다. 이 빌드는 위 EventSystem 씬 수정까지 포함한다. 최신 전용 서버는 아직 다시 빌드하지 않았으며, 이전 `ServerRemainingBuild3.txt`는 중복 요청으로 실패 결과가 덮여 있어 최종 빌드로 취급하지 않는다.
3. 빠른 재검증은 Editor 서버 + 새 Player 4개로 진행한다. 임시 실행기 `Temp/MirrorValidation/RunDedicated.ps1 -EditorServer`를 재사용하되 로그 폴더를 새 이름으로 바꾸고 Editor PID를 현재 인스턴스로 갱신한다. 현재 스크립트의 Editor PID는 20860, 로그 폴더는 `EditorQuestLogs3`이다. 이전 로그를 덮어쓰지 않는다.
4. Editor Play 시작 전 `SessionState`의 `SW.MirrorSmoke.Arguments`에 개행으로 인수를 넣는다: `--mirror-smoke-role server --mirror-profile Validation0910_Server --mirror-smoke-count 4 --mirror-smoke-duration 1800 --mirror-smoke-combat true --mirror-smoke-relics true --mirror-smoke-quests true --mirror-smoke-interruptions true --mirror-smoke-focus remaining`. 각 인수와 값이 한 줄씩이어야 한다. 테스트 씬이 현재 Build Settings에 없으면 기존 백업 후 테스트 목록을 임시로 추가하고 종료 때 복원한다.
5. 쿨다운·사망 중단·연결 종료 중단을 거쳐 **퀘스트 리롤/수락 → 실제 적 처치/드롭 아이템 줍기 → 4인 각 보상 → 한 명의 가방 부족 보류 → 그 참가자 재접속 → 유물 스택 유지 → 공간 확보 뒤 아이템 한 번 지급 → 중복 지급 방지**를 끝까지 검증한다. 현재 아직 리롤 이후를 통과하지 않았다.
6. 최신 캠프 창·채팅의 이동/공격 입력 차단, ESC/설정/스킬/인벤토리, NPC hover, StageSelect 투표·대기·재접속 표시를 실제 화면에서 검증한다. 전날 전체 진행 PASS 기록과 최신 UI 검증을 혼동하지 않는다.
7. 필요한 수정 후 Player와 Windows Server를 모두 새로 빌드하고 실제 전용 서버 + Client 4로 최종 실행한다. 문서·개인 로그는 확인한 결과만 갱신한다.

## 실행 중 알게 된 주의점

- **장시간 Unity MCP `execute_code` 요청은 시간 초과 뒤 자동 재실행될 수 있다.** 빌드 전에 고유한 SessionState 키를 설정하여 중복을 막는다. Play도 현재 상태를 검사한다. 빌드 결과는 보고서 파일과 실제 로그로 확인하며 timeout을 빌드 실패로 단정하지 않는다.
- 빌드가 실행 중인 EXE 경로를 다시 쓰면 파일 잠금으로 실패한다. 실행기는 자체 `Processes.json`에 역할·PID·EXE 경로를 저장한다. 종료할 때 PID와 정규화된 경로가 일치하고 현재 `Builds/MirrorValidation_2026-09-10` 아래인지 검증한다. Unity Editor는 이 종료 대상에서 제외한다.
- 서버 초기화가 3초 이상 걸린다. 소유 서버 PID가 UDP 7777을 실제 열었는지 확인한 뒤 Client를 실행한다. `RunDedicated.ps1`에 대기 처리가 있다.
- 재접속 프로필 이름은 영문·숫자·`-`·`_`만 허용한다. `Validation0910_Client0`~`3`을 사용한다. 점이 들어간 예전 검사 이름은 잘못된 테스트였다.
- 재접속 시 기존 시작점 배치는 유지되는 정상 동작이다. 중단 전 위치에 있던 적에게 재접속 후 공격을 기대하지 않는다.
- 초기 퀘스트 검사는 NPC까지 충분히 접근하고 위치가 서버에 전달된 뒤 요청하도록 `< 10f` 거리 제곱 + 0.3초 대기를 넣었다.
- 실제 Windows UI는 설치된 computer-use 스킬의 `@oai/sky`와 `mcp__node_repl__js`로 조작 가능했다. `mcp__cua_repl`의 native 비활성만 보고 Windows 조작 전체가 불가능하다고 판단하지 않는다. 새 계정에서는 스킬 문서를 읽고 초기화한다.

## 알려진 남은 오류와 문서

- 실제 Client마다 기존 `SciFiPitchRandomizer.Start:14` 예외 3건이 있었다. 사운드 보존 요청에 따라 유지했다.
- 전체 Player 빌드의 기존 보스 Advanced Dissolve 셰이더 오류 10건이 있었다. 후속 증분 빌드 오류 0이 이를 수정했다는 뜻은 아니다.
- 같은 PC의 별도 프로세스로 검사했다. 실제 4대 PC·지연/손실·성능/전투 시각 품질 전체 검사는 별도다.
- [팀원 원본 수정 설명](Mirror_Team_Code_Changes_2026-09-09.md), [DataManager·SettingManager 전달 내용](Mirror_BH_Manager_Handoff_2026-09-09.txt), [이전 종료 문서](Mirror_Work_Closeout_2026-09-09.md).
- 보스 Timeline 질문은 기존 서버 확정 후 로컬 Timeline 재생 구조, 참가자 고정 슬롯과 클래스별 바인딩을 유지하는 방향으로 검토만 했다. 별도의 Timeline 구현 작업은 하지 않았다.

## 종료 상태

- 진행 중이던 PlayerRemainingBuild5는 종료 정리 중 성공했다. `Succeeded errors=16 warnings=82 elapsed=00:05:30.6654590`. 16개 오류 항목은 기존 `Boss_Act_01_Up/Leg` Advanced Dissolve 셰이더의 `ObjectSpacePosition`/`uv0` 오류이며 상세는 `PlayerRemainingBuild5Errors.txt`에 보존했다. 새 Player에는 마지막 코드·EventSystem 씬 수정이 포함되지만 **실행 검증은 하지 않았다.** Server는 여전히 다시 빌드해야 한다.
- 검증 서버·숨김 Client·추가 실제 게임 창은 모두 종료했다. Unity는 Play를 멈춘 깨끗한 `Lobby_MirrorTest` Edit Mode다. 빌드 자동 재시도는 `SW.MirrorBuild.PlayerRemaining5` SessionState 키로 차단돼 있다.
- Editor Build Settings의 메모리 목록과 파일을 원래 28개 씬으로 복원했다. 백업은 `Temp/MirrorValidation/EditorBuildSettings.before-passive.asset`에 있다.
- 실제 화면 검사에 임시로 사용한 `MirrorReconnect/default.json`을 원래 바이트로 복원했다. 게임 뷰 최대화 상태를 복원하고 자동 검사 시작용 SessionState 인수를 제거했다. 계정 변경 후 Play만 눌러도 검사가 자동 시작되지 않는다.
- 빌드가 만든 PerformanceTest JSON 2개와 메타, 재생성된 Addressables link.xml/메타를 제거하고 기존 URP 설정 백업을 복원했다. 기능 변경은 보존했다.
- Editor 서버의 좌표 수정 후 4인 결과는 `Builds/MirrorValidation_2026-09-10/EditorQuestLogs3/editor-server.log`로 별도 보존했다.
- C#·Markdown `git diff --check` 통과. Unity가 저장한 로비 YAML에는 `m_Name: ` 같은 직렬화 공백 경고가 있어 일괄 수정하지 않았다.
- 개인 구현 로그 `Docs/Architecture/ImplementationLogs/김성우.md` 갱신 완료. Commit·Push 없음.
