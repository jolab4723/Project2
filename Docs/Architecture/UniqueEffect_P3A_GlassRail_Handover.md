# 고유효과 P3-A 유리빛 궤도 인계

- 작성일: 2026-09-17
- 브랜치: `codex/unity-6000-3-22-test`
- 상태: **1인 로직 구현·자동 검증(37/37) 완료 / P1~P2 전수 회귀 PASS / Codex 후속(P3-B) 준비 완료**
- 대상: `item.weapon.rifle.glassrail` / `UE_GlassRailExtraHit`

## 1. 구현 결과

유리빛 궤도 Rifle 투사체가 살아 있는 적에게 유효한 `Direct` 피해를 주면, **투사체 발사 시점에 부여된 고유효과 자격**에 따라 같은 대상에 공격력의 15% Ice `Effect` 피해를 한 번 추가한다. 추가타는 치명타가 발생하지 않으며, 직접타와 같은 `AttackId`를 사용한다.

직접 피해는 기존 Gunner 정책을 유지한다. 공격력과 치명타 등 피해 수치는 발사 순간이 아니라 **명중 시 직접 피해 처리 진입 시점**에 계산되며, 그 스냅샷을 동기 후속타까지 사용한다.

투사체는 발사 시점(`PlayerCombatAuthority_MirrorTest.ResolveServerGunnerAttack`)에서 장착 무기 정의의 `uniqueEffect`(`UE_GlassRailExtraHit`)를 주입받아 보존하며, 명중 처리(`NetworkEnemyProjectile_MirrorTest.ApplyPlayerDamage`) 시 `TryBeginGunnerHitScope(shotAttackId, shotWeaponType, shotUniqueEffect, out IDisposable hitScope)` 스코프를 활성화하여 `ItemTriggerManager_MirrorTest`가 투사체 출처를 확인하도록 연동했다.

## 2. 수명과 교체 규칙 (GPT Review Round 01/02 정책 및 중첩 보호 반영)

- **투사체 독립 자격 유지**: 이미 발사된 유리빛 궤도 탄은 비행 도중 다른 무기로 교체하거나 무기를 해제(맨손)하더라도, 투사체 자체에 각인된 고유효과 SO에 의해 15% 냉기 추가타를 온전히 유지한다.
- **같은 정의의 다른 인스턴스/재장착**: 발사된 탄은 독립된 자격을 가지므로 인스턴스 교체나 해제·재장착 여부와 무관하게 원래 탄의 고유효과를 정상 발동한다.
- **동일 권한자 중첩 스코프 진입 거절 (R02-01)**: 이미 활성화된 스코프가 있는 상태에서 동일 Authority에 새로운 스코프 진입이 시도되면, 기존 스코프를 덮어쓰지 않고 즉시 거절하며 호출부는 피해 처리를 중단한다. 또한 스코프는 자신이 획득한 `attackId`에 대해서만 소멸자를 통해 정리된다.
- **다인 독립 격리 (R02-01)**: 서로 다른 플레이어는 동일한 `attackId`를 사용하더라도 각자의 Authority에서 완전히 독립된 스코프 수명주기를 유지한다.
- **소급 부여 차단 (G05)**: 일반 소총 탄을 발사한 뒤 비행 도중 유리빛 궤도를 장착하더라도, 옛 일반 탄에는 고유효과가 없으므로 추가타가 소급 부여되지 않는다 (Direct만 1회 발생).
- **역순 도착**: 서로 다른 탄이 발사 순서와 반대로 도착해도 각각 자신의 `AttackId` 및 투사체 출처 범위에서 `Direct`와 `Effect`를 독립적으로 정상 처리한다.

## 3. 상태이상 경계

유리빛 궤도의 직접 Ice 피해에는 기존 속성 매핑에 따라 `Freeze1`이 적용된다. P3-A 추가타는 `statusEffect: null`로 등록해 한 탄의 추가타가 Freeze를 다시 적용하거나 지속시간을 이중 갱신하지 않게 했다.

## 4. 데이터 연결

- `ItemDataTable.xlsx`의 `WeaponDefinitions` 시트에서 유리빛 궤도 행의 `uniqueEffectId`를 `UE_GlassRailExtraHit`으로 지정했다.
- JSON과 생성 `ItemDefinitionSO`를 재생성해 같은 ID와 실제 `GlassRailExtraHitUniqueEffectSO` 참조를 확인했다.
- `UE_GlassRailExtraHit.asset`은 계수 15와 `damageMultiplier = 0.15`를 가지며, 툴팁 설명(`effectDescription`) 또한 "라이플 탄 적중 시 같은 대상에게 공격력의 {0}% 냉기 피해를 추가로 줍니다. (발사 후 무기 교체 또는 해제 시에도 유지, 비치명)"으로 투사체 자격 유지 정책에 일치시켰다.

