# P2-B 인페르노 구현·검증 검토 — round-03

- author: GPT
- reviewer_session_label: GPT-arcblade-r03-inferno-20260916 (대화 구분용 별칭)
- task_id: task-p2-review-arcblade-001
- round: round-03
- scope: tasks/task-p2-review-arcblade-001/round-03
- reviewed_at: 2026-09-16 (Asia/Seoul)
- reviewed_request_ready: READY / captured_at=2026-09-16T15:38:15+09:00
- evidence_captured_at: 2026-09-16T15:37:55+09:00
- source_branch_reported: codex/unity-6000-3-22-test
- source_base_commit_reported: 54aabb1aff855ed4ac645bd3fecad3219282352c
- verdict: REVISE_PLAN
- review_scope: Unity 게임의 제출 사본 정적 검토 및 검증 코드 평가. 구현 승인·실행 재검증 아님.

## 1. 핵심 판단

**인페르노의 좁은 구현 구조는 유지하는 것이 맞다.** 실제 Fighter 공격 처리 중의 직접 대상 집합을 재사용하고, 같은 대상에 비치명 Fire Effect를 동기 후속 큐로 등록하는 방식은 이번 단계에 적절하다. 별도 AttackKind, Factory, 범용 효과 실행기나 지연 스케줄러를 추가할 이유는 찾지 못했다. 읽은 정상 호출 경로에서 P2-B 때문에 새로 생긴 명백한 치명적 전투 로직 오류는 확인하지 않았다. [SOI; IT FireDamageDealt/TryFireInfernoExtraHit; AUTH ResolveServerAttack; RES]

다만 **29/39/76 PASS 보고를 근거로 모든 검증 조건 충족과 P3-A 무조건 착수를 확정하기에는 보완이 남아 있다.** 다중 Collider 시험의 전제가 보장되지 않고, 인페르노 처치 시험은 보상 처리 카운터만 확인한다. 시험이 실제 드랍·기존 씬 객체에 영향을 주지 않도록 격리해야 하며, 실행 원문 로그도 이번 회차에 없다. 이는 구현 전체를 다시 만들라는 뜻이 아니라, 현재 완료 주장을 지탱하는 작은 시험과 근거를 보강하자는 뜻이다. [VALI Validate/CreateEnemy/finally; ENEMY GrantKillRewardOnce; 회차 파일 목록]

| 요청 질문 | 결론 |
| --- | --- |
| round-02 원칙을 충족했는가? | 자격 검사·비치명·동기 큐 재사용은 적절하다. 스냅샷은 **각 직접 피해 처리 진입부터 해당 후속 큐까지**이며, 모든 공격 시작 시점의 단일 스냅샷은 아니다. |
| P2-A/P2-B가 공존하는가? | 단일 장착 무기를 교체해서 사용하는 경우와 플레이어별 실행 상태 분리에는 적합하다. 한 플레이어의 한 공격에서 복수 효과를 동시에 처리하는 범용 계약은 아니다. |
| 29/39/76 및 오류 0을 확인했는가? | 작성자의 실행 보고와 검증기 내용은 확인했다. 원문 로그·Console 내보내기·실행 세션 증거가 없어 독립 실행 인증은 하지 않는다. |
| P3-A로 넘어가도 되는가? | P3-A 대상 선정·설계는 진행할 수 있다. 아래 필수 검증 보완과 문서 정리 후 사용자 승인으로 실제 구현을 권한다. P4/P5/P6 기능을 선행 개발할 필요는 없다. |

이 문서의 약어는 §9 사본 참조표를 따른다. ‘확인’은 별도 명시가 없으면 제출 코드의 정적 확인이다. 현재 제품 동작, 시험이 실제 증명하는 범위, 다음 단계 제안을 구분한다.

## 2. round-02 피드백 이행 판정

직전 `round-02/review.md` 전문과 이번 사본을 대조했다. round-03에는 별도 response.md가 없으므로 지적별 답변 대신 HAND2의 이행 기록과 실제 코드를 비교했다.

| 이전 항목 | 이번 판정 | 근거와 남은 범위 |
| --- | --- | --- |
| F01 설명·단언 일치 | 검증 조건 수정 확인 / 에셋 확인 보류 | VALA는 이제 `이전 피해 계수의 {3}%`를 검사한다. 하지만 수정된 아크 효과 asset과 .meta는 이번 사본에 없다. 예전 문구가 여전히 남았다고 단정하지도, 에셋 수정까지 독립 확인했다고 하지도 않는다. |
| F02 공격당 1회 | 검증 코드 보완 확인 | VALA는 런타임 복제한 효과의 쿨다운을 0으로 설정하고 실제 Fighter 다중 직접 대상에서 연쇄 1회·추가 대상 1회를 검사한다. 기존 쿨다운 차단과 분리된 유효한 개선이다. |
| F02 비치명 | 대조 단언 보완 확인 | 직접 결과의 IsCritical=true, 첫 후속 결과의 false와 피해 25를 검사한다. 다만 VALA의 치명 배율 입력 1.5는 인페르노 검증에서 바로잡은 프로젝트의 +50% 단위와 다르다. §5에서 보완한다. |
| F02 후보 없음/CD 미소비 | 검증 코드 보완 확인 | 실제 장착 ItemInstance를 얻어 GetRemainingCooldown(item)<=0까지 검사한다. 발동 횟수만 검사하던 공백은 보완됐다. |
| F03 후속 중복 계약 | 좁은 계약 채택 가능 | ROAD §2.3이 단일 무기의 동기 효과 범위를 명시한다. 키에는 효과 ID가 없고 수명도 한 Resolver 호출이다. ‘모든 효과의 공격 전체 중복 방지’로 확대하지 않는다. |
| F04 스냅샷 기준 | 구현·일부 문서 충족 / 표현 불일치 잔존 | ROAD §2.3과 PLAN의 P2 기록 일부는 정확하다. HAND §1·§2, 요청 요약·일부 시험 이름에는 ‘공격 시작’이 남아 있다. 다중 직접 대상 사이의 기대값을 명시해야 한다. |
| F05 실행 근거·현황 일치 | 미완료 | 39/76 및 신규 29 PASS 보고는 있으나 실행 원문이 없다. ROAD의 머리는 P3-A 대기인데 §9는 다시 P2-B 착수 지시다. |
| R08 테스트 시작 장비 | TEST 한정 유지 | HAND는 아크 기본 지급을 사용자의 반복 테스트 의도로 설명한다. 이 검토에서 무관한 인벤토리 재설계를 요구하지 않는다. 정식 시작 장비 정책과는 계속 구분한다. |

