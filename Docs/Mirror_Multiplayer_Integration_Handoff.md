# Mirror 멀티플레이 통합 인계

작성 기준: 2026-09-09, `unity-6000-3-22-test`(사용자가 김성우 테스트 작업 브랜치로 확인). 사용자가 공통 원본 수정을 보류한 시점의 구현 결과와 후속 작업을 기록한다. **정식 승격 전이며 `_MirrorTest`를 유지한다.** 확인한 실행 범위와 미완료 기능을 구분한다.

## 1. 현재 범위와 검증 상태

현재 연결한 범위는 KY 로비 UI → 서버 참가 명부 → 캐릭터 선택·준비 → 테스트 노드 진행 → 보스 클리어 → 리더의 로비 복귀 → 새 런이다. 재접속 시에는 기존 서버 캐릭터를 복구하고, 클리어 후 새 런에서는 새 캐릭터를 생성한다. 플레이어별 `PlayerContext`가 인벤토리·장비·지갑·Stat·체력·마나·버프 런타임을 보유한다. **테스트 경로 완주는 확인했지만 정식 Act 1 전체와 모든 전투 기능이 완성된 상태는 아니다.**

| 구분 | 현재 상태 | 완료로 해석하면 안 되는 부분 |
| --- | --- | --- |
| 로비 | 서버 명부를 KY 슬롯에 표시하고 캐릭터·준비·출발·퇴장 의도를 서버에 전달. Windows Host+Client 3개의 정상 요청으로 4인 출발 확인 | 자동 요청 검증이며 모든 UI 버튼의 수동 조작 검증은 아님 |
| 참가 명부 | 예약 포함 최대 4명, 고정 슬롯·참가자 ID, 출발 후 신규 참가 금지 | 계정 서버 인증이나 영구 계정 저장은 아님 |
| 재접속 | 출발 당시 참가자가 같은 세션·비밀 토큰으로 300초 이내 복귀. 서버 런타임 보존 및 새 연결 결합 | 서버 프로세스 재시작 복구, Host migration은 구현 범위 밖 |
| 런타임 정지 | 부재 시 시각·충돌·이동·입력 및 미완료 공격·스킬을 정지하고 런타임은 유지 | 사망·부활·씬 이동과 겹친 복귀의 실제 플레이 검증은 진행 중 |
| 기본값 | 새 멀티 런의 골드·상점 혜택·서버 패시브는 0, 테스트 아이템 자동 지급 제거 | **패시브를 최종적으로 0으로 하자는 팀 결정이 아님** |
| 개발 명령 | 서버의 개발 명령 허용 조건을 통과할 때만 임의 아이템 지급 가능 | 운영 플레이 보상 설계가 아님 |
| 파이터 | 기존 SW 서버 공격·스킬 경계 유지, 실제 Stat 공격각과 서버 마나 소비 연결, 씬별 VFX 초기화 보완 | 원본 전체 진화·강화·차징 기능의 정식 멀티 전환은 미완료 |
| 거너 | 라이플·샷건·유탄 기본 공격을 서버 판정·네트워크 투사체·기존 무기 VFX에 연결. Host 실제 공격 24개 검사 통과 | 6개 스킬 shape·전체 진화·강화와 원격 전투 품질 검증은 미완료 |
| 맵 진행 | 실제 Stage1~6·Boss 환경을 쓰는 SW 테스트 씬 7개, Camp·Event·선택·로비를 연결. 맵 6개 소진 전 중복 배정을 피하고 서버 스냅샷에 보존 | 4인 전체 경로 검증 결과는 아래 최신 기록을 기준으로 한다. 원본 전체 적 종류·퀘스트·이벤트 효과까지 정식 통합한 것은 아님 |
| 노드 투표 | 현재 접속한 원래 런 참가자에게 1표씩 허용. 변경 가능, 첫 표부터 20초, 전원 투표 시 즉시 확정, 최다 득표와 동률 추첨 | 이번 사용자 요청으로 노드 선택만 기존 리더 전용에서 변경. 로비 출발·결과 복귀 권한과 이벤트 선택 정책은 별도 |
| 결과·재출발 | 리더만 클리어 결과에서 파티를 로비로 복귀시킨다. 참가자·캐릭터 선택은 유지하고 READY·이전 런타임·진행도를 초기화 | 호스트 이전이나 서버 재시작 복구를 추가한 것은 아님 |
| 적 데이터 | WBH Provider의 WJ GeneratedData와 `EnemyAttackType`을 서버에서 사용하고 결과를 클라이언트에 동기화. 처치 크레딧을 서버 지갑에 1회 지급 | 현재 3종 매핑과 normal 난이도만 사용. 전체 적 종류·실제 보스 외형/패턴의 데이터 기반 교체는 미완료 |

현재까지 확정된 검증 기록은 다음과 같다.

- 최초 세션 구현의 Unity C# 컴파일 및 Console error 0 / warning 0을 확인했다. 이후 빌드 경고와 재수정이 있으므로 최종 상태는 아래 후속 검증 기록을 기준으로 한다.
- `SW/Mirror Test/Validate Session Rules` 실행에서 **83개 검사 통과**를 확인했다. 명부·인증 자격·만료·리더 승계·격리 프로필 저장 규칙 검사이며 Unity 실제 플레이를 대신하지 않는다.
- `SW/Mirror Test/Validate Lobby Assets`에서 파이터·거너 프리팹의 `PlayerContext.IsComplete`, Missing Script, 타 플레이어 참조 검사를 통과했다. 확인된 assetId는 Fighter `2511009531`, Gunner `710334692`다. 외형·장착·전투 플레이 검증을 대신하지 않는다.
- 신규 `Lobby_MirrorTest` 씬과 `GunnerNetworkPlayer_MirrorTest` 프리팹 생성이 끝났다. 생성에는 additive 씬과 PrefabContents 경로를 사용했다. 원본 씬의 최종 디스크 변경 상태는 아래의 별도 확인 대상이다.

Windows LAN 1차 빌드가 성공했다(약 5,966 MB). 같은 PC의 별도 Windows 프로세스 4개에서 Host 1명+Client 3명, Fighter 2명+Gunner 2명이 선택·준비한 뒤 StageSelect로 함께 이동했다. 서버의 참가자 ID·슬롯과 netId 3·4·5·6이 각각 분리됐고, 시작값은 HP 10/MP 50/골드 0/빈 인벤토리였다.

Gunner Client를 종료한 뒤 같은 프로필로 프로세스를 다시 실행했다. 서버에서 같은 참가자 ID·슬롯 3·netId 6이 유지됐고, 이전 연결 → 연결 없음/부재 → 새 연결/복귀로 바뀌었다. HP·MP·골드·빈 인벤토리도 유지됐다. 잘못된 토큰과 출발 후 신규 참가 요청은 각각 인증 단계에서 거부됐다. **아이템이 있는 인벤토리·장착·버프·스킬·사망 상태 보존은 이 검사에서 검증하지 않았다.** 근거는 `Builds/MirrorLanTest/Validation/host4-*.log`, `resumed-gunner_v1.log`, `wrongtoken2-gunner_v1.log`, `newmidrun2-newcomer_v2.log`다. 재접속 자격 JSON은 공유하지 않는다.

1차 실행에서 선택 씬의 NavMeshAgent 오류, 중복 EventSystem, 연결 패널 한글 폰트 누락이 발견됐다. 실제 맵 배치가 성공한 뒤 Agent/입력을 활성화하도록 수정했고, 테스트 로비의 중복 EventSystem과 폰트를 정리했다. 테스트 Camp·Stage1에 남아 있던 삭제된 두 Manager 프리팹과 Camp의 Missing Script를 제거하고, 싱글 저장을 로드하던 `YJ_StageManager`는 해당 테스트 씬에서 비활성화했다. 원본 클래스 스크립트는 수정하지 않았다. 마지막 런타임 수정의 재빌드 검증은 별도로 기록한다.

2차 실행 파일에서도 Host+Client 3개가 정상 요청으로 Stage1에 진입했다. 소유 클라이언트에서 `snapshot=True`, `controller/control=True`, `agent/onNavMesh=True`를 확인했다. 적에게 공격받아 HP 0이 된 Gunner를 종료·재실행했을 때 같은 참가자·netId 4로 복귀했고, HP 0과 부활 횟수 3이 유지됐으며 Controller·입력·Agent는 꺼진 상태였다. 사망 중 복귀가 자동 부활을 만들지 않는 사례를 확인한 것이다. 이동 입력·공격을 직접 수행하거나 전체 사망/부활 규칙을 검증한 결과는 아니다. 근거: `travel4-*.log`, `resumed-gunner_v2.log`.

최종 Windows LAN 빌드는 **성공, 약 5,966.4 MB**다. `Builds/MirrorLanTest/MirrorLanTest.exe`와 같은 폴더의 전체 데이터를 함께 사용한다. Windows Addressables settings·catalog·bundle 검사를 통과했다. 2차 실행의 세션 검사와 별개로 최종 빌드에서 서버 전용 실행 모드 1개+Client 4개를 다시 확인했다. 서버에는 로컬 참가자가 없고, 실제 UI에서 주소로 참가 → 파이터 선택 → Ready → StageSelect 화면까지 진행했다. 서버 로그에 서로 다른 참가자 4명과 netId 3~6이 기록됐다. 근거는 `finalserver-*.log`와 `final-stage-select.png`다. **Unity Dedicated Server 타깃으로 별도 빌드한 실행 파일의 검증은 아니다.**

