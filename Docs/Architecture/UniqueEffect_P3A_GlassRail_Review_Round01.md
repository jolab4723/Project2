# GPT 검토 — task-p3a-review-glassrail-001 / round-01
- author: GPT
- reviewer_session_label: GPT-glassrail-r01-20260917-164357
- task_id: task-p3a-review-glassrail-001
- round: round-01
- scope: tasks/task-p3a-review-glassrail-001/round-01
- reviewed_at: 2026-09-17 16:45:27 KST
- verdict: **REVISE_PLAN**

---

## 📌 Codex 작업자 인계 요약 (Action Items)

> **확정 정책**: "발사된 유리빛 궤도(GlassRail) 탄은 비행 도중 무기를 교체하거나 해제하더라도 자신의 냉기 추가타(15% Ice Effect)를 유지한다."

### 1. P3-A 핵심 코드 수정 (R01-01)
- **발사부 효과 전달**: `PlayerCombatAuthority_MirrorTest.ResolveServerGunnerAttack`에서 서버 발사 시점에 Rifle/고유효과 출처(`UE_GlassRailExtraHit`)를 확정해 `NetworkEnemyProjectile_MirrorTest`에 명시적으로 전달·보관하도록 구현.
- **투사체 효과 보관**: `NetworkEnemyProjectile_MirrorTest`에 발사 시점의 고유효과 SO 참조를 보관하는 필드 추가.
- **명중 동기 스코프**: `ApplyPlayerDamage` 시점에 해당 `AttackId`와 보관된 효과를 `PlayerCombatAuthority`에 동기적으로만 노출하고 `finally`에서 정리.
- **트리거 출처 확인**: `ItemTriggerManager_MirrorTest.TryFireGlassRailExtraHit`에서 "현재 장착 무기"를 조회하는 대신, 명중 스코프에 노출된 출처/효과를 확인하도록 수정.

### 2. 단위 검증기 및 테스트 갱신 (R01-02, R01-03)
- `GlassRailExtraHitValidation_MirrorTest`: 단순 번호 변경이 아닌, **"발사 후 교체/해제 시에도 효과 유지"** 및 **"일반 탄 발사 후 GlassRail 장착 시 추가타 미발동"** 등 의미별 양성/음성 검사를 정확히 반영하고 실제 실행 결과(N/N PASS)를 보고.
- 가짜 통과(항상 false인 stub) 방지를 위해 처리 중 유효성, 처리 후 정리 검사 모두 포함.

### 3. P3-B(방어구/유물) 착수 순서
- P3-A 실제 발사/명중 검증 완료 후, **H3(투구) / B1(부츠) 중 데이터가 명확한 하나부터 순차 진행**.
- R2(오라 유물)의 반경(인계서 6m vs 요청서 10m) 및 마나 재생 밸런스는 시제품 기준이므로 착수 전 수치와 사양을 확정할 것.

---

## 핵심 판단

**사용자가 정한 “발사된 유리빛 궤도 탄은 무기 교체·해제 후에도 자신의 냉기 추가타를 유지한다”는 정책은 유지한다. 그러나 제출 코드가 이 정책을 구현한 상태는 아니며, 요청서의 최소 수정안에도 실제 발사 호출부의 효과 전달과 검증 경계가 빠져 있다. 계획을 보완하고 같은 버전의 코드·문서·실측 결과를 다시 제출해야 한다. 이 판정은 구현 승인이 아니다.**

구조적으로는 투사체가 서버에서 발사 시점의 효과 설정을 보관하고, 실제 명중을 처리하는 동기 호출 동안만 해당 플레이어의 Authority에 공격 번호와 효과를 노출하는 방식이 현재 코드에 맞는다. 기존 Resolver의 피해 계산·후속 피해 큐, EnemyAuthority의 피격/처치 처리와 데미지 표시 RPC를 재사용할 수 있다. 장착 세대 추적, 전역 효과 매니저, 새 피해 공식, 효과 SO의 네트워크 전송은 이번 정책 구현에 필요하지 않다. 다만 “현재 장착 무기를 읽지 않는다”만으로는 부족하다. 실제 서버 발사 → 투사체 보관 → 명중 스코프 → 트리거의 출처 확인까지 연결되어야 한다. [E11, E13, E14, E17]

제출 로그에는 P1 76개, P2-A 39개, P2-B 29개 통과가 **작성자의 실행 결과로 보고**되어 있다. P3-A는 실패 로그만 있다. 요청서의 “갱신 즉시 18/18 ALL PASS”와 지정 계획서·로드맵의 완료 표기는 현재 제출 자료로 입증되지 않는다. 기대값만 바꾸면 통과한다고 보장할 수도 없다. [request.md §2·§4, E19, E20, E21]

P3-B는 P3-A의 실제 발사·명중 검증 다음에 H3/B1/R2 중 하나씩 진행하는 방향을 유지한다. 방어구 활성 책임을 유물 Provider에 억지로 합치지 않는 방향은 타당하지만, 이미 있는 `OnDodge` 트리거와 중복 실행하지 않도록 책임을 좁혀야 한다. R2의 반경은 요청서의 10m와 지정 인계서의 6m가 다르며, 인계서의 수치는 확정 밸런스가 아니라 시제품 제안이다. 선택할 실제 아이템·효과·수치를 명시하기 전에는 세 종류 전체의 안전한 착수를 확정할 수 없다. [E13, E15, E18, E19]

**전달 상태:** 이 문서는 검토 내용을 작성한 다운로드용 사본이다. 현재 대화에 노출된 Remote Desktop Commander에는 파일 쓰기/수정 작업이 없어, 지정 Windows 경로에 `review.md`를 저장하지 못했다. 따라서 원격 저장·재읽기를 전제로 하는 `REVIEW_READY.txt`는 발행하지 않았다. 검토 판정과 원격 인계 완료 여부를 구분한다.

## 실제 읽은 근거와 한계

### 범위 고정 및 공통 규칙