## 3. 실제 실행 흐름과 공존 범위

### 3.1 Fighter 직접 대상 선별은 현재 호출 경로에서 적절하다

AUTH의 ResolveServerAttack은 Gunner이면 먼저 별도 분기로 빠진다. Fighter에서는 물리 후보를 WBH_ICombat으로 변환해 중복 제거하고 정렬한 뒤, 그 공격의 직접 대상 전체를 directAttackTargets에 등록한다. 각 대상의 Resolver 호출 동안만 이 집합이 유지되고 finally에서 비운다. IT는 같은 AttackId와 대상이 그 집합에 있는지, 현재 장착 효과가 Fighter의 InfernoExtraHitUniqueEffectSO인지 함께 검사한다. [AUTH ResolveServerAttack/IsDirectTargetForAttack/FindCombatTarget; IT TryFireInfernoExtraHit]

이 집합은 엄밀히 말해 ‘현재 스윙에 선정된 직접 대상 전체’이지 독립적인 인증 토큰이나 모든 피해 생산자를 분류하는 범용 API가 아니다. 다만 실제 ENEMY.HandleDamaged가 해당 대상과 결과를 넘기는 현재 경로에서는 좁은 근접 분류로 충분하다. 이번 효과만을 위해 새 분류 체계를 만드는 것은 불필요하다. Direct 이외 원인은 상위 FireDamageDealt에서 먼저 거절된다. [IT; ENEMY HandleDamaged]

### 3.2 같은 대상의 Direct와 Effect는 서로 다른 중복 기록을 쓴다

직접 피해의 중복은 AUTH.resolvedTargets가, 후속 피해의 중복은 RES.FollowUpTargets가 담당한다. 후속 drain이 직접 기록을 다시 검사하지 않으므로 인페르노의 같은 대상 추가타가 허용된다. 효과 피해는 FireDamageDealt의 Direct 필터를 통과하지 않아 아크·인페르노 및 기존 적중/치명 버프를 재귀 발동하지 않는다. 기존 OnKill 보상은 별도 경로로 남는다. [RES TryProcessPlayerDamage/EnqueueFollowUpDamage/DrainPendingQueue; IT FireDamageDealt]

같은 플레이어의 Weapon 슬롯에서 두 sealed SO 타입을 동시에 만족할 수는 없다. 따라서 두 메서드를 차례로 호출해도 정상 장착 상태에서는 아크 또는 인페르노 하나만 발동한다. 서로 다른 플레이어는 resolutionStates의 PlayerContext 키로 분리된다. 반면 같은 플레이어·같은 처리 경계에서 서로 다른 효과가 같은 대상에 Effect를 요청하면 현재 튜플 키 때문에 둘째가 거절될 수 있다. 이 미래 상황은 이번 단일 무기 구조의 결함으로 재설계하지 말고 지원 범위 밖으로 명시한다. [SOI/SOA; IT FireDamageDealt; RES DamageResolutionState]

현재 사본에는 아크 사용자와 인페르노 사용자가 같은 적을 공격하는 혼합 네트워크 세션 검사가 없다. 플레이어별 큐 분리는 정적으로 타당하고 P1의 독립 처리 시험도 있지만, 이를 두 실제 장비의 4인 동시 검증으로 부르지는 않는다. [VALP Foundation B4; VALI; VALA]

### 3.3 스냅샷과 처리 순서의 정확한 의미

RES는 각 직접 피해 처리 진입에서 공격력·치명 확률/배율·관통·속성 보너스를 캡처한다. 따라서 그 대상의 직접 피격 콜백이 공격력을 100에서 1000으로 바꾸더라도 그 대상의 인페르노 추가타는 캡처한 100을 사용한다. 비치명·방어 0·화염 보너스 0이면 추가 피해는 20이며 VALI가 이 조건을 검사한다. [RES DamageSourceSnapshot/TryProcessPlayerDamage/ExecuteDamageInternal; VALI snapshotResults]