최종 C# 컴파일 뒤 규칙 83개와 두 플레이어 자산 검사를 다시 통과했다. 1차에서 관찰한 중복 EventSystem·선택 씬 NavMesh 오류는 후속 실행에서 재발하지 않았다. 런타임에는 `PassiveSkillManager`의 데이터베이스 미연결 경고가 남아 있으며, 보류된 원본 초기화·패시브 입력 경계와 함께 해결해야 한다. 이 시점까지 전원 부재 300초 실제 대기는 미검증이었고 아래 후속 검사에서 확인했다. 살아 있는 캐릭터의 맵 내 재접속 후 직접 이동, 비어 있지 않은 장비·버프 보존, 전투 종료→StageSelect 복귀는 여전히 미검증이다.

실패한 캐시 재사용 빌드 메뉴와 일회성 Scene 수리/감사·실행 도구를 제거했다. 생성된 Addressables link.xml과 빌드가 변경한 URP 필터/런타임 목록도 작업 전 상태로 정리했다. 테스트 프로세스를 종료하고 테스트 재접속 자격을 삭제했으며, UI 확인을 위해 사용한 기본 프로필은 기존 파일이 있었으면 복원했다. 실제 실행 로그와 최종 빌드는 보관한다.

열려 있던 원본 `Act1_Camp_MergeTest`는 사용자가 **현재 씬을 저장하고 최종 빌드**하도록 승인한 뒤 저장했다. 작업 중 원본 Scene 디스크에서 관찰한 UI 위치·스크롤 크기·직렬화 필드 변경은 사용자 편집과 자동 재직렬화의 출처를 단정하지 않고 보존했다. 실제 Act 1 완주, 거너 서버 전투, 장착·사망·상점 전체 흐름은 아직 통과 항목이 아니다.

### 2026-09-08 후속 실행 검증

공통 원본 머지 전에도 가능한 세션 검증을 추가했다. 실제 실행 근거는 `Builds/MirrorLanTest/FollowupValidation`에 보관한다.

| 검사 | 실제 결과 | 근거 |
| --- | --- | --- |
| 전원 부재 300초 | 서버 전용 실행 모드에서 유일한 참가자를 종료한 뒤 실제 5분 동안 동일 참가자·netId 3·HP 10·MP 50·골드 0을 예약 상태로 유지했다. 시간이 지나자 명부가 비워지고 `Lobby_MirrorTest`로 복귀했다. | `expiry-followup_server1.log`, 실행 시각을 담은 `launched-processes.jsonl` |
| 만료 후 재접속·새 런 | 이전 프로필의 재접속은 인증 단계에서 거절됐다. 이후 새 참가자는 새 참가자 ID·netId 6으로 생성돼 StageSelect에서 새 런을 시작했다. | `expired-followup_expiry1.log`, `fresh-followup_new1.log`, 같은 서버 로그 |
| Windows 전용 서버 타깃 | Unity `StandaloneBuildSubtarget.Server` 빌드 성공, 약 2,690.2 MB. 실행 파일은 `Builds/MirrorDedicatedServer/MirrorDedicatedServer.exe`다. | Editor 빌드 완료 기록 및 `dedicated4b-followup_dedicated2.log` |
| 전용 서버 + Client 4개 | 전용 서버의 로컬 참가자 없이 Fighter 2명·Gunner 2명이 정상 선택·READY·노드 선택 요청으로 Stage1에 진입했다. 서버에서 netId 3~6과 개별 HP 감소를 확인했다. | `dedicated4b-*.log` |
| 전용 서버의 사망 재접속 | Gunner의 HP 0·부활 횟수 3·슬롯 1·netId 4가 유지되고 연결 번호만 바뀌었다. 복귀한 소유 클라이언트는 스냅샷 수신 후에도 사망 상태의 입력·Controller·Agent를 비활성으로 유지했다. | `dedicated-resume-followup_dg1.log`, 해당 서버 로그 |

전용 서버 첫 시도에서는 서버가 포트를 열기 전에 시작한 두 클라이언트가 참가하지 못했다. `Server listening on port 7777`을 확인한 뒤 새 프로필로 재실행한 두 번째 시도가 위의 4인 통과 결과다. 자동 검사기는 실패한 최초 접속을 반복하지 않으므로 서버 준비 로그를 기다린 뒤 클라이언트를 시작한다.

전용 서버 빌드는 성공했지만 외부 `Animpic/Animpic_FoliageURP`의 중복 keyword 및 Advanced Dissolve Shader Graph의 include 경로 오류가 빌드 로그에 기록됐다. 화면 없는 서버 실행에서는 파티클 메시 데이터·Dissolve 셰이더 경고도 남는다. 네트워크 예외·MissingReference/NullReference·기존 NavMesh 오류는 이번 통과 로그에서 발견하지 못했다. 외부 에셋은 수정하지 않았으며, 이 결과를 외부 셰이더 품질이나 파괴 VFX 검증 완료로 해석하지 않는다.

이번 검사는 빈 인벤토리와 기본 자원으로 진행했다. 전원 부재 중 버프 시간 정지 문제, 상태이상, 생존 상태에서 실제 이동 입력, 장비·아이템·쿨타임 보존, Act1 완주는 별도로 남아 있다. 전용 서버 타깃 전환 후 Editor가 다시 Dirty로 표시한 원본 Camp 씬은 후속 검증을 위해 저장하거나 폐기하지 않았다.

### 2026-09-08 인벤토리 입력 전환

Mirror 아이템은 `ItemUI.BindExternalInput`으로 입력 의도를 전달하며, 드래그 중에는 별도 표시용 `InventoryItem`만 이동·회전한다. 원본 Grid·장비·골드는 서버 승인 전 그대로 유지한다. 미리보기 정리 다음 프레임에 `NetworkInventoryInput_MirrorTest`가 기존 서버 요청을 한 번 보내며, Host 선반영 예외인 `alreadyAppliedByHost`와 기존 네 개의 분산 입력 어댑터를 제거했다. 기존 싱글 `ItemPrefab`은 외부 입력에 연결하지 않는다.

서버와 원격 클라이언트의 소유 모델 복구를 UI 참조에서 분리했다. 늦게 UI가 연결돼도 모델 복구가 중단되지 않으며, 같은 장착 아이템은 보존해 화면 갱신 때문에 장비 효과를 재적용하지 않는다. 잘못된 슬롯·이전 상태 번호 요청을 거절하고, 드래그 중 새 상태가 도착하면 표시만 정리한다. `InventoryCamp_MirrorTest`의 변경된 부모 프리팹 참조도 현재 Grid·장비 슬롯·툴팁에 다시 연결했다. 캠프 이동용 NavMesh는 SW 테스트 씬의 별도 자산으로 저장했다.

| 실제 검증 | 결과와 근거 |
| --- | --- |
| 싱글 실제 플레이어 | 원본 `Act1_Camp_MergeTest` Play Mode에서 지급 → 실제 spawned UI 드래그 이동·회전 → 우클릭 장착·최대 체력 증가 → 해제·체력 복원 → 검증용 아이템 정리 통과. 기존 아이템 수·골드 보존. `SW/Mirror 테스트/싱글 실제 인벤토리 검증` 메뉴로 재실행 가능. 기존 `WBH_EnemySpawner.Initialize:35` 오류는 별개로 남음. |
| Host 실제 캠프 | 정상 노드 선택 요청 → 캠프 시작점 배치·Controller 이동 → 회전·교환·잘못된 슬롯 거절·장착·해제·판매·재구매·삭제·중단 드래그·강화·월드 드롭·Raycast 재획득 통과. 강화 1 아이템 한 개와 골드 14,500 보존. `Builds/MirrorLanTest/InventoryValidation/inventory_host_v4.log`. |
| 별도 서버 + 원격 Gunner | 최종 V9 빌드에서 동일한 캠프 이동과 인벤토리 전체 흐름 통과. 클라이언트 모델과 서버 모두 `58f28092-b557-4fa3-944a-09c86620cce4` 한 개, 강화 1, 골드 14,500, HP 10/10·MP 50/50으로 일치. `inventory_client_v3_before_resume.log`, `inventory_server_v3.log`. 현재 변경의 서버 검사는 일반 Windows Player의 서버 전용 실행 모드이며, 앞선 Dedicated Server 타깃 검증과 구분한다. |
| 강화 아이템을 가진 재접속 | 클라이언트 프로세스 종료 후 서버가 참가자 `9fddb59d4cde4edba42a5e3b585f9627`·netId 3·아이템 ID·강화 1·골드 14,500·HP/MP를 예약 상태로 보존했다. 같은 프로필의 재실행 후 소유 클라이언트의 모델과 UI 연결도 같은 값으로 복원되고, 현재 맵의 안전 시작점에서 Controller·NavMesh·입력 활성 상태를 확인했다. `inventory_client_v3.log`, `inventory_server_v3.log`. 이전 실행에서 발생한 싱글 적 스포너 Awake 예외는 SW Camp의 사용하지 않는 EnemyManager 오브젝트 비활성화 후 재발하지 않았다. 팀원 원본 스크립트는 변경하지 않았다. |

검사는 `--mirror-smoke-inventory true`로 명시한 실행에서만 첫 노드를 Camp로 바꾸고 검증용 아이템을 지급한다. 일반 실행은 빈 인벤토리와 기존 서버 초기값을 유지한다. 검사 도구가 직접 씬을 바꿔 시작점 배치를 건너뛰던 초기 실패와, 회전 교환의 아래쪽 가장자리 맞춤을 잘못 기대했던 초기 실패는 통과 결과와 구분한다. 실제 Act 1 전체 진행, 장착 판매, 여러 참가자의 동시 거래, 재접속 중 버프·쿨타임 경과까지 완료한 것으로 해석하지 않는다.

