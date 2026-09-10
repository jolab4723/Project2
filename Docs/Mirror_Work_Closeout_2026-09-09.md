# Mirror 작업 종료·다음 작업 인계 — 2026-09-09

> 9월 10일 계정 변경 인계가 추가됐다. 현재 작업 상태와 다음 실행 순서는 [최신 중단·인계 문서](Mirror_Work_Closeout_2026-09-10.md)를 먼저 확인한다.

## 1. 오늘 종료 기준

사용자가 “이번 빌드 후 검증까지 하고 마무리, 나머지는 내일 진행”하도록 요청하여 기능 추가를 종료했다. 이번 작업을 **정식 멀티 전환 완료**로 판단하지 않는다. 오늘 끝난 구현, 실제 통과한 검사, 보류한 항목을 아래에서 구분한다.

- 브랜치: `codex/unity-6000-3-22-test`.
- 종료 시 HEAD: `3751cdde`의 현재 작업 트리. 이전 조사는 `8861c837`에서 시작했으며 작업 중 갱신된 팀 변경을 포함한다. 이 작업에서 Commit·Push하지 않았다.
- Unity: 6000.3.22f1, 프로젝트 `Project2`.
- 최종 검증 구성: 같은 PC의 Windows Player **Host 1 + Client 3**. 실제 별도 프로세스 네 개다.
- 팀원 원본 설명: [아주 쉽게 풀어 쓴 수정 내용](Mirror_Team_Code_Changes_2026-09-09.md).
- 저장 담당자 전달 사항: [DataManager·SettingManager 인계](Mirror_BH_Manager_Handoff_2026-09-09.txt).

이 문서가 아래 이전 조사·검증 문서의 현재 상태 설명보다 우선한다. 이전 문서의 “아직 미구현”, “승인 필요”는 당시 시점의 기록일 수 있다.

## 2. 이번에 연결한 기능

### 원본 파이터·거너 스킬

`FighterSkillAuthority_MirrorTest`가 두 캐릭터의 기존 스킬 컨트롤러를 서버에서 실행한다. 원본 스킬 공식을 별도로 재작성하지 않는다. 클라이언트는 슬롯·조준 방향·목표 위치·차징 해제 등을 요청하고 서버가 사용 가능한 상태, 마나·쿨다운·스택 등을 확인한다.

서버 Animator의 실제 AnimationEvent가 기존 스킬을 실행한다. 같은 이벤트의 중복 실행을 막되, 차징 다단히트의 서로 다른 여섯 이벤트는 유지한다. 클라이언트 AnimationEvent는 피해 승인 요청을 보내지 않는다. 원본 스킬 창은 로컬 플레이어의 어댑터에 `Bind(...)`하고, 진화·강화·쿨다운·스택 값을 서버 상태로 표시한다.

대시·백스텝 동안에는 기존 클라이언트 위치 입력이 서버 이동을 덮어쓰지 않게 한다. 끝난 위치를 소유자에게 전달하고 확인 응답을 받은 후 이동 입력을 복구한다. 스킬 중 원본 NetworkAnimator의 클라이언트 애니메이션 갱신이 서버의 스킬 애니메이션과 충돌하지 않도록 해당 구간의 전송을 제어한다. 일반 이동 전체를 서버 권한으로 전환한 작업은 아니다.

핵심 파일:

- `Assets/SW/TEST/MirrorPlayerContext/Scripts/FighterSkillAuthority_MirrorTest.cs`
- `Assets/SW/TEST/MirrorPlayerContext/Scripts/WBH_PlayerAnimation_MirrorTest.cs`
- `Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerNetworkTransform_MirrorTest.cs`
- `Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerActionInputHandler_MirrorTest.cs`
- `Assets/SW/TEST/MirrorCombat/Scripts/MirrorCooldownHud_MirrorTest.cs`
- `Assets/Editor/MirrorSkillSceneSetup_MirrorTest.cs`

### 거너 투사체의 화면 표시