대상이 A와 B라면 실제 순서는 `Direct(A) → Effect(A) → Direct(B) → Effect(B)`다. 스윙 전체의 직접타를 모두 끝낸 뒤 추가타를 몰아서 처리하는 구조가 아니다. A의 이벤트 이후 스탯이 1000이 되면 B의 새 직접 처리와 B의 추가타는 1000 기준이 될 수 있다. 이를 허용하는 현 계약을 유지하려면 문서와 두 대상 시험에 같은 기대값을 적는다. 스윙 전체 고정이 새 요구가 아니라면 Authority에 광범위한 스냅샷 전달 구조를 추가하지 않는다. [AUTH ResolveServerAttack; RES]

20%는 공격력에 곱하는 **입력 계수**다. 이후 대상 방어와 받는 피해 배율, 최소 피해 1이 적용되므로 항상 최종 직접 피해의 20%이거나 모든 적에서 정확히 20 피해라는 의미는 아니다. 인페르노는 Fire 효과타에 상태이상 인자를 null로 전달하므로 Burn을 새로 부여하지 않는다. Fighter 기본타의 기존 Slow1 전달과도 구분한다. [IT TryFireInfernoExtraHit; RES ExecuteDamageInternal; AUTH ResolveServerAttack]

### 3.4 직접 처치와 추가타 처치

직접타 이후 Status.IsDead면 IT는 큐 등록과 발동 카운터 증가를 생략한다. 대상이 나중에 무효화되면 Resolver drain이 다시 검사한다. 후속타가 처치한 경우에도 결과의 Attacker는 원래 PlayerContext.Controller이며, ENEMY는 그 컨텍스트를 보존하고 HandleDead/GrantKillRewardOnce 경로로 처리한다. 정상적인 유효 공격자 경로의 구조는 적절하다. 공격자 정보가 없는 피해에 쓰는 기존 fallback까지 정확한 귀속으로 인증하는 것은 아니다. [IT; RES; ENEMY HandleDamaged/HandleDead/GrantKillRewardOnce]

## 4. 보완 요청 요약

아래 ‘필수’는 현재 제출의 완료 주장과 안전한 검증을 위한 보완이다. 별도의 대규모 기능 확장을 요구하지 않는다.

| ID | 심각도·시점 | 근거 | 문제·영향 | 최소 수정·확인 방법 |
| --- | --- | --- | --- | --- |
| R03-01 | 중요 / 검증 완료 전 | VALI Validate/CreateEnemy; AUTH ResolveServerAttack | 다중 Collider라는 시험 전제가 확인되지 않음. 서로 다른 직접 대상에 각각 추가타가 생기는지도 미검사. | 중복 Collider를 명시 생성하고 같은 ICombat으로 매핑되는 실제 후보 2개 이상을 단언. 별도 A/B 대상 사례로 각 1회와 스냅샷 기준 확인. |
| R03-02 | 중요 / 처치 귀속 완료 전 | VALI 처치 사례; VALP Foundation D; ENEMY 보상 함수 | KillRewardCount==1만으로 수령자와 실제 골드·경험치·OnKill 1회를 증명하지 못함. | 작은 2컨텍스트 사례에 비영 보상과 OnKill 스택 표지를 두고 실제 인페르노 처치 경로의 수령자·비수령자 대조. |
| R03-03 | 중요 / 다음 시험 실행 전 | VALI Validate/finally; ENEMY HandleDead/GrantKillRewardOnce | 기존 물리 씬·실제 드랍/퀘스트 처리·추가 생성 객체로 시험 밖 상태에 영향을 줄 가능성. | 실행 환경 가드, 기존 대상 없는 시험 공간, 시험 전용 드랍 정책 및 소유 객체 정리. 반복·실패 후 원상태 확인. |
| R03-04 | 중요 / PASS 인증 전 | request/HAND; 회차 목록; VALI/VALA/VALP | 원문 실행·컴파일·Console 기록과 수정된 아크 에셋 사본이 없음. | 실제 실행 함수·환경·시각·코드 기준과 로그를 다음 회차에 제출. 현재 스위트 내용과 실행 사실을 분리. |
| R03-05 | 중요 / 인계 전 | HAND §1~2; ROAD §2.3/§8~9; PLAN 말미 | 스냅샷·최신 시작점·P1 완료 범위 표현 불일치. | 직접 처리 진입 기준으로 통일하고 다음 단계는 P3-A로 정리. Foundation과 최신 MPPM 대기를 구분. |
| R03-06 | 경미 / 회귀 보강 시 | VALA 최초 SetPlayerStats; HAND 검증 초기화 기록 | 아크는 치명 배율 입력 1.5를 아직 사용. 비치명 단언 자체가 무효인 것은 아님. | 프로젝트 단위에 맞춰 +50% 입력과 직접 피해 150 대조로 의도를 명확히 한다. |
| R03-07 | 중요 / P3-A 진입 계약 | AUTH Gunner 분기·중복 기록 | 산탄을 개별 펠릿으로 오인하거나 정의 ID만으로 발사 무기 인스턴스를 판별할 위험. | 실제 라이플의 발사/적중/교체 수명부터 검증하고, 현재 산탄 구현과 향후 펠릿을 분리한다. §7 참조. |

## 5. 검증 코드의 강점과 보완 근거

### R03-01 — 시험 이름만으로 다중 Collider를 증명할 수 없다

VALI는 snapshotResults.Count==2를 ‘다중 Collider가 있어도 Direct와 Effect 각 1회’라는 이름으로 검사한다. 그러나 CreateEnemy에는 추가 Collider 생성도, 기존 Collider 수 확인도, 공격 범위에 같은 적의 Collider가 실제 여러 개 들어왔는지 확인하는 단언도 없다. 제출물에 실제 적 prefab도 없다. 따라서 다중 Collider 조건이 실제 충족됐는지는 확인할 수 없다. 단일 Collider여도 이 검사는 똑같이 통과한다. [VALI snapshotResults 검사/CreateEnemy]

