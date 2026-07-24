# Artificer 적 파괴 연출·풀링 적용 가이드

이 문서는 테스트용 드론 코드를 그대로 복사하지 않고, 실제 적 시스템에 Artificer 파괴 연출을 붙이기 위한 설계 기준이다.

## 1. 먼저 알아둘 핵심

Artificer의 파편은 각각 생성된 `GameObject + Rigidbody`가 아니다. Artificer가 `MeshElement` 데이터를 이용해 파편 위치와 속도를 직접 계산하고 그린다.

따라서 다음 내용이 중요하다.

- 자식 파츠를 분리하거나 파괴하지 않아도 된다.
- 일반적인 `OnCollisionEnter`는 파편마다 호출되지 않는다.
- 바닥 충돌도 Artificer의 `CollisionMode`와 `MeshElement.layers`로 설정한다.
- 원본 적과 파괴 연출용 분신의 풀을 분리하는 것이 안전하다.

## 2. 테스트 전용 코드와 실제 게임 코드 구분

현재 프로토타입에는 아래 테스트용 코드가 있다.

| 코드 | 테스트에서 하는 일 | 실제 게임에서의 대체 대상 |
| --- | --- | --- |
| `EnemyManualTestReset` | 여러 적 생성, R키 리셋 | 웨이브·스폰·적 풀 관리자 |
| `EnemyDestructionTarget` | 임시 체력 1과 공격 방향 계산 | 실제 적 Health/Death 시스템 |
| `CombatDroneArtificerDestruction` | Artificer 호출, 공격 방향과 Ground 충돌 전달 | 파괴 연출 분신 내부의 저수준 실행 컴포넌트로 재사용 가능 |
| `ArtificerRuntimeTuningPanel` | 런타임 수치 실험 | 개발 빌드 디버그 도구로만 선택 사용 |

실제 적 코드가 위 테스트 클래스에 의존하게 만들지 않는다. 실제 시스템에서는 아래 데이터만 파괴 연출 계층으로 넘기면 된다.

```csharp
public readonly struct EnemyDeathVfxRequest
{
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly Vector3 ImpactPoint;
    public readonly Vector3 AttackDirection;
    public readonly float DirectionalForce;
    public readonly EnemyVisualType VisualType;
}
```

최소 입력은 `피격 위치`, `공격 방향`, `방향 힘`, `사용할 외형 종류`다.

### 현재 제공되는 파괴 연출 분신

- D형: `Assets/SW/Prefabs/Enemy/CombatDrones/DestructionEffect/Sci_Fi_Drone_D_DestructionEffect.prefab`
- F형: `Assets/SW/Prefabs/Enemy/CombatDrones/DestructionEffect/Sci_Fi_Drone_F_DestructionEffect.prefab`
- 풀 재사용 컴포넌트: `EnemyDestructionVisual`
- 런타임 조절 대상: `ArtificerRuntimeTuningTarget`
- 통합 테스트 패널: `Assets/SW/Prefabs/Enemy/CombatDrones/Enemy Manual Test.prefab`

팀 문서와 에디터에서는 초심자가 역할을 바로 알 수 있도록 **파괴 연출**이라는 표현과 `EnemyDestructionVisual` 클래스명을 사용한다.

`EnemyDestructionVisual.Play(...)`에 위치, 회전, 피격 지점, 공격 방향과 방향 힘을 전달하면 파괴가 시작된다. `Completed` 이벤트는 풀 매니저가 반납 시점을 받을 때 사용하고, `ReturnToPoolNow()`는 연출을 즉시 중단하고 오브젝트를 비활성화할 때 사용한다.

### SW 안전 생성 기능

파괴 연출 프리팹은 옛 `Tools` 변환 메뉴 대신 최상단 `SW > Artificer` 메뉴에서 만든다.

| 메뉴 | 기본 파괴 방식 | 생성 후 검증 |
| --- | --- | --- |
| `일반 적 파괴 연출 프리팹 생성 (동시)` | 모든 MeshElement 동시 해제 | MeshElement 1개 이상 |
| `보스 파괴 연출 프리팹 생성 (순차)` | Baked 순서로 파츠별 해제 | MeshElement 2개 이상, 미달 시 생성 취소 |

