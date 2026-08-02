# WBH 전투 - Artificer 실전 연동 가이드

이 문서는 로봇 프리팹 제작법이 아니라, **WBH 전투 코드가 적 사망 순간 SW Artificer 파괴 연출을 어떻게 호출하는지** 설명한다. WBH의 체력, 사망, 보상, 아이템 드랍과 적 본체 풀 반환은 그대로 유지한다. SW 코드는 파괴 연출 복제본의 재생과 반환만 담당한다.

## 1. 먼저 연동 방법 하나를 선택한다

두 방법을 동시에 사용하지 않는다.

| 선택 | WBH 코드 수정 | 적 루트에 붙일 것 | 언제 사용하는가 |
|---|---|---|---|
| A. 어댑터 자동 연동 | 없음 | `EnemyDestructionLink`, `WBHEnemyDestructionAdapter` | 기존 `WBH_EnemyStatus.OnDamaged`를 그대로 사용할 때 |
| B. 공식 사망 코드 직접 호출 | 사망 확정 지점에 1회 호출 | `EnemyDestructionLink` | 팀원이 공식 사망 흐름에서 직접 연결할 수 있을 때 |

장기적으로는 B가 가장 명확하다. 당장 WBH 코드를 수정하지 않으려면 A를 사용한다.

## 2. 씬 공통 준비

씬마다 활성 `EnemyDestructionService`를 정확히 하나만 둔다.

- 통합 테스트 씬: `Enemy Manual Test.prefab` 하나를 배치하면 패널, 결정타 배수와 실전 서비스가 함께 준비된다.
- 릴리스 씬: 패널이 필요 없으면 빈 오브젝트에 `EnemyDestructionService`와 `DestructionDamageStrengthScaler`만 둔다.
- `EnemyDestructionService > 파괴 연출 풀 목록`에는 각 `EnemyDestructionLink`에 연결한 것과 같은 파괴 연출 프리팹을 등록한다.
- `미리 준비할 수량`은 동시에 죽을 수 있는 적 수, `최대 풀 크기`는 허용할 동시 연출 상한으로 생각한다.

실제 적 본체는 기존 WBH 적 풀이 반환하고, 파괴 연출은 `EnemyDestructionService`가 별도로 반환한다.

## 3. A안 - WBH 코드를 수정하지 않는 자동 연동

적 프리팹 루트에 다음 두 컴포넌트를 붙인다.

1. `EnemyDestructionLink`
   - `파괴 연출 프리팹`: 서비스 풀 목록에 등록한 것과 같은 프리팹
   - `공격 방향 기본 힘`: 데미지 배수 적용 전 기본값
2. `WBHEnemyDestructionAdapter`
   - `피격 지점 계산용 콜라이더`: 적의 대표 Collider. 비워 두면 같은 루트에서 찾는다.

어댑터는 `WBH_EnemyStatus.OnDamaged`를 구독한다. `WBHEnemyDestructionAdapter.HandleDamaged(WBH_DamageResult)`에서 현재 체력이 0 이하인지 확인한 뒤 다음 메서드를 호출한다.

```csharp
destructionLink.TryPlayDeath(
    impactPoint,
    attackDirection,
    result.FinalDamage,
    status.MaxHealth);
```

정확한 타격점과 방향을 이미 알고 있다면 데미지 처리 직전에 선택적으로 기록한다.

```csharp
destructionAdapter.RecordHit(hitPoint, attackDirection);
combatManager.ProcessDamage(request);
```

`RecordHit`은 같은 프레임의 다음 `OnDamaged`에서 한 번만 사용된다. 호출하지 않으면 어댑터가 공격자 위치와 대표 Collider로 대체값을 계산한다.

## 4. B안 - 공식 사망 코드에서 직접 호출

적 루트에는 `EnemyDestructionLink`만 붙인다. 팀원의 공식 사망 메서드에서 **체력이 0 이하로 확정된 직후 한 번** 호출한다.

```csharp
if (currentHp <= 0f)
{
    destructionLink.TryPlayDeath(
        hitPoint,
        attackDirection,
        finalDamage,
        maxHealth);

    // 기존 WBH 책임은 그대로 실행
    DropReward();
    ReturnEnemyToPool();
}
```

| 인자 | 전달할 값 |
|---|---|
| `hitPoint` | 마지막 공격이 맞은 월드 좌표 |
| `attackDirection` | 플레이어에서 적 쪽으로 향하는 월드 방향 벡터 |
| `finalDamage` | 현재 체력을 0 이하로 만든 공격 한 번의 최종 데미지 |
| `maxHealth` | 죽은 적의 최대 체력 |