원본 거너가 서버에서 만드는 투사체와 대응하는 시각 프리팹을 연결했다. 서버의 실제 투사체 위치·회전·크기·수명을 화면용 네트워크 객체에 전달한다. Host는 원본 표현을 사용하고 원격 클라이언트는 시각 복제본을 사용한다. 원격 시각 복제본이 별도의 피해 판정을 만들지 않는다.

`NetworkSkillPresentation_MirrorTest`, `NetworkSkillVisual_MirrorTest`와 `Assets/SW/TEST/MirrorCombat/Prefabs/SkillVisuals/`가 담당한다. 원본 사운드 컴포넌트는 사용자 지시대로 유지했다. **명중·시간차 폭발의 월드 이펙트 전달은 보류 상태**다. 투사체 및 피해 검사 통과를 폭발 연출 전체 완료로 해석하지 않는다.

### 상태이상과 피해 연결

원본 `WBH_EnemyStatusEffectController`는 효과 스포너가 없어도 상태이상 규칙을 처리할 수 있게 했고, 화상은 기존 `WBH_DamageResult` 피해·사망 경로를 사용한다. SW 적의 준비 상태 연결도 이를 사용할 수 있도록 변경했다.

적이 피해를 받았을 때 공격자의 `PlayerContext`를 찾아 그 플레이어의 타격·치명타 아이템 효과에 전달하도록 SW 공통 피격 경계를 연결했다. 유물의 영구 스택 누락까지 해결한 것은 아니다.

### 퀘스트와 UI

- `QuestManager`의 진행 계산 함수를 재사용하는 SW 파티 퀘스트 상태를 구현했다.
- 서버의 첫 유효 수락, 캠프 방문별 수락·리롤, 개인별 보상 잔여 상태를 연결했다.
- NPC의 기존 버튼은 서버에 요청하고 서버가 확정한 제시값을 표시한다.
- KY 퀘스트 목록·상세 팝업에 서버 표시 데이터를 전달한다.
- 보상 슬롯은 총수량·미수령 수량·수령 완료 체크를 표시한다.
- 공용 `Assets/Resources/Prefabs/UI/Popup/QuestRewardSlot.prefab`을 만들고 정식 `QuestDetailPopup.prefab`에도 연결했다. 이동 전 GUID를 보존했다.
- 실제 내 플레이어의 정보·툴팁·미니맵·현재 지역을 HUD에 연결했다.
- 최신 캠프 UI를 SW 전투 씬에 배치하고, 메뉴를 열었을 때 로컬 전투 입력을 차단한다. 멀티 Pause는 게임 시간을 멈추지 않는다.
- 플레이어 머리 위에는 서버 참가 번호와 닉네임을 표시한다. 이 이름표는 앞선 Host+Client 및 재접속 검사에서 확인했다.

파티 보상 지급·보류·재접속 전체를 이번 최종 4인 스킬 검사에서 검증한 것은 아니다. 해당 시나리오는 내일의 필수 검사다.

## 3. 이번 최종 실행 결과

증거 폴더: `Builds/MirrorSkillValidation/Logs2/`.

| 검사 | 실제 확인한 내용 | 결과 |
| --- | --- | --- |
| 기본 전투 10단계 | 네 플레이어의 소유자 요청, 실제 공격 애니메이션, 적 HP·공격자·피격 횟수 복제, 장비·아이템 정리 | 통과 |
| 파이터 스킬 12조합 | 슬롯 3개 × 기본/진화 1/2/3. 서버 승인, 종료, 네 화면의 상태 일치 | 통과 |
| 거너 스킬 12조합 | 슬롯 3개 × 기본/진화 1/2/3. 기존 피해·투사체 수명, 네 화면의 상태 일치 | 통과 |
| 파이터 차징 | 실제 원본 AnimationEvent에 따른 피해 6회 | 통과 |
| 대시 | 이동 전용 스킬이 적에게 피해를 만들지 않음. 이동 후 소유자 확인, 조작 복구, 1초 후 위치 되돌림 없음 | 통과 |
| 백스텝 | 원본 발사와 이동 종료 뒤 소유자 조작 복구, 위치 복제 | 통과 |
| 진화·강화 선택 | 해당 소유자 선택을 서버와 네 클라이언트가 확인. 다른 참가자의 선택은 유지 | 통과 |
| 생명주기 4단계 | 참가자마다 포션·버프·사망·부활. 사망 중 이동 명령 차단과 부활 후 조작/NavMesh 복구 | 통과 |

