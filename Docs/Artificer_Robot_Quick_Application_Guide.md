# Artificer 로봇 적용 빠른 사용법

이 문서는 `Assets/Resources_GoogleDrive/Props/Enemy`의 로봇을 원본 손상 없이 **실제 게임 적 본체**와 **파괴 연출 프리팹**으로 나누어 적용하는 초심자용 안내서다.

## 가장 중요한 결론

- `Tools` 메뉴의 옛 변환 기능을 사용하지 않는다. 최상단 `SW > Artificer` 메뉴의 안전 생성 기능을 사용한다.
- 선택한 원본은 수정하지 않는다. 새 정적 메시, BuildData, 다섯 개의 필수 컴포넌트가 연결된 파괴 연출 프리팹을 지정한 팀원 폴더에 자동 생성한다.
- 일반 적은 `일반 적 파괴 연출 프리팹 생성 (동시)`, 보스는 `보스 파괴 연출 프리팹 생성 (순차)`를 선택한다.
- 테스트 씬에는 Reset과 패널이 함께 들어 있는 `Enemy Manual Test.prefab`을 **하나만** 배치한다. 패널을 따로 추가하거나 로봇마다 붙이지 않는다.

```text
실제 게임 적 본체
└─ 체력 · AI · 공격 · 이동 · 피격 Collider
   └─ 죽을 때 파괴 연출용 분신을 풀에서 꺼냄

파괴 연출용 분신 프리팹
└─ Artificer · 파편 · 디졸브 · 풀 반환만 담당
```

## 1. 안전 생성 기능이 처리하는 대상

안전 생성 기능은 정적 메시와 스킨드 메시를 자동 판별한다.

- `MeshFilter + MeshRenderer`: 읽기 가능한 정적 메시 복사본을 만든다.
- `SkinnedMeshRenderer`: 현재 자세를 정적 메시로 Bake한다.
- 정적·스킨드 혼합형: 보이는 모든 Renderer를 같은 파괴 연출 프리팹 아래에 모은다.
- 원본의 Animator, 뼈, Collider, 체력, AI, 이동·공격 스크립트는 결과물에 복사하지 않는다.

로봇 이름이나 에셋 묶음별 방법을 외울 필요는 없다. 준비 방법은 아래 두 종류뿐이며, 도구가 Renderer 구성을 보고 자동으로 선택한다.

| 준비 방법 | 판단 기준 | 도구가 하는 일 |
| --- | --- | --- |
| 정적 메시 로봇 | `MeshFilter + MeshRenderer`만 있음 | 읽기 가능한 정적 메시 복사본 생성 |
| 스킨드·혼합 로봇 | `SkinnedMeshRenderer`가 하나라도 있음 | 현재 자세를 Bake하고 보이는 Renderer를 정적 메시로 변환 |

두 종류 모두 같은 `SW > Artificer` 생성 메뉴를 사용한다. 사용자가 로봇별 변환 방법이나 예외 설정을 미리 고를 필요가 없다.

## 2. 파괴 연출 프리팹 자동 생성

### 일반 적: 동시 파괴

1. Project 창 또는 Hierarchy에서 로봇의 맨 위 GameObject를 선택한다.
2. `SW > Artificer > 일반 적 파괴 연출 프리팹 생성 (동시)`를 누른다.
3. 저장 창에서 `Assets/<각자 팀원 폴더>/Prefabs/Enemy/...`를 선택한다.
4. 저장을 누르면 파괴 연출 프리팹, 정적 메시 폴더, Artificer BuildData 폴더가 함께 생성된다.

저장 이름에 정해진 접미사는 없다. 원본과 역할을 바로 알아볼 수 있도록 `RobotA_파괴연출`, `BossSpider_DestructionVisual`, `RobotA_DeathVFX`처럼 **팀원이 식별하기 쉬운 이름**을 사용한다. 저장 창에 제안되는 기본 이름도 필요하면 바꿔도 된다.

### 보스: 파츠별 순차 파괴

