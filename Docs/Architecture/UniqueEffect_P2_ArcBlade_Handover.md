# 고유효과 P2 아크 블레이드 책임 분리 및 구현 인계 문서

- **최초 작성일**: 2026-09-16
- **문서 목적**: GPT 1차 검토(`task-p2-review-arcblade-001/round-01/review.md`) 분석 결과를 바탕으로, Codex가 아크 블레이드(P2-A)의 소규모 책임 분리와 P2-B(인페르노) 차단 요인을 선행 해결하고 안전하게 구현을 이어갈 수 있도록 지침을 제공함.
- **브랜치**: `codex/unity-6000-3-22-test`
- **작성자**: 김성우 (SW) / AI Review Bridge
- **GPT 최종 판정**: **P2-A 실험용으로 조건부 적합 / P2-B 전에 소규모 책임 분리와 전투 계약 보완 권고**
- **참조 문서**:
  - `AI-Review-Bridge/tasks/task-p2-review-arcblade-001/round-01/review.md`
  - `Docs/Architecture/UniqueEffect_Implementation_Plan.md`
  - `Docs/Architecture/UniqueEffect_P1_Handover.md`
  - `Assets/Editor/ArcBladeChainLightningValidation_MirrorTest.cs`

---

## 1. 검토 결론 요약

1. **Mirror 테스트 경로에 우선 연결한 접근은 타당함**:
   - 서버 권한 판정, 플레이어별 실행 상태 격리, Resolver 동기 FIFO 큐, 비치명타(`canCrit = false`), 효과 피해의 재발동 차단(`DamageCause.Effect`) 기반은 확고히 유지한다.
2. **거대 프레임워크나 전면 재작성은 지양 (AGENTS.md 간결성 준수)**:
   - 모든 효과를 인터페이스/팩토리/NetworkBehaviour로 재편하지 않는다.
   - 행동 구현이 비대해진 부분만 **작은 일반 C# 클래스(Executor)**와 **로컬 프레젠터(Presentation)**로 가볍게 추출한다.
3. **권장 작업 순서**:
   - **[1단계] 작은 책임 분리** (Executor + Presentation 추출, 기존 동작 100% 보존)
   - **[2단계] 피해 계약 보완** (R02 중복 등록 분리, R03 스탯 스냅샷)
   - **[3단계] 검증 보강** (R05 벽 차단 대조군, 4번째 적 상한 검증)
   - **[4단계] P2-B (인페르노 근접 화염 추가타) 구현 착수**

---

## 2. 권장 아키텍처 구조 (책임 분리)

