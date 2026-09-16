# 고유효과 P2-B 인페르노·표시 결함·화염 상태이상 인계 — round-05

- 작성일: 2026-09-16
- 작성자: Antigravity (김성우 / Codex 인계용)
- 작업 브랜치: `codex/unity-6000-3-22-test`
- 대상 태스크: `task-p2-review-arcblade-001` (round-05)
- 검토 원본: `Docs/Architecture/UniqueEffect_P2_ArcBlade_Inferno_Round05_Review.md`
- 현재 상태: **코드 수용 완료(ACCEPTABLE), 연출 결함 2종 수정 완료, 화염 속성 정식 상태이상(Burn DoT) 연계 완료, Live Play Mode 실측 완료**
- 다음 단계 권고: **P3-A 실제 Gunner 공격형 효과 1종 착수** (기존 R03 잔여 검증 항목은 명시적 후속 부채로 관리)

---

## 1. GPT round-05 검토 결과 요약

### 1.1 판정 요약
- **최종 판정**: `NEEDS_EVIDENCE` (코드 수정 수용 / 전체 종결용 실증 증빙 대기)
- **코드 검토 (Code Review)**: **수용 (ACCEPTABLE_IN_SCOPED_DIFF)**
  - round-04에서 도출된 4개 런타임 파일 수정(개별 피해 RPC 전송, 데미지 폴링 제거, 로컬 위치 분리 오프셋, 후속타 성공 콜백의 화염 RPC, 로컬 표현 수명 관리)이 올바른 방향으로 구현되었으며, 코드를 다시 설계하거나 롤백할 이유 없음.
  - `netIdentity != null` 선행 가드 추가로 Editor 미Spawn 객체에서의 NullReference 예외 및 네트워크 경고를 방지한 점 긍정적 보완으로 인정.
  - `/ponytail full` 원칙 준수 (새로운 전역 매니저나 복잡한 추상화 계층 없이 기존 시스템 책임 범위 유지).
- **종결 보류 사유**:
  - 144 PASS(29+39+76)는 3개 단위 검증 러너의 검사 수 합산이며, 독립적인 실전 네트워크 시나리오 144개를 의미하는 것이 아님.
  - 사운드 재생 확인, 텍스트 풀 분리 표출 및 자산 프리팹 직렬화 저장, 원격 멀티플레이어 환경 등에 대한 실측 증빙 및 기존 R03 검증 공백 분리 필요.

### 1.2 GPT 지적 세부 사항 (R05-01 ~ R05-05)
| ID | 중요도 | GPT 지적 요지 | 코덱스 인계 및 처리 상태 |
| --- | --- | --- | --- |
| **R05-01** | 중요 | `PresentedInfernoHitCount`는 AudioSource/clip이 없어도 증가하므로 사운드 1회 재생의 절대적 증거가 될 수 없음. | 카운터는 로컬 표현 호출 횟수이며, 실제 SFX 청취 여부는 프리팹 루트의 AudioSource/clip 유무로 구별해야 함을 명시. |
| **R05-02** | 중요 | 데미지 텍스트 2개 표출 및 화염 파티클 2.0초 수명/회수에 대해 실제 자산(`FighterNetworkPlayer.prefab`, `Hit_Red.prefab`) 직렬화 및 풀 분리 확인 필요. | 프리팹 직렬화 완료(`infernoHitPrefab` 할당) 및 `ReleaseInfernoHitAfter`/`ReleaseOwnedResources`에 의한 잔여 객체 0 확인. |
| **R05-03** | 중요 | 144 PASS로 R03의 다중 콜라이더 시험 전제, 처치 수령자 대조, 테스트 격리 문제가 자동 종결된 것은 아님. | 해당 항목들을 억지로 "완료"로 포장하지 않고, **명시적인 후속 보강 과제(Technical Debt)**로 분리하여 유지. |
| **R05-04** | 중요 | 실행 원문 로그, 타임스탬프, Host 및 원격 Client 구분 필요. 4인 완료는 로컬 표본과 별개. | 로컬 Play Mode 실측 데이터(초기 HP 10,000, 1타 9802.7, 5초 DoT 9302.7, 2타 8605.4 등) 원문 수치를 명시. |
| **R05-05** | 경미 | 아크 블레이드 계수는 25%(추가 3인 20%/16% 감쇄)이며 30%가 아님. 다음 단계는 P2-C가 아닌 P3-A(Gunner). | 수치 오기 정정 완료, 로드맵 상 다음 단계를 P3-A로 명확히 확정. |

---

## 2. GPT 검토 후 추가 해결 사항: 정식 화염 속성 DoT 데미지 연계

GPT는 round-05 review.md §3.3(Line 73)에서 다음을 핵심 지적으로 짚었습니다:
> *"현재 추가타의 상태이상 인자는 null이며, 화염 VFX가 보인다고 Burn 피해가 구현된 것은 아니다."*

사용자의 지시에 따라, 화염 무기(인페르노 등) 장착 시 정식 화상 상태이상(`WBH_StatusEffectType.Burn`)과 도트 데미지가 연계되지 않던 결함을 즉시 해결했습니다.

### 2.1 원인
- `PlayerCombatAuthority_MirrorTest.cs`의 근접 직접 공격 처리에서 상태이상 인자로 `WBH_StatusEffectPresets.Slow1`이 하드코딩되어 있었음.
- 거너 샷건, 투사체, 인페르노 화염 추가타에서도 상태이상 인자가 `null`로 전달되어 화상 효과가 발생하지 않았음.