1. 보스의 파괴 자세를 먼저 만든 뒤 맨 위 GameObject를 선택한다.
2. `SW > Artificer > 보스 파괴 연출 프리팹 생성 (순차)`를 누른다.
3. 저장 위치는 동일하게 각자 팀원 프리팹 폴더를 선택한다.
4. 도구가 실제 BuildData의 `MeshElement` 수를 검사한다. 2개 이상이면 순차 파괴 프리셋으로 저장한다.
5. `MeshElement`가 1개뿐이면 도구가 생성을 취소하고 원본 메시를 파츠별로 나누라는 설명을 표시한다.

### 자동 생성되는 파일

```text
<선택한 저장 폴더>/
├─ Robot_DestructionVisual.prefab
├─ Robot_DestructionVisual_Meshes/
│  └─ Renderer별 정적 Mesh.asset
└─ Robot_DestructionVisual_Artificer/
   └─ Robot_DestructionVisual_BuildData.asset
```

변환기는 원본 또는 원본 복제본을 읽기만 한다. 결과물은 새 경로에 생성되므로 실제 게임 적 본체의 Animator와 `SkinnedMeshRenderer`는 그대로 유지된다.

## 3. 다섯 개의 컴포넌트는 자동으로 붙는다

생성된 프리팹의 맨 위 GameObject에는 다음 구성이 이미 완료되어 있다.

```text
RobotA_파괴연출                      ← 식별하기 쉬운 이름의 루트
├─ Artificer                         ← 자동 추가·Target/BuildData 연결
├─ CombatDroneArtificerDestruction   ← 자동 추가·Artificer 연결
├─ ArtificerFragmentBurstProfile      ← 자동 추가·초반 폭발/후반 감속 연결
├─ ArtificerRuntimeTuningTarget      ← 자동 추가·Artificer 연결
├─ EnemyDestructionVisual            ← 자동 추가·Artificer/Destruction 연결
└─ Renderer별 정적 Mesh 자식들
```

수동으로 다섯 컴포넌트를 추가하거나 참조 칸에 드래그할 필요가 없다. `Split Mode = Elements`, `Use Position = On`, BuildData 생성까지 자동으로 처리된다.

### 생성 후 확인할 것

- 일반 적 메뉴: `ArtificerRuntimeTuningTarget`의 파괴 방식이 `Simultaneous`인지 확인한다.
- 보스 메뉴: 파괴 방식이 `Sequential`, 기본 순서가 `Baked`인지 확인한다.
- `CombatDroneArtificerDestruction > Fragment Collision Layers`가 `Ground`인지 확인한다.
- `EnemyDestructionVisual > Auto Deactivate On Complete`가 켜져 있는지 확인한다.
- `Artificer > Custom Dismantle`에 같은 루트의 `ArtificerFragmentBurstProfile`이 연결되어 있는지 확인한다.
- `Artificer > Build Data`에 자동 생성된 BuildData가 연결되어 있는지 확인한다.

### 처음에 팍 튀고 마지막에 느려지게 시험하기

1. 씬에는 `Enemy Manual Test.prefab`을 하나만 둔다.
2. Play 후 패널의 `6. 처음에 팍 튀는 속도`에서 `강한 타격`을 누른다.
3. `현재 값 적용`을 누르고 적을 공격한다. 대상이 아직 없다면 `적 전체 다시 생성 + 적용`을 누른다.
4. 더 강한 첫 타격은 최대 `10`까지 `처음 튀는 속도 배수`를 올리고, 짧게 팍 튀게 하려면 `빠르게 튀는 구간`을 낮춘다.
5. 마지막에 더 천천히 움직이게 하려면 `마지막 속도 배수`를 낮춘다.
6. 전체 비행 범위를 기존과 비슷하게 유지하려면 `기존 이동 거리에 가깝게 자동 보정`을 켠다.

`빠르게 튀는 구간 0.10`은 0.10초가 아니라 각 파편 수명의 처음 10%다. 거리 자동 보정을 켜면 처음 속도 강조와 마지막 감속 때문에 전체 이동 범위가 불필요하게 커지는 현상을 줄인다. 중력과 바닥 충돌 뒤의 정확한 정지 위치까지 고정하는 기능은 아니다.

### 한 프리팹이면 보스 순차 파괴가 안 되는가?

