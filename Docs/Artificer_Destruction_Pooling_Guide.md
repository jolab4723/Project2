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
| `WBHDroneManualTestReset` | 여러 드론 생성, R키 리셋 | 웨이브·스폰·적 풀 관리자 |
| `WBHCombatDroneDestructionTarget` | 임시 체력 1과 공격 방향 계산 | 실제 적 Health/Death 시스템 |
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
- 풀 재사용 래퍼: `CombatDroneDestructionProxy`
- 런타임 조절 대상: `ArtificerRuntimeTuningTarget`
- 독립 조절 패널: `Assets/SW/Prefabs/Debug/Artificer_Runtime_Tuning_Panel.prefab`

팀 문서와 에디터에서는 쉬운 표현인 **파괴 연출 분신**을 사용한다. `CombatDroneDestructionProxy`는 현재 코드의 내부 클래스명이다.

`CombatDroneDestructionProxy.Play(...)`에 위치, 회전, 피격 지점, 공격 방향과 방향 힘을 전달하면 파괴가 시작된다. `Completed` 이벤트는 풀 매니저가 반납 시점을 받을 때 사용하고, `ReturnToPoolNow()`는 연출을 즉시 중단하고 분신을 비활성화할 때 사용한다.

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

> `Docs/Artificer_Destruction_Pooling_Guide.md`를 읽고 실제 적 풀과 파괴 연출 분신 풀을 분리해 구현해 줘. 테스트용 WBHDroneManualTestReset, WBHCombatDroneDestructionTarget, CombatDroneArtificerDestruction에는 의존하지 말고, 실제 Health/Death 이벤트에서 EnemyDeathVfxRequest를 만들어 전달해. Artificer 시작 전에 모든 MeshElement를 Raycast + Ground 마스크로 설정하고, BuildData는 런타임 복제본만 수정해. 완료 후 다수 적 동시 파괴와 풀 재사용을 검증해.