최소 보완은 시험 소유 적의 자식에 Collider 하나를 추가하고, 기존과 추가 Collider가 활성·올바른 레이어·공격 거리/각도 안에 있으며 같은 WBH_ICombat으로 해석되는지 먼저 확인하는 것이다. Unity의 OverlapSphere는 고유 적 목록이 아니라 겹치는 Collider들을 반환하므로 실제 중복 후보가 존재했다는 전제와 최종 Direct/Effect 각각 1회를 함께 검사해야 한다. 같은 파일 묶음의 VALP.ValidateGunnerLiveAttacks에는 이미 명시적 두 번째 Collider를 만드는 예가 있다. 그 작은 구성을 재사용하면 된다. [AUTH; VALP GunnerValidation_SecondCollider; W2]

여기에 인페르노 대상 A/B 두 개를 한 스윙에 넣어 각각 Direct+Effect가 발생하는 사례를 추가한다. 아크의 공격당 1회 규칙을 실수로 인페르노에 적용하면 이 검사가 실패해야 한다. A의 직접 이벤트에서 스탯을 변경한 경우 B는 새 직접 처리 스냅샷을 따른다는 기대도 별도로 명시한다. 이 항목은 현재 제품에서 중복 피해가 발생했다는 보고가 아니다.

### R03-02 — 보상 ‘처리 횟수’와 ‘정확한 지급’을 구분한다

VALI의 두 처치 사례는 KillRewardCount==1을 검사하지만, 시험 적의 exp와 credit은 0이고 OnKill 버프 표지도 장착하지 않는다. ENEMY의 해당 카운터는 수령자가 없더라도 증가할 수 있으므로 그 값만으로 올바른 플레이어에게 보상이 지급됐음을 입증할 수 없다. [VALI CreateEnemy/DirectKill/EffectKill; ENEMY GrantKillRewardOnce]

P1 Foundation D는 A가 직접 피해를 주고 B가 별도 Effect로 처치한 후, 보상 함수를 직접 호출하여 B의 골드 80·경험치 증가·OnKill 버프와 A의 무보상을 검사한다. 이는 유효한 별도 근거다. 하지만 그 시험은 인페르노 장착·Fighter 직접 대상 선별·실제 OnDead 콜백을 연결한 통합 사례는 아니다. 새 인페르노 시험과 기존 P1을 모두 무효로 볼 이유는 없지만, 둘을 합쳐 검증하지 않은 경로까지 완료라고 쓰면 안 된다. [VALP ValidateUniqueEffectP1Foundation, Section D]

후속 시험은 한 플레이어가 먼저 피해를 주고 다른 플레이어의 실제 인페르노 추가타가 처치하도록 구성하면 충분하다. 비영 골드·경험치와 OnKill 스택의 전후 차이, 비수령자의 무변화를 확인하고 사망/보상 통지를 반복해도 변화가 없는지 확인한다. OnKill은 버프 목록 개수뿐 아니라 스택 수도 검사한다. 기존 보상 공식을 바꾸거나 별도 보상 매니저를 만들 필요는 없다.

### R03-06 — 비치명 검사는 개선됐으나 아크의 치명 배율 단위를 정리한다

VALI는 SetPlayerStats(context,100,100,50)와 직접 피해 150을 명시적으로 대조한다. 반면 VALA는 여전히 세 번째 수치에 1.5를 넣고 직접 결과의 IsCritical만 확인한다. HAND가 설명한 프로젝트 단위대로라면 두 입력은 서로 다른 의미다. 아크도 +50% 입력과 직접 150 확인으로 통일하는 편이 명확하다. 단, 아크의 후속 IsCritical=false 및 피해 25 검사는 이미 존재하므로 ‘비치명을 전혀 검사하지 않는다’는 이전 지적을 그대로 반복하지 않는다. [VALI/VALA SetPlayerStats 및 최초 사례; HAND §4]

### R03-03 — 테스트 자체의 외부 부작용과 정리 범위를 먼저 제한한다

VALI는 실행 중인 세션을 거부하는 가드 없이 NetworkServer.active를 리플렉션으로 true로 만들고, 현재 물리 공간의 원점 부근에 시험 객체를 생성한다. AUTH의 범위 쿼리는 created 목록에 속한 적만 고르지 않으므로, 기존 씬의 같은 레이어 적이 근처에 있으면 시험 공격의 대상에 포함될 수 있다. 이는 코드에서 도출한 조건부 위험이며 실제 사용자 씬에서 피해가 발생했다고 관찰한 것은 아니다. [VALI Validate; AUTH ResolveServerAttack]

또한 VALI가 새로 연결한 OnDead→HandleDead는 실제 서버의 구독 구조와 맞는 보완이지만, 동시에 ENEMY의 퀘스트 보고·사망 진단·드랍 추첨·월드 아이템 생성·사망 표현 경로까지 호출한다. exp=0과 credit=0은 드랍 금지가 아니다. finally는 created에 넣은 플레이어와 적만 파괴하므로, 이 경로에서 별도로 생긴 월드 아이템은 추적 대상에 없다. 실제 생성 여부는 드랍 추첨과 실행 환경에 달려 있다. [VALI CreateEnemy/finally; ENEMY HandleDead/GrantKillRewardOnce/ResolveDropItemDefinition]

