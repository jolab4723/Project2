# 고유효과 P3-A 유리빛 궤도 인계

- 작성일: 2026-09-17
- 브랜치: `codex/unity-6000-3-22-test`
- 상태: **1인 로직 구현·자동 검증 완료 / 실제 입력·네트워크 실플레이 미검증**
- 대상: `item.weapon.rifle.glassrail` / `UE_GlassRailExtraHit`

## 1. 구현 결과

유리빛 궤도 Rifle 투사체가 살아 있는 적에게 유효한 `Direct` 피해를 주고, 발사에 사용한 정확한 무기 인스턴스와 장착 세대가 명중 순간까지 유지되어 있으면 같은 대상에 공격력의 15% Ice `Effect` 피해를 한 번 추가한다. 추가타는 치명타가 발생하지 않으며, 직접타와 같은 `AttackId`를 사용한다.

직접 피해는 기존 Gunner 정책을 바꾸지 않았다. 공격력과 치명타 등 피해 수치는 발사 순간이 아니라 **명중 시 직접 피해 처리 진입 시점**에 계산되며, 그 스냅샷을 동기 후속타까지 사용한다. 투사체가 보존하는 것은 효과 자격 판정에 필요한 아이템 ID, `ItemInstance.instanceId`, 장착 세대, 무기 종류와 고유효과 SO뿐이다.

## 2. 수명과 교체 규칙

- 다른 무기로 교체하면 이미 날아간 탄의 직접 피해는 유지하고 유리빛 궤도 추가타만 취소한다.
- 같은 `itemId`라도 다른 `ItemInstance`로 바뀌면 추가타를 취소한다.
- 같은 인스턴스를 해제했다 다시 장착해도 장착 세대가 달라지므로 이전 탄의 추가타를 취소한다.
- 두 탄이 발사 순서와 반대로 도착해도 각각 자신의 `AttackId` 범위에서 `Direct`와 `Effect`를 처리한다.
- 공격 출처는 `NetworkEnemyProjectile_MirrorTest.ApplyPlayerDamage`의 동기 호출 동안만 `PlayerCombatAuthority_MirrorTest`에 공개하고 `finally`에서 즉시 지운다. 영구 공격 출처 Dictionary나 새 Manager는 없다.

## 3. 상태이상 경계

Round05에서 추가된 속성 매핑 때문에 유리빛 궤도의 직접 Ice 피해에는 기존 `Freeze1`이 적용된다. P3-A 추가타는 `statusEffect: null`로 등록해 한 탄의 추가타가 Freeze를 다시 적용하거나 지속시간을 이중 갱신하지 않게 했다.

## 4. 데이터 연결

- `ItemDataTable.xlsx`의 `WeaponDefinitions` 시트에서 유리빛 궤도 행의 `uniqueEffectId`를 `UE_GlassRailExtraHit`으로 지정했다.
- JSON과 생성 `ItemDefinitionSO`를 재생성해 같은 ID와 실제 `GlassRailExtraHitUniqueEffectSO` 참조를 확인했다.
- `UE_GlassRailExtraHit.asset`은 계수 15와 `damageMultiplier = 0.15`를 가진다. 15%는 시범 튜닝값이며 최종 밸런스 확정값이 아니다.
- 이번 단계는 새 효과 타입의 공용 표 스키마·임포터까지 확장하지 않았다. 양산 공정에 편입할 때 다른 담당자 데이터/임포터 스크립트 수정이 필요하면 먼저 승인을 받아야 한다.

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

- Unity 스크립트 컴파일 성공.
- `GlassRailExtraHitValidation_MirrorTest`: **18/18 PASS**.
- 실제 생성 유리빛 궤도 데이터와 SW Gunner·일반 적·Gunner 투사체 프리팹을 사용했다.
- 명시적으로 두 Collider를 둔 같은 적에서 `Direct` 1회 + `Effect` 1회를 확인했다.
- 치명 직접타 150과 비치명 Ice 추가타 15, 동일 `AttackId`, 직접타 도중 공격력을 1000으로 바꿔도 후속타 15가 유지되는 처리 진입 스냅샷을 확인했다.
- 다른 무기 교체, 같은 정의의 다른 인스턴스, 같은 인스턴스 해제·재장착에서 직접타만 남고 고유효과가 취소됨을 확인했다.
- 두 발의 역순 도착에서 각 탄의 `Direct` + `Effect`가 독립 처리됨을 확인했다.
- P2-B 인페르노 29/29, P2-A 아크 블레이드 39/39, P1 Foundation 76/76 회귀 통과.
- 최종 Console Error 0건, `git diff --check` 통과.

## 7. 아직 완료로 보지 않는 범위

- 실제 플레이어 입력 → 공격 AnimationEvent → `NetworkServer.Spawn` → 투사체 이동·물리 충돌의 solo Host 실플레이.
- Host와 원격 Client의 피해·상태이상·표현 일치, 4인 MPPM, 정식 Act1 화면·성능.
- 유리빛 궤도 전용 VFX와 음향.
- 새 고유효과 타입을 Excel 한 행만으로 완전 재생성하는 공용 임포터 확장.

다음 검증은 위 solo Host 실제 흐름부터 시작한다. 자동 검증 18/18은 실제 투사체의 피해 진입 메서드와 프리팹을 사용했지만, 입력·Spawn·비행 전체를 재생한 결과로 확대 해석하지 않는다.