두 메뉴 모두 선택한 원본을 수정하지 않고 새 프리팹과 Renderer별 정적 메시, 전용 BuildData를 지정한 팀원 폴더에 만든다. 프리팹 루트에는 `Artificer`, `CombatDroneArtificerDestruction`, `ArtificerFragmentBurstProfile`, `ArtificerRuntimeTuningTarget`, `EnemyDestructionVisual`을 자동 추가하고 서로 연결한다.

생성 결과가 프리팹 하나여도 순차 파괴할 수 있다. 프리팹은 파괴 연출 전체를 담는 컨테이너이고, 실제 순차 단위는 BuildData의 `MeshElement`다. Renderer가 하나여도 연결되지 않은 메시 섬이 여러 개라면 `Use Position` 분할로 여러 MeshElement가 나올 수 있다. 반대로 BuildData가 MeshElement 1개뿐인 진짜 통짜 메시라면 파츠별 순차 파괴가 불가능하므로 모델링 단계에서 파츠를 분리하거나 보스 단계별 전용 연출 프리팹·컨트롤러를 만든다.

### 런타임 조절 패널의 실제 한글 항목

테스트 씬에는 `Enemy Manual Test.prefab`을 하나만 둔다. 이 프리팹 안에 적 생성·리셋, 파괴 연출 풀과 `ArtificerRuntimeTuningPanel`이 함께 들어 있으므로 별도 패널 프리팹을 추가하지 않는다.

패널 상단의 `현재 파괴 연출: 일반 / 보스 / 전체`는 지금 활성화되어 조절 가능한 파괴 연출 수다. 아직 적을 파괴하지 않았다면 0으로 표시될 수 있으며, 적이 죽어 분신이 활성화되면 자동으로 목록을 갱신하고 현재 값을 적용한다.

| 화면 구역 | 실제 한글 항목 | 동작 |
| --- | --- | --- |
| `1. 파괴 방식 적용 범위` | `일반/보스 구분 유지 (추천)` | 일반 적은 동시, 보스는 순차 파괴를 유지한다. 공통 연출 값만 함께 적용한다. |
| `1. 파괴 방식 적용 범위` | `전체 동시 파괴` | 보스를 포함한 모든 파괴 연출을 동시 방식으로 강제한다. |
| `1. 파괴 방식 적용 범위` | `전체 순차 파괴` | 일반 적을 포함한 모든 파괴 연출을 순차 방식으로 강제한다. |
| `보스 순차 파괴 세부 설정` / `전체 순차 파괴 세부 설정` | `전체 파괴 시간`, `공격 지점부터`, `중심부터`, `바깥부터`, `무작위`, `에셋 기본 순서` | 추천 혼합 모드에서는 `보스 순차 파괴 세부 설정`, 전체 순차 모드에서는 `전체 순차 파괴 세부 설정`으로 표시된다. 적용 대상도 각각 보스만 또는 모든 대상으로 바뀐다. |
| `2. 파편이 남는 시간` | `최소`, `최대` | 파편별 유지 시간 범위를 정한다. |
| `3. 파편 크기와 사라짐` | `파편 크기`, `수명이 끝날 때 파편 크기 줄이기`, `크기 감소 시작`, `Advanced Dissolve로 부드럽게 사라지기`, `디졸브 시작`, `디졸브 무늬 크기`, `빛나는 가장자리` | 파편 크기, 축소와 디졸브를 조절한다. |
| `4. 파편 힘` | `퍼지는 힘 최소`, `퍼지는 힘 최대`, `공격 방향 힘`, `회전 세기` | 폭발 확산과 공격 방향 반응을 조절한다. |
| `5. 움직임` | `중력`, `튕김`, `공기 저항` | 바닥에 떨어진 뒤 움직임을 조절한다. |
| `6. 처음에 팍 튀는 속도` | `초반 폭발 속도 커브 사용`, `원래 움직임`, `강한 타격`, `묵직한 보스`, `처음 튀는 속도 배수`, `빠르게 튀는 구간`, `마지막 속도 배수`, `바닥에서 띄우는 비율`, `기존 이동 거리에 가깝게 자동 보정` | 파편이 처음에는 빠르게 튀고 수명 후반에는 느려지는 속도 곡선을 조절한다. 바닥에 붙은 파츠는 전체 속도를 키우지 않고 진행 방향만 위쪽으로 보정한다. |