권장 최소 조치는 다음과 같다. 오프라인 검증기는 실행 중인 Host/Client 세션을 명시적으로 거부한다. 시험 전용 빈 공간 또는 격리된 시험 씬에서 실행하고, 선택된 물리 후보가 모두 시험 소유인지 확인한다. 처치 시험에는 가능한 기존 설정 경계로 결정적인 시험용 드랍 정책을 주거나, 시험이 실제 생성한 객체의 식별자를 기록해 그것만 정리한다. 퀘스트와 진단 카운터 등 시험 외 상태도 변경하지 않는 환경을 선택하거나 전후 복구를 검증한다. 실제 게임의 모든 드랍을 전역으로 끄거나 장면 전체 객체를 일괄 삭제하는 방식은 피한다.

의도적 예외를 발생시킨 시험과 동일 시험의 연속 2회 실행에서도 원래 객체·인벤토리·퀘스트·서버 상태 및 시험 생성물의 잔류 여부를 확인한다. 공용 거대 테스트 프레임워크를 새로 만들라는 요청은 아니다. 이 문제는 제품의 인페르노 피해 공식과 구분되는 **검증기 안전성** 항목이다.

### R03-04 — 실행 결과는 보고·검증 코드·실측을 분리한다

회차 디렉터리와 manifest를 확인했지만 별도 Logs, Console 내보내기, 검증 실행 응답 원문은 없다. 검증 소스의 Debug.Log 문자열은 실행 영수증이 아니다. 따라서 아래 범위까지만 판정한다.

| 항목 | 작성자 보고 | 이번에 확인한 코드 범위 | 독립 실행 판정 |
| --- | --- | --- | --- |
| Inferno Validate | 29/29 PASS | 설정/실제 Fighter 해석 경로, 치명 150+비치명 20, 스냅샷, 두 처치, 제외 원인 | 미실행. 다중 Collider 전제와 지급 검증은 위 보완 필요. |
| Arc Validate | 39/39 PASS | CD 0 복제 SO, 강제 치명 대조, 후보 없음/CD 미소비, 벽·상한·동일 대상 중복 | 미실행. 수정 아크 에셋 사본도 필요. |
| P1 ValidateUniqueEffectP1Foundation | 76/76 PASS | 메타데이터, 버프 필터, 재진입·FIFO·사망 스킵·예외 복구, 두 컨텍스트 보상 | 미실행. 같은 파일의 다른 Live/MPPM 검사가 실행됐다는 뜻이 아님. |
| 컴파일 / Console Error | 성공 / 0 | 문서에 해당 보고 존재 | 컴파일 완료 상태·검사 시각·실제 Console 기록 미제공. |

VALI/VALA는 리플렉션으로 서버 플래그를 설정하고 비공개 ResolveServerAttack 또는 Resolver를 호출한다. 실제 프리팹과 물리 코드를 사용하는 유용한 Editor 로직 검사지만, 입력→Command→애니메이션 확인→네트워크 Spawn→원격 수신의 종단 간 Play 시험은 아니다. Mirror의 ClientRpc는 Spawn된 NetworkIdentity와 관찰자 경로를 사용하므로 플래그 설정만으로 네트워크 검증을 대신할 수 없다. [VALI/VALA 초기화; W1]

다음 제출에는 실행한 함수명, Unity 버전, Edit/Play 및 실제 연결 인원, 시작·종료 시각, 해당 작업 트리/스냅샷 식별, 원문 PASS/예외/Console 결과를 함께 넣는다. 29·39·76이라는 숫자를 유지하려고 검사를 삭제하지 말고 보강 후 실제 검사 수를 기록한다. 컴파일 성공은 전체 회귀와 런타임 오류 0을 자동 증명하지 않는다.

## 6. 데이터와 인계 문서 정리

### 인페르노 연결은 사본에서 일관되며 수동 SO 예외는 명시돼 있다

JSON의 인페르노 행은 `item.weapon.axe.inferno / Fighter / Axe / Fire / UE_InfernoExtraHit`이고, 생성 ITEMI에도 같은 아이템·효과 ID가 있다. EI의 표시 계수 20과 실행 계수 0.2도 일치한다. 단, EI와 스크립트의 .meta가 없어 ITEMI의 참조 GUID가 실제 그 에셋을 가리키는지까지 바이트 근거로 검증하지 않았다. VALI의 실제 로드·타입 단언은 이 연결을 확인하려는 적절한 시험이다. [JSON 747~763행; ITEMI; EI; VALI 최초 설정 검사]

HAND가 ‘아이템 연결은 ItemTable에서 재현, 새 효과 SO 자체는 수동 시범 자산’이라고 구분한 것은 좋다. 이번 프로토타입 때문에 WJ 데이터 파이프라인 전체를 즉시 확장할 필요는 없다. 다만 양산·정식 이식 전에는 원본 표, importer 지원 또는 명시적 수동 보존 규칙과 .meta를 함께 확인한다. 이번 사본에는 ItemTable 원본 및 importer가 없어 재생성 실행까지 인증하지 않는다. [HAND §3; ROAD §5]

### R03-05 — 최신 문서의 시작점과 의미를 통일한다