최종 V9 Windows Player 빌드 성공·오류 0과 Editor 컴파일 완료·Console 오류 0을 확인했다. Fighter/Gunner 인벤토리·지갑, Camp의 Grid·장비 슬롯·Tooltip 필수 참조 검사와 Camp Missing Script 0을 확인했다. 원격 검증은 실제 spawned UI 콜백을 실행한 배치 모드 검사이며 원격 화면 캡처 검증과 구분한다. 복귀 화면에서 플레이어 스폰 전 발생하는 기존 HUD의 WBH_PlayerStatus 탐색 경고와 패시브 DB 경고는 남아 있다. 기존 Windows Addressables 산출물을 사용한 코드·씬 재빌드이며, 콘텐츠 자체의 재생성 검증은 앞선 빌드 기록을 따른다. 검증 프로세스·격리 자격 7개·임시 실행 도구와 빌드 생성 메타데이터를 정리하고 빌드가 바꾼 URP 필터/런타임 목록만 복구했다. 원본 Camp의 Dirty 상태는 저장하지 않고 유지했다.

### 2026-09-08 전체 테스트 런·적 데이터 통합

main 병합본 `5c253649`에 기존 작업 stash를 복원하고 `.meta`의 오인 rename 충돌을 원래 GUID로 해결했다. stash는 삭제하지 않았다. 이번 단계에서 팀원 원본 스크립트를 추가로 수정하지 않았다.

- `EnemyType → EnemyAttackType`, 정수 `id → 문자열 enemyId`, 보상 `credit` 변경을 SW 프리팹에 반영했다. `WBH_EnemyDataProvider.TryCreateEnemyInfo`에 현재 층·normal 난이도·출발 인원을 전달하고 `NetworkServer.Spawn` 전에 반환 데이터를 주입한다. 재접속 예약도 출발 인원에 포함한다. 배율 공식이나 스폰 Manager를 복제하지 않았다.
- 현재 매핑은 `enemy.normal.melee.working_machine`, `enemy.normal.ranged.patrol_drone`, `enemy.boss.boss.SpiderX`다. 보스는 SpiderX **데이터**를 사용하지만 기존 Mirror 보스 외형과 테스트 패턴을 유지한다. Elite 노드는 아직 일반 적의 웨이브 수를 늘리는 테스트 구성이다.
- 클리어 결과에 리더용 `로비로 돌아가기` 버튼을 연결했다. 리더 외 요청과 클리어 전 복귀를 서버가 거절한다. 로비 복귀 시 이전 플레이어를 연결에서 제거한 뒤 파괴하고, 선택한 캐릭터를 유지한 채 READY를 해제한다. 재출발은 전원의 새 READY를 요구한다. `Reedy` 오탈자와 READY 라벨 참조도 수정했다.
- 보스 Agent를 원본 이동 초기화 전에 활성화하고 씬의 실제 EffectPool과 연결된 Spawner를 사용한다. 싱글용 `EnemyKillReward`는 공통 네트워크 초기화에서 비활성화해 서버 보상과 겹치지 않게 했다. 새 한글 보스 이름은 기존 Pretendard 폰트와 정상 머터리얼로 표시한다.

| 실제 검사 | 결과 |
| --- | --- |
| 최종 Host + 원격 Gunner | `run_host_v6.log`, `run_client_v6.log`에서 11개 노드와 보스 결과·로비 복귀·새 런 PASS. 전투 1·2·5·7·9층, 캠프 3·4·10층, Elite 6층, Event 8층, Boss 11층을 통과했다. |
| 새 런 초기화 | 같은 연결·참가자·캐릭터 선택을 유지하면서 netId `3→120`, `4→121`의 새 객체 생성. 서버와 소유 클라이언트에서 골드 0·빈 인벤토리·진행도 0 확인. |
| 적 데이터 동기화 | 양쪽 전체 `WBH_EnemyInfo`와 체력이 Provider 결과와 일치. 근거리/원거리 HP는 1층 `300/225`, 5층 `360/270`, 9층 `390/292.5`, 11층 보스 `1200`. |
| 보상 중복 방지 | 근거리 20·원거리 15·보스 500 크레딧을 처치자에게 각각 1회 지급. 같은 적에 종료 피해를 두 번 호출해도 중복 지급되지 않았다. 검사용 시작 골드 5,000은 새 런에서 0으로 초기화됐다. |
| 화면·오류 | 호스트 클리어 버튼, 비리더 대기 버튼, 복귀한 2인 로비와 READY 표시 캡처 확인. 최종 두 실행 로그에 Exception·LogError·FAIL 없음. |
| 컴파일·참조 | 세션 규칙 93개 통과. 현재 로비·전투 씬 Missing Script 0. Windows Player V6 빌드 성공. |
| 최신 전용 서버 | 같은 호환 버전 `2026090803`의 Windows Server 타깃 빌드 성공(약 2,796.0 MB). 별도 Gunner Client가 인증·READY 후 Stage1에 진입했다. 서버와 클라이언트 netId 3, 소유 스냅샷·Controller·입력·NavMesh·인벤토리 UI 연결을 확인했다. 이후 적의 공격으로 HP가 0이 되는 동기화도 확인했다. |

근거는 `Builds/MirrorLanTest/RunValidation/`의 위 로그와 `run_host_v6_clear.png`, `run_client_v6_clear.png`, `run_host_v6_lobby.png`다. V5에서 발견한 보스 싱글 보상 지갑 NRE와 한글 폰트 NRE는 V6에서 재발하지 않았다.

최신 전용 서버 접속 검사의 근거는 같은 폴더의 `run_dedicated_v6.log`, `run_dedicated_client_v6.log`다. 두 로그에서 Exception·LogError·FAIL은 발견하지 못했다. 화면 없는 서버의 외부 파티클 메시·Dissolve 셰이더 경고는 남아 있다. 최신 Dedicated Server 검사는 첫 전투 진입까지이며, 위 11개 노드 완주는 Host+Client 개발 빌드에서 검증했다.

이 검사는 **개발 빌드의 명시적 `--mirror-smoke-travel full-run`**에서 실제 적에게 검증용 종료 피해를 준 결과다. 정상 선택 요청, Controller 이동과 포탈 진입, 이벤트·결과 버튼을 사용하며 진행도를 직접 완료시키지 않는다. 실제 무기·스킬 공격으로 보스를 공략한 결과나 전투 밸런스 검증으로 해석하지 않는다. 실행에는 `--mirror-smoke-duration 900`과 서로 다른 프로필을 사용한다.

통신 호환 버전은 `2026090803`이다. 이전 실행 파일과 섞어 사용하지 않는다. V6의 빌드 결과는 Succeeded지만 변경하지 않은 Advanced Dissolve 보스 셰이더 등의 컴파일 오류 16건이 빌드 보고서에 남아 있다. C#·최종 실행의 오류 0과 구분한다. 사용하지 않는 옛 `Act1_Stage1_MirrorCombatTest`에도 기존 Missing Prefab 2개가 남아 있어 현재 시작 경로로 사용하지 않는다.

마지막으로 Editor를 일반 Player 타깃으로 복구하고 C# 컴파일 완료, 세션 규칙·Fighter/Gunner 참조 검사를 다시 통과했다. Console에서 조회한 오류 65개는 모두 변경하지 않은 Advanced Dissolve·Animpic 셰이더 항목이며 C# 오류는 없었다. 코드·문서 `git diff --check`는 통과했다. Unity가 저장한 Scene·Prefab·meta의 빈 값 뒤 공백은 일괄 수정하지 않았다. 검증 프로세스·격리 자격 11개·임시 실행 도구·빌드 생성 파일을 정리했고, 빌드가 바꾼 URP 필터/런타임 목록만 복구했다. 원본 Camp 씬의 Dirty 상태와 사용자의 기존 변경은 보존했다.

### 2026-09-08 실제 Act1 맵·파티 투표·거너 기본 공격

이번 요청은 팀원 원본 스크립트를 수정하지 않고 SW `_MirrorTest` 경계를 확장했다. 원본 API가 필요한 전체 스킬·공통 피해·상태이상·퀘스트는 7절의 변경 요구로 유지한다. 무조건 원본 전체를 복제하지 않고, 독립적인 맵 환경·승강기·투표와 기존 투사체 확장만 분리했다.

- `MirrorAct1SceneRoute_MirrorTest`가 노드 확정 시 실제 Act1 맵을 배정한다. 일반 실행은 새 런마다 임의 seed, 명시적 smoke 실행만 재현 seed를 사용한다. 배정된 `sceneName`과 `usedStageSceneNames`는 동일 스냅샷에 남는다. 보스는 별도 Boss 맵이다.
- `MirrorAct1SceneSetup_MirrorTest`는 Stage2~6/Boss의 원본 Environment·조명·경계·스폰 위치를 SW 템플릿으로 옮긴다. 원본 Stage6이 Act2_Stage4 NavMesh를 참조하므로 SW Stage6 지형만 별도 베이크했다. 원본 씬은 저장하지 않았다. 시작점·포탈의 NavMesh 샘플 성공을 확인했다.
- Stage5 실제 플랫폼은 고정 출발·도착·착지 지점과 기존 `MirrorFourPlayerElevator_MirrorTest`를 사용한다. 현재 접속한 생존 대기자 전원이 탑승하면 상승한다. 위층 도착자는 다음 탑승의 분모에서 제외한다. 도착 위치 보정과 NavMesh 복구 뒤 입력을 돌려준다.
- 기존 StageSelect 노드·Reticle·TMP 글꼴을 사용해 표 수·자기 선택·남은 시간을 표시한다. 로딩 중인 참가자는 투표 분모에 유지하고, 제출은 Ready·플레이어 생성 완료 후 허용한다. 끊김·연결 교체 시 이전 표를 제거하며, 이전 revision의 요청은 거부한다.
- 거너 종류는 서버 장착 아이템에서 결정한다. 라이플은 직선 투사체, 샷건은 90도 범위·대상당 1회, 유탄은 포물선·폭발 판정이다. 적의 기존 네트워크 투사체 이동을 재사용하며 플레이어/적 진단 카운터는 구분한다. 소유자 사망·부재·씬 이동·20초 수명에 탄을 정리한다. 발사 예약 중 무기 교체는 취소하고, 이미 날아간 탄은 발사 때의 종류·속성·속도·사거리와 명중 때의 Stat을 사용한다.
- 파이터 마나는 실제 실행 확정 시 `WBH_PlayerStatus.TryUseMana`를 1회 호출한다. 취소·중복·만료·부족한 마나의 재사용을 막는다. 대시는 서버 실행 승인 후 시작한다. `WBH_PlayerEffect.Initialize`를 기존 씬 스포너 조회 경로에서 호출해 스킬을 먼저 사용해도 VFX가 초기화된다.