### 2.2 코드 수정 (`/ponytail full`)
1. **속성별 상태이상 단일 매핑 헬퍼 구현** (`PlayerCombatAuthority_MirrorTest.cs`):
   `csharp
   public static WBH_StatusEffectData? GetStatusEffectForElement(ElementType element)
   {
       return element switch
       {
           ElementType.Fire => WBH_StatusEffectPresets.Burn1,
           ElementType.Ice => WBH_StatusEffectPresets.Freeze1,
           ElementType.Electric => WBH_StatusEffectPresets.Electric1,
           _ => null
       };
   }
   `
2. **모든 공격 진입점에 매핑 주입**:
   - 근접 직접 공격(`PerformDirectAttack`, `ResolveServerAttack`): `GetStatusEffectForElement(status.CurrentElement)` 전달.
   - 거너 샷건(`pendingGunnerElement`) 및 투사체(`shotElement`): 동일 헬퍼 적용.
   - 인페르노 추가타(`ItemTriggerManager_MirrorTest.TryFireInfernoExtraHit`): `WBH_StatusEffectPresets.Burn1`을 명시 전달하여 추가타 적중 시에도 화상 부여 및 지속시간 갱신.

### 2.3 실시간 Live Play Mode 실측 검증
- **환경**: Unity 6000.3.22f1, `Act1_Stage1_MirrorSessionTest` (Mirror Host)
- **대상**: 인페르노(Fire 속성) 장착 Fighter vs `[Dummy] Normal_Melee_HP10000` (HP 10,000)

| 검증 항목 | 실측치 | 판정 및 상세 |
| --- | --- | --- |
| **화상 상태이상 즉시 부여** | `HasBurn = True`, `remTime = 5.00s` | 1타 타격 즉시 Burn1(5초 지속) 효과 등록 확인 |
| **1초 주기 DoT 틱 데미지** | `LastDamage = 100` | 10,000 HP의 1%인 100 DoT 피해 정확히 발생 확인 |
| **5초간 누적 DoT 피해** | `HP 9802.7 → 9302.7` (-500) | 5회 틱으로 500 피해 정확히 감소 (`WBH_BurnEffect.OnTick`) |
| **데미지 텍스트 표출** | `PresentedDamageTextCount = 7 → 14` | 직접타 + 추가타 + 매 DoT 틱마다 화염 데미지 텍스트 분리 표출 |
| **2타 재공격 및 화상 갱신** | `HP 9105.4 → 8605.4` (-500) | 화상 지속시간 5.0초 재갱신 및 5회 DoT 틱 정상 지속 |

---

## 3. 현 시점 파일 상태 및 변경 내역

| 파일 경로 | 주요 변경 내용 |
| --- | --- |
| `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyAuthority_MirrorTest.cs` | 피해마다 `RpcShowDamage` 호출, `netIdentity != null` 가드 보강, 기존 `lastDamage` 진단 필드 유지 |
| `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyCombatView_MirrorTest.cs` | `LateUpdate` 데미지 폴링 제거, `OffsetDamageText`로 연속 피해 텍스트 위치 분리 |
| `Assets/SW/TEST/MirrorPlayerContext/Scripts/ItemTriggerManager_MirrorTest.cs` | 후속타 성공 콜백(`onResolved`)에서만 `RpcPresentInfernoHit` 발행, 추가타에 `Burn1` 주입 |
| `Assets/SW/TEST/MirrorPlayerContext/Scripts/UniqueEffectPresentation_MirrorTest.cs` | `Hit_Red` 파티클/오디오 재생 및 2.0초 자동 수명 관리, OnDisable/OnDestroy 정리 |
| `Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerCombatAuthority_MirrorTest.cs` | `GetStatusEffectForElement` 헬퍼 추가 및 무기 속성에 따른 상태이상 주입 (`Fire => Burn1`) |
| `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyProjectile_MirrorTest.cs` | 거너 투사체 적중 시 속성별 상태이상 주입 연계 |
| `Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab` | `UniqueEffectPresentation_MirrorTest`에 `Hit_Red.prefab` 직렬화 할당 |

---

## 4. 코덱스 후속 작업 및 P3-A 착수 가이드

1. **P2-B 및 표시 결함 종결 상태**:
   - 코드 차원의 문제(텍스트 덮어쓰기, 화염 VFX 미호출, 화염 DoT 미부여)는 **완전 해결 및 실측 검증 완료**.
   - R03-01(다중 콜라이더 시험), R03-02(처치 수령자 대조), R03-03(테스트 격리) 및 4인 Dedicated Server 실측은 P3-A 진행 중 또는 이후의 통합 QA 단계에서 보완할 후속 부채로 명시함.
2. **다음 권장 태스크 (P3-A)**:
   - **대상**: 거너(Gunner) 실제 공격형 고유효과 1종 구현.
   - **주의사항**:
     - 인페르노의 Fighter 근접 제약을 해제하지 말 것.
     - 거너 샷건의 다중 펠릿/단일 공격 ID 규칙과 투사체 수명/무기 교체 세대 정보를 고려할 것.
     - 기존에 정비된 `RpcShowDamage`와 `GetStatusEffectForElement` 파이프라인을 그대로 재사용할 것.
