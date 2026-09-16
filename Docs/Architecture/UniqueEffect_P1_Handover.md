# 고유효과 P1 기반 구조 및 MPPM 2인 검증 인계 문서

- **최초 작성일**: 2026-09-14
- **최종 갱신일**: 2026-09-15 (GPT Review round-01/02/03 피드백 및 Codex 인계 사항 반영)
- **브랜치**: `codex/unity-6000-3-22-test`
- **작성자**: 김성우 (SW)
- **상태 요약**:
  - **Foundation 범위**: **76/76 checks PASS (100% 통과, Live Unity 6000.3.22f1 실측 완료)**
  - **MPPM 2인 실기 세션**: 러너 코드 보완 완료(총 67 checks 구성) / **최신 트리 실기 세션 최종 회귀 대기 (Codex 인계)**
  - **피해 원인 명칭 (Q2)**: `DamageCause.Effect = 2` 현행 유지 확정 (Alias 미도입)
- **참조 문서**:
  - `Docs/Architecture/UniqueEffect_Implementation_Plan.md`
  - `Docs/Architecture/ImplementationLogs/김성우.md`
  - `Assets/Editor/MirrorCombatBoundaryValidation_MirrorTest.cs`
  - `Assets/SW/TEST/MirrorPlayerContext/Scripts/MirrorMppmCombatRunner_MirrorTest.cs`
  - `AI-Review-Bridge/tasks/task-p1-review-damagecause-001/round-03/review.md`

---

## 1. 개요 및 요약

고유효과 P1 메타데이터(`DamageCause`, `AttackId`) 및 전투 경계 기반 구조를 구축하고, 단위 검증(Foundation 76 PASS)을 완료한 상태에서 Codex로 후속 실기 검증 및 P2 진입을 원활히 넘기기 위한 인계 문서이다. 타 담당 영역(BH 5개 파일, WJ 3개 파일)은 사용자 승인 범위 내에서 최소한으로 수정되었으며 신규 매니저나 임의 계층을 생성하지 않았다.

### 핵심 구현 및 보완 완료 사항

1. **타 담당 영역 수정 승인 준수**:
   - BH 파일 5개(`WBH_DamageRequest.cs`, `WBH_DamageResult.cs`, `WBH_CombatManager.cs`, `WBH_Projectile.cs`, `T_PlayerCombat.cs`) 및 WJ Gunner 스킬 파일 3개(`GunnerArcProjectile.cs`, `GunnerBomb.cs`, `GunnerDecoy.cs`)에 메타데이터 전달 코드를 반영함.
   - 기존 생성자와 100% 호환되도록 구성하여 타 시스템 파괴를 방지함.
2. **공격 메타데이터 확충 및 전달**:
   - `DamageCause` 열거형(`Direct`, `Skill`, `Effect`, `DoT`) 및 `AttackId`(`uint`) 필드를 요청·결과 구조체에 추가.
   - 근접, 투사체, 산탄, 거너 기본 사격, Mirror CombatResolver 및 결과 생성부에서 메타데이터를 명시적으로 주입.
   - 실제 적 Prefab 55개 전수 조사 결과 단일 Collider 구조임을 확인하고 공격별 대상 HashSet을 제거하여 간소화함.
3. **피해 경계 및 후속 피해 큐 안전성 확보**:
   - `WBH_CombatManager`: 대상 사망 여부(`IsDead`) 검사로 사망 후 남은 콜라이더의 허위 발동을 원천 차단하되, 정상 처치 공격의 결과와 이벤트는 안전하게 보존.
   - `WBH_CombatResolver_MirrorTest` (`EnqueueFollowUpDamage`): 동기 피해 처리 경계 안(`IsResolving == true`)에서만 등록 허용(경계 밖 등록은 즉시 `false` 반환), 동일 플레이어 재진입 차단, 플레이어별 독립 큐, FIFO 순차 실행, 대상 생존 재검사, 예외 발생 시 큐 제거 및 상태 복구 보장.
   - 상태 누수 원천 차단: 재진입 및 중복 검증을 통과한 후에만 `resolutionStates`에 등록하도록 조치 완료(R01-01).