검토 루트는 `C:\Users\user\AI-Review-Bridge`, 고정 작업은 `tasks/task-p3a-review-glassrail-001`, 고정 회차는 `round-01`이다. `GPT_START_HERE.md`, `README.md`, `RULES.md`의 v2 개정절, `TASK_ROUTING_V2.md`, `templates/review.md`를 읽었다. 이 작업의 `CURRENT_ROUND.txt`, `round-01/REQUEST_READY.txt`, `request.md`, `evidence/manifest.md`를 대조했다.

`REQUEST_READY.txt`의 task_id/round/author는 각각 `task-p3a-review-glassrail-001`/`round-01`/`Gemini`이며, ready_at은 위 메타데이터에 기록했다. 자료에 적힌 프로젝트 경로, 브랜치, 커밋은 작성자의 출처 설명으로만 읽었다. 원본 프로젝트나 Git에 접근하지 않았다. 지정된 인계 문서 안의 다른 작업 ID·과거 검토·외부 링크를 따라가지 않았다.

### 근거 식별표

아래 E 번호는 이 검토서 안에서만 사용하는 식별자다. 모든 경로는 고정 회차의 `evidence/` 기준이다. `request.md`와 `evidence/manifest.md`는 별도로 전체를 읽었다.

| 식별자 | 사본 상대 경로 | 실제 읽은 범위 |
| --- | --- | --- |
| E01 | `Assets/Editor/ArcBladeChainLightningValidation_MirrorTest.cs` | 전체 |
| E02 | `Assets/Editor/GlassRailExtraHitValidation_MirrorTest.cs` | 전체 |
| E03 | `Assets/Editor/InfernoExtraHitValidation_MirrorTest.cs` | 전체 |
| E04 | `Assets/Editor/MirrorCombatBoundaryValidation_MirrorTest.cs` | 전체. P1 Foundation 및 Gunner 라이브/취소 검증 경로 포함 |
| E05 | `Assets/Resources/DataFiles/ItemData/2. JSONFile/ItemDataTable.json` | GlassRail 항목 검색 및 주변 75행. JSON 전체 내용 검토는 하지 않음 |
| E06 | `Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/item.weapon.rifle.glassrail_유리빛 궤도.asset` | 전체 |
| E07 | `Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/UniqueEffectPool/UE_GlassRailExtraHit.asset` | 전체 |
| E08 | `Assets/SW/Scripts/Equipment/Effects/GlassRailExtraHitUniqueEffectSO.cs` | 전체 |
| E09 | `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyAuthority_MirrorTest.cs` | 전체 |
| E10 | `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyCombatView_MirrorTest.cs` | 전체 |
| E11 | `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyProjectile_MirrorTest.cs` | 전체 |
| E12 | `Assets/SW/TEST/MirrorPlayerContext/Scripts/GunnerCombatPresentation_MirrorTest.cs` | 전체 |
| E13 | `Assets/SW/TEST/MirrorPlayerContext/Scripts/ItemTriggerManager_MirrorTest.cs` | 전체 |
| E14 | `Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerCombatAuthority_MirrorTest.cs` | 전체 |
| E15 | `Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerInventorySync_MirrorTest.cs` | 전체 |
| E16 | `Assets/SW/TEST/MirrorPlayerContext/Scripts/UniqueEffectPresentation_MirrorTest.cs` | 전체 |
| E17 | `Assets/SW/TEST/MirrorPlayerContext/Scripts/WBH_CombatResolver_MirrorTest.cs` | 전체 |
| E18 | `Docs/Architecture/Armor_Relic_UniqueEffect_Review_Round01_Handover.md` | 전체. 현재 회차의 지정된 설계 참고 사본으로만 사용 |
| E19 | `Docs/Architecture/UniqueEffect_Custom_Implementation_Roadmap.md` | 전체 |
| E20 | `Docs/Architecture/UniqueEffect_Implementation_Plan.md` | 전체 |
| E21 | `logs/unity_validation_execution.log` | 전체 31행 |

manifest에 등록된 21종 모두에 접근했으며, 코드 14종은 전체를 읽었다. JSON 1종만 관련 항목을 발췌 확인했다. JSON과 아이템 asset의 `uniqueEffectId`는 `UE_GlassRailExtraHit`로 일치하고, 효과 asset·설정 클래스에는 실행 계수 `0.15` 및 표시 계수 `15`가 존재한다. 그러나 `.meta`가 없어 GUID 참조 연결까지 독립적으로 검증한 것은 아니다. [E05–E08]

### 사실·실행·출처의 구분

| 항목 | 이번 검토에서 확인한 상태 |
| --- | --- |
| 제출 코드의 호출 관계·조건·문서 간 정책 차이 | 사본 정적 검토로 확인 |
| P1 / P2-A / P2-B | E21에 각각 PASS 76 / 39 / 29가 기록됨. 합계 144는 제출 로그의 보고값 |
| P3-A | E21에는 실패가 기록됨. 정책 변경 후 성공 로그 없음 |
| 실제 입력 → AnimationEvent → 서버 발사 → 충돌 → 추가타 | 검증 코드/계획은 읽었으나 이번 검토에서 실행하지 않음 |
| Solo Host의 실제 데미지 숫자·HP·장비 교체 | P3-A 실행 증거 미제출. 193이라는 수치는 요청서의 목표/예시이지 이 검토에서 관찰한 값이 아님 |
| 원격 Client·2인/4인·Dedicated Server | 이번 회차에서 실행하지 않았고 P3-A 완료 증거도 확인하지 못함 |
| manifest의 SHA-256 재계산 | NOT_VERIFIED. 기록값을 읽었을 뿐 원시 파일 바이트를 독립 재계산하지 않음 |
| 사본과 원본의 동일성·브랜치·커밋·미커밋 변경 없음 | NOT_VERIFIED. 원본과 Git에 접근하지 않음 |
| 전체 프로젝트 컴파일·Prefab/Scene 연결·전체 회귀 | 미실행·미확인. 제출 코드의 범위를 넘어 완료 선언하지 않음 |

