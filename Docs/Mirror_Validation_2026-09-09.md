# Mirror 구현·빌드 검증 — 2026-09-09

브랜치: `codex/unity-6000-3-22-test`. 이번 변경은 아직 커밋하지 않았다.

## 2026-09-09 작업 종료 시점의 추가 검증

이 절이 아래의 이전 구현 단계보다 최신이다. [상세 종료·재개 문서](Mirror_Work_Closeout_2026-09-09.md)에 구현 파일, 초기 실패, 남은 작업을 기록했다.

- Windows Host+Client 3에서 기본 전투 10단계, 파이터 스킬 12조합, 거너 스킬 12조합, 플레이어별 포션·버프·사망·부활 4단계까지 총 38단계 통과. 서버 및 모든 Client가 최종 PASS를 출력했다. 근거: `Builds/MirrorSkillValidation/Logs2/`.
- 차징 실제 피해 6회, 이동 전용 대시의 무피해·이동, 백스텝 종료 후 조작 복귀, 소유자 진화·강화 선택 및 네 화면의 HP·위치 일치를 확인했다.
- 런타임 오류 0은 아니다. 보존 요청을 받은 `SciFiPitchRandomizer.Start()` 예외가 Host 4건·각 Client 3건 남았다. 최초 이름표 오류는 재시도 로그에서 0건이나 모든 NPC hover를 전수 검사한 것은 아니다.
- 최종 Unity BuildReport는 성공·오류 0·경고 24이며 `BuildSummaryFinal.txt`, `BuildMessagesFinal.txt`에 보존했다. 앞선 증분 요약은 오류 0·경고 1이었다. 최초 전체 빌드의 기존 보스 Built-in 셰이더 오류 16건은 수정 완료로 취급하지 않는다.
- 앞선 카메라 없는 원본 API 42개 검사와 Host의 양 캐릭터 각 12조합, 공용 의뢰 보상 슬롯 세 상태 검사를 완료했다. 최종 EXE 검사가 모든 UI·퀘스트 시나리오까지 다시 실행한 것은 아니다.
- 아래 이전 기록의 “스킬 실행 미연결”, “상태이상 원본 수정 미승인”, “전투 HUD 미배치”는 후속 구현으로 상태가 바뀌었다. 4인 퀘스트 지급·재접속, 월드 폭발 효과, 유물 저장 스택, 개인 저장·최종 크레딧 이월, 이번 스킬의 전용 서버 구성과 실제 4PC 검증은 남아 있다.
- 사용자 요청에 따라 이번 빌드 검증까지만 끝냈다. 테스트 프로세스를 종료하고 깨끗한 Lobby Edit Mode를 확인했으며 김성우 구현 로그를 갱신했다. Commit·Push 없음.

## 이하: 초기화·패시브 단계에서 수행한 이전 검증 기록

## 구현 범위

- 승인받은 `PlayerStatManager`, `WBH_PlayerStatus`에 멱등 초기화와 구독 해제를 적용했다. 기존 `PlayerStat` 인스턴스와 레벨·경험치·HP·MP를 초기화하지 않는다.
- 참가자가 패시브 ID와 단계를 제출하면 서버 DB로 검증·계산하고 참가자 명부에 고정한다. 개인 Stat과 공용 상점 최고 투자자 적용 경로에 공급하며 재접속 때 다시 초기화하지 않는다.
- 두 네트워크 플레이어 프리팹의 임시 초기화 guard와 원본 스크립트를 제거했다.
- SW 테스트 씬의 끊어진 시각 참조 18개를 정리했다. 11개 빌드 씬의 참조 검증은 예외 목록 없이 실행한다.
- Stage 5 승강기 중간에서 사망·부활할 때 현재 위치에 NavMesh가 없으면 서버가 확인한 현재 씬 시작점으로 복구한다.
- 종합상황실은 처음에 접힌 상태이며 열기 버튼으로 펼칠 수 있다.

로컬 패시브 JSON의 형식·단계는 검증하지만 계정 서버 구매 이력은 확인하지 않는다. 기존 클라이언트 권한 이동을 서버 이동 시뮬레이션으로 바꾸지는 않았다.

## 빌드 기록

산출물과 원본 로그는 Git 제외 경로 `Builds/MirrorLanTest/Validation0909`에 보관한다. Client는 `Builds/MirrorLanTest/Client0909/MirrorLanTest.exe`, 전용 서버는 `Builds/MirrorDedicatedServer/MirrorDedicatedServer.exe`에 출력한다.