HAND의 ‘공격 시작 시점 공격력’과 ‘공격 시작 스냅샷’은 ROAD §2.3의 정확한 계약인 ‘각 직접 피해 처리 진입 시점’으로 고친다. 요청서의 ‘완벽 보장’도 단일 장착 무기·동기 Fighter 공격·유효한 공격자라는 전제 안에서 설명한다. 이는 코드를 더 넓은 계약으로 바꾸라는 지시가 아니라 실제 코드에 설명을 맞추라는 요청이다.

ROAD는 머리에서 P2-B 완료/P3-A 대기로 기록하지만 §8~§9는 P2-B 미착수와 다음 P2-B 게이트를 지시한다. PLAN 말미와 HAND2 마지막 진행 경계에도 이전 시작점이 남아 있다. 과거 이력은 보존하되 ‘당시 지침’으로 표시하고, 현재 시작점은 P3-A로 단일화한다. P1은 Foundation 76 보고와 최신 MPPM 회귀 대기를 별도로 표시한다. [ROAD §3/§8/§9; PLAN §4/§8; HAND2 §4]

REQ_READY의 READY와 captured_at만 있는 간략 형식은 공통 규칙의 task_id/round/author/ready_at 형식과 다르다. 이번에는 사용자의 정확한 경로, CURRENT_ROUND, request/manifest의 식별자가 일치하므로 검토를 진행했다. 다음 회차에는 표준 필드를 채운다. manifest의 ‘줄 수’와 도구가 반환한 공백 포함 줄 수 역시 다르므로 집계 기준을 명시하는 것이 좋다. 이를 사본 변조의 근거로 사용하지 않으며 원본 입력은 수정하지 않았다.

## 7. P3-A 착수 방향 — 실제 Gunner 발사 수명부터 확인

**우선 후보는 실제 라이플 1종의 ‘명중 직후 제한 추가타’다.** 이번 JSON에서 `item.weapon.rifle.glassrail`(유리빛 궤도)은 Gunner/Rifle/Rare/Ice이고 uniqueEffectId가 공란이다. 그 아이템을 승인된 실험 대상으로 삼는 경우, 빙결이나 Burn 없이 단순한 Ice 추가타만 연결하는 안을 검토할 수 있다. 이는 새 최종 기획 확정이나 해당 아이템 변경 승인이 아니며, 기존 인페르노의 Fighter 전용 게이트를 풀어 재사용하지 않는다. [JSON의 glassrail 행; ROAD P3-A]

라이플을 권하는 이유는 이미 AUTH가 실제 NetworkEnemyProjectile_MirrorTest를 초기화하고 NetworkServer.Spawn하는 경로를 가지고 있어, **탄 비행 뒤 적중과 발사 후 장비 교체**라는 최소 두 위험을 검사할 수 있기 때문이다. 효과 자체는 적중 순간의 동기 추가타로 제한하므로 시간차 폭발·지속 상태이상 스케줄러는 필요하지 않다. 투사체 구현 사본은 이번에 없어 세부 충돌/소멸/귀속은 다음 단계에서 먼저 읽어야 한다. [AUTH ResolveServerGunnerAttack]

현재 Shotgun 분기는 개별 펠릿을 발사하지 않는다. OverlapSphere 후보에 각도·벽 검사를 한 뒤 같은 AttackId로 대상별 피해를 요청하는 구조다. 따라서 이를 이용한 시험은 ‘범위형 사격의 다중 Collider·대상’ 시험이며 ‘실제 다중 펠릿’ 시험이라고 쓰면 안 된다. 자동 사격도 실제 입력/발사 코드가 확인되기 전에는 연속된 승인 공격과 동일시하지 않는다. [AUTH ResolveServerGunnerAttack]

### R03-07 — P3-A에서 먼저 확정할 최소 계약

AUTH.IsGunnerShotCurrent는 현재 무기의 **정의 itemId와 종류**를 비교한다. 같은 정의의 다른 ItemInstance로 바꾼 경우나 장비를 뺐다가 다시 끼운 경우를 이 비교만으로 식별할 수 없다. 새 고유효과는 발사 당시 아이템 인스턴스와 장착 세대 등 필요한 최소 출처를 서버가 보관하고, 적중 시 신규 효과를 발동할 자격이 남아 있는지 확인하도록 권한다. 기존 탄의 직접 피해를 유지하는 정책과 해제한 장비의 새 고유효과를 금지하는 정책을 구분한다. [AUTH TryGetGunnerWeapon/IsGunnerShotCurrent; ROAD §2.3]

기존 AUTH 주석은 이미 발사된 탄이 명중 시점 스탯을 읽는다고 설명한다. 이를 이번 리뷰만으로 발사 시점 스냅샷으로 바꾸지 않는다. 첫 P3-A는 ‘탄의 직접 피해 진입에서 캡처한 수치를 즉시 후속타까지 유지’하는 현재 정책을 선택할 수 있다. 발사 시점 고정이 필요한 기획이면 해당 투사체 데이터에만 좁게 확장하고 별도 승인·시험으로 기록한다.

AUTH.resolvedAttackId와 IT.lastChainAttackIds는 마지막 공격 번호 중심의 기록이다. 늦게 도착한 공격이 A→B→A 순서로 다시 처리되는 경로에는 공격 수명 전체의 중복 방지를 보장하지 않는다. 투사체의 충돌·소멸 코드에서 그런 순서가 가능한지 먼저 확인한다. 실제 필요할 때만 살아 있는 공격/탄별 유한 기록을 두고 충돌 완료·만료·접속 종료 때 정리한다. 한 번 적중하고 즉시 소멸하여 해당 위험이 없는 경로라면 근거를 기록하고 범용 캐시 도입을 유예한다. [AUTH TryRegisterResolvedTarget; IT TryFireChainLightning]