E21은 환경·메뉴 항목·결과를 정리한 실행 기록이다. 수정 전/후의 전체 Console 출력이나 P3-A 라이브 관찰 기록을 대체하지 않는다. 로그에 적힌 실행 커밋 `e5e81d2d6`과 코드 유입 커밋 `392be84fd`가 다르다는 사실만으로 버전 오류라고 단정하지는 않는다. 다음 회차에서는 실제 실행한 소스 상태와 제출 사본의 대응 관계가 명확해야 한다.

## 지적 사항

### 지적 목록

| ID | 심각도 | 근거 파일·줄 또는 함수 | 문제와 영향 | 최소 수정안 | 확인 방법 |
| --- | --- | --- | --- | --- | --- |
| R01-01 | 차단 | E14 `ResolveServerGunnerAttack`; E11 두 `InitializePlayerServer`, `ApplyPlayerDamage`; E13 `TryFireGlassRailExtraHit` | 실제 발사 호출부는 효과 인자 없는 초기화를 사용한다. 효과 인자가 있는 오버로드도 인자를 버리며 투사체 보관 필드가 없다. 트리거는 현재 장착 무기를 읽는다. 새 정책이 구현되지 않았고, 제안에도 실제 발사 호출부 변경이 명시되지 않았다. | 실제 서버 발사 시 Rifle/효과 출처를 확정해 투사체에 전달·보관한다. 명중 동기 스코프 안에서 같은 AttackId의 보관 효과만 조회한다. 현재 장비로의 fallback은 금지한다. | 실제 발사 경로에서 Glass→다른 무기/해제 후 추가타 유지, 일반 탄→Glass 장착 후 추가타 미발동을 모두 검증한다. |
| R01-02 | 차단 | request.md §2-1·§2-2·§4; E02 `Validate` 89·94·110·112·129·147행; E19·E20 완료 표기; E21 18–22행 | 성공 증거 없이 18/18 완료 또는 즉시 통과를 단정한다. 실패 번호와 기대값 수정 대상으로 지목한 번호도 제출 코드와 맞지 않는다. 번호만 보고 바꾸면 정상 스냅샷/정리 검사를 훼손할 수 있다. | 실행 전 계획과 관찰된 결과를 구분한다. 번호 대신 안정적인 검사 ID·의미로 기대값을 갱신하고 실제 실행 결과를 다음 회차에 낸다. | 검사별 ID/결과, 최초 실패 위치, 전체 N/N 합계가 소스·로그와 일치해야 한다. 기존 18이라는 개수를 유지하는 것이 목표가 아니다. |
| R01-03 | 중요 | E02 `CreateProjectile`, `ApplyProjectileDamage`, `Validate`; E14 `TryGetGunnerHitSource`; E04 `ValidateGunnerLiveAttacks` | 단위 검증은 실제 발사와 다른 오버로드를 호출하고 private 피해 함수로 진입한다. 스코프 정리 검사는 항상 false인 stub으로도 통과한다. 기존 라이브 검증기는 `uniqueEffect=null`로 만들고 피해 1회를 기대하므로 그대로는 P3-A를 검증하지 못한다. | 검증 준비/복구 코드는 재사용하되 실제 GlassRail 데이터와 발사 경로를 사용한다. 처리 중 양성 확인, 처리 후 음성 확인, 잘못된 출처·예외·수명 경계를 보강한다. | 아래 G01–G18의 의미별 조건과 실제 Host 관찰을 충족한다. 단위 검사 통과와 네트워크/화면 검증을 별도 표기한다. |
| R01-04 | 중요 | E07 `effectDescription`; E13 GlassRail 주석; E19 P3-A 사양; E20 정책 표·P3-A 현황; request.md §1-1 | 요청서의 새 정책과 효과 툴팁·주석·지정 문서의 “같은 장착 인스턴스/세대 유지 시에만 발동” 규칙이 충돌한다. 이전 완료 표기까지 남아 있어 재구현 방향이 다시 뒤집힐 수 있다. | 사용자 확정 정책을 우선하여 해당 설명과 검증 기준을 일치시킨다. 미실행 항목은 미검증으로 정정하고, 데이터 생성 경로 또는 수동 관리 예외를 명시한다. | “발사 후 교체/해제해도 효과 유지”가 코드·설명·검사·실행 로그에 일관되어야 한다. 현 round-01 입력은 고치지 않고 다음 회차로 제출한다. |
| R01-05 | 중요 | request.md §3-2; E18 H3/B1/R2 제안; E19 P3-B·다음 착수 절 | R2는 인계서에서 6m·마나 재생 +15% 시제품인데 요청서는 10m·종류 불명 보너스로 바뀌었다. H3/B1/R2는 실제 아이템 연결이 확정된 구현 완료 항목이 아니다. 세 종류를 묶어 안전한 착수로 판정할 근거가 부족하다. | 변경한 튜닝인지 정정인지 명시하고 실제 itemId/effectId/스탯/수치를 확정한다. P3-A 실사격 검증 다음, 기존 데이터·사건이 가장 명확한 한 종류만 선택한다. | 선택한 한 종류의 자격·발동·중복·해제·경계 검사를 통과한 뒤 다음 종류로 이동한다. 6m도 승인된 최종 밸런스로 단정하지 않는다. |
| R01-06 | 중요 | E13 `Fire`, `HandleStateEntered`, `FireIfReady`; E15 `ServerMoveGridItem`, `ServerDropInventoryItem`, `TryRestoreOwnedStateFromSnapshots`; E18 활성 책임·소유권 계약 | 방어구 어댑터가 기존 `OnDodge` 트리거까지 다시 실행하면 이중 적용 위험이 있다. 서버 그리드에서도 이동·복구 중 일시적 제거가 존재하므로 단순 제거 사건을 R2 소유 상실로 간주하면 깜박임/재적용 위험이 있다. 실제 Provider/버프/장비 하위 구현은 이번 사본에 없다. | 방어구 어댑터는 누락된 장착 자격·상시/임계형 수명 관리로 한정한다. Triggered 실행은 기존 진입점을 유지한다. 소유 효과는 서버 확정 상태와 작업 종료 후의 소유 집합을 기준으로 멱등 조정한다. | 동일 장비 알림 반복·가방 이동/회전·실패 복구에서 중복/재시작 없음, 실제 소유 이전·마지막 소유 제거 및 재활성화 경계 검증. 현재 버그가 실측됐다는 뜻은 아니다. |
| R01-07 | 경미 | E11 `TryBindPlayerVisual`, `FinishPlayerImpact`, `RpcPlayerImpact`; E12 `FindBinding`, `PlayImpact`; E14 `ServerPresentGunnerImpact` | 투사체가 보관한 impact prefab을 쓰는 RPC는 실제 명중 경로에서 호출되지 않는다. 실제 경로는 현재 장착 외형을 다시 찾으므로 교체 후 itemId가 다르면 충돌 VFX가 생략될 수 있다. 피해 숫자 경로와는 별개다. | P3-A 수치 검증과 충돌 VFX 보존 여부를 구분한다. VFX 보존까지 범위에 넣을 때만 기존 보관 참조를 쓰는 단일 표현 경로를 선택한다. 두 경로 동시 호출은 금지한다. | 비행 중 교체 전/후 Host·원격의 충돌 VFX를 확인한다. 새로운 냉기 전용 VFX 제작이나 피해 구조 재설계를 선행 요구하지 않는다. |