| 위치 / 클래스 | 맡을 책임 | 맡기지 않을 책임 |
| --- | --- | --- |
| **기존 `ItemTriggerManager_MirrorTest`**<br>(NetworkBehaviour) | • 서버 자격 확인 (`netIdentity.isServer`)<br>• 기존 버프 트리거 분배 (`FireDamageDealt`)<br>• 고유 효과별 실행기 호출<br>• 플레이어별 쿨다운 및 공격 번호 상태 보관<br>• 얇은 ClientRpc 어댑터 유지 | • 공간 탐색 세부 알고리즘 (OverlapSphere/Linecast)<br>• LineRenderer 구성 및 절차적 렌더링 |
| **신규 `ChainLightningExecutor_MirrorTest`**<br>(일반 C# 클래스) | • 연쇄 후보 공간 탐색 (`Physics.OverlapSphere`)<br>• 벽 차단 (`Linecast`) 및 직접 타격 대상 제외<br>• 순차 감쇠 계산 및 `EnqueueFollowUpDamage` 등록<br>• 등록 성공 수 반환 (매니저가 쿨다운 소비 결정) | • SyncVar, ClientRpc, 화면 객체 생성<br>• 장비 전체 순회, 플레이어 수명주기 이벤트 |
| **신규 `UniqueEffectPresentation_MirrorTest`**<br>(로컬 프레젠터) | • 서버 ClientRpc가 전달한 좌표 구간 시각화<br>• `LineRenderer`, Material 구성, 지그재그 연출<br>• 재생 시간(0.14s) 후 객체 수명 관리 및 안전 정리 | • 대상 선택, 피해량·감쇠율 계산<br>• 쿨다운 계산, 서버 전투 판정 |
| **기존 `WBH_CombatResolver_MirrorTest`** | • 공용 피해 공식 계산<br>• 동기 FIFO 큐 순차 실행, 대상 사망 재검사<br>• `canCrit = false`, `DamageCause.Effect` 전달 | • 연쇄 범위, 무기별 개별 기획 분기, VFX |
| **`ChainLightningUniqueEffectSO`** | • 튜닝 데이터 (25%, 80%, 4m, 3명, 0.8s) 정의 | • 플레이어별 런타임 쿨다운·공격 기록·서버 실행 |

> [!NOTE]
> 일반 C# 클래스는 `MonoBehaviour`/`NetworkBehaviour`를 상속받지 않는 순수 로직 클래스를 의미합니다. 불필요하게 모든 의존성에 인터페이스를 붙이거나 DI 컨테이너를 도입하지 않습니다.

---

## 3. 핵심 차단 및 중요 지적 사항 (Codex 필독)

### [R02 — P2-B 차단] 직접 피해와 효과 추가타의 중복 등록 집합 분리
- **현상**:
  - `WBH_CombatResolver_MirrorTest`의 `DrainPendingQueue`에서 `TryRegisterResolvedTarget(attackId, target)`을 호출하여 동일 `attackId`와 대상의 중복을 막고 있음.
  - 하지만 이 집합이 직접 피해와 효과 피해를 구분하지 않음.
  - **P2-B (인페르노 화염 추가타)**는 **동일 공격·동일 대상에게 직접 피해 + 화염 추가타**를 가해야 함.
  - 현재 상태에서는 직접 피해로 이미 등록된 대상에게 화염 추가타가 enqueue되더라도, `Drain` 단계에서 동일 대상 중복으로 판단되어 **무조건 스킵(차단)**됨.
- **조치 방안**:
  - `CombatAuthority`의 직접 타격 대상 중복 기록과, 후속 효과 피해의 중복 기록을 구분한다.
  - 또는 Resolver의 후속 큐 처리에서는 직접 공격 대상 집합을 그대로 재사용하여 효과 추가타를 거절하지 않도록 검증 계약을 정비한다.
  - *(AttackId를 0으로 바꾸거나 임의 가짜 번호를 발급해 우회하지 말 것)*.

### [R03 — 중요] 공격측 스탯 스냅샷 캡처
- **현상**:
  - 현재 `WBH_CombatResolver_MirrorTest`는 후속 피해를 실행할 때마다 `attackerStatus.AttackPower`를 실시간으로 다시 읽음.
  - 만약 직접 타격 시 OnDamageDealt나 OnCrit으로 공격력 버프가 켜지거나 처치 경험치로 레벨업하면, 연쇄 번개의 기준 공격력이 중간에 변동될 수 있음.
- **조치 방안**:
  - 직접 공격 시점의 기준 공격측 수치(공격력, 속성 보너스, 관통)를 캡처하여 `PendingFollowUpDamage`에 전달하거나, 합의된 단일 기준 시점을 명시한다.

### [R04 — 중요] 수명 경계와 연쇄 중복 기록의 정합성
- **현상**:
  - `PlayerCombatAuthority`는 연결 종료 시 공격 번호를 초기화하지만, `ItemTriggerManager`의 `lastChainAttackIds`는 초기화되지 않음.
- **조치 방안**:
  - 서버에서 플레이어 세션/공격 수명이 리셋될 때 연쇄 중복 기록도 함께 정리하는 진입점을 마련한다. (단, 재장착을 통한 쿨다운 우회를 막기 위해 쿨다운 유지와 중복 기록 초기화는 분리).

### [R05 — 중요] 단위 검증 테스트 보강
- **현상**:
  - 현재 벽 차단 검사는 (0,0,0) -> x=2, 4, 6 적 3명을 잡고 종료되므로, (0,0,3)의 벽 뒤 적이 피해를 안 입은 것이 벽 차단 때문인지 3명 상한 때문인지 불명확함 (거짓 양성 위험).
- **조치 방안**:
  - **벽 검사**: 벽 뒤 적만 단독 후보로 두었을 때 0회 피해 -> 벽 제거 시 1회 피해를 검증하는 대조군 테스트 구성.
  - **대상 상한**: 연결 가능한 후보를 4명 이상 배치하고 정확히 3명만 피격되는지 검증.
  - **공격당 1회**: 한 번의 스윙으로 다중 적을 직접 타격했을 때 연쇄 번개가 1회만 시작되는지 실제 Authority 경로로 검증.

### [R06 ~ R08 — 기타 보완 사항]
- **R06**: `TryFindNearestChainTarget`에서 `Mathf.Approximately`로 인한 비대칭 정렬을 방지하고, `거리 제곱의 엄격한 대소 -> 정확히 같은 경우 netId`로 결정론적 정렬을 단순화한다.
- **R07**: 로컬 프레젠터가 번개 LineRenderer 객체의 수명을 관리하여, 플레이어 비활성화/파괴 시에도 고아 객체가 남지 않도록 보장한다.
- **R08**: 테스트 편의를 위해 `PlayerInventorySync_MirrorTest`에 하드코딩했던 `DefaultFighterWeaponItemId = "item.weapon.greatsword.arcblade"`를 확인하고, 테스트용 장비 지급과 공용 시작 무기 정책을 명확히 분리한다.
- **R09**: SO 및 툴팁 문구의 "이전 피해의 80%"를 실제 계산식에 맞게 "이전 피해 계수의 80%"로 일치시킨다.

---

## 4. Codex 단계별 실행 가이드 (Action Items)

Codex는 아래 4단계 순서에 따라 작업을 진행합니다. 각 단계가 끝날 때마다 컴파일 및 단위 테스트를 수행하여 회귀를 방지합니다.

### 1단계: 순수 클래스 추출 (P2-A 책임 분리)
1. `Assets/SW/TEST/MirrorPlayerContext/Scripts/ChainLightningExecutor_MirrorTest.cs` 생성:
   - `ItemTriggerManager_MirrorTest`의 `TryFireChainLightning`, `TryFindNearestChainTarget`, `TryGetCombatPoint`를 이관.
   - 필요한 컨텍스트(`PlayerContext`, `ChainLightningUniqueEffectSO`, `WBH_DamageResult`, `WBH_ICombat firstTarget`)를 인자로 수신.
   - 등록 성공 여부 및 큐 등록 수를 반환하여 매니저가 쿨다운을 소비하도록 구성.
2. `Assets/SW/TEST/MirrorPlayerContext/Scripts/UniqueEffectPresentation_MirrorTest.cs` 생성:
   - `RpcPresentChainLightning` 및 `PresentChainLightning` 코루틴, Material 캐시, LineRenderer 생성 및 정리 로직을 이관.
3. `ItemTriggerManager_MirrorTest.cs` 정리:
   - 비대해진 탐색/렌더링 코드를 제거하고, Executor 호출 및 프레젠터 RPC 트리거만 담당하도록 슬림화.
4. **검증**: `ArcBladeChainLightningValidation_MirrorTest`를 실행하여 기존 18/19 checks가 100% 정상 통과함을 확인.

### 2단계: P2-B 선행 전투 계약 정비 (R02, R03)
1. `WBH_CombatResolver_MirrorTest.cs` & `PlayerCombatAuthority_MirrorTest.cs`:
   - `DrainPendingQueue`에서 후속 효과 피해가 동일 대상이라는 이유로 일괄 차단되지 않도록 중복 등록 정책 수정 (직접 피해 대상 집합과 효과 추가타 집합 구분).
   - 공격 시작 시점의 공격력/스탯 스냅샷 보존 및 후속 피해 전달 반영.

### 3단계: 단위 검증 강화 (R05)
1. `ArcBladeChainLightningValidation_MirrorTest.cs` 보강:
   - 벽 단독 대조군 테스트 케이스 추가 (벽 존재 시 0회, 벽 제거 시 1회).
   - 4번째 적 추가 배치로 3명 상한 엄밀 검증.
   - 다중 직접 타격 시 1회 시작 검증.

### 4단계: P2-B 인페르노 근접 화염 추가타 착수
- 1~3단계 완료 후 사용자의 승인을 얻어 P2-B(인페르노 무기의 직접 공격 시 화염 추가타 1회) 구현 착수.

---

## 5. 2026-09-16 이행 결과

1. **책임 분리 완료**
   - `ItemTriggerManager_MirrorTest`: 서버 자격·장비 확인·쿨다운·공격당 중복 기록·얇은 ClientRpc만 유지.
   - `ChainLightningExecutor_MirrorTest`: 후보 탐색, 직접 대상 제외, 벽 판정, 결정론적 정렬, 감쇠 및 후속 피해 등록 담당.
   - `UniqueEffectPresentation_MirrorTest`: 로컬 `LineRenderer` 생성과 비활성화·파괴 시 정리 담당.
2. **P2-B 선행 전투 계약 완료**
   - 직접 피해 대상 기록과 `(AttackId, DamageCause, Target)` 후속 피해 기록을 분리하여 동일 대상 Direct + Effect 1회를 허용했다.
   - 직접 피해 처리 진입 시점의 공격력·치명타·관통·속성 보너스를 동기 후속 큐까지 고정했다.
   - 서버 공격 번호 수명 초기화와 고유효과 공격 중복 기록 초기화를 연결하되, 쿨다운은 별도로 유지했다.
3. **2차 검토 F01·F02 완료**
   - 설명을 `이후 전이는 이전 피해 계수의 {3}%`로 수정했다.
   - 쿨다운 0인 실제 Fighter 다중 직격, 치명 직접타 대비 비치명 후속타, 후보 없음 시 쿨다운 미소비를 독립 검증했다.
4. **검증 결과**
   - Unity 6000.3.22f1 컴파일 성공.
   - 아크 블레이드 P2-A 1인 검증 **39/39 PASS**.
   - P1 Foundation 회귀 **76/76 PASS**.
   - 검증 직후 Unity Console Error **0건**.
5. **아직 완료로 보지 않는 범위**
   - 원격 클라이언트 ClientRpc 표현, 4인 MPPM, 정식 Act1 화면 품질과 성능은 미검증이다.
   - 현재 스냅샷은 투사체 발사 순간이 아니라 직접 피해 처리 진입 시점이다.
   - 현재 동기 후속 큐는 Burn 틱·지연 폭발·메아리 효과의 스케줄러가 아니다.
   - `PlayerInventorySync_MirrorTest.DefaultFighterWeaponItemId`의 아크 블레이드 지정은 반복 테스트 편의를 위한 사용자 의도이므로 유지했다.

P2-B는 구현하지 않았다. 다음 작업은 사용자의 별도 진행 지시 뒤 `UniqueEffect_Custom_Implementation_Roadmap.md`를 기준으로 착수한다.