아니다. **프리팹 파일이 하나인 것**과 **파괴 가능한 파츠가 하나인 것**은 다르다.

- 생성 결과는 하나의 파괴 연출 프리팹이지만, 내부에는 Renderer별 정적 메시 자식과 여러 `MeshElement`가 들어간다.
- 순차 파괴 가능 여부는 프리팹 개수가 아니라 `Artificer BuildData > Mesh Elements` 수로 결정한다.
- 실제 검증에서 Renderer가 1개인 `SciFiDroid01Animated`도 `Use Position` 분할 후 34개의 MeshElement가 생성되어 보스 순차 프리셋을 사용할 수 있었다.
- BuildData가 정말 1개 MeshElement라면 순차로 보여 줄 두 번째 파츠가 없으므로 의미 있는 파츠별 순차 파괴가 불가능하다.
- 이 경우 보스 메뉴가 생성을 중단한다. 원본을 Blender 등에서 파츠별 메시로 나누거나, 보스 단계별로 별도의 파괴 연출 프리팹·전용 컨트롤러를 설계한다.

보스도 보통은 **파츠마다 프리팹을 따로 만들지 않고**, 여러 파츠가 들어 있는 파괴 연출 프리팹 하나를 사용한다. 머리→팔→몸통처럼 정확한 연출 순서가 필요할 때만 파츠 그룹 또는 단계별 프리팹을 별도로 만든다.

### 파편 수가 기대와 다를 때

자산 이름에 따른 별도 규칙은 없다. 먼저 자동 생성된 기본값으로 재생하고, 파편이 지나치게 크거나 적을 때만 테스트 패널에서 파편 크기와 연출 값을 조절한다. 메시 분할 기준을 직접 바꾸면 BuildData 재생성이 필요하고 파편 수가 급격히 늘 수 있으므로, 초심자는 팀의 파괴 연출 담당자와 함께 조정한다.

## 4. `Enemy Manual Test` 프리팹으로 직접 공격 테스트

### 씬에 한 번만 배치

`Assets/SW/Prefabs/Enemy/CombatDrones/Enemy Manual Test.prefab`

이 프리팹에는 다음 네 컴포넌트가 이미 함께 들어 있다.

- `EnemyManualTestReset`: 여러 적 생성, R 키·버튼 리셋
- `EnemyDestructionVisualPool`: 파괴 연출을 재사용하는 테스트용 풀
- `ArtificerRuntimeTuningPanel`: 실행 중 파괴 값 조절
- `DestructionDamageStrengthScaler`: 결정타 데미지 비율을 초기 충격 배수로 바꾸는 커브

따라서 패널 프리팹을 따로 배치하거나 스크립트를 새 오브젝트에 다시 붙이지 않는다. `Enemy Manual Test`를 씬에 하나만 놓으면 된다.

### Reset의 `Enemy 1`, `Enemy 2`에 무엇을 넣나

두 칸은 특정 로봇 모델 전용이 아니라 비교 테스트를 위한 범용 슬롯이다.

| Inspector 표시 | 실제 의미 | 넣을 것 |
| --- | --- | --- |
| `Enemy 1 - 실제 적 본체` | 적 종류 1번 슬롯 | 살아 있을 때 보일 실제 적 프리팹 |
| `Enemy 1 - 파괴 연출` | 1번 적의 파괴 연출 | 같은 외형이며 이름으로 역할을 알아보기 쉬운 파괴 연출 프리팹 |
| `Enemy 1 생성 수` | 1번 적 생성 수 | 원하는 마릿수 |
| `Enemy 2 - 실제 적 본체` | 적 종류 2번 슬롯 | 살아 있을 때 보일 두 번째 적 프리팹 |
| `Enemy 2 - 파괴 연출` | 2번 적의 파괴 연출 | 같은 외형이며 이름으로 역할을 알아보기 쉬운 파괴 연출 프리팹 |
| `Enemy 2 생성 수` | 2번 적 생성 수 | 원하는 마릿수 |

예를 들어 드론과 Spider Robot을 비교하고 싶다면 `Enemy 1`에는 드론의 실제 적 본체와 파괴 연출을 한 쌍으로 넣고, `Enemy 2`에는 Spider Robot의 두 프리팹을 한 쌍으로 넣는다.