최종 서버 출력은 `PASS server steps=38 clients=4 actual owner commands/animation/HP/cleanup`이다. Host와 원격 Client 세 개 모두 `PASS client steps=38 replicated HP and cleanup`을 출력했다. 검사 자체의 FAIL은 네 로그 모두 0건이다.

스킬 24조합은 캐릭터별 대표 한 명씩 실행했다. 기본 전투와 생명주기는 네 명 모두 검사했다. 진화·강화의 모든 가능한 교차 조합, 모든 장비·공격속도·거리 조건까지 전수 검사한 것은 아니다. 생명주기 검사의 치명 피해는 개발 검사에서 직접 가한 것이며 실제 적 공격 증거와 구분한다.

## 4. 실행 중 남은 오류

`SciFiArsenal.SciFiPitchRandomizer.Start()`의 NullReferenceException이 Host 4건, 각 원격 Client 3건씩 발생했다. 위치는 다음과 같다.

`Assets/Resources_GoogleDrive/VFX/Sci-Fi Arsenal/Sci-Fi Effects/Scripts/SciFiPitchRandomizer.cs:14`

사용자가 용준님의 사운드 작업을 알리고 보존을 요청하여 원본 컴포넌트를 제거·비활성화하지 않았다. 따라서 **검사 38단계 통과와 런타임 오류 0건은 다른 주장**이다. 이번 결과는 오류 없는 게임 전체 완료가 아니다.

최초 실행에서 발생한 `YJ_NameTag.ChangeText` 예외는 아래 재시도 로그에서는 0건이다. 다만 최종 자동 검사는 모든 NPC를 마우스로 가리키는 전수 검사가 아니므로, 다음 수동 확인 때 이름표 hover도 확인한다.

## 5. 빌드 기록을 읽는 방법

- 최초 전체 빌드: 성공, 오류 항목 16·경고 71. 기존 보스 Advanced Dissolve 셰이더의 Built-in Subshader 컴파일 오류가 포함되어 있다. `BuildMessages.txt`에 보존했다.
- 증분 빌드 요약 `BuildSummary2.txt`: 성공, 오류 0·경고 1.
- 마지막 Unity BuildReport: 성공, 오류 0·경고 24. 전체 경고는 `BuildMessagesFinal.txt`, 요약은 `BuildSummaryFinal.txt`에 보존했다. 외부 패키지 경고 등이 포함되어 있다.

빌드 대기 중 일부 MCP 응답은 시간 초과로 끝났지만 Unity의 작업은 계속 진행됐다. 결과는 호출 성공 여부가 아니라 실제 BuildReport와 실행 로그로 판정했다. 앞선 오류가 증분 빌드에 다시 출력되지 않았다는 이유로 보스 원본 셰이더를 수정했다고 주장하지 않는다.

실행 파일: `Builds/MirrorSkillValidation/Player/MirrorLanTest.exe`.

마지막 산출물의 어셈블리 메타데이터에서도 임시 유물 스택 함수가 포함되지 않았음을 확인했다. 오늘 종료한 코드에는 그 미검증 작업을 채택하지 않았다.

## 6. 처음 실패했던 검사와 재검증

### 이동 전용 대시의 잘못된 기대값

첫 `Logs/host.log` 검사에서는 파이터 슬롯 2 기본 대시 후 적 HP 감소를 기다리다 실패했다. 원본 `ExecuteDash`에는 타격 호출이 없고 이동·진화 효과만 있다. 따라서 게임 코드를 공격 스킬로 바꾸지 않고, 검사 조건을 “이동함·적 HP 유지·위치 및 조작 복구”로 수정했다. `Logs2`에서 대시 네 조합이 통과했다.

### NPC 이름표 초기화