### R01-01 상세 — 수정안에서 빠지면 안 되는 실제 연결

제출 사본에서 확인한 경로는 다음과 같다.

```text
PlayerCombatAuthority.ResolveServerGunnerAttack
  → 효과 인자 없는 InitializePlayerServer(...)
  → NetworkEnemyProjectile.ApplyPlayerDamage
  → WBH_CombatResolver.TryProcessPlayerDamage
  → EnemyAuthority.HandleDamaged
  → ItemTriggerManager.FireDamageDealt
  → TryFireGlassRailExtraHit가 “현재 장착 무기”를 검사
```

E11의 긴 초기화 오버로드는 `instanceId`, `equipGeneration`, `effect`를 받지만 짧은 오버로드 호출 외에는 사용하지 않는다. 요청서에서 “유지”한다고 쓴 `shotUniqueEffect` 필드는 제출 사본에는 없다. E14의 `WeaponEquipGeneration => 1u`와 `TryGetGunnerHitSource(...) => false` 역시 구현된 추적/스코프가 아니라 placeholder다.

현재 조건에서 예상되는 문제는 양방향이다. GlassRail 탄을 발사하고 일반 라이플로 바꾸거나 해제하면 추가타가 빠진다. 반대로 일반 라이플 탄을 먼저 발사한 뒤 명중 전에 GlassRail을 장착하면, 탄의 출처와 무관하게 추가타를 부여할 수 있다. 후자는 제출 조건문으로부터의 정적 추론이며, 이번 검토에서 실사격으로 재현한 것은 아니다. 임의의 비영(非零) AttackId를 가진 비투사체 Direct 피해도 같은 출처 검사를 통과할 여지가 있다. [E11, E13]

최소 보완은 아래 계약으로 한정한다.

1. 실제 발사를 확정하는 서버 호출부에서 그 탄에 해당하는 Rifle 고유효과를 읽어 전달한다. 효과가 없는 탄에는 명시적으로 null을 전달한다. 이미 출발한 탄의 효과를 나중의 인벤토리 상태로 추론하지 않는다.
2. 투사체는 발사 시 확정한 효과 설정 참조를 자신의 수명 동안 보관한다. SO를 런타임에 변경하지 않는 설정 데이터 원칙을 유지하며, 공격력까지 새로 발사 시점에 고정하는 변경은 하지 않는다.
3. 적중 직전에는 서버·유효 표적·실제 Rifle 출처를 확인하고, 같은 플레이어의 Authority를 지역 변수로 잡은 뒤 해당 AttackId와 효과를 동기 처리 구간에만 노출한다.
4. 트리거는 `DamageCause.Direct`, 같은 AttackId, 유효한 투사체 출처와 보관 효과, 살아 있는 같은 표적을 확인하고 기존 `EnqueueFollowUpDamage`에 등록한다. 현재 장비로의 대체 조회는 하지 않는다.
5. 정상 반환·거절·예외에서 같은 Authority의 스코프를 `finally`로 정리한다. 이미 활성인 같은 플레이어의 스코프를 중첩 호출이 무조건 덮어쓰거나 지우지 않도록 진입을 거절하는 등 작은 가드를 둔다. 기존 동일 공격자 재진입 거절 의미를 유지하며, 큰 스코프 프레임워크는 만들지 않는다.
6. EnemyAuthority의 기존 피해 이벤트를 유일한 트리거 호출 경로로 유지한다. Projectile이나 Resolver에서 `FireDamageDealt`를 추가 호출하지 않는다.

위 목록은 수정 계약이며, 현재 존재하지 않는 API의 구현 완료를 뜻하지 않는다. 실제 발사 시그니처·테스트 호출부·조회 API를 함께 맞춰야 한다. 불필요해진 장착 세대 인자와 placeholder는 실제 호출자 확인 후 정리하되, 이번 정책과 다른 미래 소유권/다음 공격 토큰의 수명 규칙까지 일괄 삭제하지 않는다.

### R01-02 상세 — 검사 번호와 완료 보고의 불일치

E02의 지역 `Check` 선언 자체를 제외하고 실행되는 `Check(...)` 호출 순서로 계산하면 다음과 같다. 아래 번호는 **제출 사본 기준의 정적 순서**다.