### 기존 Live 검증기를 활용하되 효과를 제거한 시험으로 끝내지 않는다

VALP.ValidateGunnerLiveAttacks는 실제 Host, 공개 EquipmentTransaction, 실제 AnimationEvent, 네트워크 Spawn, 명시적인 두 번째 Collider, 벽 대조군을 사용하는 유용한 출발점이다. 하지만 그 시험은 복제 아이템의 uniqueEffect를 null로 만든다. 따라서 그대로 통과해도 신규 공격형 효과를 검사한 것은 아니다. 해당 실기 흐름을 재사용하면서 승인된 실제 효과를 유지하고, 직접 피해와 효과 피해를 구분해 기록한다. 이 메서드는 이번 76 Foundation 보고와 별개이며 이번에 실행됐다는 근거는 없다. [VALP 해당 메서드]

| P3-A 최소 사례 | 확인할 결과 |
| --- | --- |
| 실제 장착·발사·충돌 | 테스트 메서드의 직접 피해 호출만이 아니라 실제 입력/AnimationEvent/탄 적중에서 Direct와 Effect가 의도 횟수로 발생한다. |
| 발사 후 다른 무기로 교체 | 기존 탄의 직접 피해 정책은 유지하며 이전 탄이 새 무기의 고유효과를 얻지 않는다. |
| 같은 itemId의 다른 인스턴스로 교체·해제 후 재장착 | 정의 ID 일치만으로 옛 발동 자격이 되살아나지 않는다. |
| 연속 발사와 서로 다른 도착 순서 | 정상적으로 다른 공격은 누락하지 않고 동일 공격·대상의 반복 충돌은 의도 횟수만 처리한다. |
| 명시적 중복 Collider·벽·무효 대상 | 후보 전제가 실제 충족되며, 중복 피해·벽 너머 후속 피해·사망 대상 허위 발동이 없다. |
| 직접 치명·후속 비치명·스탯 변경 | 채택한 스냅샷 시점과 효과 계수가 일치하며 비치명 정책이 유지된다. |
| Host+원격 Client | 서버 판정과 각 화면의 결과가 맞는다. 아크/인페르노와 Gunner 혼합 시 플레이어 간 상태가 섞이지 않는다. |
| 정리·실패·반복 | 탄·시험 드랍·임시 설정·공격 기록이 수명 종료 및 예외 이후 남지 않는다. |

4인·정식 Act1·성능은 실제 지원 완료를 선언하기 전 별도 확인한다. 한 프레임의 모든 타격에 객체·RPC를 더 만드는 방향보다 현재의 짧은 결과 전달 구조를 유지한다. Host에서는 ClientRpc도 실행되므로 로컬 연출을 별도 호출한다면 중복 방지가 필요하다. 신규 Inferno VFX는 현재 구현되지 않았으므로 이 검토에서 ‘원격 표현만 확인하면 완성’으로 축약하지 않는다. [IT/PRE; HAND §5; W1]

## 8. 다음 제출의 최소 완료선

**권장 순서:** 먼저 R03-03의 시험 환경·정리 안전성을 확보한다. 그 환경에서 R03-01의 실제 중복 Collider와 다중 직접 대상, R03-02의 인페르노 처치 수령자 대조를 기존 검증기에 추가한다. R03-06의 수치 표현을 정리하고 세 회귀를 실행한 뒤, R03-04 실행 근거와 R03-05의 최신 문서를 함께 제출한다. 그 다음 사용자 승인으로 P3-A의 실제 Gunner 1종을 진행한다.

| 제출물 | 필요한 범위 |
| --- | --- |
| 보강한 검증기와 실행 원문 | Inferno/Arc/Foundation 각각의 정확한 진입 함수, 검사 수, 실패·예외·Console 기록. 다중 Collider와 보상 수령자 대조 포함. |
| 시험 대상과 수정 아크 자산 | 실제 사용한 Fighter/적 prefab 및 필요한 의존성·.meta, 수정 UE_ArcBladeChainLightning.asset, 신규 Inferno SO/아이템의 참조 .meta. |
| 현재 계약을 반영한 문서 | 각 직접 처리 진입 스냅샷, 단일 무기 동기 중복 범위, Editor 로직과 네트워크 시험 구분, P3-A 다음 시작점. |
| P3-A 착수용 코드 사본 | NetworkEnemyProjectile_MirrorTest와 선택한 실제 Gunner 아이템/프리팹/발사·충돌·해제 경로. 불필요한 전체 프로젝트 제출은 요구하지 않음. |

데이터 완전 재생성, Burn, 보호막, 지연 폭발, 모든 플랫폼의 네트워크 인증을 이번 P2-B 마감의 선행 구현으로 묶지 않는다. 현재 계수·대상 검사·기존 Resolver·처치 경계는 유지하고 실제로 필요한 시험과 인계 오류만 좁게 보완한다. 양산 전 데이터 파이프라인과 정식 장면 연결은 별도 완료선으로 남긴다.

## 9. 실제 읽은 근거와 한계