| 빌드 | 결과 | 근거·한계 |
| --- | --- | --- |
| Client v1 전체 빌드 | 성공 | 6168.22 MB, 291.54초. 기존 Boss Advanced Dissolve 셰이더 오류 10개가 기록됨. |
| Dedicated v1 전체 빌드 | 성공 | 2884.78 MB, 513.77초. 오류 0, 경고 63개. |
| Client v2 전체 빌드 | 성공 | 6168.22 MB, 567.27초. 기존 Boss 셰이더 오류 16개, C# 오류 없음. |
| Client v3 스크립트 빌드 시도 | 실패 | 전달한 28개 씬 목록과 기존 11개 씬 캐시가 달라 Unity가 거부. 기능 검증에 사용하지 않음. |
| Client v4 스크립트 빌드 | 성공 | 정확한 11개 씬 목록 사용. 133.28초, 오류 0, 경고 19개. 승강기 복구·기본 접힘·버프 시간 진단 반영. |
| Dedicated v2 스크립트 빌드 | 성공 | 364.10초, 오류 0, 경고 27개. Client v4와 동일 코드. |
| Client v5 최종 스크립트 빌드 | 성공 | 278.85초, 오류 0, 경고 21개. 버프 진단 시작 시 남은 시간이 0이면 실패 처리하고 로그 필드를 `profileShopLevel`로 정정. 게임 동작은 v4와 동일. |
| Dedicated v3 최종 스크립트 빌드 | 성공 | 326.12초, 오류 0, 경고 21개. Client v5와 동일 코드. |

전체 Player 빌드의 기존 셰이더 오류는 `Assets/WBHTest/Material/Boss_Act_01_Up.shader`, `Boss_Act_01_Leg.shader`의 `uv0` / `ObjectSpacePosition` 오류다. 최종 Editor 타깃 복구 때도 외부 Advanced Dissolve의 `Assets/Amazing Assets/Advanced Dissolve/Shaders/cginc/Defines.cginc` include 누락 오류가 남았다. C# 오류와 구분하며 성공 BuildReport만으로 해당 보스 머터리얼의 표시 품질까지 정상이라고 판단하지 않는다.

## 실행 증거