### 처음에는 빠르고 마지막에는 느린 파편 설정

이 기능은 Artificer 원본 스크립트를 수정하지 않는다. 파괴 연출 프리팹의 `ArtificerFragmentBurstProfile`이 Artificer의 `CustomDismantle` 확장 지점에서 각 파편의 초기 속도와 이후 속도를 보정한다. 안전 생성 기능으로 만든 프리팹에는 자동으로 추가·연결되므로 팀원이 스크립트를 직접 붙일 필요가 없다.

빠르게 확인하려면 다음 순서로 사용한다.

1. Play 후 패널의 `6. 처음에 팍 튀는 속도`를 연다.
2. `강한 타격`을 누른다. 기본값은 처음 강조 `3.5`, 빠른 구간 `0.10`, 마지막 속도 `0.12`, 바닥에서 띄우는 비율 `0.35`, 거리 자동 보정 사용이다.
3. `현재 값 적용`을 누른 뒤 적을 공격한다. 아직 파괴 연출이 활성화되지 않았다면 `적 전체 다시 생성 + 적용`으로 다시 생성한다.
4. 더 날카롭게 튀게 하려면 `처음 튀는 속도 배수`를 올리고 `빠르게 튀는 구간`을 줄인다.
5. 무거운 파편처럼 보이게 하려면 `묵직한 보스`를 누르거나 `빠르게 튀는 구간`을 늘리고 `마지막 속도 배수`를 낮춘다.
6. 기존보다 너무 멀리 날아가면 `기존 이동 거리에 가깝게 자동 보정`을 켠다.

`빠르게 튀는 구간`은 초 단위가 아니라 파편 수명의 비율이다. 예를 들어 `0.10`은 각 파편 수명의 처음 10%다. 최소·최대 수명이 서로 달라도 각 파편에 같은 비율로 적용된다.

`바닥에서 띄우는 비율`은 파편의 전체 속력은 유지하면서 초기 방향에 필요한 최소 위쪽 성분을 만든다. 권장 시작값은 `0.35`다. 바닥에 붙어 미끄러지는 느낌이면 `0.40~0.50`, 너무 위로 솟으면 `0.15~0.30`으로 조절하고, `0`이면 방향 보정과 충돌 유예를 사용하지 않는다. 이 값이 0보다 크면 파괴 직후 최대 약 `0.12초` 동안만 Ground 충돌을 유예한 뒤 원래 `Raycast` 충돌로 자동 복구하므로, 파편은 바닥에서 빠져나온 뒤 정상적으로 떨어지고 멈춘다.

거리 자동 보정은 커브를 사용하지 않았을 때의 자유 비행 거리에 가깝도록 전체 속도 배율을 정규화한다. 그래서 `처음 튀는 속도 배수 3.5`는 화면에 보이는 정확한 순간 속도가 반드시 3.5배라는 뜻이 아니라 **커브 모양에서 처음을 얼마나 강하게 강조할지**를 뜻한다. 중력, 바닥 Raycast, 튕김이 개입한 뒤의 최종 정지 위치까지 완전히 같게 만드는 옵션은 아니다.

프리셋의 용도는 다음과 같다.

| 프리셋 | 용도 | 특징 |
| --- | --- | --- |
| `원래 움직임` | Artificer 기본 운동과 비교 | 속도 커브를 끈다. |
| `강한 타격` | 일반 적의 즉각적인 폭발감 | 짧고 강한 초반 가속 뒤 빠르게 감속한다. |
| `묵직한 보스` | 큰 파츠가 무겁게 퍼지는 연출 | 일반 적보다 초반 강조가 낮고 빠른 구간은 길며, 후반은 더 느리다. |