manifest 20종 중 **17종 전문**, ItemDataTable.json의 인페르노 및 P3-A 검토 관련 행, 역사 문서 2종의 서두를 읽었다. request/manifest/READY와 같은 태스크의 round-02 review도 읽었다. 역사 문서 2종은 2026-07-13 자료로 확인했으며 현재 Mirror 구현 판정의 근거로 사용하지 않았다. 전체 데이터 2,613행이나 역사 문서 전부를 이번에 검토했다고 주장하지 않는다.

아래 경로는 모두 round-03/evidence 기준이다. 문서 본문 인용은 이 약어와 실제 메서드/절을 사용한다. 줄 수는 Remote Desktop Commander가 반환한 전체 줄 수이며 manifest의 집계 기준과 구분한다.

| 약어 | 사본 경로 | 읽은 범위 |
| --- | --- | --- |
| HAND | Docs/Architecture/UniqueEffect_P2B_Inferno_Handover.md | 전문 55줄 |
| ROAD | Docs/Architecture/UniqueEffect_Custom_Implementation_Roadmap.md | 전문 209줄 |
| PLAN | Docs/Architecture/UniqueEffect_Implementation_Plan.md | 전문 223줄 |
| HAND2 | Docs/Architecture/UniqueEffect_P2_ArcBlade_Round02_Handover.md | 전문 75줄 |
| SOI | Assets/SW/Scripts/Equipment/Effects/InfernoExtraHitUniqueEffectSO.cs | 전문 12줄 |
| SOA | Assets/SW/Scripts/Equipment/Effects/ChainLightningUniqueEffectSO.cs | 전문 16줄 |
| IT | Assets/SW/TEST/MirrorPlayerContext/Scripts/ItemTriggerManager_MirrorTest.cs | 전문 291줄 |
| AUTH | Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerCombatAuthority_MirrorTest.cs | 전문 877줄 |
| RES | Assets/SW/TEST/MirrorPlayerContext/Scripts/WBH_CombatResolver_MirrorTest.cs | 전문 213줄 |
| ENEMY | Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyAuthority_MirrorTest.cs | 전문 1,039줄 |
| EX | Assets/SW/TEST/MirrorPlayerContext/Scripts/ChainLightningExecutor_MirrorTest.cs | 전문 127줄 |
| PRE | Assets/SW/TEST/MirrorPlayerContext/Scripts/UniqueEffectPresentation_MirrorTest.cs | 전문 105줄 |
| VALI | Assets/Editor/InfernoExtraHitValidation_MirrorTest.cs | 전문 295줄 |
| VALA | Assets/Editor/ArcBladeChainLightningValidation_MirrorTest.cs | 전문 404줄 |
| VALP | Assets/Editor/MirrorCombatBoundaryValidation_MirrorTest.cs | 전문 936줄, Foundation과 다른 진입점 구분 |
| EI | Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/UniqueEffectPool/UE_InfernoExtraHit.asset | 전문 22줄 |
| ITEMI | Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/item.weapon.axe.inferno_인페르노.asset | 전문 42줄 |
| JSON | Assets/Resources/DataFiles/ItemData/2. JSONFile/ItemDataTable.json | 인페르노 747~764행, Gunner 후보 1580~1869행 및 관련 검색 결과 |
| HIST1/HIST2 | Docs/Architecture/01_ProjectInventory.md / 02_ArchitectureReview.md | 각각 서두 15줄, 역사 자료 확인에 한정 |

원본 프로젝트·Git·Scene·Prefab·Editor 상태를 조회하거나 수정하지 않았고, 컴파일·Play·네트워크 테스트를 실행하지 않았다. manifest SHA-256은 작성자 제공값이며 독립 재계산과 원본/사본 동일성은 NOT_VERIFIED다. 실제 사용 프리팹, WBH_EnemyStatus/Controller 및 PlayerContext/StatSet 구현, 데이터 importer·원본 표, 투사체 충돌 구현이 없는 범위의 동작은 확정하지 않았다.

### 공식 API 참고

2026-09-16 조회. 설치된 Mirror의 정확한 버전·전송 설정 및 이번 프로젝트 실행 결과를 대신하지 않는다.

- W1: Mirror, Remote Actions — ClientRpc의 Spawn된 NetworkIdentity·Host 로컬 클라이언트·관찰자 범위. `https://mirror-networking.gitbook.io/docs/manual/guides/communications/remote-actions`
- W2: Unity 6000.3, Physics.OverlapSphere — 구와 겹치거나 접촉하는 Collider 반환. `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics.OverlapSphere.html`

## 10. 저장 범위와 최종 판정

고정 범위는 task-p2-review-arcblade-001 / round-03이다. 작성 전 CURRENT_ROUND=round-03, 요청·manifest 식별자, REQUEST_READY의 captured_at을 대조했고 기존 review/REVIEW_READY가 없음을 확인했다. 이전 회차 결과, 다른 태스크, 공통 규칙, 요청과 evidence, CURRENT_ROUND는 변경하지 않았다. 이 review.md를 전체 재읽기한 뒤 같은 회차의 REVIEW_READY.txt를 마지막에 작성하는 절차를 따른다.

**최종 판정은 REVISE_PLAN이다.** 인페르노의 최소 구현 방향과 공용 Resolver 재사용은 유지한다. 시험 전제·보상 지급 대조·검증기 격리·실행 원문·최신 문서부터 보완한 뒤, 사용자 승인으로 P3-A 실제 Gunner 1종에 진입하는 것이 적절하다. 이 판정은 새 기능 구현, 정식 씬 이식, 전체 네트워크 합격의 승인이 아니다.