한 종류만 시험하려면 다음 중 하나를 선택한다.

- 권장: 사용할 프리팹을 한 슬롯에 넣고, 사용하지 않는 슬롯의 Count를 `0`으로 둔다.
- 두 슬롯에 같은 프리팹을 넣고 각 Count를 원하는 수로 나누어도 된다.

Count가 1 이상인 슬롯의 Prefab은 비워 두면 안 된다.

### 나머지 Reset 값

- `Current Enemy 1`, `Current Enemy 2`: 런타임 상태 확인용이므로 비워 둔다.
- `Enemy 1/2 Rotation`: 1번·2번 프리팹이 처음 바라볼 방향이다.
- `Enemy 1/2 Position`: 이전 단일 배치 호환용 값이다. 현재 다중 배치는 `Formation Center`를 기준으로 한다.
- `Grid Columns`: 한 줄에 배치할 수.
- `Horizontal Spacing`, `Depth Spacing`: 좌우·앞뒤 간격.
- `Formation Center`: 전체 대형의 중심 위치.
- `Alternate Variants`: 두 종류를 Enemy 1, Enemy 2 순서로 번갈아 배치.
- `Enemy Health`: 수동 테스트 체력. `1`이면 한 번 맞고 파괴된다.
- `Directional Force`: 플레이어 공격 방향으로 파편을 미는 힘.
- `Enemy Layer`: 테스트 적 레이어. 현재 기본값 10.
- `Reset Key`: 전체를 다시 생성하는 키. 기본값 `R`.

### 플레이어 공격을 받기 위한 프리팹 조건

시험할 실제 적 본체의 루트에는 맞을 수 있는 `BoxCollider` 같은 Collider가 있어야 한다. `Enemy Manual Test`가 적을 생성할 때 같은 루트에 `EnemyDestructionTarget`을 자동으로 추가하고 체력·방향 힘을 설정하므로, 이 테스트 스크립트를 각 프리팹에 미리 붙일 필요는 없다.

WBH 공격 코드는 맞은 Collider가 있는 GameObject에서 전투 인터페이스를 찾으므로 자식 Collider만 있는 로봇은 루트 Collider를 추가하거나 피격 전달 어댑터가 필요하다.

### 실행 순서

1. 씬에 `Enemy Manual Test.prefab`을 하나 배치한다.
2. Reset의 1번·2번 실제 적 본체·파괴 연출 슬롯과 생성 수를 설정한다.
3. Play하면 설정된 대형으로 로봇이 자동 생성된다.
4. 패널에서 파괴 방식 적용 범위와 값을 선택하고 `현재 값 적용`을 누른다.
5. `R` 또는 화면의 다시 생성 버튼으로 반복한다.

### 파편이 처음부터 힘 있게 뜨게 하기

패널의 `6. 처음에 팍 튀는 속도`에서 먼저 `강한 타격`을 누른다. 이 프리셋은 처음 속도 강조 `3.5`, 빠른 구간 `0.10`, 마지막 속도 `0.05`를 사용한다. 값을 바꾼 뒤에는 `현재 값 적용`을 누르고, 이미 생성·파괴했던 적까지 같은 조건으로 다시 시험하려면 `적 전체 다시 생성 + 적용`을 누른다.

- 파편은 위쪽으로 강제 보정되지 않고 공격 방향 초기 충격과 퍼지는 힘 방향을 그대로 사용한다.
- 착지 시간은 애니메이션 값이 아니라 속도·중력·공기 저항과 바닥 Raycast 충돌 결과다.
- 더 빨리 바닥으로 떨어뜨리려면 `중력`을 높이거나 초기 힘을 낮춘다.
- 착지 후 오래 미끄러지거나 튀면 `튕김`을 낮추고 `공기 저항`을 높인다.

### 결정타 데미지 배수 커브 시험하기

