# 고유효과 P2 아크 블레이드 2차 검토(round-02) 인계 및 보완 지침

- **작성일**: 2026-09-16
- **문서 목적**: GPT 2차 검토(`task-p2-review-arcblade-001/round-02/review.md`) 결과를 바탕으로, Codex가 구현한 책임 분리 구조를 확정 채택하고, 남은 소규모 보완 항목(F01~F05)을 해결하여 P2 완결 및 P2-B로 원활히 넘어가도록 지침을 제공함.
- **브랜치**: `codex/unity-6000-3-22-test`
- **GPT 최종 판정**: **CHANGES_REQUESTED — 책임 분리 구조는 채택 승인 / 소규모 보완(F01, F02) 및 계약 명시 후 P2-B 착수 권고**
- **참조 문서**:
  - `AI-Review-Bridge/tasks/task-p2-review-arcblade-001/round-02/review.md`
  - `Docs/Architecture/UniqueEffect_P2_ArcBlade_Handover.md`

---

## 1. 2차 검토 핵심 결론

1. **책임 분리 아키텍처 채택 확정 (R01, R06, R07)**:
   - `ItemTriggerManager_MirrorTest` (자격·쿨다운·얇은 RPC)
   - `ChainLightningExecutor_MirrorTest` (순수 C# 클래스: 공간 탐색·벽 차단·감쇠·큐 등록)
   - `UniqueEffectPresentation_MirrorTest` (로컬 프레젠터: LineRenderer 수명 및 누수 방지)
   - `WBH_CombatResolver_MirrorTest` (공용 피해 계산 및 동기 큐)
   - **이 구조는 매우 적절하며 그대로 유지한다. 새로운 거대 프레임워크나 추가 계층을 만들지 말 것.**
2. **P2-B(화염 추가타) 동일 대상 차단 해소 확인 (R02)**:
   - `followUpAttackTargets` 튜플 분리로 단일 효과의 직접 피해 + 동일 대상 Effect 추가타 경로가 정상 동작함을 승인함.
3. **P2-B 착수 전 필수 보완 사항 (F01, F02)**:
   - 에셋 설명문과 테스트 단언의 실제 계산 일치 (F01).
   - 검증 테스트의 독립성 보강 (F02: 쿨다운 분리 1회 발동, 비치명 대조 단언, 쿨다운 미소비 단언).

---

## 2. Codex 구현 보완 지침 (F01~F05)

### [F01 — 필수] 에셋 설명문과 테스트 단언 일치 (R09)
- **문제점**:
  - `Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/UniqueEffectPool/UE_ArcBladeChainLightning.asset`의 `effectDescription`이 여전히 `"이후 전이는 이전 피해의 {3}%"`로 되어 있음.
  - `ArcBladeChainLightningValidation_MirrorTest.cs:51`도 `"이전 피해의 {3}%"`를 검사하고 있어, 실제 계산(`이전 피해 계수의 80%`)과 불일치함.
- **조치 사항**:
  1. `UE_ArcBladeChainLightning.asset`의 설명문을 `"이후 전이는 이전 피해 계수의 {3}%"`로 수정.
  2. `ArcBladeChainLightningValidation_MirrorTest.cs`의 단언을 `effect.effectDescription.Contains("이전 피해 계수의 {3}%")`로 수정.

### [F02 — 필수] 단위 검증 테스트 3종 독립성 보강 (R05)
- **문제점 및 조치**:
  1. **공격당 1회 검사를 쿨다운(0.8s)과 분리**:
     - 현재 Fighter 다중 적중 테스트는 0.8초 쿨다운이 걸려 있어, `lastChainAttackIds` 중복 방지 필터가 없어도 쿨다운 때문에 1회만 발동하는 것처럼 보일 수 있음.
     - **조치**: 테스트 내에서 `UnityEngine.Object.Instantiate(definition.uniqueEffect)`로 런타임 복제한 SO의 `cooldownSeconds = 0f`로 설정한 임시 무기를 장착하고, Fighter 다중 직격 시 연쇄 번개가 공격당 정확히 1회만 시작됨을 검증하는 독립 케이스 추가.
  2. **비치명(`canCrit: false`) 대조 단언 추가**:
     - 직접타는 치명타 확률 100%(`critRateFlat = 100f`, `critMultiplier = 1.5f`)를 주어 `IsCritical == true`를 유도하고, 후속 연쇄타의 `WBH_DamageResult.IsCritical == false`이며 피해량이 비치명 기준(25%)으로 들어갔는지 대조 단언 추가.
  3. **후보 없을 때 쿨다운 미소비 단언 보강**:
     - 주변에 추가 적이 없을 때 발동 횟수(`ChainLightningTriggerCount`)뿐만 아니라, `ItemTriggers.GetRemainingCooldown(definition.uniqueEffectId) <= 0f` (또는 쿨다운 미기록 상태)임을 함께 확인.

### [F03 & F04 — 계약 명시] 전투 리졸버 및 스냅샷 범위 확정
- **동일 대상 후속타 계약 범위 (F03)**:
  - 현재 Resolver의 `followUpAttackTargets`는 `(AttackId, DamageCause, Target)` 기준의 한 처리 경계 집합임.
  - 이번 P2-A/P2-B는 **단일 장착 무기의 공격형 효과(연쇄 번개, 화염 추가타)** 범위로 한정하므로 현 구조로 충분함. P2-B 구현 시에도 공격·대상당 1회 추가타 규칙을 명확히 유지할 것.
- **스냅샷 기준 (F04)**:
  - 현재 스냅샷은 직접 피해 처리 직전 시점(`TryProcessPlayerDamage` 진입 시) 캡처하여 해당 후속타 큐까지 고정 적용하는 방식임.
  - "직접 피해 처리 시작 기준 스냅샷"으로 계약 정의 및 주석을 통일할 것.

---

## 3. 권장 진행 절차

1. `UE_ArcBladeChainLightning.asset` 설명문 수정 (F01)
2. `ArcBladeChainLightningValidation_MirrorTest.cs` 단언 및 테스트 케이스 3종 보강 (F01, F02)
3. 에디터 검증 실행하여 전수 통과 확인
4. P2-A 검증 완료 후, P2-B(인페르노 근접 화염 추가타) 구현으로 원활히 착수

---

## 4. 2026-09-16 이행 상태

- **F01 완료**: 에셋 설명과 검증 단언을 `이전 피해 계수의 {3}%`로 통일했다.
- **F02 완료**: 쿨다운 0 다중 직격의 공격당 1회, 치명 직접타와 비치명 연쇄타 대조, 후보 없음 시 쿨다운 미소비를 각각 독립 단언으로 추가했다.
- **F03·F04 명시 완료**: 현 중복 키는 P2-A/P2-B의 단일 장착 무기 동기 후속 효과까지만 보장하며, 스냅샷은 직접 피해 처리 진입 시점부터 해당 동기 큐가 끝날 때까지 유지한다.
- **검증 완료**: P2-A 39/39, P1 Foundation 76/76, Unity 컴파일 성공, 검증 직후 Console Error 0건.
- **보류**: 원격 ClientRpc·4인 MPPM·정식 Act1·성능 검증은 이번 1인 검증 범위에 포함하지 않았다.
- **진행 경계**: P2-B는 아직 시작하지 않았으며 사용자의 별도 지시 후 착수한다.