| 제출 코드의 검사 순서 | 행 | 실제 의미 |
| --- | --- | --- |
| 10 | 89–90 | HP와 현재 공격력 변화를 대조하여 명중 시점 스냅샷·피해 순서를 확인 |
| 12 | 94 | 피해 처리 후 `TryGetGunnerHitSource`가 false인지 확인 |
| 13 | 110–111 | 다른 무기로 바꾼 옛 탄의 결과가 1개인지 확인하는 구 정책 기대값 |
| 14 | 112–113 | 위 교체에서 추가타 진단 카운터가 증가하지 않는다는 구 정책 기대값 |
| 15 | 129–130 | 같은 itemId의 다른 인스턴스로 바꾼 옛 탄의 추가타 미발동 기대값 |
| 16 | 147–148 | 같은 인스턴스 해제·재장착 후 추가타 미발동 기대값 |

E21의 예외 위치는 129행으로, 제출 사본에서는 15번째 검사에 해당한다. 따라서 “11개 통과 후 12번 실패”라는 요약은 이 사본의 순서와 맞지 않는다. 제출 코드 그대로 해당 지점까지 실행되었다면 앞의 14개 검사를 통과했어야 한다. 이 지적은 실패 자체를 부정하는 것이 아니라 **실행 기록과 제출 코드의 대응을 정정하라는 뜻**이다.

요청서가 지목한 10/12/14를 일괄 변경하면, 교체 정책과 무관하게 유지해야 할 스냅샷 검사와 스코프 정리 검사를 건드리게 된다. 변경할 것은 교체·다른 인스턴스·재장착 사례의 의미와 연결된 기대값이다. 기존 카운터 검사도 함께 맞추고, 새 부정 사례와 실제 발사 경로 검사를 추가한다. 검사 수가 증가하면 실제 N/N을 보고한다. [request.md §2-2, E02, E21]

### R01-03 상세 — 단위·실사격·화면 검증을 구분

E02는 Prefab 인스턴스와 실제 피해 코드를 활용하지만, 서버 상태를 reflection으로 설정하고 긴 초기화 오버로드를 직접 호출한 뒤 private `ApplyPlayerDamage`를 호출한다. 실제 입력, 발사 호출부, NetworkServer.Spawn, 물리 충돌, `FinishPlayerImpact`의 consumed/수명 경계는 이 검사만으로 검증되지 않는다.

특히 `!TryGetGunnerHitSource(...)`만 확인하는 정리 검사는 조회 함수가 항상 false여도 통과한다. **피해 처리 중에는 정확한 공격/효과가 조회되고, 처리가 끝나면 조회되지 않는다는 양성·음성 검사가 함께 있어야 한다.** 예외나 거절 다음의 정상 공격에서도 이전 탄의 효과가 남지 않아야 한다. [E02, E14]

E04의 `ValidateGunnerLiveAttacks`는 재사용할 준비·복구 절차를 제공한다. 공개 장비 트랜잭션, 실제 AnimationEvent 발사, 네트워크 적/탄 spawn 및 관찰 카운터, 중복 Collider, 벽 차단, finally 정리 등이 있다. 그러나 무기 종류별 첫 정의를 고르고 `fixtureDefinition.uniqueEffect = null`, `weaponEnchantElement = None`으로 바꾸며 피해 표시 증가를 1회로 기대한다. 따라서 이 코드를 그대로 실행한 PASS는 GlassRail 추가타 PASS가 아니다. 실제 GlassRail 정의·효과·속성을 사용하고 직접/추가 피해 쌍을 별도로 검사해야 한다.

E09의 reliable `RpcShowDamage`와 E10의 0.12초 burst 배치는 재사용 대상이다. 다만 0.12초는 적별 로컬 표시 배치 창이며 “같은 AttackId의 정확한 한 쌍”을 증명하는 키는 아니다. 서버의 AttackId·DamageCause·치명 여부·피해값·HP 변화와 클라이언트의 표시 카운터/화면을 함께 대조한다. 한 Host에서 숫자 2개를 본 결과만으로 원격 Client까지 검증 완료로 표시하지 않는다.

### R01-04 상세 — 효과 귀속과 피해 수치 기준은 별개

이번 결정은 **고유효과 자격의 발사 시점 귀속**이다. E11·E17과 E19가 사용하는 피해 수치 기준은 **명중 시 피해 해결에 진입할 때의 스냅샷**이다. 이를 구분하여 유지한다.

따라서 비행 중 무기 교체로 공격력이 달라졌다면, 현 피해 규칙에서는 그 탄의 직접타와 추가타 계산에 명중 시 공격력 스냅샷이 쓰인다. 직접타 처리 중 다른 이벤트가 공격력을 바꾸더라도 이미 시작된 추가타는 같은 스냅샷을 사용한다. “이전 무기의 효과를 유지”한다는 결정을 “발사 당시 공격력을 새로 저장”한다는 결정으로 확대하지 않는다.

추가타는 직접타의 최종 표시 피해에 0.15를 곱한 값이 아니다. 기존 Resolver에서 공격력에 0.15와 냉기 보정 등을 적용하고 방어·관통·피해 배율 등의 기존 규칙을 거쳐 계산되며, `canCrit: false`다. 공격력 100, 방어/속성 보정 등 없음이라는 테스트 조건에서는 비치명 직접 100 + 추가 15, 치명 직접 150 + 추가 15가 기준 예시다. 요청서의 직접타 193만으로 추가타의 최종 숫자를 확정해서는 안 된다. [E02, E17]

직접타의 기존 Ice 상태이상 경로는 유지하고 GlassRail 추가타의 상태이상 인자는 현재처럼 null을 유지한다. 추가타에 Freeze를 다시 넣거나 상태이상 계약을 이번 최소 수정에 끼워 넣지 않는다. 실제 Freeze 적용 횟수까지 완료 주장하려면 초기화된 상태이상 경로의 별도 관찰이 필요하다. E02의 private 피해 호출만으로 해당 게임플레이 상태이상을 검증했다고 할 수 없다. [E09, E11, E13, E14]