패널 없이 실제 게임에 넣을 때도 구조는 같다. 안전 생성 프리팹에 이미 저장된 기본값을 사용하거나, 개발용 패널에서 값을 확정한 뒤 프리팹의 `ArtificerFragmentBurstProfile` 값으로 옮긴다. 런타임에 코드로 바꾸려면 `ArtificerRuntimeSettings`의 `useBurstSpeedCurve`, `initialSpeedMultiplier`, `burstDuration`, `finalSpeedMultiplier`, `groundClearanceLift`, `preserveBurstTravelDistance`를 채워 `ArtificerRuntimeTuningTarget.ApplySettings(...)`에 전달한다.

`ArtificerFragmentBurstProfile`은 Artificer의 `CustomDismantle` 경로를 사용하므로, 사용자 확장에서 속도만 바꾸고 위치 적분을 생략하면 파편이 전혀 움직이지 않는다. 현재 구현은 사용자 `Remove(...)` 안에서 Artificer의 기본 `RemoveElement(...)`를 호출해 원래 중력·드래그·위치·회전·충돌 계산을 그대로 실행한 뒤 속도 곡선만 덧씌운다. 이 호출은 삭제하면 안 된다.

패널 아래 버튼은 다음 순서로 사용한다.

1. `현재 값 적용`: 활성화된 파괴 연출에 지금 값을 적용한다.
2. `적 전체 다시 생성 + 적용`: Reset의 Enemy 1·2 설정으로 실제 적을 다시 만들고 값을 적용한다.
3. `대상 목록 새로고침`: 씬의 활성 파괴 연출 목록만 다시 읽는다.

일반 적과 보스를 동시에 비교할 때는 Reset의 Enemy 1에 일반 적 본체·동시 파괴 분신, Enemy 2에 보스 본체·순차 파괴 분신을 각각 한 쌍으로 넣고 생성 수를 정한다. 패널에서는 `일반/보스 구분 유지 (추천)`를 선택한다. 일반·보스 판정은 이름이 아니라 안전 생성 시 프리팹에 저장된 기본 파괴 방식으로 구분하므로, `전체 동시 파괴`를 시험한 뒤 추천 모드로 돌아와도 원래 구분을 복원한다.

## 3. 권장 풀 분리 구조

```text
게임플레이 적 풀
└─ Enemy 본체
   ├─ 체력·AI·공격·충돌 판정
   └─ 살아 있을 때 보여 줄 일반 외형

파괴 연출 분신 풀
└─ Destruction Visual
   ├─ 동일한 로봇 외형
   ├─ Artificer
   ├─ 전용 BuildData 런타임 복제본
   └─ 파괴 종료 후 풀 반환 컴포넌트
```

적이 죽을 때 권장 순서:

1. 실제 적의 체력·AI·공격·충돌을 정지한다.
2. 실제 적의 위치와 회전을 읽는다.
3. 공격자에서 적 방향으로 `AttackDirection`을 계산한다.
4. 파괴 연출 분신 풀에서 같은 외형을 꺼낸다.
5. 분신에 위치, 회전, 충돌 레이어, 공격 방향을 전달한다.
6. 실제 적은 즉시 게임플레이 풀로 반환한다.
7. 분신은 Artificer 파괴를 재생한다.
8. 파편 유지 시간이 끝나면 분신을 파괴하지 말고 연출 풀로 반환한다.

이 구조에서는 실제 적의 부모·자식 관계가 유지되므로 적 풀 초기화가 단순하다. Artificer가 사용하는 외형 데이터는 연출용 분신 안에서만 바뀐다.

## 4. 공격 방향 연결

기본 방향은 공격자에서 피격 대상으로 향하는 방향이다.

```csharp
Vector3 attackDirection = victimPosition - attackerPosition;
attackDirection.y = 0f;

if (attackDirection.sqrMagnitude < 0.0001f)
{
    attackDirection = victimForward;
}

attackDirection.Normalize();
```