Editor 검증: 세션 규칙 93개, 투표 규칙 31개, 맵 배정 25개, 플랫폼 규칙 16개, 실제 서버 Fighter 클래스·각도·마나 경계 10개 통과. 11개 씬의 Missing Script·교차 씬 참조, 전투 씬 7개의 엄격한 참조·스폰·포탈·카메라 연결 및 두 플레이어 프리팹의 필수 참조 검사를 통과했다. 기존 HUD의 끊어진 MP 머터리얼 참조 1곳을 기본 UI 머터리얼로 복구했다. 기존 로비·선택·Camp에는 원본에서 이어진 Particle 머터리얼/이미지 Sprite 누락 18곳이 남아 별도 경고로 보고한다.

Host 실제 전투 검증: Gunner 3종 각각 정상/근접 벽 조건에서 실제 AnimationEvent, HP 감소, 중복 Collider 피해 1회, 중복 이벤트 거부, 서버 Spawn/Host 관찰 카운터 등 24개 통과. 공개 인벤토리·장비 API로 임시 무기를 장착했으며 검사 뒤 아이템·골드 및 임시 객체 정리를 확인했다. 원격 Command 장착이나 원격 전투 검증과는 구분한다. Fighter는 실제 AnimationEvent 기본 공격으로 HP 100000→99994, 슬롯 0 스킬로 99994→99985.5를 확인했다. 이 스킬 데이터의 비용은 0이므로 마나 소비는 별도의 비용 5.9/MP 6 경계 검사로 검증했다. 스킬을 먼저 사용하는 재검사에서 VFX 스포너 연결과 Console error 0을 확인했다.

Editor Host 1인 전체 진행 검사는 **11개 노드 PASS**다. 고정 검사용 시드에서 Stage6→Stage2→Camp→Camp→Stage5→Stage1→Stage4→Unknown→Stage3→Camp→Boss를 실제 서버 씬 전환과 노드 UI·이동 명령·포탈/선택 버튼으로 통과했다. Stage5 탑승 후 y 0.5267→11.6018 상승·위층 NavMesh/조작 복구를 확인했다. 보스 결과 버튼→로비→READY→새 런에서 netId 3→116, 골드 0·아이템 0·진행도 0과 서버 초기화 검사를 통과했다. 적 종료용 개발 피해를 사용한 진행 검사이며 실제 전투 난이도·수동 완주 검증은 아니다. 현재 실행 구간 Exception/FAIL 0. 근거: `Builds/MirrorLanTest/RunValidation/editor-act1-full-run.log`, `editor_clear.png`, `editor_lobby.png`, `vote-ui.png`.

지도 안내와 보스 결과를 Game 캡처로 확인했다. 최종 세션 93·투표 31·맵 배정 25·플랫폼 16 규칙 및 플레이어 두 자산 검사를 통과했다. 11개 씬 Missing Script·교차 씬 참조 검사와 7개 전투 씬의 엄격한 참조 검사를 통과했으며 기존 비전투 씬의 시각 참조 경고 18개는 별도로 남겨 두었다. Editor Console 조회 오류 0. 임시 Editor 씬 목록은 기존 28개로 복구하고 빌드가 변경한 URP 필터·TimeManager 직렬화·미리보기 텍스처와 Build Settings만 되돌렸다. 사용자가 열어 둔 Dirty Camp 씬은 원래 경로·1872개 Transform을 보존하고 원본 파일에 저장하지 않았다.

이 시점에 보류했던 Windows 빌드·4인 검증은 아래 2026-09-09 후속 결과로 갱신한다. 첫 층은 선택지가 하나이므로 분할 투표 검사는 실제 두 번째 분기층까지 진행했다.

### 2026-09-09 실제 Windows EXE 4인·전용 서버 검증

후속 빌드·수정 요청에 따라 앞서 제시한 `WeaponBoneRetargeter.cs` 전체의 `#if UNITY_EDITOR`/`#endif` 두 줄을 적용해 Player에서 EditorWindow 코드가 컴파일되지 않게 했다. 게임 원본 스크립트는 추가 수정하지 않았다. 호환 버전은 `2026090804`이며, 같은 PC의 `127.0.0.1:7777`에서 Host+Client 3개와 **Windows Server 타깃 EXE+Client 4개**를 각각 실행했다. 후자는 `WindowsServer`·Null graphics·서버 UDP 포트 로그로 구분했다.

| 검사 | Host 포함 4인 | 전용 서버 + 4인 |
| --- | --- | --- |
| 실제 맵·전체 진행 | `host-full-04` 전원 통과 | `dedicated-full-01` 전원 통과 |
| 방장 A / 나머지 3명 B 투표 | `host-split-02`: 두 번째 층 B 노드로 전원 이동 | `dedicated-split-01`: 같은 다수결 결과와 서버 확정 통과 |
| 실제 공격·스냅샷·정리 | `host-combat-04`: 공격 10단계·생명주기 4단계 통과 | `dedicated-combat-01`: 같은 14단계와 네 클라이언트 확인 통과 |
| 개인 인벤토리·공동 구매 | `host-inventory-03`: 네 명 개별 UI 흐름·공유 재고 경쟁 통과 | `dedicated-inventory-01`: 같은 검사 통과 |
| 강화 아이템 재접속 | 같은 participant/netId·instance/강화1·골드15,000·HP/MP·조작 복원 | 같은 항목 보존 및 새 connection으로 복원 |
| 잘못된 토큰·런 도중 신규 참가 | 정확한 거절 사유와 미승인·연결 종료 확인 | 서버 부재 확정 후 같은 검사 통과 |
| 실제 적 공격·사망 재접속 | 이번 추가 검사는 전용 서버에서 수행 | `dedicated-death-01`: 실제 적 공격으로 네 명 HP0, Gunner 재접속 후 같은 participant/netId·HP0·입력/NavMesh 차단 유지 |

전체 진행은 Stage6→Stage2→Camp→Camp→Stage5→Stage1→Stage4→Unknown→Stage3→Camp→Boss의 11개 노드를 실제 노드 UI·이동 명령·포털·미지 선택·결과 버튼으로 통과했다. 두 구성 모두 여섯 전투 맵·Camp·Event·Boss 방문 집합을 확인했고, Stage5에서 네 명 모두 약 y0.5→11.6018 상승 후 위층 이동·포털 통과를 확인했다. 보스 결과에서 비방장의 복귀 요청은 서버가 거절하고 방장 버튼으로 전원이 로비에 복귀했다. READY 후 새 런에서 같은 접속자에게 새 netId를 발급하고 이전 아이템·골드·진행도를 제거했다. Host의 netId는 3/4/5/6→126/127/129/128, 전용 서버 클라이언트는 5/4/3/6→130/128/127/129였다.

공격 검사는 각 Fighter의 기본 공격·슬롯0, 각 Gunner의 라이플·샷건·유탄을 실제 소유자 Command와 자연 AnimationEvent로 실행했다. 서버 HP 감소·피해 표시 횟수·공격자와 네 클라이언트 복제값, 중복 확인 거부 및 대상/탄/임시 장비 정리를 검사했다. 각 플레이어는 실제 회복 포션의 HP 증가·충전 1회 차감, HP/MP·버프 동기화, 사망 중 공격/스킬 거부와 부활 후 조작/NavMesh 복원도 통과했다. 사망 이동은 유효한 목적지로 이동 명령을 보낸 뒤 최소 0.5초 동안 매 프레임 경로 없음·속도0·위치 불변을 검사했고, 네 명 모두 변위0이었다. `ResetPath()`가 `isStopped`를 false로 바꾸는 엔진 동작을 확인했으므로 정지 플래그만으로 이동 실패를 판정하지 않는다.

인벤토리는 네 명 각각 이동·회전·교환·거절·장착/해제·판매/구매·삭제/중단·강화·드롭/재획득과 서버 소유 모델을 검사했다. 이어 같은 판매 재고 instance를 동시에 요청해 **성공1·정상 충돌 거절3·소유자1·공유 revision 증가1**을 확인했다. 성공자만 실제 가격이 차감되고 나머지 아이템·강화·골드는 보존됐다. 재접속 비교는 이 공동 거래까지 끝난 상태를 기준으로 했으며, 장착 슬롯·배치·버프 남은 시간 보존까지 검사했다는 뜻은 아니다.

이번 EXE 실행에서 수정한 원인은 다음과 같다.

- 최종 시작점 확정 전에 입력이 열리거나 중복 위치 확정으로 이동 명령이 지워지지 않도록 `MirrorSpawnedPlayerBinder`의 씬별 확정과 입력 복원 순서를 보완했다. 연속 Camp 전환과 실제 포털 이동을 네 명 모두 다시 통과했다.
- Stage5는 폭 0.1m의 `Collider1` 진입선으로 전원 점유를 검사하던 연결을, 기존 발판 영역 `Collider2`(4×1×2m)로 바꿨다. SW 씬과 재생성 코드에만 반영했다. 변경 전에는 회피로 선 밖에 선 Host 때문에 출발하지 않았고, 변경 후 두 구성의 네 명 탑승을 통과했다.
- 검사 도구도 비활성 클라이언트 Collider의 실제 중심, 이동 명령 후 실제 위치 변화, 실제 판매 가격에 맞는 공동 구매 예산을 사용하도록 수정했다. 실패를 통과로 숨기지 않도록 예외·미완료·거절 사유를 엄격히 판정한다.