툴팁 제안은 “라이플 탄이 적중하면 같은 대상에게 공격력의 {0}% 냉기 피해를 추가로 줍니다. 발사한 탄의 추가 효과는 무기 교체·해제 후에도 유지됩니다.” 정도로 정책을 명시하는 것이다. 비치명 여부 등 표시 범위는 기존 툴팁 규칙에 맞춘다. 수동 SO 예외를 허용하는 현 단계에서 데이터 파이프라인 전체 개편을 선행 요구하지는 않지만, 재생성 시 효과 연결/설명이 지워지지 않도록 원천 데이터 또는 예외 관리 위치는 기록해야 한다. [E07, E19, E20]

## 추가 자료·질문

질문 때문에 현재 검토를 중단하지 않는다. 아래는 차기 요청자가 선택한 범위를 독립적으로 재검토할 수 있도록 필요한 자료다. 지금 round-01의 입력을 덮어쓰라는 요청이 아니다.

### P3-A 보완에 필요한 자료

실제 수정 상태의 E02·E11·E13·E14, 설명/상태를 정정한 E07·E19·E20, 변경 파일 목록과 각 R01 지적의 대응을 다음 회차에 포함한다. E17 등 재사용 파일도 다음 회차가 독립적으로 이해될 만큼 필요한 사본을 유지한다. 계획만 보완한 회차라면 실제 구현 전이라는 상태를 명시하고 PASS를 기재하지 않는다.

수정 구현을 완료했다고 보고하는 회차에는 실행한 코드 상태와 대응하는 전체 결과를 포함한다. 실행 환경, 시각, 브랜치/소스 상태, 실행한 메뉴/조작, 검사별 ID, 최종 합계, 실패·예외 및 정리 결과가 필요하다. P1/P2-A/P2-B 기존 통과값을 새 수정 이후 통과값으로 재사용하지 않는다.

실제 연결 검증까지 주장하려면 `GunnerNetworkPlayer.prefab`, `GunnerProjectile_MirrorTest.prefab`, `Normal_Melee_MirrorTest.prefab`의 관련 구성 사본 또는 연결을 판별할 수 있는 발췌, `GlassRailExtraHitUniqueEffectSO.cs.meta`, `UE_GlassRailExtraHit.asset.meta`를 포함한다. 실제 테스트 Scene은 요청서의 `Act1_Stage1_MirrorSessionTest`에 대응하는 정확한 경로를 manifest에 기록한다. 원본 프로젝트 전체나 무관한 Scene은 필요하지 않다.

### P3-B 한 종류를 선택한 뒤 필요한 자료

공통적으로 실제 itemId/effectId, 생성 데이터/효과 SO, 중복 정책, 서버 활성·해제 조건을 명시한다. 현재 인계서의 후보 이름과 H3/B1/R2 코드를 실제 구현 아이템 ID로 오인하지 않는다.

H3를 고르면 사용하려는 Threshold SO와 평가기, 현재/최대 마나 제공부, 마나 재생 스탯 합성부, 장착 수명 관리 경로를 포함한다. B1을 고르면 사용할 `TriggeredBuffUniqueEffectSO`, `PlayerBuffManager`, 일반 이동/회피 이동에서 해당 이동속도를 소비하는 부분을 포함한다. R2를 고르면 `PlayerRelicEffectProvider`, `BuffFieldZone` 또는 실제 오라 처리부, 원천별 버프 해제/선택 경로, 소유 변경의 실제 이벤트 발생부를 포함한다.

방어구 어댑터를 추가하는 경우에는 `EquipmentSystem`·`EquipmentTransaction`의 관련 장착/해제/복구 통지, 현재 활성화 호출자까지 필요하다. E15만으로 이 하위 구현의 순서·공유 SO 부작용·상태 재적용 여부를 확정할 수는 없다. 이번에 선택하지 않은 종류의 전체 의존성까지 한꺼번에 수집할 필요는 없다.

### 의미별 재검증 조건

아래 항목은 미실행 요구사항이다. 기존 검사 18개와 일대일 대응하는 목록이 아니며, 실제 검사 개수는 구현 결과대로 보고한다.

| ID | 조건 | 통과 기준 |
| --- | --- | --- |
| G01 | 실제 Authority 발사 경로의 GlassRail 적중 | 서버에서 보존한 해당 탄의 효과로 Direct 1회 후 Effect 1회. 두 피해의 AttackId·공격자가 일치 |
| G02 | GlassRail 발사 후 다른 무기로 교체 | 유효 표적이 직접타 후 생존하면 원래 탄의 Ice 추가타 유지 |
| G03 | GlassRail 발사 후 무기 해제 | G02와 동일. 현재 장착 슬롯이 비어도 효과 유지 |
| G04 | 같은 itemId의 다른 인스턴스 교체 및 같은 인스턴스 해제·재장착 | 각각 원래 탄의 효과 유지. 인스턴스/장착 세대로 취소하지 않음 |
| G05 | 일반 라이플 발사 후 GlassRail 장착 | Direct만 발생. 나중에 장착한 효과가 옛 일반 탄에 소급 부여되지 않음 |
| G06 | 효과 없는 탄·비Rifle·투사체 출처 없는 Direct·잘못된 AttackId | GlassRail 추가타 없음. 각 제외 사례를 구분해 기록 |
| G07 | Skill / Effect / DoT | GlassRail 재발동 없음. 기존 Direct 전용 트리거 계약 유지 |
| G08 | 공격력 100, 보정 없는 치명 직접타와 추가타 | 지정 조건의 직접 150, 추가 15; 추가는 Ice·Effect·비치명. 최종 직접 피해 × 15%로 계산하지 않음 |
| G09 | 발사 후 명중 전 스탯 변경 및 직접타 이벤트 중 스탯 변경 | 전자는 명중 시점 스냅샷을 사용하고, 후자는 그 공격의 후속 피해 스냅샷을 바꾸지 않음 |
| G10 | 직접타 처치 / 추가타 처치 | 직접타 처치 때 추가타 미발동; 추가타 처치 때 공격자 귀속과 처치 보상 1회. 정상 사망 이벤트 연결 포함 |
| G11 | 다중 Collider·중복 명중 콜백·실제 consumed 경계 | 같은 탄/대상에 Direct와 Effect가 중복되지 않음. private 함수 재호출 검사와 실제 충돌 검사를 구분 |
| G12 | 서로 다른 탄의 역순 도착 | 각 탄의 AttackId/효과가 섞이지 않고 각각 필요한 Direct/Effect 쌍이 발생 |
| G13 | 동기 스코프 진입·종료 | 처리 중 일치하는 출처 조회는 성공, 불일치 조회는 실패; 종료 후 조회 실패 |
| G14 | 예외·거절·동일 공격자 재진입·다른 플레이어의 중첩 처리 | 이전 효과 누수나 활성 스코프 훼손 없음. 플레이어별 격리와 기존 재진입 거절 유지 |
| G15 | 사망·일시 부재/연결 종료·Scene 변경·만료 | 기존 투사체 유효성/정리 경계 유지. “무기 교체에도 유지”를 “모든 수명 경계 무시”로 바꾸지 않음 |
| G16 | Solo Host 실사격 | 실제 입력/발사/충돌과 서버 HP·피해 메타데이터·표시 카운터를 대조. 고정 더미로 숫자 분리와 교체 중 적중을 관찰 |
| G17 | 기존 P1 / P2-A / P2-B | 수정한 같은 소스 상태로 재실행. 결과 합계와 실제 출력 첨부 |
| G18 | 단계적으로 원격 Client·복수 플레이어 및 데이터 연결 | Host 결과와 따로 기록. 다른 공격자 효과가 섞이지 않음, 원격 표시 정상. `.meta`/Prefab 연결 또는 실제 로딩 증거 확인 |