누적 피해나 남은 체력을 `finalDamage`에 넣지 않는다. `TryPlayDeath`는 적 본체를 비활성화하거나 풀에 반환하지 않으므로 기존 WBH 사망 처리를 이어서 실행한다.

## 5. 호출 뒤 SW 코드가 처리하는 순서

| 스크립트와 메서드 | 처리 내용 |
|---|---|
| `EnemyDestructionLink.TryPlayDeath(...)` | 한 생명당 첫 요청만 소비하고 적 Transform과 타격 정보를 스냅샷으로 만든다. |
| `EnemyDestructionService.TryPlay(...)` | 같은 씬의 풀에서 연출을 빌리고 결정타 데미지 배수를 계산한다. |
| `EnemyDestructionVisual.Play(...)` | 첫 사용 준비를 기다린 뒤 파괴 실행기를 시작하고 완료를 감시한다. |
| `CombatDroneArtificerDestruction.Play(...)` | Artificer 분해, 공격 방향 초기 충격과 Ground 충돌을 적용한다. |
| `EnemyDestructionVisual.Completed` | 서비스가 완료된 연출을 초기화하고 풀에 반환한다. |

팀원 코드는 `EnemyDestructionLink.TryPlayDeath(...)`까지만 호출한다. 아래 단계는 SW 내부 구현이므로 직접 호출하거나 완료를 감시할 필요가 없다.

## 6. WBH 코드가 계속 담당하는 것

- 실제 적 체력 감소와 사망 확정
- 공격자, 최종 데미지와 최대 체력의 원본
- 경험치·아이템·퀘스트·웨이브 같은 보상 처리
- 실제 적 AI, Collider와 Animator 중지
- 실제 적 본체의 비활성화 또는 `WBH_EnemyPoolManager` 반환

Artificer 연출 실패나 풀 부족이 실제 적의 사망 처리를 막으면 안 된다. `TryPlayDeath`의 반환값은 연출 재생 성공 여부일 뿐, 사망 성공 여부가 아니다.

## 7. 패널에서 확정한 값을 실전에 사용

런타임 패널은 연출 값을 비교하는 개발 도구다. Play 중 변경은 에셋에 자동 저장되지 않는다.

확정한 수명, 힘, 중력, Drag, 속도 커브, 크기, 디졸브와 동시/순차 값은 **파괴 연출 프리팹**의 `ArtificerRuntimeTuningTarget > Active Settings`에 저장한다. `ArtificerRuntimeTuningTarget`이 속도 커브를 `ArtificerFragmentBurstProfile`에 전달하므로 같은 값을 두 곳에 중복 입력하지 않는다.

결정타 데미지 배수는 씬의 `DestructionDamageStrengthScaler`에서 조절한다.

## 8. 자주 생기는 연결 문제

| 현상 | 먼저 확인할 것 |
|---|---|
| 사망해도 연출이 나오지 않음 | Link와 Service 양쪽에 같은 파괴 연출 프리팹이 등록됐는지, 씬 Service가 하나인지 |
| 방향이 부정확함 | `RecordHit`을 `ProcessDamage` 직전에 호출했는지, 방향이 플레이어 → 적인지 |
| 사망 연출이 두 번 호출됨 | A와 B를 동시에 사용했는지. 직접 호출할 때 Adapter를 제거했는지 |
| 지속 피해 사망만 누락됨 | `WBH_EnemyStatus.TakeDamage(float)` 경로가 `OnDamaged`를 발생시키는지 |
| 적은 죽지만 연출이 빠짐 | 풀의 미리 준비 수량·최대 크기와 해당 프리팹 등록 상태 |

## 9. 팀원용 최종 체크

- [ ] A 자동 연동 또는 B 직접 호출 중 하나만 선택했다.
- [ ] 씬에 활성 `EnemyDestructionService`가 하나 있다.
- [ ] Link와 Service에 같은 파괴 연출 프리팹을 연결했다.
- [ ] 결정타 한 번의 `finalDamage`와 적 `maxHealth`를 전달한다.
- [ ] `attackDirection`은 플레이어에서 적 방향이다.
- [ ] 기존 WBH 보상·드랍·적 풀 반환을 계속 실행한다.
- [ ] SW의 Visual·Artificer 실행기·완료 이벤트를 직접 호출하지 않는다.

팀원에게 전달할 한 문장:

> 적 사망이 확정되는 지점에서 `EnemyDestructionLink.TryPlayDeath(hitPoint, attackDirection, finalDamage, maxHealth)`만 한 번 호출해 주세요. 연출 재생·완료·풀 반환은 SW 컴포넌트가 담당합니다.