공중으로 조금 뜨게 하고 싶으면 Artificer에 전달하기 전에 위쪽 성분을 섞는다.

```csharp
Vector3 fragmentDirection =
    (attackDirection + Vector3.up * 0.2f).normalized;
```

## 5. 바닥을 뚫지 않게 하는 설정

### 현재 TestStage에서 발견한 원인

- 파편 충돌 모드: `Raycast`
- 파편 충돌 마스크: `Default` (`1`)
- 실제 바닥: `Ground` 레이어 13 (`8192`)

Raycast가 Ground를 검사하지 않아 파편이 바닥을 통과한 것이다.

### 반드시 Artificer와 모든 MeshElement에 적용

Artificer의 실제 파편 충돌 코드는 각 `MeshElement.layers`를 사용한다. Artificer 컴포넌트의 레이어만 바꾸면 이미 만들어진 BuildData 파편에는 반영되지 않을 수 있다.

파괴 직전, `StartDismantle()`보다 먼저 아래와 같은 처리가 필요하다.

```csharp
int groundLayer = LayerMask.NameToLayer("Ground");
int groundMask = groundLayer >= 0 ? 1 << groundLayer : 0;

artificer.collisionMode = Artifice.CollisionMode.Raycast;
artificer.layers = groundMask;

foreach (MeshElement element in EnumerateMeshElements(
             artificer.buildData.meshes))
{
    element.collisionMode = Artifice.CollisionMode.Raycast;
    element.layers = groundMask;
}

artificer.ClearDismantle();
artificer.StartDismantle();
```

`ClearDismantle()`는 변경된 MeshElement 설정으로 파괴 큐를 다시 만들게 하기 위해 호출한다.

### Raycast와 Simple 선택 기준

| 방식 | 사용하기 좋은 경우 | 특징 |
| --- | --- | --- |
| `Raycast` | 경사로, 계단, 높이가 다른 발판 | 실제 Collider 표면을 따라가지만 파편 수만큼 연산 비용이 증가한다. |
| `Simple` | 바닥 높이가 항상 같은 평평한 테스트장 | `collisionY` 한 값만 비교하므로 빠르지만 경사와 발판을 표현하지 못한다. |

실제 스테이지에는 `Raycast + Ground 레이어`를 권장한다. Ground로 쓸 Collider는 Trigger가 아니어야 한다.

## 6. 자연스럽게 떨어지는 권장 시작값

| 항목 | 권장 시작값 | 의미 |
| --- | ---: | --- |
| 파편 유지 시간 | 2.5~4초 | 바닥에 떨어진 모습을 볼 수 있는 시간 |
| 중력 배수 | 1.0~1.5 | 아래로 떨어지는 속도 |
| 튕김 | 0.05~0.15 | 바닥 반사량 |
| 직선 저항 | 0.2~0.5 | 미끄러짐을 줄이는 값 |
| 회전 저항 | 0.15~0.3 | 바닥에서 계속 도는 현상 감소 |
| 방향 힘 | 3~6 | 공격 방향으로 밀리는 힘 |
| 방사 힘 | 0.5~3 | 중심에서 퍼지는 힘 |

Artificer의 Raycast 충돌은 파편 메시 전체의 실제 Collider 충돌이 아니라 이동 지점 기반의 근사 충돌이다. 매우 큰 파츠는 일부가 바닥에 살짝 들어가 보일 수 있다. 정밀 물리 파괴가 필요한 보스 파츠만 별도 Rigidbody 방식으로 제작하고, 일반 몬스터는 Artificer 근사 충돌을 사용하는 편이 효율적이다.

## 7. 파편 크기 감소와 Advanced Dissolve

현재 D/F 파괴 프리팹은 수명이 끝날 때 파편이 갑자기 꺼지지 않도록 아래 두 연출을 함께 사용한다.