정식 멀티플레이 완료 판단 전에는 2인/4인 및 필요한 Dedicated 환경의 검증을 별도로 남긴다. 이번 P3-A 최소 설계 검토에서 그 환경 전체를 이미 검증했다거나 즉시 한 번에 구현·실행해야 한다고 주장하지 않는다.

## 유지할 결정과 다음 단계

### 유지할 기술 결정

효과 SO는 설정 데이터, 투사체는 발사 출처와 이동/충돌 수명, Authority는 서버 승인과 짧은 명중 출처 스코프, TriggerManager는 발동 자격과 후속 피해 등록, Resolver는 피해 수치와 큐 순서, EnemyAuthority는 실제 피해/처치 사건과 네트워크 결과, View/Presentation은 표현을 담당하도록 유지한다. 현재 필요한 정보가 있으므로 새 전역 Manager·Factory·일괄 AttackKind 체계는 추가하지 않는다. [E08–E17, E19, E20]

발사 전 예약 취소와 발사 후 효과 귀속을 혼동하지 않는다. E14의 발사 전 무기 유효성 검사는 별도 경계다. 이미 출발한 탄의 추가타를 장비 교체로 취소하지 않되, E11의 사망/일시 부재·Scene·시간 제한·충돌 소비 경계는 유지한다.

후속 피해는 같은 동기 해결의 스냅샷을 사용하고, `DamageCause.Effect`, 동일 AttackId, 비치명, 동일 생존 표적이라는 계약을 유지한다. 기존 중복 키를 실제 복수 효과 생산자가 충돌하기도 전에 전역적으로 확장하지 않는다. 추가타 수치나 게임플레이를 VFX 유무에 종속시키지 않는다.

현재 E15는 Gunner 기본 테스트 무기를 GlassRail, Fighter 기본 테스트 무기를 Inferno로 지정하고 있다. 회귀 검사는 실제 장착한 아이템을 기록해 “기본 지급이므로 원하는 효과일 것”이라고 가정하지 않는다. 이 검토를 이유로 다른 클래스의 기본 지급이나 과거 테스트 구성을 임의로 바꾸지 않는다.

### P3-B의 안전한 순서와 경계

권고 순서는 **P3-A 수정 계획 정합화 → 별도 승인된 최소 구현 → 단위/회귀 → P3-A Solo Host 실제 발사 검증 → H3/B1 중 자료가 명확한 한 종류 → 나머지 한 종류 → R2**다. E19의 “가장 명확한 한 종류부터” 원칙을 따르는 권고이며, H3/B1의 선후를 새 필수 아키텍처로 강제하는 것은 아니다.

이번 제출에서 B1의 `OnDodge` 진입점은 E13에 직접 보인다. 그래서 B1은 기존 Triggered 경로 재사용을 확인하는 작은 첫 후보가 될 수 있다. 그러나 이동속도가 회피 이동에도 소비되는지는 하위 이동 코드 없이 확정할 수 없으므로, “일반 이동만 변경”한다는 설명까지 검증한 것으로 처리하지 않는다.

| 후보 | 지정 인계서의 시제품 기준 | 착수 전에 고정할 계약 |
| --- | --- | --- |
| H3 | 마나 25% 이하에서 마나 재생 +30% | 실제 Threshold 입력/비율·정확한 스탯을 연결. 기본 재생이 0이면 퍼센트 보정만으로 회복량이 생기지 않음. 최대 마나 0, 임계 경계 왕복, 장착/해제·복구에서 중복 적용 없음 |
| B1 | 회피 상태 진입 시 2초간 이동속도 +20%, 쿨다운 6초 | 성공 회피가 아니라 상태 진입임을 명시. 기존 OnDodge 1회만 처리. 일반 이동과 회피 거리/무적/회피 쿨다운 영향은 실제 스탯 소비부로 확인 |
| R2 | 2×2 소지형 후보, 반경 6m, 자신과 살아 있는 아군의 마나 재생 +15% | 요청서의 10m 변경 여부를 명시. 같은 오라의 가장 강한 효과만 적용, 원천 A가 사라져도 B가 남으면 보존, 혼자 있을 때 자신 적용, H3와의 합성 규칙 고정 |