근거 폴더는 `Builds/MirrorLanTest/OvernightValidation/{검사명}`이며 `processes.json`, `build-hashes.json`, 각 EXE 로그와 재접속 비교 JSON을 보존했다. 전체 진행 화면은 `Builds/MirrorLanTest/RunValidation/host-full-04-p0_*.png`, `dedicated-full-01-p0_*.png`다. 실제 표시 창에서 맵·캐릭터·클리어·로비 화면을 확인했다. 숨긴 창의 검은 캡처는 시각 검증 근거로 사용하지 않았다. 완료 조건을 확인한 뒤 직접 종료한 묶음은 정상 종료 코드까지 통과한 것으로 주장하지 않는다. 강제 종료 직후 기존 연결이 10초 타임아웃 전에 남아 있던 인증 초기 시도는 실패 로그로 보존하고, 서버 `absent=True` 확인 후 다시 검증했다.

최종 Player는 `client-05` 빌드 성공, 전용 서버는 `server-01` 개발 빌드 성공(약 2,884.7MB, 오류0·경고63)이다. Player에는 기존 `WBHTest/Material/Boss_Act_01_Up/Leg.shader`의 **Built-in SubShader 1 오류10·경고69**가 남는다. 실제 SW 보스는 다른 Robot Support 머터리얼을 사용하며, 원본 `WBHTest/Prefabs/Etc/Manager.prefab`의 EnemyPool 참조가 별도 원본 보스와 이 셰이더를 빌드에 포함한다. 효과 없는 SW 셰이더 복사본은 채택하지 않았다. 원본 참조 정리·Built-in 셰이더 수정은 후속 범위이고 깨끗한 Player 빌드라고 표현하지 않는다. Host 인벤토리는 `client-04`에서 검사했으며 이후 `client-05` 변경은 Stage5 탑승 연결과 전투 검사 도구로, 인벤토리 구현은 동일하다.

마감 확인: 아홉 검사 묶음의 기본 실행 로그에서 각각 클라이언트4개의 완료 표시와 예상하지 않은 FAIL/주요 예외0을 다시 집계했다(`OvernightValidation/validation-summary.json`). 11개 씬 Missing Script·교차 씬 참조 및 7개 전투 씬 엄격 참조 검사를 다시 통과했고 C# 컴파일 오류는0이다. Player 타깃 복원 뒤 Editor Console에는 기존 Foliage 중복 keyword·Advanced Dissolve include 경로 등 셰이더 오류65개가 남아 있어 Console 전체 오류0은 아니다. 빌드가 변경한 URP 필터·전역 런타임 목록, 생성 link.xml·임시 씬 백업을 정리했다. 원래 Dirty Camp의 경로·1872개 Transform을 유지하고 파일에 저장하지 않았으며, 기존 사용자 변경을 보존했다. 실제 EXE 검사 프로세스는 모두 종료했다. 김성우 개인 구현 로그를 갱신했고 Commit/Push는 수행하지 않았다.

범위 한계: 전체 진행에서는 종료용 개발 피해를 사용했으므로 수동 전투 난이도 완주와 구분한다. 공격 검사는 별도로 실제 공격 경로를 사용했다. 네 대의 PC·외부 LAN·지연/패킷 손실·성능·모든 스킬/VFX·진화/강화 조합을 검증한 것은 아니다. 거너6스킬·원본 전체 상태이상/피해·퀘스트·패시브 정책과 기존 비전투 시각 참조 경고18개는 7절 및 이전 기록의 후속 항목으로 남는다.

## 2. 실행 준비와 확인 순서

1. 같은 변경본으로 서버와 클라이언트를 준비한다. `MirrorTestNetworkManager.CompatibilityVersion`이 맞지 않으면 참가 인증 단계에서 거부한다. DTO 또는 SyncVar 구조를 바꾼 뒤에는 모든 테스트 프로세스의 빌드를 맞춘다.
2. 시작 씬은 `Assets/SW/TEST/MirrorCombat/Scenes/Lobby_MirrorTest.unity`다. 연결 패널의 Host, Join, Server, Reconnect가 각각 호스트 참가, 클라이언트 참가, 전용 서버, 저장 자격 복귀를 요청한다.
3. 동일 PC 접속 주소는 `localhost`다. 현재 로비 자산의 포트는 `7777`이며 다른 PC에서는 서버 주소와 실제 Transport 설정을 맞춘다.
4. 빌드에는 아래 테스트 씬이 포함되어야 한다. Scene/Build Settings를 임의로 다시 생성·저장하지 말고 현재 구성과 누락 여부부터 확인한다.
5. 한 PC에서 여러 클라이언트를 실행할 때는 각 실행 명령에 `--mirror-profile client1`, `--mirror-profile client2`처럼 서로 다른 이름을 붙인다. 지정하지 않으면 `default`를 사용한다. 호스트가 참가하면 호스트까지 4명 정원에 포함되고, 전용 서버 자체는 참가자가 아니다.
6. 각 참가자가 캐릭터를 선택하고 준비한 뒤 리더가 출발한다. 캐릭터를 변경하면 준비가 해제된다. 로비의 표시 상태가 바뀌는 기준은 서버가 보낸 명부다.
7. 재접속 확인은 명시적 퇴장과 연결 끊김을 구분한다. 퇴장은 자격을 포기하며 Reconnect 대상이 아니다. 테스트 파일의 토큰이나 프로필 JSON을 Console·채팅·이슈에 붙여 넣지 않는다.

| 역할 | 정확한 테스트 씬 경로 |
| --- | --- |
| 로비 | `Assets/SW/TEST/MirrorCombat/Scenes/Lobby_MirrorTest.unity` |
| 노드 선택 | `Assets/SW/TEST/MirrorCombat/Scenes/StageSelect_MirrorSessionTest.unity` |
| 캠프 플레이 | `Assets/SW/TEST/MirrorCombat/Scenes/Act1_Camp_MirrorSessionTest.unity` |
| 전투 Stage1 | `Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage1_MirrorSessionTest.unity` |
| 전투 Stage2 | `Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage2_MirrorSessionTest.unity` |
| 전투 Stage3 | `Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage3_MirrorSessionTest.unity` |
| 전투 Stage4 | `Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage4_MirrorSessionTest.unity` |
| 전투 Stage5 | `Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage5_MirrorSessionTest.unity` |
| 전투 Stage6 | `Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage6_MirrorSessionTest.unity` |
| 보스 | `Assets/SW/TEST/MirrorCombat/Scenes/Act1_BossStage_MirrorSessionTest.unity` |
| 이벤트 | `Assets/SW/TEST/MirrorCombat/Scenes/Unknown_Stage_MirrorSessionTest.unity` |

`SessionCampScene` 상수는 이름과 달리 **노드 선택 씬**을 가리킨다. 실제 캠프 플레이는 `SessionCampGameplayScene`이다. 이동·입력 복구 판단에는 `CurrentSessionRoute`의 `StageSelect`, `Camp`, `Event`, `Combat`을 사용한다.

관련 프리팹은 `Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab`과 `Assets/SW/TEST/MirrorPlayerContext/Prefabs/GunnerNetworkPlayer_MirrorTest.prefab`이다. 전자는 기존 테스트 파일명이며, 그 이름만으로 정식 승격된 자산이라고 판단하지 않는다. `SW/Mirror Test/Validate Lobby Assets`는 자산 참조 확인용이고 `Create KY Lobby`는 생성 작업이므로 단순 실행 준비 때 반복 호출하지 않는다.

자동 실행 확인용 `MirrorSessionSmokeDriver_MirrorTest`도 있다. 일반 실행에서는 명시적인 `--mirror-smoke-role`이 없으면 드라이버가 붙지 않는다. 아래는 실행 파일 경로를 바꿔 사용할 인자 예시다. 자동화도 일반 참가·선택·준비·출발 요청을 사용하며 임의 아이템을 지급하지 않는다. 인자와 값 사이에는 공백을 둔다.

```powershell
& "<Windows 빌드 실행 파일>" --mirror-smoke-role host --mirror-smoke-count 4 --mirror-smoke-duration 60 --mirror-profile host_validation
& "<Windows 빌드 실행 파일>" --mirror-smoke-role client --mirror-smoke-count 4 --mirror-smoke-duration 60 --mirror-profile client1_validation --mirror-smoke-class Gunner --mirror-address 127.0.0.1
```

`--mirror-smoke-role`은 `host`, `server`, `client`, `resume`을 받는다. 추가 참가자는 다른 `--mirror-profile`을 사용한다. `resume` 검사는 끊긴 참가자와 같은 프로필 이름을 사용해야 한다. 60초 실행 확인과 300초 예약 경계 검증은 별도 케이스로 수행한다. 서버·클라이언트 프로세스 재실행 복귀의 결과는 실제 관찰 뒤에 기록한다.

## 3. 코드를 읽는 순서

아래 SW 파일의 공통 폴더는 `Assets/SW/TEST/MirrorPlayerContext/Scripts/`다.