- **크기 감소:** `shrinkStart` 이후 `disPlaceScaleCurve`가 1에서 0으로 줄어든다.
- **디졸브:** `dissolveStart` 이후 Advanced Dissolve의 `_AdvancedDissolveCutoutStandardClip`을 파편별로 0에서 1까지 올린다.

### 스케일 곡선 방향 주의

Artificer는 폭발 파편을 그릴 때 `RemoveElement()`에 일반적인 진행률이 아니라 `1 - fragmentProgress`를 전달한다. 따라서 원시 `disPlaceScaleCurve`는 파괴 중 x축을 `1→0` 방향으로 읽는다.

현재 `ArtificerRuntimeTuningTarget`은 이 역방향 진행값을 내부에서 보정하며, 다음 조건을 만족하도록 곡선을 만든다.

- 파편 시작: `Evaluate(1) = fragmentScale`
- 축소 시작: `Evaluate(1 - shrinkStart) = fragmentScale`
- 파편 종료: `Evaluate(0) = 0`

일반적인 `0→1` 진행률 기준으로 축소 곡선을 만들면 파편이 0 크기로 나타나 점점 커진 뒤 디졸브되는 것처럼 보인다. 다른 구현에서 Artificer의 스케일 곡선을 직접 설정할 때도 반드시 이 방향을 확인한다.

런타임 패널의 `파편 크기와 사라짐` 구역에서 다음 값을 시험할 수 있다.

| 패널 항목 | 의미 | 권장 시작값 |
| --- | --- | ---: |
| 파편 크기 | 원래 파편 크기에 곱할 배율 | 1.0 |
| 크기 감소 시작 | 파편 수명 중 축소가 시작되는 비율 | 0.70 |
| 디졸브 시작 | 파편 수명 중 디졸브가 시작되는 비율 | 0.65 |
| 디졸브 무늬 크기 | 트라이플래너 Cutout Map 반복 크기 | 2.5 |
| 빛나는 가장자리 | 디졸브 경계선 두께 | 0.06 |

예를 들어 시작값이 `0.70`이면 파편 수명의 마지막 30% 동안 작아진다. 파편 유지 시간이 4초라면 약 2.8초부터 축소가 시작된다.

`ArtificerRuntimeTuningTarget`은 원본 로봇 재질을 직접 수정하지 않는다. 각 파괴 인스턴스의 런타임 BuildData에서만 Advanced Dissolve 재질 복사본을 만들고, 풀에서 해당 인스턴스가 폐기될 때 복사본을 정리한다. 따라서 살아 있는 로봇의 재질과 공유 원본 재질에는 영향이 없다.

다른 로봇에 적용할 때는 다음 조건을 확인한다.

1. 팀 프로젝트에 `Amazing Assets/Advanced Dissolve/Lit` 셰이더가 있어야 한다.
2. `ArtificerRuntimeTuningTarget`의 `Dissolve Map`에 Cutout Map을 지정한다. 비어 있으면 절차적 노이즈 맵을 자동 생성한다.
3. 커스텀 툰 셰이더를 쓰는 적은 Advanced Dissolve Lit으로 바꾸면 외형이 달라질 수 있으므로 전용 디졸브 셰이더를 준비한다.
4. 파편 렌더링은 `Renderer` 오브젝트가 아니라 `Graphics.RenderMesh`이므로 일반 Renderer 페이드 스크립트로는 제어할 수 없다.

파괴 시작 시 원본 Renderer를 너무 일찍 끄면 한 프레임 깜빡일 수 있다. 현재 `CombatDroneArtificerDestruction`은 Artificer 원본 소스를 수정하지 않고, 파편 렌더가 시작될 때까지 원본 Renderer를 한 프레임 유지해 전환 공백을 줄인다.

## 8. 공유 BuildData를 직접 수정하지 않기

같은 BuildData 에셋을 여러 적이 공유한 상태에서 런타임 값을 직접 변경하면 모든 인스턴스에 영향을 줄 수 있다.

권장 방식:

1. 연출 분신을 풀에서 꺼낼 때 BuildData 런타임 복제본을 만든다.
2. 충돌·힘·파괴 순서는 복제본에만 적용한다.
3. 연출 분신을 풀로 반환할 때 런타임 상태를 초기화한다.
4. 분신을 완전히 폐기할 때만 복제한 ScriptableObject를 정리한다.

현재 프로토타입의 `ArtificerRuntimeTuningTarget`이 이 원칙을 보여 주는 참고 구현이다.

## 9. 풀 반환 시 초기화할 값

파괴 연출 분신을 다시 쓸 때 최소한 다음을 복원한다.

- Artificer 파괴 큐와 진행 상태
- 원본 Renderer 표시 상태
- 파편 순서 목록
- Collider 활성 상태
- NavMeshAgent 또는 이동 컴포넌트 상태
- 런타임 힘 방향과 충돌 레이어
- 코루틴과 지연 반환 예약
- 위치, 회전, 로컬 스케일

풀에서 꺼낼 때 초기화가 끝나기 전에는 화면에 표시하지 않는다.

## 10. 다수 적 성능 주의점

드론 한 기가 약 47~48파츠이므로 10기가 동시에 파괴되면 약 480개의 파편이 움직인다. Raycast 방식은 움직이는 파편마다 충돌 검사를 수행할 수 있다.

권장 최적화 순서:

1. 일반 몬스터는 동시 파괴를 사용한다.
2. 파편 유지 시간을 필요 이상 길게 두지 않는다.
3. 멀리 있는 적은 파편 수가 적은 BuildData 또는 단순 VFX를 사용한다.
4. 동시에 재생할 파괴 연출 분신 수에 상한을 둔다.
5. 화면 밖에서는 즉시 또는 짧은 시간 후 풀로 반환한다.
6. 평평한 전용 방에서는 필요할 때만 `Simple` 충돌을 선택한다.

## 11. 다른 로봇에 적용하는 체크리스트

- [ ] `SW > Artificer`의 일반 적 또는 보스 안전 생성 메뉴를 사용했는가?
- [ ] 생성된 프리팹 루트에 다섯 컴포넌트가 자동 연결되어 있는가?
- [ ] 생성된 BuildData가 팀원 폴더 아래에 있고 프리팹에 연결되어 있는가?
- [ ] 보스 순차 파괴라면 BuildData의 MeshElement가 2개 이상인가?
- [ ] 파괴 가능한 외형의 모든 Renderer가 Artificer 대상에 포함되어 있는가?
- [ ] 해당 외형 전용 BuildData를 만들었는가?
- [ ] BuildData를 인스턴스별로 안전하게 복제하는가?
- [ ] 실제 적과 파괴 연출 분신 풀이 분리되어 있는가?
- [ ] 공격 방향 벡터를 사망 요청에 포함했는가?
- [ ] 파괴 전에 모든 MeshElement에 Ground 충돌 마스크를 적용했는가?
- [ ] 바닥 Collider가 Ground 레이어이고 Trigger가 아닌가?
- [ ] 실제 적의 AI와 Collider를 먼저 중지했는가?
- [ ] 파편 유지 시간이 끝난 뒤 분신이 풀로 반환되는가?
- [ ] 축소·디졸브 시작값과 Cutout Map이 의도대로 적용되는가?
- [ ] 10기 이상 동시 파괴 성능을 확인했는가?

## 12. AI에게 구현을 요청할 때 전달할 내용

아래처럼 요청하면 된다.

> `Docs/Artificer_Destruction_Pooling_Guide.md`를 읽고 실제 적 풀과 파괴 연출 풀을 분리해 구현해 줘. 테스트용 EnemyManualTestReset, EnemyDestructionTarget에는 의존하지 말고, 실제 Health/Death 이벤트에서 EnemyDeathVfxRequest를 만들어 전달해. 파괴 연출 내부의 CombatDroneArtificerDestruction은 재사용해도 된다. Artificer 시작 전에 모든 MeshElement를 Raycast + Ground 마스크로 설정하고, BuildData는 런타임 복제본만 수정해. 완료 후 다수 적 동시 파괴와 풀 재사용을 검증해.