표의 수치는 E18의 설계 제안이지 현재 데이터에서 구현·승인 완료된 값이 아니다. R2를 기존 다른 이동속도 오라의 10m 설정과 혼합하지 않는다.

방어구 활성 어댑터는 Helmet/Chest/Boots의 실제 장착 자격과 필요한 상시/Threshold 수명 책임을 작게 맡긴다. E13이 이미 장착 아이템의 Triggered 효과를 실행하므로, 어댑터가 같은 `OnDodge`를 재발행하거나 공유 SO의 전역 `OnEquip` 경로를 그대로 호출하는 방식은 피한다. 같은 알림이 여러 번 와도 한 번 활성화된 상태가 다시 쌓이지 않아야 한다.

R2의 소유는 마우스로 드래그 중인가가 아니라 서버가 확정한 소유권으로 판단한다. E15에는 서버 그리드 이동 중 제거/재배치, 드랍 실패 시 반환, 스냅샷 복구 중 제거/재생성이 존재한다. 따라서 낮은 수준의 `Remove` 알림 하나만으로 오라를 영구 해제하거나 쿨다운을 초기화한다고 가정하면 안 된다. 서버 거래 종료 시점의 소유 집합과 원천별 효과를 멱등하게 맞추는 작은 연결을 우선 검토한다. UI의 owner-only `RequestCompleted`를 서버 전체 효과의 유일한 수명 신호로 사용하지 않는다.

요청서의 “바닥 드랍/거래 시에만 비활성화”는 소유권 구분 취지로 해석하되, 사망·부활·연결 종료·새 런·비활성화/재활성화에 대한 적용/정리 계약까지 없애는 문장으로 사용하지 않는다. 해당 생존/수명 규칙은 선택한 효과의 기준으로 명시하고 검증한다.

P4-A 상태이상 계약 → P4-B 완성 빌드 검증 → P5-A 보호막 → P5-B 다음 공격 토큰의 순서는 유지한다. 새 상태이상 틱/중첩 체계, 보호막 흡수, 다음 공격 소비 토큰은 이번 P3-A 변경에 합치지 않는다. 미래 소유권 효과의 세대 관리 필요성도 P3-A의 장착 세대 제거와 별도로 판단한다. [E18–E20]

### 다음 회차의 종료 조건

다음 회차는 각 R01 ID에 대해 수용·반박·추가 확인 및 연결 근거를 제시한다. 우선 R01-01과 R01-02의 실제 발사 전달 누락·출처 검사·성공 보고 불일치를 해소하고, R01-03의 실제 경로/양성·음성 검사를 충족한다. 문서/툴팁을 같은 정책으로 맞추고 P3-B는 선택한 한 종류의 자료로 범위를 좁힌다.

R01-07의 충돌 VFX는 수치 정확성과 분리해 처리한다. 기존 숫자 표시가 맞다는 이유로 VFX 보존도 검증됐다고 보고하지 않으며, 반대로 냉기 전용 VFX 미제작을 이유로 최소 피해 구조 전체를 재설계하지 않는다.

사용자의 별도 구현 승인이 없는 상태에서 이 검토서만으로 코드를 수정하거나 P3-B 전체 착수를 승인하지 않는다. REQUEST_READY 이후 입력 불변 원칙에 따라 추가 근거·정정은 다음 회차에 둔다. 다음 회차 폴더나 포인터는 이 검토에서 생성·갱신하지 않았다.

## 고정 범위와 저장 확인

검토 시작부터 종료 대조까지 `task-p3a-review-glassrail-001 / round-01`을 유지했다. 전역 `ACTIVE_TASK.txt`를 따라가지 않았고 다른 task 및 회차로 전환하지 않았다. `reviewer_session_label`은 사람의 구분용 별칭이며 실제 시스템 세션 ID가 아니다.

종료 대조에서 이 작업의 `CURRENT_ROUND.txt`는 여전히 `round-01`이었다. `REQUEST_READY.txt`의 task_id/round/author/ready_at도 시작 때와 같았다. 다시 읽은 `request.md`와 `evidence/manifest.md`의 내용에서 변경을 확인하지 못했고, 핵심 투사체/트리거/실행 로그 재읽기 내용도 앞선 검토와 일치했다. 이는 읽은 텍스트의 대조이며 모든 입력의 원시 바이트 해시를 재검증하거나 원자적 잠금을 확보했다는 뜻이 아니다.

원격 `round-01/review.md`와 `round-01/REVIEW_READY.txt`는 최초 및 종료 존재 확인에서 모두 ENOENT였다. 기존 검토서나 다른 작성자의 부분 파일을 덮어쓰지 않았다.

| 저장/변경 항목 | 이번 대화의 실제 상태 |
| --- | --- |
| 이 검토서 내용 작성 | 완료. 대화 첨부용 다운로드 사본으로 제공 |
| 지정 Windows 경로의 `round-01/review.md` | NOT_SAVED — 현재 노출된 원격 도구에 파일 쓰기/수정 작업이 없음 |
| 원격 `review.md` 저장 후 재읽기 | NOT_PERFORMED — 원격 저장 자체가 이루어지지 않음 |
| 지정 Windows 경로의 `round-01/REVIEW_READY.txt` | NOT_CREATED — 원격 검토서 저장·재읽기 조건 미충족 |
| `CURRENT_ROUND.txt`, `ACTIVE_TASK.txt`, Gemini 입력·evidence·공통 규칙 | 변경하지 않음 |
| 원본 코드·Scene·Prefab·프로젝트 설정·Git | 접근하여 변경하지 않음 |
| Unity 실행·컴파일·실사격·멀티플레이 시험 | 이번 검토에서 실행하지 않음 |

다운로드 파일의 작성·확인은 원격 검토 폴더에 대한 저장·재읽기를 대체하지 않는다. 따라서 **검토 내용은 전달하지만, AI-Review-Bridge의 원격 인계 완료를 선언하지 않는다.** 원격 파일 작성·재읽기가 실제로 수행되기 전에는 완료 표식을 만들지 않는다.