| 검사 | 결과 | 로그·확인 범위 |
| --- | --- | --- |
| 패시브 입력 검증 | 통과 | Editor 66개 검사. JSON 구조·크기, ID, 중복, 해금/현재/최대 단계, DB 효과 계산. |
| 세션 / 투표 / 플랫폼 규칙 | 통과 | Editor 각각 93 / 31 / 16개 검사. |
| 11개 SW 빌드 씬 참조 | 통과 | Missing Script·끊어진 참조 검사. 빌드 목록 밖의 이전 Stage1 테스트 씬에는 기존 Missing Prefab 2개가 남아 있음. |
| 실제 Editor Host 초기화 | 통과 | `editor-initialization.log`: 동일 Stat 인스턴스, Initialize 반복, 재활성화, 이벤트 구독, 자원 보존. 최초 시도는 테스트 씬이 Build Settings에 없어 실패했고 씬 목록을 임시 반영한 재시도에서 통과. |
| Host + 3 Client 전투 | 통과 | `combat_host_v1`: Fighter 기본/스킬, Gunner Rifle/Shotgun/Grenade 총 14단계. 실제 Command·애니메이션 이벤트·피해·중복 타격 방지·포션·사망·부활·사망 중 이동 금지. |
| Dedicated + 4 Client 전투 | 통과 | `combat_dedicated_v1`: 같은 14단계. EXE 창을 표시하고 실행. |
| Host + 3 Client 전체 런 | 논리 통과 | `full_host_v1`: 11개 노드, 6개 전투 맵, Camp/Event/Boss, 결과 버튼→로비→새 런, 새 netId·진행·아이템·골드 초기화. 개발용 마무리 피해 사용. 당시 창이 숨겨져 있어 화면 캡처 증거는 없음. |
| 개인 패시브 4종 | 통과 | 위 4인 실행에서 empty / attack1 / attack5-shop / all-max. 서버 DB 결과·개인 원시 Stat·최종 Stat·구독 및 초기화 보존 확인. |
| 정원 4명에서 추가 접속 | 서버 용량 제한 확인 | Host 로그에 `Server full` 및 연결 종료. Authenticator 전에 종료되어 잘못된 패시브/중도 참가 거절 사유 검증에는 사용할 수 없음. |
| Stage 5 최초 검사 | 검사 시작 조건 오류 | `platform_host_v1`: 초기 Run Snapshot 준비를 기다리지 않아 실패. 검사 시작 조건 수정. |
| Stage 5 중간 사망·부활 | 실제 결함 재현 | `platform_host_v2`: 원격 Gunner가 Y=5.61에서 HP 10/10으로 부활해도 controller/control/agent가 false, NavMesh 샘플 실패. 다른 3명의 상단 도착은 통과. 수정 후 재검증 필요. |
| Stage 5 수정 후 Host 4인 | 통과 | `platform_host_v4`: 3인 선탑승 대기→4번째 탑승→중간 사망→나머지 상단 도착→승강기 복귀→부활·이동까지 서버와 4명 모두 통과. 부활 소유자 netId 4가 맵 시작점으로 복구 후 0.192m 이동. |
| 지연 환경 Host 4인 인벤토리 | 통과 | `inventory_host_v4`: 각 전송 측 100ms, 최대 20ms 지터, unreliable 손실·순서 변경 각각 2%. 각자 실제 UI 이동·회전·교환·거절·장착·해제·판매·구매·삭제·취소·강화·드롭·획득, 동일 재고 경쟁의 성공자/소유자 1명 확인. |
| 아이템·패시브 재접속 보존 | 통과 | `resume_inventory_v4`: 슬롯 3 Gunner, 동일 참가자·netId 6, HP 12/12·MP 50/50·골드 15000·아이템 `6d2cac76-6231-4d15-8ade-661466e4f1e1`·강화 1·검증 프로필 ShopLevel 1 유지. 새 연결에서 Controller·입력·NavMesh 복구 및 resume 종료 PASS. `passiveRank` 로그 필드는 공격 단계가 아니라 `Member.PassiveProfile.ShopLevel`이다. |
| 잘못된 패시브 / 출발 후 신규 참가 | 통과 | `reject_passive_v4`, `reject_midrun_v4`: 실제 연결 여유가 있는 상태에서 각각 정확한 서버 거절 사유 및 미승인·연결 종료, 클라이언트 종료 PASS 확인. |
| 상황실 기본 접힘 | 통과 | `platform_host_v4/collapsed-stage5.png`: 새 실행에서 작은 열기 버튼만 표시. |
| Stage 5 ESC 설정 진입 | 미연결 확인 | `platform_host_v4/escape-no-popup.png`: 실제 ESC 입력 후 설정창이 열리지 않음. SW UI Binder의 ESC는 상태창/인벤토리 닫기만 수행하고 Pause 이벤트로 이어지지 않음. |
| 표시된 Host 4인 전체 런 | 통과 | `full_host_v4`: 11개 노드·6개 전투 맵·Camp/Event/Boss·결과→로비→새 런 모두 서버와 4명 통과. netId 3~6→127~130. `Client0909/RunValidation/v0909_full_host_v4_*`에 실제 렌더 52장 저장; Stage5, Stage1, 클리어, 복귀 로비 화면 확인. 개발용 마무리 피해를 사용한 진행 검증. |
| 전용 서버 4인 승강기 + 지연 | 통과 | `platform_dedicated_v2`: 서버 + 4 Client, 각 전송 측 100ms·지터·손실 조건. 3인 선탑승 대기, 중간 사망, 나머지 상단 도착, 부활 소유자 netId 3의 시작점 복구·0.192m 이동 및 4명 최종 확인. |
| 표시된 전용 서버 4인 전체 런 | 통과 | `full_dedicated_v2`: 4명 각각 11개 노드·결과·로비·새 런 통과, 서버 새 런 초기화 통과. 실제 화면 52장 저장, 1번 Client의 클리어 화면·로비 복귀 버튼 표시 확인. |
| 싱글 실제 거너 회귀 | 통과 | `single-regression.txt`, `final-editor-validation.txt`: 원본 캠프의 실제 InventoryController·spawned ItemUI에서 지급→이동/회전→장착/최대 HP 증가→해제/복원→fixture 정리. 같은 Stat·레벨·경험치·HP 10·MP 50, 반복 초기화/재활성화 뒤 구독 1회, 실제 X=-10→-9 이동 확인. 플레이 전후 게임 저장 JSON 6개는 백업과 동일. |
| 실제 버프 시간·재접속 | 전원 부재 일시정지 확인 | `buff_server_v2b`: 연결 중 10초 버프가 12초 뒤 만료. 다음 버프 적용 직후 클라이언트를 정상 종료하면 전원 부재의 timeScale 0에서 잔여 9.600초가 실제 12초 뒤에도 9.600초. `resume_buff_v4`로 같은 소유자 복귀 후 다시 정상 만료. 전투와 각 플레이어 버프를 함께 정지시키는 현재 동작이며, 이 관찰만으로 결함이라고 판정하지 않는다. |
| 전원 부재 300초 예약 만료 | 로비·명부 정리 확인 | `buff_server_v2b`: 재접속 클라이언트의 두 번째 종료 후 실제 300초 동안 기다림. 예약 참가자 출력이 사라지고 서버가 Lobby로 전환되는 것을 확인. 서버는 450초 실행을 마치고 admission/session 종료 PASS. |
| 실제 NetworkEnemy 상태이상 | 미적용 확인 | `network-status.txt`: Editor Host의 살아 있는 `Normal_Range_MirrorTest(Clone)`에서 기존 공개 API로 Burn/Slow/Stun을 요청. 모두 applied=false, active=false, remaining=0. 기존 준비 guard가 거절하며 피해 성공과 구분. |
| 최종 Client v5 + Dedicated v3 접속 | 통과 | `final_admission_v5_v3`: 실제 전용 서버·4 Client의 입장/READY/StageSelect, 개인 패시브 4종·반복 초기화/재활성화 검증 통과. 서버는 100초 실행 후 admission/session PASS로 종료했고, Client는 검사 통과 후 실행기가 종료했다. 네 창을 표시하고 StageSelect 화면 확인. |