| 순서 | 파일 | 읽을 핵심 |
| --- | --- | --- |
| 1 | `MirrorSessionRoster_MirrorTest.cs` | 참가자 원본, 고정 슬롯, 준비·리더·출발, 재접속 자격과 300초 만료 |
| 2 | `MirrorSessionAuthenticator_MirrorTest.cs`, `MirrorReconnectProfile_MirrorTest.cs` | 플레이어 생성 전 인증, 소유자에게만 자격 응답, 로컬 프로필 저장 |
| 3 | `MirrorTestNetworkManager.cs`, `MirrorSessionLifecycle_MirrorTest.cs` | 같은 NetworkManager의 partial 구현. 명부 요청, 씬 Ready 뒤 생성·복구, 연결 해제, 런 진행도 |
| 4 | `MirrorLobbyBridge_MirrorTest.cs` | KY UI 이벤트를 네트워크 요청으로 바꾸고 서버 스냅샷을 슬롯에 반영 |
| 5 | `PlayerContext.cs`, `MirrorSpawnedPlayerBinder.cs` | 플레이어 소유 상태 연결, 로컬 입력 등록, 부재 캐시, 스냅샷 이후 조작 복구 |
| 6 | `PlayerInventorySync_MirrorTest.cs`, `NetworkShopPlayerState_MirrorTest.cs`, `PlayerRuntimeStateSync_MirrorTest.cs` | 아이템·장비·골드·최종 Stat·체력·버프의 서버 원본과 소유자 표시 |
| 7 | `PlayerCombatAuthority_MirrorTest.cs`, `FighterSkillAuthority_MirrorTest.cs`, `WBH_CombatResolver_MirrorTest.cs` | 입력·애니메이션 확인과 실제 피해 판정의 분리, 취소와 쿨다운 |
| 8 | `PlayerStatInitializationGuard_MirrorTest.cs` | 아직 원본 API로 해결하지 못한 초기화 순서 보완과 제거 조건 |
| 9 | `Assets/Editor/MirrorSessionRulesValidation_MirrorTest.cs` | 네트워크 실행 없이 확인하는 규칙 검사의 범위와 실패 조건 |
| 10 | `MirrorStageVoting_MirrorTest.cs`, `MirrorAct1SceneRoute_MirrorTest.cs` | 참가자별 투표·기한·revision, 노드 확정 시 실제 맵 배정과 보존 |

KY 로비는 다음 기존 파일의 공개 경계로 연결한다. 이 세 파일의 변경 승인은 유지된다.

| 정확한 파일 | 현재 연결 API와 역할 |
| --- | --- |
| `Assets/Scripts/UI/Lobby/KY_MultiplayerLobbyController.cs` | `ConfigureExternalState(string participantId, string displayName)`, `ReadyChangeRequested(bool)`, `CharacterChangeRequested(KY_CharacterId)`, `SetPlayers(...)`. 외부 모드에서는 확정 데이터를 생성·수정하지 않고 의도를 보낸다. null 슬롯 위치를 유지한다. |
| `Assets/Scripts/UI/Lobby/KY_LobbyFlowController.cs` | `ConfigureExternalFlow()`, `ShowLobby()`, `HidePanels()`, `LeaveRequested`. 기본 선택 화면 자동 표시를 억제하고 외부 연결 상태에 따라 패널을 연다. |
| `Assets/Scripts/UI/Lobby/KY_LobbyPlayerSlot.cs` | `ShowPlayer(...)`, `ShowEmpty()`. Selecting 상태는 캐릭터 선택 중으로 표시하고 두 캐릭터 모델을 숨긴다. |

## 4. 서버와 클라이언트의 책임

서버는 참가 자격, 런 참가자 명부, 준비와 리더 권한, 아이템 소유·거래, 쿨다운·피해·체력과 런 진행도의 최종 값을 결정한다. 클라이언트는 입력 의도, 조준과 애니메이션 확인을 보내고 서버 결과를 UI·애니메이션·효과로 표시한다. 로컬 애니메이션이 재생됐다는 사실만으로 적 체력을 줄이지 않는다.

| 코드 표기 | 방향과 의미 | 현재 코드의 예 |
| --- | --- | --- |
| `[Command]` / `Cmd...` | 소유 클라이언트의 요청을 서버에서 실행한다. 요청 자체가 성공을 뜻하지 않으며 서버가 소유권·상태 번호·중복·거리·쿨다운 등을 검사한다. | `CmdRequestAttack`, `CmdRequestSkill`, 인벤토리·상점 거래 요청 |
| `[ClientRpc]` / `Rpc...` | 서버가 관찰 클라이언트들에 표현을 전달한다. 클라이언트가 서버 상태를 확정하는 경로가 아니다. | 스킬 시작 애니메이션과 타격 효과 |
| `[TargetRpc]` / `Target...` | 서버가 지정한 한 연결에 응답한다. | 거래 완료 결과, 소유자 위치 확인 |
| `[SyncVar]`, `SyncList` | 서버가 변경한 값을 클라이언트에 복제한다. Owner 모드는 해당 소유자에게만 전달한다. | 골드, 아이템 스냅샷, 쿨다운 |
| `NetworkMessage` | 특정 플레이어 객체가 없어도 연결 단위 메시지를 보낸다. 인증 단계와 로비 단계에 사용한다. | 참가 인증, 로비 요청·명부 |

로비 공개 스냅샷에는 `participantId`, 슬롯, 이름, 캐릭터·준비·연결 상태가 들어가지만 **재접속 토큰은 들어가지 않는다**. 닉네임과 바뀔 수 있는 `connectionId`를 참가자 본인 증명으로 사용하지 않는다. 32바이트 난수 토큰은 소유 연결에만 응답하고 해당 프로세스의 로컬 프로필에 저장한다.

현재 위치 동기화에는 기존 클라이언트 권한 이동 경계가 남아 있다. 서버가 씬 시작 위치를 다시 확인하고 부재 이동을 정지하는 것과, 모든 이동을 서버 시뮬레이션으로 검증하는 것은 별개다. 후자를 이미 구현했다고 해석하지 않는다.

## 5. 재접속 예시와 상태 보존

예를 들어 출발 당시 슬롯 2 참가자가 서버 경과 시각 100초에 끊기면 예약 마감은 400초다. 399초에 같은 세션·토큰으로 복귀하면 같은 `Member`와 `RuntimeContext`에 새 연결이 붙는다. 정확히 400초부터는 거부한다. 표시 이름이 같거나 새 연결 ID가 이전 ID와 같아도 토큰 검사를 대신하지 못한다. 같은 토큰으로 두 연결이 동시에 복귀하려 하면 이미 연결된 참가자의 두 번째 복귀를 거부한다.

끊김 처리에서는 캐릭터를 즉시 새로 만들거나 인벤토리를 초기화하지 않는다. 서버는 보존 캐릭터를 대상 목록에서 제외하고 부재 상태로 전환한다. Binder는 Renderer·Collider·Controller·NavMeshAgent 상태를 캐시해 숨기고 입력·추적·미완료 공격과 대시를 멈춘다. `NetworkIdentity`, 체력·버프·인벤토리 등 런타임을 유지한다. 복귀 때는 서버가 현재 씬 Ready를 확인한 뒤 연결하고, 입력은 `HasSnapshot && !IsDead && !IsTemporarilyAbsent`를 만족할 때만 허용한다.

상점·인벤토리의 `ServerResetOwnerRequests()`는 새 연결의 요청 번호를 받기 위해 중복 요청 기록만 초기화한다. 아이템·골드·상태 revision을 초기화하지 않는다. 완료한 공격·스킬의 서버 쿨다운과 이미 변경된 마나를 복귀 보상처럼 되돌리지 않는다.

리더 이탈 시 기존 연결 참가자의 입장 순서로 승계한다. 빈 로비 슬롯을 새 참가자가 재사용해도 먼저 들어온 참가자보다 앞서 승계하지 않는다. 복귀자가 기존 리더 권한을 자동으로 가져오지 않는다. 이는 적용할 리더 정책이며 다시 결정할 항목이 아니다.

전원이 부재이고 유효한 예약만 남으면 현재 구현은 `Time.timeScale = 0`으로 게임 시간을 일시 정지하지만, 예약 만료는 실제 경과 시각으로 계속 판단한다. **이 구현은 기존 버프의 `deltaTime` 기반 시간도 멈춘다. 따라서 요구된 서버 시간 기준 버프·쿨다운 경과 유지까지 완성했다고 볼 수 없다.** 각 효과의 시간 원본을 확인해 전원 부재 중에도 요구한 기간 경과가 반영되도록 보완·검증해야 한다. 예약이 모두 만료되면 보존 런타임을 정리하고 새 로비 세션으로 돌아가는 경로가 있다. 이 결합 동작의 실제 플레이 검증은 진행 중이다.

프로필 위치는 `Application.persistentDataPath/MirrorReconnect/{profile}.json`이다. 저장 시각은 연결 종료 시각이 아니므로 오래 플레이했다는 이유만으로 로컬에서 토큰을 300초 만료시키지 않는다. 유효 기간은 서버의 예약 마감이 결정한다. 파일을 저장해도 서버 프로세스가 사라진 뒤 런을 복원하는 기능은 생기지 않는다.

## 6. 승인된 원본 경계와 아직 남은 초기화 보완

최근 검토한 네 원본 파일 중 **`PlayerStat.cs`의 공개 API 변경만 승인·반영**했다. `Assets/WJ_TestPlace/Script/Player/PlayerStat.cs`의 `NotifyValuesChanged()`는 외부 상태 소유자가 최종 수치를 일괄 반영한 뒤 `OnStatChanged`를 한 번 알린다. 재계산·경험치·패시브 정책을 바꾸지 않는다. `PlayerRuntimeStateSync_MirrorTest`는 이 API로 스냅샷 반영을 알린다.

`WBH_CombatManager.cs`, `WBH_PlayerStatus.cs`, `PlayerStatManager.cs` 원본 수정은 보류다. 이번 사용자는 독립 가능한 기능을 `_MirrorTest`로 만들거나 필요한 원본 API 변경을 문서화하도록 허용했다. 이에 기존 SW 테스트 경계를 확장했으며, 세 원본 전체의 새 복제본은 만들지 않았다. 후속 정식 통합은 아래의 원본 공개 경계를 승인된 범위에서 정리한다.