4. **고유효과 필터 가드 및 처치 귀속 한정 보장**:
   - `ItemTriggerManager_MirrorTest`: `DamageCause.Direct`만 `OnDamageDealt`를 트리거하고 `Skill`, `Effect`, `DoT` 재발동 차단.
   - `NetworkEnemyAuthority_MirrorTest`: **유효한 PlayerContext가 명시적으로 전달된 직접/효과 피해에 한정**하여 최종 치명타 공격자에게만 OnKill/경험치/골드를 단독 귀속하고 `GrantKillRewardOnce` 중복 호출도 1회만 반영(멱등성 보장, R02-04).
5. **정밀 자동 검증 (Foundation 76/76 PASS 실측)**:
   - 초기 69개 항목에서 R01/R02 보완(B3-1 누수 방지, C2 치명타 후속 스킵 및 콜백 0회 검증, D4 멱등성 2회 호출 검증)을 거쳐 **Live Unity 6000.3.22f1에서 76개 전원 통과 완료**.
6. **MPPM 2인 자동 러너 정비**:
   - `MirrorMppmCombatRunner_MirrorTest.cs`: R01-03 스폰 순서(`InitializePlayerServer` 선행) 일치 및 T03/T04 엄밀화(총 67 checks 구성) 완료.

---

## 2. 변경된 파일 목록 및 상세 내용

### 2-1. BH 영역 (사용자 승인 범위)
| 파일 경로 | 주요 변경 내용 |
| --- | --- |
| `Assets/WBHTest/Scripts/Combat/WBH_DamageRequest.cs` | `DamageCause DamageCause { get; }`, `uint AttackId { get; }` 추가, 11개 인자 생성자 추가 및 기존 5개 인자 호환 유지 |
| `Assets/WBHTest/Scripts/Combat/WBH_DamageResult.cs` | `DamageCause DamageCause { get; }`, `uint AttackId { get; }` 추가, 10개 인자 생성자 추가 및 기존 4개 인자 호환 유지 |
| `Assets/WBHTest/Scripts/Combat/WBH_CombatManager.cs` | `TakeDamage` 전 `target.Status.IsDead` 검사로 사망 후 허위 발동 차단, `enemyAuthority.IsServerDamageHandlingActive` 확인 후 중복 이벤트 생략 |
| `Assets/WBHTest/Scripts/WBH_Projectile.cs` | 요청 복제 시 `DamageCause`와 `AttackId` 보존 |
| `Assets/WBHTest/Scripts/Player/T_PlayerCombat.cs` | 0이 아닌 시퀀스 공격 ID 생성 및 전달, SectorAttack 한 번에 동일 ID 사용 |

### 2-2. WJ 영역 (사용자 승인 범위)
| 파일 경로 | 주요 변경 내용 |
| --- | --- |
| `Assets/WJ_TestPlace/Script/Player/Skill/GunnerArcProjectile.cs` | 명중 요청 재구성 시 `DamageCause`와 `AttackId` 보존 |
| `Assets/WJ_TestPlace/Script/Player/Skill/GunnerBomb.cs` | 1·2차 폭발 명중 요청에서 `DamageCause`와 `AttackId` 보존 |
| `Assets/WJ_TestPlace/Script/Player/Skill/GunnerDecoy.cs` | 폭발 명중 요청에서 `DamageCause`와 `AttackId` 보존 |