일부 프로세스는 핵심 검사 완료 후 테스트 실행기가 종료했다. 모든 프로세스의 자연 종료 코드까지 통과한 것으로 표현하지 않는다.

버프 최초 시도 `buff_server_v2`는 종료가 늦어 부재 전에 이미 버프가 만료됐다. before=0 / after=0 출력은 유효한 부재 중 시간 검증으로 계산하지 않는다. 두 번째 시도는 로그 신호 직후 창을 정상 종료하여 잔여 9.6초를 확보했다. 이후 검사 코드에도 시작 잔여 시간이 0이면 실패하도록 조건을 추가했다. `reject_expired_v4`는 서버의 예정 종료 뒤 시작해 거절 사유를 검증하지 못했으며 통과로 계산하지 않는다. 299/300초 정확한 경계는 93개 세션 규칙 검사, 실제 300초 경과 후 정리는 위 실행으로 확인했다.

## 확인한 한계

- 전원 부재 시 `Time.timeScale = 0`으로 전투와 각 플레이어의 버프 시간이 함께 멈춘다. 일부 참가자만 부재하고 다른 참가자가 플레이하면 서버 버프 시간은 계속 흐르는 코드 경로다. 전원 부재 중 버프만 실제 시간으로 소모할 필요는 별도 정책 문제이며, 결함으로 단정했던 이전 표현을 정정한다. `PlayerBuffManager`는 수정하지 않았다.
- NetworkEnemy의 Burn/Slow/Stun은 기존 준비 guard가 적용을 막는다. 피해 검사 통과를 상태이상 통과로 계산하지 않는다. 원본 상태이상 VFX null 경계와 SW guard 연결은 별도 승인·구현 대상이다.
- 거너 6개 스킬 shape, 공용 퀘스트 전체 흐름은 이번 1·2번 구현 범위에 포함된 완성 기능이 아니다.
- ESC 설정창은 멀티 전투 씬에 아직 연결하지 않았다. 기존 설정 적용 코드는 클라이언트 로컬 Screen/Quality/AudioListener를 변경하지만 같은 PC의 EXE는 `settings.json`을 공유한다. 전투 중 개인별 설정 적용 완료로 표시하지 않는다.
- 싱글 캠프 시작에서는 수정하지 않은 `WBH_EnemySpawner.Initialize()`의 `eliteView.Initialize()`에서 null 예외가 발생했다. 플레이어 초기화·인벤토리·이동 회귀는 통과했지만 이 원본 씬 전체가 오류 없다고 판단하지 않는다.
- 모두 같은 PC의 localhost 실행이다. 실제 4대 PC의 LAN/방화벽·WAN 조건, 프레임 성능·전투 밸런스와 모든 원본 스킬/진화/강화의 품질 검증을 대신하지 않는다.

## 최종 정리 확인

- 최종 Client v5·Dedicated v3 빌드 뒤 C# Console 오류 0개, Editor의 Player 타깃·28개 원래 Build Settings·깨끗한 Lobby 씬·Prefab Stage 없음 확인. 빌드가 바꾼 URP 필터/런타임 목록과 Build Settings는 원상 복구했다.
- 테스트 프로세스와 이번 실행의 재접속 프로필, 생성된 Addressables/성능검사 파일을 정리했다. 기존 저장 JSON 6개는 SHA-256 대조에서 모두 동일하며 사용자 RenderTexture 변경은 보존했다.
- `git diff --check`는 Unity가 저장한 빈 YAML 값의 행 끝 공백 5개를 보고한다. Unity 직렬화 형식을 유지했으며, 행 끝 공백을 제외한 검사는 통과했다.
- 김성우 개인 구현 로그의 Git 구현 이력·마지막 기록 표를 갱신했다. Commit·Push는 하지 않았다. 빌드 파일·원본 실행 로그·화면 증거는 Git 제외 경로에 보존한다.