1. Hierarchy에서 `Enemy Manual Test`를 선택한다.
2. Inspector의 `Destruction Damage Strength Scaler`에서 `데미지 비율 → 초기 충격 배수` 그래프를 클릭한다.
3. 가로축은 `결정타 데미지 ÷ 적 최대 체력`, 세로축은 `공격 방향 초기 충격`에 곱할 배수로 생각한다.
4. Play 후 패널의 `7. 결정타 데미지에 따른 세기`에서 미리보기 체력·데미지를 움직여 현재 비율과 배수를 확인한다.
5. 실제 공격 테스트는 미리보기 수치가 아니라 플레이어 공격의 실제 `FinalDamage`를 사용한다.

기본 커브는 체력 대비 결정타 데미지가 `0% / 50% / 100%`일 때 `0.7 / 1.0 / 1.8배`다. 현재 기본 `공격 방향 초기 충격 3`에서는 각각 `2.1 / 3 / 5.4`가 전달된다. 패널의 기본 운동값은 `중력 2.5`, `공기 저항 1`이며, 배수 기능을 끄면 초기 충격은 항상 기본값 그대로 적용된다.

정식 적에 적용할 때는 테스트용 `EnemyDestructionTarget`을 붙이지 않는다. 실제 사망 메서드에서 죽음을 만든 공격 한 번의 최종 데미지와 적 최대 체력을 `DestructionDamageStrengthScaler.EvaluateMultiplier(...)`에 넣고, 계산된 배수를 파괴 연출 재생 요청 또는 `CombatDroneArtificerDestruction.TriggerDestruction(...)`의 네 번째 인자로 전달한다.

### 일반 적과 보스를 동시에 비교할 때

소환은 패널이 아니라 Reset의 Enemy 1·2 슬롯이 담당한다.

1. Enemy 1에는 일반 적 본체와 `일반 적 파괴 연출 프리팹 생성 (동시)` 결과물을 넣는다.
2. Enemy 2에는 보스 본체와 `보스 파괴 연출 프리팹 생성 (순차)` 결과물을 넣는다.
3. 각 생성 수를 정하고, 번갈아 배치하려면 `Alternate Variants`를 켠다.
4. 패널의 `1. 파괴 방식 적용 범위`에서 `일반/보스 구분 유지 (추천)`를 선택한다.

추천 모드에서는 일반 적은 동시, 보스는 순차 파괴를 유지한다. `보스 순차 파괴 세부 설정`에서 전체 파괴 시간과 공격 지점·중심·바깥·무작위·에셋 기본 순서를 바꿀 수 있고, 나머지 파편 수명·크기·디졸브·힘·움직임은 두 종류에 공통 적용된다.

`전체 동시 파괴`와 `전체 순차 파괴`는 두 종류의 차이를 잠시 무시하고 같은 방식으로 비교할 때만 사용한다. 다시 추천 모드를 누르면 안전 생성 프리팹에 저장된 일반·보스 구분을 복원한다.

선택 사항:

- 애니메이션 없는 정적 드론의 부유·회전: `CombatDroneVisualAnimator`
- 플레이어 추적 시험: 기존 `NavMeshAgent + EnemyFollower`
- 스킨드/Animator 로봇에는 `CombatDroneVisualAnimator`를 붙이지 않는다.

## 5. 실제 게임에 연결할 때

실제 게임에서는 테스트용 `EnemyDestructionTarget`과 `EnemyManualTestReset`을 사용하지 않는다.

```text
적 체력이 0이 됨
→ 공격 방향과 타격 지점 계산
→ 파괴 연출 분신 풀에서 하나 꺼냄
→ 파괴 연출의 EnemyDestructionVisual.Play(
     위치, 회전, 타격 지점, 공격 방향, 방향 힘)
→ 실제 적 본체는 적 풀로 반환
→ 파괴가 끝난 분신은 자동 비활성화되어 분신 풀로 반환
```

최종 확인:

- 실제 적 본체와 파괴 분신이 서로 다른 프리팹인가.
- `EnemyDestructionVisual`은 파괴 연출 프리팹에만 붙어 있는가.
- 분신 루트에 다섯 컴포넌트와 BuildData가 모두 연결됐는가.
- 테스트용 Collider와 `EnemyDestructionTarget`이 같은 GameObject에 있는가.
- 런타임 조절 패널은 씬에 하나만 있는가.
- Ground 충돌, 파편 수명, 축소·디졸브와 풀 반환을 확인했는가.