SW UI 생성기가 참조 중인 `NameTag1`을 비활성 상태로 저장하여, `YJ_NameTag.Awake`가 텍스트를 준비하기 전에 hover가 `ChangeText`를 호출할 수 있었다. 기존 `YJ_OutlineOnMouseHover.Start`가 숨김을 처리하기 전에 이름표 Awake가 실행되도록 SW 생성기의 초기 활성 상태를 수정하고 해당 씬들을 갱신했다. JYJ 원본 스크립트는 수정하지 않았다.

### 앞선 Editor 검사

- 카메라 없는 원본 API 검사 42개: 조준 전달, 잘못된 입력, 차징 마나 처리, 중복 해제, 소유자 범위, 폭탄 목표 위치, 스킬 취소, 퀘스트 진행 계산, 스포너 없는 상태이상과 치명 화상 등을 검사했다.
- Editor Host에서 Fighter 12·Gunner 12조합과 파이터 스택 소모/재충전을 확인했다. 기록은 `FighterHost.txt`, `GunnerHost.txt`다.
- 공용 퀘스트 보상 슬롯은 진행 중·미수령·수령 완료 세 상태와 Missing 참조를 확인했다. 화면은 `Temp/MirrorValidation/QuestRewardSlot_Common.png`다.

위 검사는 수행 당시의 범위다. 최종 EXE의 4인 스킬 검사가 이를 전부 다시 실행한 것은 아니다.

## 7. 내일 가장 먼저 할 일

1. **현재 변경과 사용자/팀원 추가 변경부터 비교**한다. 아래 검증 파일과 미커밋 코드를 기반으로 이어가며 이전 브랜치·프리팹으로 덮어쓰지 않는다.
2. **같은 최신 코드로 전용 서버 + Client 4**를 빌드·실행한다. 오늘 최종 검사는 Host 구성이다. 카메라 없는 전용 서버, 원격 파이터 대시·차징, 스킬 중 끊김·사망·재접속을 확인한다.
3. **사운드 작업 결과를 확인**한다. `SciFiPitchRandomizer`를 임의 삭제하지 말고 담당자의 AudioSource 연결과 비교한다.
4. **폭발 효과 전달을 상의**한다. `WBH_PlayerEffect.cs` 원본 변경은 보류 상태다. 피해와 별개로 유탄 명중·폭탄 첫/두 번째 폭발이 모든 관찰 화면에서 한 번씩 보이는지 확인한다.
5. **유물의 저장 스택을 연결하고 검증**한다. 아래 8절이 조사 결과다.
6. **퀘스트 실제 다인 흐름을 검증**한다. NPC 대화 → 동시 수락/리롤 → 실제 처치/획득 → 네 명 보상 → 가방 부족 대기 → 공간 확보 후 지급 → 재접속 → 중복 지급 없음. 다음 Act와 최종 Act를 구분한다.
7. **최신 UI 수동 회귀**를 한다. 스킬 창·상태창·ESC/설정·채팅·인벤토리 입력 차단, NPC hover, StageSelect 투표·선택 대기·재접속 표시를 확인한다.
8. **개인 저장과 최종 정산**은 DataManager/SettingManager 담당자와 경계를 정한 뒤 진행한다. 아래 보류 목록을 새 승인으로 간주하지 않는다.

실제 4대 PC의 LAN, 인위적 지연·손실에서의 이번 스킬 변경, 성능·밸런스, 전투 시각 품질의 전수 검사는 별도 남아 있다.

## 8. 유물 스택 조사 결과 — 코드 미반영

추가 연결 코드를 잠시 작성했으나 사용자의 오늘 종료 요청에 따라 그 변경만 걷어냈다. 기존 변경은 유지했다. 다음 세 SW 파일에서 이어갈 수 있다.