### 2-3. SW 영역
| 파일 경로 | 주요 변경 내용 |
| --- | --- |
| `Assets/SW/TEST/MirrorPlayerContext/Scripts/WBH_CombatResolver_MirrorTest.cs` | `DamageCause`·`AttackId` 명시 전달, `EnqueueFollowUpDamage` 동기 큐 등록 및 반환(`bool`), 재진입 거절, 상태 누수 방지(R01-01), FIFO 처리 및 예외 정리 |
| `Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerCombatAuthority_MirrorTest.cs` | 근접/산탄/연속 사격 시 동일 시퀀스 공격 ID 전달, 플레이어별 최근 AttackId/대상 중복 판정 |
| `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyProjectile_MirrorTest.cs` | 투사체 생성 시 `attackId` 주입 및 피격 시 명시 전달 |
| `Assets/SW/TEST/MirrorPlayerContext/Scripts/ItemTriggerManager_MirrorTest.cs` | `OnDamageDealt`를 `DamageCause.Direct`로 한정하여 연쇄 재발동 차단 |
| `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyAuthority_MirrorTest.cs` | `IsServerDamageHandlingActive` 프로퍼티 제공, 최종 치명타 공격자에게만 보상 단독 귀속, 멱등성 보장 |

### 2-4. 테스트 및 검증 파일
| 파일 경로 | 주요 변경 내용 |
| --- | --- |
| `Assets/Editor/MirrorCombatBoundaryValidation_MirrorTest.cs` | `ValidateUniqueEffectP1Foundation`: C2(enqueue true 2건 및 callback 0회), D4(멱등성 2회), B3-1(누수) 보강으로 **76 checks PASS** 달성 |
| `Assets/SW/TEST/MirrorPlayerContext/Scripts/MirrorMppmCombatRunner_MirrorTest.cs` | MPPM 2인 T00~T07 자동 검증 러너: R01-03 스폰 순서 일치, T03 비Direct 피격 4건, T04 경험치 엄밀화 반영 (총 67 checks 구성, 컴파일 0건) |
| `Assets/Settings/PlayMode/2 Player.asset` | Player 2의 `m_InitialScene` 명시 연결 (`Act1_Stage1_MirrorCombatTest.unity`), Tag: `mppm-p1-validation` |
| `Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage1_MirrorCombatTest.unity` | Missing Prefab 인스턴스 2개 정리 (Missing Script 0, Missing Prefab 0) |

---

## 3. 정밀 단위 검증 결과 (Foundation 76/76 PASS)

Live Unity 6000.3.22f1 Editor 실측 실행:
```text
[MirrorUniqueEffectP1Validation] PASS 76 checks (0.02s)
```
- **A (효과 필터)**: 버프 및 Direct 전용 효과 필터 (Skill/Effect/DoT 차단)
- **B (경계 및 누수 방지)**: 경계 밖 등록·동일 플레이어 재진입 거절, 타 플레이어 독립 처리, **중복 거절 후 resolutionStates 잔류 누수 없음(B3-1)**
- **C (큐 순차 실행 및 스킵)**: 후속 피해 FIFO, **치명 직접 피해 후속 등록 성공(true 2건) 및 대상 사망으로 인한 스킵·콜백 0회 검증(C2)**, 사망 스킵(C3), 예외 복구(C4)
- **D (메타데이터 및 처치 귀속)**: DamageCause/AttackId 보존, 전달, 처치 보상 단독 귀속, **GrantKillRewardOnce 2회 호출 멱등성 검증(D4)**

---

## 4. 실제 MPPM 2인 검증 현황 및 GPT Review 피드백

### 4-1. 2026-09-14 과거 트리 실기 이력 (63/63 PASS)
Unity 6000.3.22f1 Main Editor(Host) + Virtual Player(Client) 실기 실행:
```text
[MirrorMppmCombatRunner] ALL SCENARIOS T00~T07 PASSED! (63 checks)
```

### 4-2. 2026-09-15 최신 작업 트리 현황 (러너 보완 완료 / 실기 세션 대기)
1. **러너 코드 보강 완료 (총 67 checks 구성)**:
   - `SpawnTestProjectile`: `InitializePlayerServer` 호출 후 `NetworkServer.Spawn` 순서로 일치.
   - T03: 비Direct 피해 피격 성공(`bool`) 및 타깃 HP 감소 4건 추가.
   - T04: 시작 레벨 대비 경험치 증가 엄밀 검증 추가.