`PlayerStatInitializationGuard_MirrorTest`는 아직 제거하면 안 된다. `T_PlayerController.Awake`가 `PlayerStatManager.Awake` 및 `WBH_PlayerStatus.Awake`보다 먼저 실행될 때 사용할 `Stat`과 소유 `statManager` 참조가 준비되지 않는 문제를 보완한다. 현재는 조기 실행과 Reflection으로 `PlayerStatManager.Stat`의 private setter 및 `WBH_PlayerStatus.statManager` 필드에 최소 참조를 넣는다. `NotifyValuesChanged()` 추가만으로 이 초기화 문제가 해결되지는 않는다. 두 원본의 초기화 API가 멱등적으로 자신의 참조를 확보하고 실제 스폰·재접속 검증을 통과한 뒤, 테스트 프리팹 참조까지 확인해 guard를 제거한다.

## 7. 보류 중인 원본 변경 요구

아래 표에서 보류한 것은 **타 담당 원본 파일의 구체적인 수정 승인**이다. 이미 승인된 기능 정책을 다시 미정으로 돌리지 않는다. 퀘스트는 다음 사용자 요청을 기준으로 구현한다.

- 파티 공용 퀘스트이며 파티 진행도를 공유한다.
- 아무 참가자나 수락할 수 있고 서버가 받은 첫 유효 요청이 승리한다. 퀘스트 수락을 리더 전용으로 제한하지 않는다.
- 캠프 방문마다 파티의 수락·리롤을 각각 1회 허용한다.
- 유효 참가자와 재접속 예약 참가자는 각자 정확히 한 번 보상을 받는다. 인벤토리가 가득 차면 보상을 유실하거나 중복 지급하지 않고 지급 대기로 유지한다.
- 전체 통합 목표는 실제 Act 1의 Stage 1~6·Camp·Unknown·Boss다. SW 맵 연결은 최신 1절을 따르며, 퀘스트·원본 전체 전투 통합은 남아 있다.

최신 사용자 판단에서 미정인 정책은 **패시브를 개인별로 적용할지, 한 참가자의 패시브를 공통 적용할지**다. 서버 패시브·상점 수치 0은 이 결정 전 임시 기본값이며 싱글 프로필을 서버 런타임에 연결하지 않은 상태다. 이 정책 선택과 아래 원본 변경 승인은 별도로 다룬다.

| 정확한 원본 파일 | 현재 문제·연결 한계 | 필요한 최소 변경 | 필요한 검증 | 적용할 정책·구현 경계 |
| --- | --- | --- | --- | --- |
| `Assets/WBHTest/Scripts/Combat/WBH_CombatManager.cs` | `ProcessDamage`가 공통 피해를 계산한 뒤 `ItemTriggerManager.Instance`에 발동을 전달한다. 공격자 개인 효과와 전역 상태가 섞일 수 있다. 현재 SW 검사 경로와 원본 계산 경로가 나뉘어 있다. | 피해 계산의 공통 실행 경계와 공격자 소유 trigger 전달 경계를 제공한다. 호출자가 검증한 소유자를 받되 피해 공식이 두 벌로 자라지 않게 원본을 공통화한다. | 두 플레이어의 피해·치명타 발동이 자기 버프에만 적용되는지, 원본 싱글 피해값이 유지되는지, 클라이언트가 중복 피해를 만들지 않는지 | 효과의 실제 소유자는 공격자. 파티 공유 효과가 있다면 별도 규칙으로 명시 |
| `Assets/WBHTest/Scripts/Player/WBH_PlayerStatus.cs` | `Initialize(T_PlayerController)`가 controller를 넣고 즉시 Stat 기반 속도를 읽지만 자기 `statManager` 준비는 Awake 순서에 기대고 있다. | 공개 초기화에서 자신의 필수 참조를 먼저 확보하고 중복 호출에도 안전하게 동작하도록 한다. 다른 캐릭터의 Manager를 찾지 않는다. | 다른 Awake 순서, 런타임 스폰, 재활성화·재접속, null 참조·중복 구독 여부 | 초기화 책임을 이 컴포넌트와 소유 `PlayerContext` 사이에서 명확히 분배 |
| `Assets/WJ_TestPlace/Script/Player/PlayerStatManager.cs` | Stat 생성 시점이 Awake에 있고 패시브 계산·구독은 `PassiveSkillManager.Instance`를 읽는다. 멀티의 플레이어별 원본과 초기화 순서가 확정되지 않았다. | 이미 만든 Stat을 덮어쓰지 않는 초기화 경계와 패시브 소스 연결 경계를 제공한다. 싱글의 기존 소스를 유지하되 멀티의 권한 있는 입력을 구분한다. | 서로 다른 장비·버프·패시브를 가진 2인, 스폰·재접속, 싱글 회귀, 중복 초기화로 체력·레벨 초기화되지 않음 | 패시브 개인 적용 또는 특정 참가자 기준 공유, 저장 원본, 변경 시점, 공유 기준 참가자의 이탈 처리 |
| `Assets/WJ_TestPlace/Script/Player/Skill/FighterSkillController.cs` | 로컬 입력·애니메이션 대기·진화·강화·실제 실행이 연결되어 있다. SW 파이터 검증 경로가 원본 전체 기능과 같다고 볼 수 없다. | 검증된 aim·slot과 서버가 보유한 evolution·enhancement로 실행하는 공개 경계. 피해와 이동·표현 책임을 분리하며 새 원본 복제는 만들지 않는다. | 각 shape와 진화·강화, 애니메이션 미확인·취소, 중복 요청, 쿨다운·자원 유지, 타인 캐릭터에 피해 권한이 섞이지 않음 | 서버가 소유하는 스킬 선택·진화·강화 원본과 차징·취소 규칙 |
| `Assets/WJ_TestPlace/Script/Player/Skill/GunnerSkillController.cs` | SW 경로에서 라이플·샷건·유탄 기본 공격은 연결했지만 거너 스킬은 미연결이다. 원본의 SectorSlash, LineSlam, Dash, ArcProjectile, BombThrow, BackstepShot 총 6개 shape를 서버 검증 완료로 취급할 수 없다. | 검증된 aim·slot 및 서버의 진화·강화 상태로 실행하는 경계. 서버 피해·투사체 생성과 클라이언트 애니메이션·효과를 나눈다. | 6개 shape 각각의 발사·충돌·피해 1회성, 진화·강화, 다인 관찰, 발사 중 끊김·재접속, 투사체 수명 | 투사체 권한, 자원·쿨다운 시점, 복귀 시 지속 투사체의 소유 처리 |
| `Assets/WJ_TestPlace/Script/Buff/TriggeredBuffUniqueEffectSO.cs` | `PlayerBuffManager.Instance`, `Time.time`, SO 내부 쿨다운 저장 및 `ItemInstance.persistedStackCount`를 함께 사용한다. 플레이어·세션 간 공유 상태와 시간 원본을 구분해야 한다. | 명시적인 owner·서버 시각·아이템 스택 실행 경계를 제공하고 런타임 기록의 소유 위치를 분리한다. 장착·해제·발동도 같은 소유자를 사용한다. | 같은 SO를 쓰는 두 소유자, 아이템별/공유 중복 정책, 전원 부재 중 서버 시간 경과, 재장착과 양도 시 스택 유지, 세션 초기화 | 소유자와 아이템의 스택 기록을 보존하고 서버 시간으로 효과·쿨다운 기간을 유지. 전원 부재의 timeScale 정지만으로 이 요구를 완료 처리하지 않음 |
| `Assets/WJ_TestPlace/Script/Quest/QuestBoardNPC.cs` | NPC가 로컬 의뢰 추첨·리롤·수락 방문 상태를 보유하고 Singleton UI·QuestManager를 호출한다. | 서버가 정한 제안·방문 상태를 표시하는 경계와 수락·리롤 의도 이벤트를 제공한다. 로컬 추첨이 서버 결과를 덮어쓰지 않게 한다. | 동시 수락에서 첫 유효 요청만 성공, 캠프 방문당 수락·리롤 각 1회, 캠프 재방문, 늦은 표시 갱신, 재접속 후 남은 방문 횟수 | 확정: 파티 공용, 아무 참가자나 수락 가능, 첫 유효 요청 승리, 캠프 방문마다 수락·리롤 각 1회. 남은 승인은 이 원본의 구체적 수정 |
| `Assets/WJ_TestPlace/Script/Quest/QuestManager.cs` | 진행·완료와 보상 지급이 로컬 Singleton 및 `InventoryController.Instance`를 기준으로 한다. | 서버의 파티 진행 원본과 참가자별 보상 기록·지급 대기 경계, 표시용 목록·진행 스냅샷 반영 경계를 제공한다. | 두 명 이상이 동시에 완료 조건 충족, 유효·재접속 예약 참가자별 보상 정확히 1회, 인벤토리 가득 참→공간 확보 후 지급, 재접속 중 중복·누락 방지 | 확정: 파티 진행 공유, 유효·재접속 예약 참가자 각자 1회 보상, 인벤토리 가득 차면 지급 대기. 남은 승인은 이 원본의 구체적 수정 |
| `Assets/Scripts/UI/Popup/Quest/KY_QuestPopup.cs` | private `quests` 목록과 private `RefreshList()`로 표시하며 서버 목록을 전달하는 공개 경계가 없다. | 서버 파티 공용 목록을 복사·반영하는 공개 API. 팝업은 수락·진행·지급 대기 상태를 소유하지 않는다. | 팝업 닫힘 중 수신, 재오픈 최신 공용 목록, 빈 목록·변경·풀 반환, 복귀자의 진행·보상 상태 표시 | 확정된 파티 공용 퀘스트를 표시. 남은 승인은 이 UI 원본의 공개 반영 API 수정 |
| `Assets/Scripts/StageSelect/YJ_StageSelectManager.cs` | Start에서 로컬 저장을 읽거나 맵을 생성한다. SW 어댑터는 서버 스냅샷과 투표를 연결했으며 정식 통합 시 초기화 순서를 공통 API로 정리해야 한다. | 맵 데이터·프리팹 준비와 로컬 저장 로드·새 맵 생성을 분리하는 공개 초기화 경계. 멀티에서는 서버 진행 스냅샷을 기준으로 표시한다. | 새 런, 다른 로컬 세이브를 가진 2인, 씬 복귀·재접속, 시드·노드 ID·선택·방문·클리어 일치, 실제 Act 1 경로 | 참가자별 1표·변경 가능, 최다 득표 선택, 동률 서버 추첨. 리더만 선택하던 이전 정책은 이번 요청으로 대체됨 |
| `Assets/Scripts/Environment/Movement/YJ_PointMove.cs` | 원본은 로컬 플랫폼 상태다. SW Stage 5 복사 씬은 기존 Mirror 승강기 어댑터로 서버 탑승·이동을 연결했으며 원본을 수정하지 않았다. | 정식 통합 시 서버가 활성화·경로·정지·리셋을 결정하는 실행 경계와 클라이언트 표시 반영. 승객과 NavMesh 조작의 소유자를 명시한다. | 다인 탑승·내림, 이동 중 끊김·사망, 도착 NavMesh 복구, 클라이언트 중복 활성화 거부 | 살아 있고 연결된 대기 승객 전원 탑승 후 이동, 도착한 승객은 다음 탑승 조건에서 제외 |