## 5. 변경 파일

- `Assets/SW/Scripts/Equipment/Effects/GlassRailExtraHitUniqueEffectSO.cs`
- `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyProjectile_MirrorTest.cs`
- `Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerCombatAuthority_MirrorTest.cs`
- `Assets/SW/TEST/MirrorPlayerContext/Scripts/ItemTriggerManager_MirrorTest.cs`
- `Assets/Editor/GlassRailExtraHitValidation_MirrorTest.cs`
- `Assets/Resources/DataFiles/ItemData/1. ExcelFile/ItemDataTable.xlsx`
- `Assets/Resources/DataFiles/ItemData/2. JSONFile/ItemDataTable.json`
- `Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/item.weapon.rifle.glassrail_유리빛 궤도.asset`
- `Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/UniqueEffectPool/UE_GlassRailExtraHit.asset`

다른 담당자 스크립트, Scene, Prefab, ProjectSettings, Packages는 수정하지 않았다.

## 6. 검증 결과

- Unity 스크립트 컴파일: **성공 (오류 0건)**
- `GlassRailExtraHitValidation_MirrorTest`: **37/37 PASS** (전수 통과)
  - 실제 생성 유리빛 궤도 데이터와 SW Gunner·일반 적·Gunner 투사체 프리팹 사용.
  - 명시적 다중 Collider 대상 중복 방지 (Direct 1회 + Effect 1회 발생 확인).
  - 치명 직접타 150과 비치명 Ice 추가타 15, 동일 `AttackId`, 처리 진입 시점 공격력 스냅샷 확인.
  - 발사 후 다른 무기 교체 시에도 원래 탄의 Direct + Effect(15% 냉기 추가타) 정상 유지 확인.
  - 같은 정의의 다른 인스턴스 교체, 무기 해제(맨손), 무기 해제 후 동일 인스턴스 재장착 시에도 원래 탄의 추가타 유지 확인.
  - [G05] 일반 탄 발사 후 유리빛 궤도 장착 시 옛 탄에 추가타가 소급 부여되지 않음을 확인.
  - 두 발의 역순 도착에서 각 탄의 `Direct` + `Effect`가 독립 처리됨을 확인.
  - [R02-01] 동일 권한자 중첩 스코프 진입 거절 및 기존 활성 스코프 안전 보존 확인.
  - [R02-01] 서로 다른 플레이어의 동일 AttackId 독립 스코프 격리 확인.
  - [R02-01] 예외 발생 시 스코프 정리 및 다음 탄 정상 동작 확인.
- 회귀 검증 전수 통과:
  - P1 Foundation (`MirrorCombatBoundaryValidation_MirrorTest.ValidateUniqueEffectP1Foundation`): **76/76 PASS**
  - 아크 블레이드 (`ArcBladeChainLightningValidation_MirrorTest`): **39/39 PASS**
  - 인페르노 (`InfernoExtraHitValidation_MirrorTest`): **29/29 PASS**
  - 플랫폼 탑승 규칙 (`MirrorPlatformValidation_MirrorTest`): **16/16 PASS**
  - 세션 규칙 (`MirrorSessionRulesValidation_MirrorTest`): **PASS (93 checks)**
  - 패시브 프로필 (`MirrorPassiveValidation_MirrorTest`): **PASS (70 checks)**
  - 스테이지 투표 (`MirrorStageVoteValidation_MirrorTest`): **PASS (31 checks)**
- 최종 Unity Console Error 0건, `git diff --check` 통과.

## 7. Codex 후속 작업 지침 (P3-B)

- P3-A 유리빛 궤도의 모든 검증과 정책 반영이 완결되었으므로, Codex는 추가 수정 없이 곧바로 후속 작업인 **P3-B** (H3/B1/R2 고유효과 파이프라인 확장)에 착수할 수 있다.
- P3-B 진행 시에도 `ItemTriggerManager_MirrorTest` 및 `PlayerCombatAuthority_MirrorTest`의 `TryBeginGunnerHitScope` 패턴을 일관되게 활용하도록 권장한다.
