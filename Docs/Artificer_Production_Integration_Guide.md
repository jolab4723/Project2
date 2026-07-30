# Artificer 실전 전투 연결 가이드

이 문서는 테스트 패널을 실제 적 코드에 옮기는 문서가 아니다. 실제 적 본체는 기존 전투·체력·보상·재사용 흐름을 그대로 유지하고, 사망 순간에 파괴 연출 복제본만 요청하는 구조를 설명한다.

## 1. 무엇을 어디에 붙이나

| 위치 | 붙일 컴포넌트 | 역할 |
|---|---|---|
| 전투 씬에 하나 | `EnemyDestructionService`, `DestructionDamageStrengthScaler` | 파괴 연출 복제본을 미리 준비하고 재사용한다. |
| 각 적 프리팹 | `EnemyDestructionLink` | 사망 위치·방향·데미지를 서비스에 전달한다. |
| 현재 WBH 적 프리팹 | `WBHEnemyDestructionAdapter` | 기존 `WBH_EnemyStatus.OnDamaged`를 받아 링크를 자동 호출한다. |

`EnemyDestructionService`는 씬마다 정확히 하나만 둔다. 테스트용 `Enemy Manual Test` 패널은 값 비교용으로 계속 사용할 수 있지만 실제 전투의 필수 컴포넌트는 아니다.

## 2. 씬 준비

1. 빈 게임 오브젝트를 만들고 `Enemy Destruction Service`처럼 알아보기 쉬운 이름을 붙인다.
2. `EnemyDestructionService`를 추가한다. `DestructionDamageStrengthScaler`도 자동으로 함께 붙는다.
3. `파괴 연출 풀 목록`에 사용할 파괴 연출 프리팹을 추가한다.
4. `미리 준비할 수`는 전투 중 동시에 죽을 수 있는 적 수 정도로 잡는다.
5. `최대 풀 수`는 미리 준비할 수 이상으로 잡는다. 풀이 모두 사용 중이면 현재 요청은 건너뛰고, 다음 요청을 위해 한 개만 추가 준비한다.

실제 적 본체 풀과 파괴 연출 풀은 별개다. 적 본체는 기존 `WBH_EnemyPoolManager`가 반환하고, 파괴 연출은 `EnemyDestructionService`가 반환한다.

## 3. 적 프리팹 준비

원본 에셋을 직접 수정하지 말고 각 팀원 작업 폴더의 프리팹 복사본을 사용한다.

1. 적 프리팹 루트에 `EnemyDestructionLink`를 붙인다.
2. `파괴 연출 프리팹`에 해당 로봇의 Artificer 연출용 프리팹을 넣는다.
3. 기본 방향 힘을 설정한다. 최종 세기는 이 값에 데미지 배수가 곱해진다.
4. WBH 전투 시스템을 그대로 쓰는 적이면 같은 루트에 `WBHEnemyDestructionAdapter`를 붙인다.
5. 어댑터의 `대상 콜라이더`에는 피격 위치 계산에 쓸 적 루트 콜라이더를 넣는다. 비어 있으면 같은 오브젝트의 콜라이더를 자동으로 찾는다.

WBH 스크립트 수정은 필수가 아니다. 어댑터는 기존 피격 이벤트를 듣고 체력이 0 이하가 된 프레임에 한 번만 파괴 연출을 요청한다.

## 4. 팀원 전투 코드에서 직접 연결할 때

향후 공식 사망 메서드에서 직접 호출할 수 있다면 어댑터보다 아래 방식이 더 명확하다.

```csharp
destructionLink.TryPlayDeath(
    hitPoint,
    attackDirection,
    finalDamage,
    maxHealth);
```

- `hitPoint`: 마지막 공격이 맞은 월드 좌표
- `attackDirection`: 플레이어에서 적 쪽으로 향하는 월드 방향 벡터
- `finalDamage`: 방어력 등을 적용한 마지막 실제 데미지
- `maxHealth`: 해당 적의 최대 체력

`TryPlayDeath`는 한 생명당 한 번만 성공한다. 적 본체를 비활성화하거나 풀에 반환하지 않으므로 기존 사망·보상·웨이브 처리는 그대로 둔다.

현재 WBH 구조에서 정확한 공격 방향까지 넘기고 싶다면 데미지 처리 직전에 한 줄만 추가한다.

```csharp
destructionAdapter.RecordHit(hitPoint, attackDirection);
combatManager.ProcessDamage(request);
```

`RecordHit`은 같은 프레임의 다음 피격 이벤트에서만 사용된다. 호출하지 않아도 어댑터가 공격자에서 적으로 향하는 방향과 콜라이더의 가까운 지점을 대신 계산한다.

## 5. 현재 범위와 주의점

- `WBH_EnemyStatus.TakeDamage(float)`로 직접 들어오는 지속 피해는 `OnDamaged`를 발생시키지 않으므로 현재 어댑터가 감지하지 못한다. 지속 피해도 파괴 연출이 필요하면 WBH 담당자가 공식 데미지 경로를 하나로 합친 뒤 같은 직접 호출을 연결하는 것이 안전하다.
- 장기적으로 공식 사망 코드에서 `TryPlayDeath`를 직접 호출하게 되면 해당 적의 `WBHEnemyDestructionAdapter`는 제거한다. 링크와 어댑터를 두 경로에서 동시에 호출할 필요는 없다.
- 서비스는 같은 씬의 활성 인스턴스가 정확히 하나일 때만 요청을 받는다. 중복 배치하면 안전을 위해 재생하지 않는다.
- 런타임 테스트 패널에서 확정한 값은 `ArtificerFragmentBurstProfile`과 실전 서비스 설정에 옮긴다.

## 6. 바로 확인할 수 있는 예제

- 씬: `Assets/SW/TEST/CombatDrones/Integration/CharacterMoveTest_ArtificerIntegration.unity`
- 적 복사 프리팹: `Assets/SW/TEST/CombatDrones/Integration/Droid_01 2_ArtificerIntegration.prefab`
- 파괴 연출 프리팹: `Assets/SW/Prefabs/Enemy/CombatDrones/SciFiDroid01Animated_DestructionVisual.prefab`

예제는 원본 `Assets/WBHTest` 씬과 프리팹을 수정하지 않고 SW 폴더에 복사해 연결했다.