추가 시간 경계가 필요한 원본은 `Assets/WJ_TestPlace/Script/Player/PlayerBuffManager.cs`다. private `Update()`가 `tracker.Tick(Time.deltaTime)`을 호출하므로 전원 부재의 timeScale 0에서 버프도 멈춘다. 동일 tracker를 재사용하는 공개 시간 진행 경계를 제공하고, 네트워크 캐릭터는 서버만 실제 경과 시간을 전달하도록 해야 한다. 싱글의 기존 시간 정책은 유지하며 클라이언트 표시가 중복 Tick을 만들지 않게 한다. 살아 있는 다른 참가자의 유무, 예약 중 만료, 복귀 직후 잔여 시간·재계산을 확인해야 한다. 이것도 별도 원본 수정 승인 대상이며 Manager 복제나 Reflection 접근으로 우회하지 않는다.

상태이상의 별도 원본 승인 대상은 `Assets/WBHTest/Scripts/StatusEffect/WBH_EnemyStatusEffectController.cs`다. 현재 `PlayStatusEffect()`는 `effectSpawner`가 없는 경우에도 `SpawnPersistentEffect()`를 호출한다. 먼저 이 시각 재생 경계를 null-safe하게 만들고 원본 상태이상 규칙은 계속 적용되게 해야 한다. 이어 SW `NetworkEnemyAuthority_MirrorTest`의 `originalStatusEffectsReady = localEffectSpawnerInitialized` 의존을 제거한다. 현재 이 값은 주석 처리된 로컬 VFX 초기화에 매여 있으므로 전용 서버 빌드·피해 처리 통과만으로 상태이상까지 적용된다고 볼 수 없다. 원본 승인 후에는 효과 스포너 유무 두 조건에서 Burn·Slow·Stun의 상태 변화와 만료를 같은 입력으로 검사하고, VFX 누락이 피해·상태 처리를 중단하지 않는지 확인한다. 원본 승인 전에 SW의 guard만 해제하면 null 예외를 만들 수 있어 이번에는 변경하지 않았다.

### 패시브 결정을 적용할 위치

개인 적용이라면 참가자별 패시브 원본을 해당 `PlayerContext`의 Stat·발동 효과·상점 혜택에만 공급한다. 공통 적용이라면 기준 참가자를 참가자 ID로 고정하고 그 참가자의 패시브 구성을 서버가 각 유효 참가자에게 계산해 공급한다. 전용 서버에는 호스트 캐릭터가 없으므로 기준을 단순히 `Host`나 현재 로컬 Singleton으로 찾으면 안 된다. 기준 참가자 선정, 리더 변경·연결 끊김·명시적 퇴장 때의 유지 여부, 런 도중 패시브 변경 허용 시점을 결정해야 한다.

두 경우 모두 클라이언트가 최종 `StatSet` 수치를 보내는 이전 경로는 복구하지 않는다. 서버가 허용된 패시브 ID·단계와 데이터 정의로 계산하는 입력 경계가 필요하다. 신뢰할 수 있는 패시브 저장 원본의 연결은 아직 구현되지 않았다. 현재 0 기본값은 이 입력 경계와 정책이 정해질 때까지의 임시 상태다.

보류한 세 파일의 최소 구현 순서는 다음과 같다. 메서드 이름은 제안이며 기존 API 확인 뒤 최소 변경으로 확정한다.

1. `PlayerStatManager`: `EnsureInitialized()` 성격의 멱등 초기화로 이미 만든 Stat을 덮어쓰지 않고 자신의 필수 참조를 확보한다. 패시브 공급 방식은 이 초기화와 별도 설정으로 받으며, 설정을 적용한 뒤 기존 `Recalculate()`를 한 번 사용한다. 싱글 기본 흐름을 유지하고 네트워크 프리팹만 외부 원본을 선택한다.
2. `WBH_PlayerStatus`: 기존 `Initialize(T_PlayerController)` 내부에서 자신의 `PlayerStatManager`와 체력 참조를 먼저 확보한다. controller 연결 후 이동 속도를 읽으며, Awake 순서나 다른 플레이어의 Singleton에 의존하지 않게 한다. 위 두 변경과 실제 생성·재접속 검증 후 `PlayerStatInitializationGuard_MirrorTest`를 제거한다.
3. `WBH_CombatManager`: 기존 `ProcessDamage`의 계산·적용 경계를 SW에서도 호출할 수 있게 하고, 공격자 소유 발동 효과 수신자를 명시적으로 전달한다. `DamageTakenModifier`, 피격 위치·방향·EffectData까지 같은 경로로 처리한 뒤 별도 Mirror 피해 공식의 삭제 가능성을 검증한다. 동일 입력의 싱글/서버 피해 비교와 공격자별 효과 분리 검사를 먼저 통과해야 한다.

`PlayerStat.NotifyValuesChanged()`만으로 위 초기화·패시브·피해 경계가 해결되는 것은 아니다. 현재 승인한 API는 서버 스냅샷 적용 후 알림에만 사용했다.

## 8. 구현 재개 순서와 승격 기준

1. 최신 구현과 검증은 위의 실제 Act1 맵·파티 투표·거너 기본 공격 절을 기준으로 삼는다. 이전 빌드·4인 Stage1·재접속 기록은 당시 버전의 결과이며 최신 코드 검증을 대신하지 않는다.
2. 호스트 포함 4인 또는 전용 서버+4인의 실제 프로세스 검증을 수행한다. 정원 초과, 중도 참가 거부, 동시 복귀, 299초/300초, 리더 이탈, 전원 부재, 사망 상태 복귀를 각각 확인한다. 보존 전후 인벤토리·장비·골드·체력·버프·쿨다운을 비교한다. 전원 부재 시 timeScale 정지로 멈추는 버프 시간 경계를 보완하고 서버 시간 기준 기간 경과를 별도로 검증한다.
3. 패시브 정책을 팀이 확정한 뒤 `PlayerStatManager`의 입력 원본과 초기화 API 변경안을 구체화한다. `WBH_PlayerStatus`의 초기화와 공통 피해/owner trigger 변경을 같은 연결 흐름에서 정리하고, 해당 원본 수정 범위의 승인이 정해지면 구현한다. 승인 보류를 새 복제 파일로 우회하지 않는다.
4. 공통 전투 경계가 안정되면 원본 파이터·거너의 실행 API를 연결한다. 거너 6개 shape를 순서대로 서버 판정·관찰 표현·끊김 정리까지 확인한다. 외형이 보인다는 이유로 전투 완료 처리하지 않는다.
5. 이미 승인된 파티 공용 퀘스트·수락·리롤·개별 보상·지급 대기 요구를 구현 기준으로 삼는다. NPC → QuestManager → KY 팝업의 구체적인 원본 API 수정 범위 승인이 정해지면 연결한다. 퀘스트 정책 자체를 다시 결정하는 단계는 두지 않는다. SW의 StageSelect 투표와 Stage 5 플랫폼 연결은 원본 승격 경계가 정해지면 공통 API로 옮기고 실제 Act 1 전체 경로를 다시 검증한다.
6. 원본 단일 계산 경로, 싱글 회귀, 다인 실제 전투, 재접속 상태 보존, 씬·프리팹 참조 검증이 모두 끝난 뒤 정식 승격 대상을 정한다. 그전에는 `_MirrorTest` 접미사와 필요한 초기화 guard를 유지한다. 삭제는 코드 참조와 Scene/Prefab 직렬화 참조를 함께 확인한 뒤 수행한다.

후속 작업자는 확정된 퀘스트·리더 요구를 유지하되, 노드 선택은 이번 사용자가 요청한 참가자 투표 정책을 적용한다. 미정인 패시브 적용 방식과 원본 API의 구체적 수정 승인을 구분한다. 이 문서에 명시한 확정 요구는 구현 기준이며, 보류 원본의 변경 승인을 대신하지 않는다. 승인된 파일 범위에서 구현을 재개한다.