2. **실행 상태**:
   - 컴파일 에러 0건, Foundation 76/76 PASS 및 러너 구성 완료 상태다.
   - 유실됐던 테스트 씬 Missing Prefab 2개와 `2 Player` 프로필의 Player 2 초기 씬·`mppm-p1-validation` 태그를 복원했다.
   - 최신 트리의 2인 세션 실기 재실행은 Player 2 재기동 단계에서 사용자 요청으로 중단했으며, 67 checks 통과로 판정하지 않는다.

### 4-3. GPT 3차 검토(`round-03/review.md`) 핵심 지적 요약
- **해결 확인된 지적**:
  - `R02-01` (C2 False-Positive 차단): enqueue 2건 true 단언 및 callback 0회 단언으로 사망 스킵 정상 입증 확인.
  - `R02-03` (문서 책임·시점 분리): 과거 이력과 최신 상태 분리, SW Resolver로 큐 소유 일원화 확인.
  - `R02-04` (처치 귀속 범위 한정): 유효한 PlayerContext 전달된 피해로 보장 범위 한정 확인.
- **남은 과제**:
  - `R03-01` (차단): 최신 변경 트리에서 MPPM Host+Virtual Player 2인 T00~T07 실제 재실행 및 최종 PASS 원문 로그 확보 필요.
  - `R03-02` (중요): 최신 MPPM 실기 통과 전까지 문서 어조를 "Foundation 범위 검증 완료 / 네트워크 실기 최종 회귀 대기"로 유지하고, 실기 통과 후 최종 판정(`READY_FOR_USER_DECISION`)으로 전환할 것.

---

## 5. Codex 작업 인계 가이드 (남은 작업)

Codex는 신규 게임 코드를 추가하지 않고 현재 완성된 P1 트리를 그대로 이어받아 아래 순서로 마무리합니다.

### 1단계: MPPM 2인 실기 세션 실행
- **환경 설정**:
  - Unity 6000.3.22f1 Editor에서 `Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage1_MirrorCombatTest.unity` 씬이 활성화된 상태 확인.
  - `Assets/Settings/PlayMode/2 Player.asset` 설정에 따라 MPPM 2인 모드(Main Editor: Host, Player 2: Client) 실행.
- **러너 동작**:
  - Main Editor와 Player 2에 모두 `mppm-p1-validation` 태그가 활성화되어 씬 로드 시 `MirrorMppmCombatRunner_MirrorTest`가 자동 기동됨.
  - Host가 시작되고 Client가 연결(포트 7777, localhost)되면 T00부터 T07까지 순차 자동 수행.
- **확인 대상 로그**:
  - `[MirrorMppmCombatRunner] ALL SCENARIOS T00~T07 PASSED! Total Checks Passed: 67` (또는 실제 통과 수치).
  - 예외 0건, 에러 0건 확인 후 세션이 자동 종료(`StopMppmSession`)됨.

### 2단계: 검증 로그 저장 및 인계 문서 최종 갱신
- MPPM 67 checks PASS 원문 로그를 `AI-Review-Bridge/tasks/task-p1-review-damagecause-001/round-04/evidence/Logs/` 등에 첨부.
- 본 문서의 상태 요약 및 Section 4-2를 "최신 트리 MPPM 실기 회귀 완료(PASS)"로 갱신하고, GPT 판정 요청을 `READY_FOR_USER_DECISION`으로 제시.

### 3단계: 사용자 최종 승인 및 P2 진입
- GPT 최종 승인 확인 후, **사용자에게 P1 완료를 보고하고 P2 구현 승인을 요청**.
- 사용자 승인 획득 시 P2-A(아크 블레이드 연쇄 번개) 및 P2-B(인페르노 근접 화염 추가타) 구현에 착수.