| 파일 | 현재 누락 | 최소 후속 방향 |
| --- | --- | --- |
| `ItemTriggerManager_MirrorTest.cs` | 발동 후 `ApplyBuff`만 호출하여 `ItemInstance.persistedStackCount`를 올리지 않는다. | 원본 `TriggeredBuffUniqueEffectSO`의 최대 스택 규칙에 맞춰 아이템 수를 갱신하고 기존 `SetBuffStack` 사용. 현재 플레이어별 쿨다운 딕셔너리 유지. |
| `PlayerRelicEffectProvider_MirrorTest.cs` | 기존 소유권 이벤트는 있으나 TriggeredBuff의 제거·복원 처리가 없다. | 기존 switch/소유권 해제에 해당 효과 처리를 넣는다. 별도 이벤트 구독 매니저를 만들지 않는다. 서버가 활성 버프를 결정한다. |
| `PlayerInventorySync_MirrorTest.cs` | `CreateItemInstance`와 `ToSnapshotJson`에서 기존 `ItemSaveData.persistedStackCount`를 누락한다. | 기존 필드를 양방향으로 전달한다. 발동 중 바뀐 스택도 소유자 스냅샷에 반영하고 드롭·재획득을 검사한다. |

필수 검사: 같은 효과를 가진 두 플레이어의 독립 발동, ShareCooldown/PerItem 차이, 최대 스택, 실제 소유권 상실 시 제거, 재획득·드롭 후 복원, 클라이언트 표시, 재접속 보존. 원본 `TriggeredBuffUniqueEffectSO`나 저장 자료형을 먼저 고쳐야 하는 상황은 현재 확인되지 않았다.

## 9. 승인·보류와 확정된 요구

- 승인받아 변경한 원본: BH 2개, WJ의 PlayerStatManager·Fighter/GunnerSkillController·SkillPopupController·QuestManager·QuestBoardNPC, KY/UI 관련 파일. 정확한 파일별 설명은 팀원용 문서를 따른다.
- `DataManager.cs`, `SettingManager.cs`는 수정 금지 요청을 유지했다.
- `WBH_PlayerEffect.cs` 수정은 팀 상의 전까지 보류한다.
- 사운드 원본과 관련 컴포넌트는 보존한다.
- `PlayerBuffManager.cs`는 변경하지 않았다. 전원 부재 시 게임과 각자의 버프 시간이 함께 멈추는 기존 동작이다. 개인별 부재와 전원 부재를 혼동하지 않는다.
- 퀘스트는 파티 공용 진행, 첫 유효 수락, 캠프 방문별 파티 1회 수락·리롤, 참가자별 보상과 공간 부족 시 지급 대기가 기준이다.
- **다음 Act가 있으면 런 상태와 보상 대기를 유지한다. 마지막 Act에서만 아이템을 정리하고, 패시브와 공용인 크레딧은 가지고 나온다.** 현재 Mirror의 Act1 테스트 결과 복귀는 정식 마지막 Act 정산 구현이 아니다. 영구 크레딧 이월·저장 실패 복구·중복 방지 저장은 미구현이다.

## 10. 종료 시 정리

- 이번 `MirrorSkillValidation` 경로로 실행한 Host/Client 네 개를 최종 PASS 확인 뒤 종료했다. 900초 자연 종료까지 기다렸다고 기록하지 않는다.
- Unity는 Edit Mode의 `Lobby_MirrorTest.unity`, Dirty 없음, Prefab Stage 없음, 컴파일·빌드 중 아님을 확인했다.
- Build Settings는 원래 상태와 Diff가 없다. 빌드가 자동 변경한 URP prefilter 값과 GlobalSettings 런타임 목록은 해당 자동 변경만 복원했다.
- 원본 `G_S1_E3_0_Projectile.prefab`은 Diff가 없고 사운드 컴포넌트를 유지했다.
- 사용자 RenderTexture와 관련 없는 변경은 보존했다. 전체 작업 트리가 깨끗한 상태는 아니다.
- 김성우 개인 구현 로그의 Git 구현 이력·상세 메모·마지막 기록 표에 오늘의 구현과 검증 한계, 다음 작업 문서 위치를 추가했다. 다른 담당자의 로그와 DecisionLog는 수정하지 않았다.

실행 로그·빌드·캡처는 로컬 Git 제외 경로이므로 문서만 다른 PC로 옮기면 원본 증거 파일이 따라가지 않는다. 필요하면 `Builds/MirrorSkillValidation` 및 관련 캡처를 별도로 전달한다.
