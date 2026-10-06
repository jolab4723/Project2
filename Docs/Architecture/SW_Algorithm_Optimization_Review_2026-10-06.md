# SW 스크립트 알고리즘 최적화 검토 목록

- 작성일: 2026-10-06
- 기준 브랜치: `codex/unity-6000-3-22-test`
- 문서 상태: 1~9절은 최초 정적 리뷰이며, 후속 구현의 채택·보류 판단과 실제 검증 결과는 10절에 기록했다.
- 목적: SW 영역에서 향후 수정할 파일, 수정이 필요한 이유, 변경 범위와 검증 기준을 정리한다.
- 작업 상태: SW 런타임 후보 6개를 전부 또는 일부 반영했다. Unity CLI로 컴파일·격리 회귀·실제 싱글 전투를 검증하고 개인 구현 로그를 갱신했다. Scene·Prefab·설정과 타 담당자 코드는 변경하지 않았다.

## 1. 범위와 판단 기준

앞선 SW·WBH·YJ·WJ 전수 리뷰에서 확인한 후보 중, 이번 문서의 본 목록은 **`Assets/SW/**` 안의 스크립트만** 다룬다. SW 무기 제작 흐름에 연결된 `Assets/Editor/**` 도구는 별도 부록에 둔다. 다른 담당자의 코드는 원인을 설명하는 의존성으로만 언급하며 수정 대상에 포함하지 않는다.

이 문서의 높은 우선순위는 코드 구조에서 개선 가능성이 크다는 뜻이다. 실제 CPU 시간, GC Alloc, 프레임 저하의 주원인을 측정해서 확정한 것은 아니다. 작은 고정 상한, 낮은 호출 빈도, 특정 프리팹 구조에 의존하는 항목은 조건부 또는 후순위로 표시했다. 여러 세션에서 작업 중이므로 링크의 줄 번호는 작성 시점 기준이고, 구현 직전에는 함수 이름으로 현재 코드를 다시 확인해야 한다.

복잡도 표기의 Dictionary·HashSet 조회는 평균적인 비용을 뜻한다. 표에 적힌 개선 비용은 해당 검색·중복 계산 부분의 비용이며, 물리 질의·렌더링·직렬화·이벤트 구독자의 비용까지 모두 없어진다는 의미는 아니다.

### 우선순위

| 구분 | 의미 |
| --- | --- |
| A | 첫 수정 묶음으로 검토할 항목. 실제 플레이 흐름에서 반복되거나 증가에 따른 비용이 뚜렷함 |
| B | 데이터·동시 대상 수가 커지거나 해당 기능을 사용할 때 개선 효과가 기대되는 항목 |
| C | 저빈도 처리, 작은 현재 규모, 개발 도구 또는 구현 난이도 대비 효과 확인이 필요한 항목 |

## 2. SW 런타임 수정 후보 요약

| ID | 우선순위 | 대상 | 현재 비용 또는 중복 | 수정 방향 |
| --- | --- | --- | --- | --- |
| SW-01 | A | PlayerInventorySync | 아이템마다 스냅샷 검색·JSON 해석: O(I²·L) | 작업 단위로 한 번 해석하고 instanceId 인덱스 공유 |
| SW-02 | A | ArtificerFragmentBurstProfile | 파편마다 목록 검색: 한 회차 최악 O(F²) | 부위·piece 의미를 반영한 조회 인덱스 |
| SW-03 | A, 공용 코드 의존성 있음 | PlayerRuntimeStateSync | 전체 버프 재구성·스택별 재계산: 버프 순회 부분 O(T·B) | 버프 변경 판별, 시간 갱신 분리, 가능한 범위의 일괄 적용 |
| SW-04 | A/B | NetworkItemTriggerManager, PlayerItemEffectState | 과거 쿨타임 키 전체 순회: O(H)/서버 프레임 | 변경 키 동기화, 안전한 만료 키 정리 |
| SW-05 | B | PlayerItemEffectState | 공격 트리거마다 전체 그리드 검색 | 소지·장비 변경 시 트리거별 후보 목록 갱신 |
| SW-06 | B, 구조에 따라 효과 달라짐 | GunnerWeaponVfxBinding, ProjectileVisualAnimator | 모든 PS를 열거한 뒤 하위 PS까지 재귀 처리 | 각 PS를 한 번 처리하고 컴포넌트 배열 재사용 |
| SW-07 | B | PlayerInventorySync, NetworkShopState | 아이템마다 Scene/Resources 검색, ID 비교용 객체 생성 | 기존 DB 전달·조회 재사용, 최소 데이터만 해석 |
| SW-08 | B/C | MirrorBossIntro | 매 프레임 본 이름을 형제 전체에서 검색 | 인트로 시작 시 본 쌍을 구성하고 재사용 |
| SW-09 | B/C | PlayerItemEffectState, PlayerSpatialShotEffects | 소수 대상만 사용할 때도 후보 전체 정렬 | 해당 경로에 한해 상위 K개 선택 |
| SW-10 | C | GunnerWeaponVfxBinding | 사거리가 같아도 공격마다 PS 모듈 재설정 | 사거리 변경 시에만 갱신 |
| SW-11 | C, 큰 아이템에서 재검토 | InventorySwapPlanner | 배치 후보 두 집합의 모든 조합에서 칸 검사 | 각 후보의 독립 검사를 먼저 수행 |
| SW-12 | C | NetworkEnemyWaveSpawner | 살아 있는 항목을 반복 이동시키는 RemoveAt | 안정적인 일괄 필터 또는 압축 |
| SW-13 | C | UniqueEffectPresentation | VFX 반환마다 List.Remove 선형 검색 | 순서 불필요 시 HashSet 또는 제거 방식 변경 |
| SW-14 | C | PlayerRelicEffectRuntime | 전체 해제 중 남은 유물을 매번 검색 | 효과별 개수·대표 객체 재사용 또는 전용 일괄 정리 |
| SW-15 | C | ArtificerRuntimeTuningPanel | 등록할 때마다 중복 검색과 전체 정렬 | 일괄 수집 후 한 번 정렬 |
| SW-16 | C | ItemDropRollService, ShopStockRollService | 같은 DB 후보를 추첨마다 여러 번 검색 | 추첨 묶음에서 후보·분류 결과 재사용 |

## 3. 우선 수정 후보 상세

### SW-01. 인벤토리 스냅샷 검색과 JSON 해석의 중첩

**대상**

- [PlayerInventorySync.cs:1703](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Network/Player/PlayerInventorySync.cs:1703): `FindSnapshotIndex`
- 같은 파일의 `HasOneSnapshotPerOwnedItem`(1715), `IsOwnedStateEqualToSnapshots`(1749), `TrySynchronizeOwnedSnapshots`(1760)

**현재 동작과 수정 이유**

`FindSnapshotIndex`는 스냅샷 배열을 앞에서부터 검색하며 각 항목을 `FromSnapshotJson`으로 해석한다. 소유 아이템 전체를 검증하는 함수는 아이템마다 이 검색을 호출한다. 동기화 함수는 전체 검증을 마친 뒤 다시 같은 검색을 반복한다.

소유 아이템·스냅샷이 각각 I개이고 JSON 하나의 길이가 L이면, 해당 조회·해석 부분은 O(I²·L)까지 늘어난다. 아이템이 두 배가 될 때 비교 횟수가 대략 네 배가 되는 구조이며, 문자열을 비교하는 비용 외에도 JSON 객체 생성이 반복된다. 이동·장비 변경·스냅샷 검증처럼 정상 플레이 중 사용하는 경로라 첫 수정 후보로 삼을 만하다.

현재 주석은 소유 아이템이 적은 테스트 환경에서 단순성을 선택했다고 명시한다. 따라서 작은 인벤토리에서 당장 큰 프레임 저하가 발생한다고 단정할 수는 없다.

**권장 수정 범위**

먼저 **동일 작업 안에서만 유지하는 인덱스**를 도입하는 것이 가장 작고 안전하다. 스냅샷을 한 번씩 해석해 `instanceId → 인덱스/해석 결과`를 구성하고 검증·비교·동기화가 이를 공유하게 한다. 이 부분은 평균 O(I·L)로 줄일 수 있다. 장기 캐시는 SyncList 변경과 서버·클라이언트 수명 처리를 추가로 요구하므로 필요성이 확인된 뒤 검토한다.

**보존할 동작과 향후 검증**

중복 instanceId, 누락 항목, 그리드/장비 소유권, 실패 시 롤백, 서버 권한과 동기화 순서를 유지해야 한다. 동일한 I개 아이템으로 이동·회전·장비 교환·획득을 반복해 JSON 해석 횟수, 함수 CPU 시간, GC Alloc을 비교한다. 큰 인벤토리뿐 아니라 현재 작은 인벤토리에서도 거래 결과가 같아야 한다.

### SW-02. 파편별 BuildQueue 선형 조회

**대상**

- [ArtificerFragmentBurstProfile.cs:147](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Enemy/Destruction/ArtificerFragmentBurstProfile.cs:147): 제거/이동 처리에서 `FindQueue` 호출
- [ArtificerFragmentBurstProfile.cs:338](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Enemy/Destruction/ArtificerFragmentBurstProfile.cs:338): `FindQueue`

**현재 동작과 수정 이유**

`states`가 Dictionary여도 `FindQueue`는 키를 직접 조회하지 않고 `states.Keys` 전체를 순회한다. 여기서 찾지 못하면 BuildQueue를 다시 검색하고, 정확한 piece 검색이 실패한 경우 element만으로 한 번 더 검색한다. 파편 하나를 찾는 데 O(F), F개를 모두 처리하는 회차에서는 최악 O(F²) 조회가 된다.

이 구조는 파편이 많은 보스나 여러 적의 동시 파괴에서 비용이 겹칠 수 있다. 단, 이번 리뷰는 외부 Artificer 코드의 실제 콜백 횟수와 Profiler 시간을 측정하지 않았으므로, 파괴 시 모든 부하를 이 검색의 탓으로 볼 수는 없다.

**권장 수정 범위**

추적 시작과 Queue 변경 시 조회 인덱스를 구성한다. 현재 빠른 검색은 element만 비교하고 fallback은 piece도 비교하므로, 먼저 **element와 piece의 유일성 및 fallback 선택 규칙**을 확인해야 한다. 이를 확인하지 않고 단순한 element 사전으로 바꾸면 다른 파편을 선택할 수 있다.

등록·추적 해제·풀 반환·재사용 때 인덱스를 함께 정리하고, 정상 조회는 평균 O(1)로 만든다. F개 조회 비용은 평균 O(F)로 줄어든다.

**보존할 동작과 향후 검증**

`artificer.RemoveElement(...)`는 유지해야 한다. 이동·중력·Drag·충돌 처리와 연결되므로 검색 최적화를 위해 제거하면 안 된다. 공유 BuildData와 외부 패키지 원본도 변형하지 않는다. 일반 적 다수의 동시 파괴와 보스 부위 분리를 별도로 측정하고, 파편 위치·piece 선택·풀 재사용·시각 결과를 비교한다. 이번 문서에서는 파편 속도나 품질 저하 수치를 제안하지 않는다.

### SW-03. 체력·마나 동기화에도 버프 전체 재적용

**대상**

- [PlayerRuntimeStateSync.cs:303](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Network/Player/PlayerRuntimeStateSync.cs:303): 스냅샷마다 `ApplyClientBuffs` 호출
- [PlayerRuntimeStateSync.cs:329](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Network/Player/PlayerRuntimeStateSync.cs:329): 전체 제거 후 버프·스택별 재적용

**현재 동작과 수정 이유**

클라이언트는 스냅샷을 받을 때 모든 버프를 지우고, 각 버프의 stackCount만큼 `ApplyBuff`를 호출한 뒤 활성 목록을 다시 찾아 스택과 시간을 지정한다. 최종 서버 스탯도 이후 별도로 적용한다.

연결된 WJ `PlayerBuffManager.ApplyBuff`는 적용이 성공할 때마다 스탯 전체를 재계산한다. 버프 B종·총 재적용 횟수 T에 대해 버프 순회 부분만 O(T·B) 수준이며, 버프당 스택 S가 비슷하면 O(S·B²)로 표현할 수 있다. 여기에 장비·패시브 재계산과 이벤트 구독자의 비용도 추가된다.

체력·마나만 바뀌었거나 버프 시간이 줄어든 경우에도 같은 전체 재구성을 수행하는 점이 핵심이다. 현재 동기화 버프는 최대 32종으로 제한되어 있지만, 작은 상한에서도 수신 빈도가 높으면 불필요한 재계산과 UI 재구성이 반복된다.

**SW 안에서 먼저 개선할 범위**

이전 스냅샷 및 현재 적용 상태와 비교해 버프 구성·스택·전달된 효과 값이 동일하면 전체 제거/재적용을 생략한다. 남은 시간만 변경된 경우에는 기존 인스턴스의 시간 갱신으로 처리할 수 있는지 확인한다. 비교 자체는 O(B)로 제한하는 것이 좋다.

`ResolveBuffSource`의 fallback은 새 `SnapshotBuffSource`를 만들기 때문에 참조 동일성만으로 비교하면 안 된다. sourceKind·sourceId와 실제 효과 데이터를 함께 비교해야 한다. 최초 적용·알 수 없는 소스·씬 전환·사망/부활·0스택 폐열 처리는 별도로 보존한다.

**공용 코드 변경이 필요한 부분**

버프 전체를 일괄 적용한 뒤 스탯 재계산과 알림을 한 번만 보내는 완전한 해결은 WJ 버프 API의 지원이 필요할 수 있다. 이번 SW 목록에서는 그 파일을 수정 대상으로 잡지 않는다. 버프 수가 그대로라는 이유로 동적 StatEffects 변경까지 무시해서도 안 된다.

**향후 검증**

체력만 변경, 마나만 변경, 시간만 감소, 스택 증가/감소, 효과 값 변경, 버프 추가/제거, 사망/부활을 구분해 확인한다. 스냅샷당 재계산·아이콘 재구성 횟수를 측정하고 최종 스탯과 서버 권위 상태가 동일한지 비교한다.

### SW-04. 과거 쿨타임 키 증가와 매 프레임 전체 동기화 검사

**대상**

- [NetworkItemTriggerManager.cs:421](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Network/Player/NetworkItemTriggerManager.cs:421): `LateUpdate → PublishState`
- [PlayerItemEffectState.cs:791](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Player/PlayerItemEffectState.cs:791): PerItem 쿨타임 키 구성
- [PlayerSpatialShotEffects.cs:69](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Player/PlayerSpatialShotEffects.cs:69): WorldEnder 최초 충전 판정

**현재 동작과 수정 이유**

서버는 매 프레임 `effects.Cooldowns` 전체를 순회한다. 값이 달라진 항목만 SyncDictionary에 기록하므로 매 프레임 모든 항목을 네트워크로 재전송하는 것은 아니다. 그러나 비교를 위한 전체 순회는 계속 수행한다.

PerItem 버프 키는 효과 이름과 instanceId로 만들어지고, 이번 검토에서 그 과거 키를 제거하는 경로는 확인되지 않았다. 따라서 현재 소지 아이템 수가 적어도 이전에 사용한 아이템 수 H에 따라 O(H)/프레임 비용과 사전 메모리가 증가할 수 있다. 이는 O(H²) 알고리즘이 아니라, 장기 세션에서 커지는 목록을 매 프레임 다시 읽는 문제다.

**권장 수정 범위**

쿨타임 변경이 일어나는 기존 공통 지점에서 변경된 키를 표시해 그 항목만 게시하는 방식을 검토한다. 만료되고 더 이상 필요한 의미가 없는 PerItem 키는 적절한 시점에 정리하고, 원본에서 삭제할 때 동기화 사전에서도 삭제한다.

만료 키 전체를 무조건 삭제하면 안 된다. WorldEnder는 키가 없는 상태를 최초 충전 시작으로 해석하고, 진행 중인 쿨타임을 장비 교체 때 삭제하면 쿨타임 우회가 가능하다. 키 종류·만료·소유 상태에 따른 정리 조건을 먼저 구분해야 한다.

**향후 검증**

많은 instanceId로 발동 후 폐기·교체를 반복한 긴 세션에서 사전 크기와 PublishState 시간을 확인한다. Shared/PerItem 중복 정책, 장비 교체, 동일 아이템 재획득, WorldEnder 충전, 원격 클라이언트의 쿨타임 표시가 유지되어야 한다.

### SW-05. 공격 트리거마다 전체 인벤토리 검색

**대상**

- [PlayerItemEffectState.cs:382](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Player/PlayerItemEffectState.cs:382): `FireDamageDealt`
- [PlayerItemEffectState.cs:403](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Player/PlayerItemEffectState.cs:403): `Fire`
- [InventoryGrid.cs:226](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Inventory/Core/InventoryGrid.cs:226): `GetAllItems`

**현재 동작과 수정 이유**

`Fire`는 장비와 전체 그리드를 읽어 유물 발동 후보를 찾는다. `GetAllItems`는 G개 칸을 검사해 HashSet으로 중복을 제거하고 새 목록을 반환한다. 직접 피해가 발생할 때 호출되고 치명타에서는 OnDamageDealt와 OnCrit로 연속 호출한다. 해당 트리거 효과가 없어도 같은 검색을 한다.

공격 횟수 H에 대한 그리드 탐색이 O(H·G)이며, 각 호출의 임시 컬렉션도 발생한다. 개별 탐색은 선형이고 정상적이지만 소지 정보가 바뀌지 않았는데 매 공격마다 같은 후보를 재발견하는 부분은 줄일 수 있다.

**권장 수정 범위**

기존 소지·장비 변경 이벤트와 재조정 흐름을 사용해 트리거별 후보 목록을 갱신한다. 최소 변경으로는 한 피해 이벤트에서 찾은 소지 목록을 치명타 검사까지 재사용할 수 있다. 후보가 비어 있으면 즉시 종료하게 한다.

**보존할 동작과 향후 검증**

유물은 인벤토리에만 있어도 발동할 수 있다는 현재 규칙, Shared/PerItem 중복 정책, 지속 스택, 서버 실행 권한, 발동 순서와 이벤트 중 소지 상태 변경을 보존한다. 유물 없음·다수 유물·치명타·빠른 연속 공격·장비 교체·매각/폐기 시 후보가 즉시 맞게 갱신되는지 확인한다.

WJ의 `CooldownIconUIContainer`도 같은 그리드를 매 프레임 검색하지만, 그 UI 수정은 이번 SW 범위에 포함하지 않는다.

## 4. 조건부·후순위 런타임 후보 상세

### SW-06. 파티클 전체 열거와 하위 시스템 재귀 처리의 중복

**대상**

- [GunnerWeaponVfxBinding.cs:316](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Equipment/Visuals/GunnerWeaponVfxBinding.cs:316): `GunnerVfxPlayback.Restart`
- 같은 파일의 `StopAndClear`(345)
- [ProjectileVisualAnimator.cs:165](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Equipment/Visuals/ProjectileVisualAnimator.cs:165)

**수정 이유**

모든 자식 ParticleSystem을 열거하면서 각각 `Stop(true)`, `Clear(true)`, `Play(true)`로 하위 시스템까지 다시 처리한다. 부모와 자식 PS가 중첩되면 같은 자식이 여러 번 처리된다. 중복 방문 수는 각 PS 아래의 PS 수 합이며, 깊은 사슬 구조에서는 최악 O(P²)까지 커진다. 형제 위주 프리팹은 O(P)에 가까워 실제 효과는 계층 구조에 따라 다르다. 재생마다 `GetComponentsInChildren` 배열을 다시 만드는 경로도 있다.

**수정 방향·주의점**

현재처럼 각 PS를 직접 열거한다면 하위 재귀 처리 없이 한 번씩 처리하는 방식을 검토한다. 수집 배열은 인스턴스 수명 동안 재사용한다. 단, 비활성 분기를 강제로 켜지 않는 현재 의도와 sub-emitter 동작을 보존해야 한다. 단순히 모든 true를 false로 치환한 뒤 완료로 볼 수 없다.

**검증**

부모/자식 PS가 있는 무기, 비활성 선택 분기가 있는 무기, sub-emitter, 풀 반환 후 재사용을 각각 확인한다. 같은 공격 횟수에서 재생 함수 시간·GC Alloc과 실제 잔상/방출 결과를 비교한다.

### SW-07. 정의 조회의 Scene 검색과 ID 비교용 아이템 객체 생성

**대상**

- [PlayerInventorySync.cs:1976](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Network/Player/PlayerInventorySync.cs:1976): `ResolveDefinition`
- [NetworkShopState.cs:656](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Network/Player/NetworkShopState.cs:656): `FindStockIndex`

**수정 이유**

정의를 찾을 때마다 ItemManager를 Scene에서 찾고, 없거나 해당 정의를 못 찾으면 `Resources.LoadAll` 결과 전체를 검색한다. I개 아이템 복원에서는 Scene 조회가 I번 반복되고, fallback에서는 정의 D개 조회도 반복된다. Scene/Resources 호출 자체의 엔진 비용은 측정하지 않았지만 같은 자료를 반복 요청하는 구조는 코드에서 확인된다.

상점은 instanceId를 비교하기 위해 `CreateItemInstance`까지 호출해 정의 조회와 객체 생성을 수행한다. ID 검색에 필요한 정보보다 많은 일을 한다.

**수정 방향·주의점**

기존 ItemDatabase 참조를 복원 작업에 전달하고 `GetById`를 재사용한다. fallback도 작업 또는 적절한 수명 단위로 한 번 구축한다. 상점 ID 비교는 저장 데이터의 instanceId만 해석하거나 재고 변경 시 인덱스를 구성한다.

현재 주석에는 StageSelect에서 `ItemManager.Instance` 접근 시 잘못된 임시 Singleton이 생성될 수 있다는 이유가 명시되어 있다. 최적화하면서 그 접근을 다시 도입하면 안 된다. 씬 전환 시 파괴된 참조와 변경된 데이터베이스도 처리해야 한다.

**검증**

ItemManager가 있는 전투 씬과 없는 StageSelect 양쪽에서 복원·상점 검색을 확인한다. 조회 횟수·할당을 비교하고 임시 매니저 생성, 정의 누락, instanceId 및 아이템 옵션 변경이 없어야 한다.

### SW-08. 보스 인트로 본 매칭을 매 프레임 다시 검색

**대상**

- [MirrorBossIntro.cs:184](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Network/Combat/MirrorBossIntro.cs:184): `ApplyPlayerFormationAnimation`
- 같은 파일의 `CopyBoneHierarchy`(195)

**수정 이유**

원본 본의 각 자식을 대상 본의 형제 목록에서 이름으로 검색하는 중첩 루프가 매 프레임 재실행된다. 같은 구조의 계층에서 본별 자식 수를 d라고 하면 매칭 비용은 대략 Σd²이고, 넓은 계층의 최악 경우 본 수 B에 대해 O(B²)까지 증가한다. 플레이어 복제 배우 수만큼 반복된다. 대부분의 본이 자식을 적게 가지면 선형에 가까우며, 플레이어 최대 4명·인트로 중에만 실행되어 우선순위는 낮춘다.

**수정 방향·검증**

배우 준비 때 이름/경로 매칭을 한 번 수행해 원본 Transform과 대상 Transform 쌍을 저장한다. 재생 중에는 그 쌍의 값을 복사하면 본 수에 비례한다. 배우 교체·인트로 종료 때 정리한다. 이름이 중복된 본, 외형 자식, 서로 다른 계층, 누락 본의 처리와 인트로 위치·애니메이션·종료 복구를 확인한다.

### SW-09. 상위 소수 대상만 사용하는 경로의 전체 정렬

**대상**

- [PlayerItemEffectState.cs:178](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Player/PlayerItemEffectState.cs:178), 같은 파일의 234·341
- [PlayerSpatialShotEffects.cs:146](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Player/PlayerSpatialShotEffects.cs:146)

**수정 이유**

후보 N개를 O(N log N)으로 정렬하지만 일부 경로는 최대 16개만 사용한다. 밀집 전투에서 N이 커질 때 상위 K개만 선택하면 O(N log K + K log K)로 줄일 수 있다. 후보가 적으면 기존 정렬이 더 단순하고 충분히 빠르므로 측정 후 선택한다.

**수정 방향·주의점**

각 효과에서 실제 소비하는 대상 수와 필터 순서를 확인한 뒤 제한된 경로에만 적용한다. 거리 계산 결과도 반복 비교에서 재사용할 수 있다. 현재의 거리 기준과 동률 처리, Collider 중복 제거, 시야/유효성 검사를 보존한다.

Wildfire처럼 실패한 대상은 건너뛰고 성공 횟수를 채우는 경로는 처음 K개로 잘라 버리면 효과가 달라진다. 관통탄·벽 충돌처럼 정렬 순서가 게임 규칙인 경로도 일괄 변경하지 않는다. 같은 입력에서 선정 대상과 타격 순서가 같아야 한다.

### SW-10. 산탄 사거리가 같아도 파티클 모듈 재설정

**대상**

- [GunnerWeaponVfxBinding.cs:113](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Equipment/Visuals/GunnerWeaponVfxBinding.cs:113): 사거리 배율 계산
- 같은 파일의 `ApplyShotgunTravelDistanceScale`(133)

**수정 이유·방향**

현재 사거리와 직전 사거리가 같으면 relativeScale은 1이지만, 여전히 전체 PS를 수집하고 위치·수명·Shape·Renderer 값을 다시 설정한다. 공격 R회·PS P개에서 반복 설정 부분은 O(R·P)다.

인스턴스 최초 생성과 실제 사거리 변경 C회에만 적용하면 이 부분을 O(C·P + R)로 줄일 수 있다. 원본 프리팹을 바꾸지 않고 런타임 인스턴스만 조절하는 구조는 유지한다. 무기 교체 시 기준 배율을 초기화하고 사거리 증가 후 감소에서 배율이 누적 오차 없이 복원되는지 확인한다.

### SW-11. 인벤토리 교환 배치 후보의 곱집합 검사

**대상**

- [InventorySwapPlanner.cs:214](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Inventory/Placement/InventorySwapPlanner.cs:214): 후보 생성
- 같은 파일의 230~239: 두 후보 집합의 모든 조합 검사

**수정 이유**

이상적인 배치를 바로 사용할 수 없으면 두 아이템의 후보 위치 집합 A·B를 만들고 모든 조합에 `CanPlacePairIgnoring`을 수행한다. 각 아이템 면적을 a·b라고 하면 칸 검사 비용이 최악 O(A·B·(a+b))다. 후보 범위는 원래 아이템 영역과 겹치는 위치로 제한되어 있으며 일반적인 작은 아이템에서는 비용이 작다. 전체 인벤토리에 대한 무제한 조합 탐색으로 해석하면 안 된다.

**수정 방향·검증**

각 아이템 후보가 다른 고정 아이템과 충돌하는지 먼저 한 번씩 판정하고, 통과한 후보 쌍에 대해 서로의 겹침과 점수를 계산한다. 반복 칸 검사를 줄이는 효과가 있는지 큰 아이템·넓은 그리드에서 측정한다. 기존 `SwapCandidate.IsBetterThan`의 의도/안정성 우선순위, 회전, 경계, 실패 시 원상복구를 유지해야 한다. 첫 번째 유효 후보를 선택하는 방식으로 바꾸면 기존 최적 후보 선택과 결과가 달라질 수 있다.

### SW-12. 적 목록에서 반복 RemoveAt

**대상**

- [NetworkEnemyWaveSpawner.cs:318](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Network/Combat/NetworkEnemyWaveSpawner.cs:318): `RemoveFinishedEnemies`

**수정 이유·방향**

뒤에서부터 제거해 인덱스 안전성은 지키지만, 앞쪽에 종료된 적이 많고 뒤쪽에 살아 있는 적이 남으면 살아 있는 항목을 여러 번 이동시킨다. 적 E개에 대해 최악 O(E²)이며, 모두 종료된 경우 뒤부터 끝 요소를 제거하므로 O(E)에 가깝다.

순서를 유지하는 `RemoveAll` 또는 한 번의 압축으로 O(E)에 정리하는 방식을 검토한다. 제거 조건의 생명주기·Unity null 의미를 보존하고, 같은 프레임 다수 사망 및 남은 적 수/다음 웨이브 판정을 확인한다.

### SW-13. VFX 반환 때마다 활성 List 선형 검색

**대상**

- [UniqueEffectPresentation.cs:345](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Network/Player/UniqueEffectPresentation.cs:345): Bolt 반환
- 같은 파일의 Inferno 반환(595~600)

**수정 이유·방향**

각 반환에서 `List.Remove`가 해당 객체를 검색하고 뒤 항목을 이동한다. 활성 V개가 반환되는 묶음의 최악 비용은 O(V²)다. 현재 동시 V가 작으면 낮은 우선순위다.

활성 목록의 순서를 사용하지 않는지 확인한 뒤 HashSet으로 전환하거나, 순서가 불필요한 제거 방식을 사용한다. 반환 중 비활성화·풀·코루틴·OnDisable 정리가 서로 중복 실행되지 않는지 확인해야 한다. 한 효과를 두 번 반환하지 않고 종료 때 모든 객체가 회수되어야 한다.

### SW-14. 유물 전체 해제 시 남은 동일 효과 반복 검색

**대상**

- [PlayerRelicEffectRuntime.cs:166](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Player/PlayerRelicEffectRuntime.cs:166): `RemoveEffect`
- 같은 파일의 `ClearEffects`(209)

**수정 이유**

한 항목을 제거할 때 남은 소지 유물을 검색해 동일 효과가 있는지 확인하고, 실행 객체가 있으면 다시 대표 소유자를 찾는다. 전체 해제는 유물 R개에 이 함수를 반복 호출하므로 O(R²)까지 늘어난다. 씬 종료·비활성화 등 저빈도 경로이며 작은 유물 수에서는 후순위다.

**수정 방향·검증**

이미 존재하는 passiveCounts처럼 효과별 소유 개수/대표 객체를 활용하거나, 전체 해제 전용 경로에서 효과마다 한 번만 버프 제거·실행 객체 종료를 수행한다. 일반 단일 아이템 제거와 전체 정리의 동작을 구분한다. 같은 효과의 마지막 사본이 제거되기 전까지 버프·오라·실행 객체를 유지하는 규칙, 즉시 Unbind와 비활성화를 보존하고 서버 종료·씬 전환·중복 유물 제거를 확인한다.

### SW-15. 개발용 파괴 튜닝 패널의 반복 정렬

**대상**

- [ArtificerRuntimeTuningPanel.cs:63](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Enemy/Destruction/ArtificerRuntimeTuningPanel.cs:63): `RefreshTargets`
- 같은 파일의 `RegisterTarget`(85)

**수정 이유·방향**

전체 새로고침 중 대상을 하나씩 등록하면서 `Contains`로 중복 검사하고 매 등록마다 목록을 정렬한다. 마지막에도 다시 정렬한다. N개 일괄 등록의 최악 상한은 O(N² log N)이며, 일괄 수집·중복 제거 후 한 번만 정렬하면 O(N log N)으로 줄일 수 있다.

단일 대상 등록은 기존 순서가 필요하면 별도로 처리한다. 개발용 버튼·시작·등록 경로이므로 실제 전투 프레임 병목과 구분한다. 이름 순서, 동시/순차 프리셋 수, 첫 대상 설정 읽기, 풀에서 생성된 대상 등록을 확인한다.

### SW-16. 드롭·상점 추첨의 같은 후보 재검색

**대상**

- [ItemDropRollService.cs:36](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/WorldItem/DropRules/ItemDropRollService.cs:36)
- [ShopStockRollService.cs:25](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Shop/Stock/ShopStockRollService.cs:25)
- [ShopStockInitializer.cs:79](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Shop/Stock/ShopStockInitializer.cs:79)

**수정 이유**

상점은 매 추첨마다 DB에서 고유 아이템 목록을 만들고 희귀도별 후보 유무를 검사한 뒤 다시 후보를 선택한다. 드롭도 종류별 후보 유무와 최종 후보 목록을 여러 번 검색한다. DB D개·분류 수 K개·추첨 M회에서 O(M·K·D) 수준의 반복 검색이다. 현재 분류 수와 기본 상점 재고는 작은 고정값이므로 이를 D에 대한 제곱 알고리즘으로 볼 수는 없다.

**수정 방향·검증**

한 재고 생성 묶음에서 고유 목록과 희귀도/종류별 후보를 재사용한다. 플레이어의 드롭 확률 보정은 같은 추첨 안에서 재사용 가능한지 확인한다. 글로벌 캐시를 새로 만들기보다 기존 DB와 생성 작업의 수명에 맞춰 작게 적용한다. 후보 없음의 fallback, 가중치, 중복 ID 처리, 난수 호출 순서와 재현성이 바뀌지 않는지 확인한다.

## 5. SW 관련 Editor 도구 부록

아래 파일은 `Assets/SW/**` 밖에 있다. SW 무기 제작·검증 흐름과 연결되어 별도로 기록했으며, **엄격하게 SW 폴더만 수정하는 첫 작업 묶음에는 포함하지 않는다.** 공용 도구의 실제 변경 범위와 사용자를 확인한 뒤 별도 작업으로 다루는 편이 적합하다.

| ID | 우선순위 | 대상 | 수정 이유 |
| --- | --- | --- | --- |
| ED-01 | B, 작은 수정 후보 | WeaponBoneRetargeter | 키 배열 반복 조회에 따른 O(K²) 복사 |
| ED-02 | B, 실패 입력에서 우선 | WeaponInventoryIconMarginWindow | 미리보기 실패 후 GUI 이벤트마다 자동 재처리 |
| ED-03 | B/C | WeaponGripFitValidatorWindow | 손 검사 정점마다 무기 삼각형 전체 검색 |
| ED-04 | C | WeaponVisualPrefabGeneratorWindow, WeaponVisualCatalogSO | 카탈로그 반복 선형 조회가 중첩됨 |
| ED-05 | C | WeaponInventoryIconMarginWindow | 알파 행 평활화의 O(H²) 구간 합 |
| ED-06 | C | WeaponInventoryIconMarginWindow | GUI 이벤트마다 manifest 읽기·해석·정렬 반복 |

### ED-01. AnimationCurve 키 배열 반복 조회

**위치:** [WeaponBoneRetargeter.cs:100](C:/Users/user/Desktop/Project2_test/Project2/Assets/Editor/WeaponBoneRetargeter.cs:100)

키 K개를 처리하는 루프 안에서 `curve.keys[i]`와 `curve.keys[i-1]`를 호출한다. `keys` getter가 배열 복사본을 반환하므로 이 조회 부분만 O(K²) 데이터 복사가 된다. 루프 전에 배열을 한 번 받아 재사용하거나 해당 API의 인덱서 사용을 검토한다.

키 조회·복사는 O(K)로 줄일 수 있지만 tangent 설정 API 내부 비용까지 선형이라고 보장하는 것은 아니다. 기존 시간·값·tangent·wrap mode와 재타기팅 결과가 동일한지 확인한다.

### ED-02. 미리보기 실패 후 자동 재시도 반복

**위치:** [WeaponInventoryIconMarginWindow.cs:355](C:/Users/user/Desktop/Project2_test/Project2/Assets/Editor/WeaponInventoryIconMarginWindow.cs:355)

`EnsureProcessedPreview`는 preview가 null이면 다시 생성한다. 생성 중 예외로 preview가 남지 않고 결과 표시가 Repaint를 요청하면 다음 GUI 처리에서도 동일 입력을 다시 읽고 변환할 수 있다. 완전 투명 이미지처럼 계속 실패하는 입력에서 재처리가 반복되는 구조다.

GUI 이벤트 F회·픽셀 P개·매핑/정의 검사 N개에 대해 O(F·(N+P))의 반복 처리로 볼 수 있다. 입력·설정 조합의 실패 상태를 기록하고 실제 변경 또는 명시적 재시도 때만 다시 실행하도록 한다. 정상 이미지의 첫 생성과 설정 변경 후 갱신은 유지해야 한다. 투명 이미지·잘못된 입력·원본 교체·설정 변경으로 반복 Repaint가 끝나는지 확인한다.

### ED-03. 손 정점과 무기 표면 거리의 전수 조합

**위치:** [WeaponGripFitValidatorWindow.cs:1151](C:/Users/user/Desktop/Project2_test/Project2/Assets/Editor/WeaponGripFitValidatorWindow.cs:1151), `FindMinimumSurfaceDistance`(1223)

손 검사 정점 H개마다 무기 삼각형 T개를 검색한다. 각 삼각형에 AABB 검사가 있어도 방문 자체는 H·T개이므로 O(H·T)다. 무기 메시가 크거나 여러 무기를 연속 QA할 때 늘어난다.

Grip 주변 후보 삼각형을 한 번 추려서 각 정점이 해당 부분만 검색하도록 하는 최소 변경을 먼저 검토한다. 큰 메시에서도 비용이 남을 때 공간 인덱스를 검토한다. 임의 거리 컷으로 실제 가까운 표면을 제외하지 않아야 하며, 기존 침범/접촉 판정과 보고 수치를 비교한다. 모든 입력에서 O(H log T)가 보장되는 것으로 표현하지 않는다.

### ED-04. 런타임 외형 식별의 카탈로그 이중 검색

**위치:** [WeaponVisualPrefabGeneratorWindow.cs:434](C:/Users/user/Desktop/Project2_test/Project2/Assets/Editor/WeaponVisualPrefabGeneratorWindow.cs:434), [WeaponVisualCatalogSO.cs:60](C:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Equipment/Visuals/WeaponVisualCatalogSO.cs:60)

카탈로그 V개를 순회하면서 각 itemId를 다시 카탈로그의 선형 조회 함수에 전달한다. 하나의 후보 검사에서 O(V²)이고 부모 계층 D단계를 검사하면 최악 O(D·V²)까지 커진다.

해당 작업에서 itemId→prefab 인덱스를 한 번 구성하거나, 이미 읽은 직렬화 항목의 prefab 참조를 직접 사용하면 중첩 검색을 줄일 수 있다. 외형 역조회도 작업 단위로 재사용한다. 일반 장착의 카탈로그 조회는 장비 변경 시에만 일어나므로 그 경로까지 매 프레임 병목으로 해석하지 않는다. GUID·itemId·Prefab 연결, 부모 외형 판정, 카탈로그 편집 후 갱신을 확인한다.

### ED-05. 알파 분석 행 평활화의 반복 구간 합

**위치:** [WeaponInventoryIconMarginWindow.cs:783](C:/Users/user/Desktop/Project2_test/Project2/Assets/Editor/WeaponInventoryIconMarginWindow.cs:783)

각 행에서 주변 행 값을 다시 더하고, 평활화 반경은 contentHeight/100에 비례한다. 높이 H에 대해 해당 부분은 O(H²)이고 전체 분석에는 픽셀 P개 검색과 행 정렬 비용도 있다.

행 누적합 또는 이동 구간 합을 만들면 평활화 부분을 O(H)로 줄일 수 있다. 전체 분석은 기존 픽셀 순회·정렬 때문에 여전히 O(P + H log H) 수준일 수 있다. 일반 크기의 PNG에서는 픽셀 처리 비용이 더 클 수 있으므로 후순위다. 경계의 구간 길이·분모·반올림·최종 여백과 출력 이미지를 비교한다.

### ED-06. 매핑 목록 표시마다 manifest 반복 읽기

**위치:** [WeaponInventoryIconMarginWindow.cs:197](C:/Users/user/Desktop/Project2_test/Project2/Assets/Editor/WeaponInventoryIconMarginWindow.cs:197), `GetMappings`(420), `LoadManifest`(949)

등록 매핑을 그리는 GUI 이벤트마다 manifest를 읽고 JSON을 해석하며 정렬한다. 데이터가 그대로인 동안 파일 I/O·객체 생성·정렬을 반복한다.

창 수명 동안 표시 목록을 재사용하고 저장·자동 매핑·삭제·외부 파일 변경 때 갱신하는 방식을 검토한다. 외부 변경 감지를 생략한 전역 영구 캐시보다 창 또는 작업 범위의 캐시가 적합하다. 기존 수동 매핑 우선순위, GUID 유지, 자동 파일명 매핑과 다른 창의 저장 결과 반영을 확인한다.

## 6. 첫 수정 묶음 제안

| 순서 | 작업 묶음 | 선택 이유 | 선행 확인 |
| --- | --- | --- | --- |
| 1 | SW-01 스냅샷 인덱스 | 반복 JSON 해석을 줄이고 변경 지점이 비교적 명확함 | 거래·롤백·SyncList 변경 흐름 |
| 2 | SW-02 파편 조회 인덱스 | 다수 파괴에서 제곱 조회가 겹칠 수 있음 | element/piece 대응과 풀 수명 |
| 3 | SW-03 버프 불필요 재구성 생략 | 체력/마나/시간 변화에 재계산이 반복됨 | SW만으로 가능한 비교·갱신 범위 |
| 4 | SW-04 쿨타임 변경 게시·정리 | 긴 세션에서 역사적 키 수에 따라 비용이 증가함 | 키별 최초 충전·장비 교체 규칙 |
| 5 | SW-05 공격 효과 후보 재사용 | 공격 빈도가 높을수록 같은 소지 정보 검색이 반복됨 | 기존 소지/장비 이벤트와 발동 중 변경 |
| 별도 | SW-06, SW-10 파티클 처리 | 같은 무기 VFX 파일에서 중복 처리를 함께 줄일 수 있음 | 실제 프리팹 중첩·sub-emitter |

최초 문서 작성 단계에서는 구현과 실행 검증을 수행하지 않았다. 후속 수정 요청에서 실제로 채택한 범위는 아래 10절과 같다.

## 7. SW 수정만으로 해결되지 않는 연결 항목

이 표는 수정 요청 목록이 아니라 범위 의존성을 설명한다.

| 연결 파일 | 관계 | 이번 문서에서의 처리 |
| --- | --- | --- |
| [PlayerBuffManager.cs:104](C:/Users/user/Desktop/Project2_test/Project2/Assets/WJ_TestPlace/Script/Player/PlayerBuffManager.cs:104), [BuffTracker.cs:72](C:/Users/user/Desktop/Project2_test/Project2/Assets/WJ_TestPlace/Script/Buff/BuffTracker.cs:72) | 적용 성공마다 스탯 재계산. 지속시간만 갱신한 경우도 성공으로 처리 | SW-03에서 불필요한 호출을 먼저 줄임. 일괄 API와 공용 갱신 정책 변경은 별도 범위 |
| [CooldownIconUIContainer.cs:128](C:/Users/user/Desktop/Project2_test/Project2/Assets/WJ_TestPlace/Script/Player/CooldownIconUIContainer.cs:128) | 매 프레임 전체 그리드를 검색해 쿨타임 UI 후보를 찾음 | SW-05와 같은 후보 정보가 도움이 될 수 있으나 UI 파일은 수정 대상에서 제외 |
| WBH·YJ 파티클 스크립트 | SW-06과 유사한 하위 시스템 중복 처리 | SW 파일의 수정 후보만 본 목록에 포함 |
| WJ 데이터 importer·YJ 맵 생성·WBH 보상 지급 | 앞선 전체 리뷰의 다른 담당 영역 후보 | 이번 SW 문서의 수정 목록에서 제외 |

## 8. 이번 단계에서 우선 수정 대상으로 삼지 않은 구조

- Dictionary·HashSet·Queue를 사용하는 BFS와 이미 인덱스를 구성하는 데이터베이스 조회는 순회 자체가 필요하며, 중첩 루프 모양만으로 교체할 이유가 없다.
- 네트워크 플레이어는 현재 최대 4명이다. 플레이어 간 작은 이중 루프를 데이터 규모가 무제한인 O(N²) 병목과 동일하게 취급하지 않는다.
- 채팅은 기록 상한과 변경 시 갱신 구조가 있다. 매 프레임 전체 기록을 재구성하는 구조로 보지 않는다.
- 월드 아이템 툴팁은 hover 변경 시 내용을 갱신한다. 매 프레임 문자열 전체를 다시 만드는 문제로 판단하지 않았다.
- 파편 속도 커브의 샘플링/적분은 준비 단계에서 캐시한다. 이를 파편마다 매 프레임 적분하는 비용으로 오인하지 않는다.
- 파괴 VFX 풀의 Queue 기반 재사용과 제한된 풀 크기는 유지한다. 조회 개선을 이유로 본체와 연출 복제본의 풀을 합치지 않는다.
- 거리 기반 정렬, 배치 검사, 장비 거래의 롤백은 결과의 의미를 가진다. 최적화를 위해 검증이나 선택 기준을 삭제하지 않는다.
- 낮은 빈도의 선형 처리까지 전부 영구 캐시로 바꾸면 무효화와 씬 수명 처리가 더 복잡해질 수 있다. 해당 항목의 실제 호출 비용을 확인한 뒤 변경 범위를 정한다.

## 9. 향후 구현 시 비교할 측정 항목

이 절은 이번에 실행한 테스트 결과가 아니라 후보의 효과를 판정하기 위한 비교 항목이다.

| 대상 | 동일 조건에서 비교할 항목 |
| --- | --- |
| 인벤토리 | 아이템/칸 수, 작업당 JSON 해석·정의 조회 횟수, 함수 시간, GC Alloc, 거래 결과 |
| 파괴 | 활성 파편 수, 동시 파괴 수, 조회·전체 프레임 시간, 물리/렌더링 비용, 풀 재사용, 시각 품질 |
| 버프 | 버프 종류·스택 수, 스냅샷 종류, 재계산·알림·UI 재구성 횟수, 최종 스탯 |
| 쿨타임 | 현재 키 수·과거 instanceId 수, PublishState 시간, 변경/삭제 동기화와 충전 결과 |
| 공격 효과 | 타격 빈도, 그리드 칸 수, 후보 수, 검색·할당 횟수, 발동 순서 |
| 파티클 | 계층 깊이·PS 수, 공격/반환 횟수, 컴포넌트 수집·처리 시간, 잔상과 재사용 결과 |
| Editor 도구 | 입력 크기·키/정점/삼각형/매핑 수, 처리 횟수·시간·할당, 실패 시 재시도 횟수, 출력 동일성 |

최초 문서 작성 단계에서는 위 측정과 Unity 실행 검증을 수행하지 않았다. 후속 구현에서 확인한 결과와 미검증 범위를 아래에 구분한다.

## 10. SW 범위 구현·검증 결과 (2026-10-06)

사용자의 후속 수정 요청과 `$ponytail ultra`에 따라 실제 호출 흐름과 현재 데이터 규모를 확인했다. 신규 전역 캐시나 Manager 없이 반복 작업을 줄일 수 있는 아래 6개 후보를 채택했다. 런타임 수정은 `Assets/SW/**`의 10개 스크립트에 한정했다.

### 10-1. 채택한 변경

| 후보 | 최종 변경 | 유지한 경계 |
| --- | --- | --- |
| SW-01 | 소유 스냅샷 전체 검사·비교·동기화 호출마다 instanceId 인덱스를 한 번 만들고 JSON을 한 번씩 해석한다. | 개수·중복·누락 검사, 그리드/장비 상태 비교와 변경된 SyncList 항목만 기록하는 규칙을 보존한다. 단일 항목 검색 경로와 거래·롤백 규칙은 유지한다. 영구 인덱스를 추가하지 않는다. |
| SW-03 | 버프 출처·메타데이터·스택·효과가 같으면 기존 BuffInstance의 남은 시간만 갱신한다. 재구성이 필요할 때도 버프당 한 번 적용한 뒤 서버 스택을 지정한다. | 실제 목록을 비교하므로 외부 삭제 뒤에도 복구한다. 폐열의 0스택과 동적 효과값, 스택 감소를 처리한다. WJ 버프 API는 수정하지 않는다. |
| SW-04 일부 | 쿨다운 변경을 공통 setter의 이벤트로 전달하고 서버 시작 시 전체 상태를 한 번 게시한다. 매 프레임 과거 키 전체를 검색하던 부분을 제거한다. | 서버 종료 때 구독을 해제한다. 키 부재가 월드엔더 최초 충전을 뜻하므로 만료 키 삭제는 도입하지 않는다. 키 저장량 증가는 별도 과제로 남는다. |
| SW-06 한정 | 이미 수집한 각 ParticleSystem을 `withChildren:false`로 한 번씩 처리한다. | 기존 활성 계층 검사·정지/초기화/재생 순서와 Trail 처리를 유지한다. 새로운 캐시 수명 관리는 추가하지 않는다. |
| SW-10 | 같은 샷건 거리 배율이면 ParticleSystem 모듈 재설정을 생략한다. | 공격 VFX 인스턴스를 다시 만들면 적용 배율도 초기화해 새 인스턴스에 설정이 적용되게 한다. |
| SW-12 | 웨이브 생존 목록에서 반복 RemoveAt 대신 안정적인 RemoveAll 압축을 사용한다. | Unity의 파괴 객체 null 판정, 사망 판정과 생존자 순서를 보존한다. |

### 10-2. 보류한 후보

| 후보 | 이번에 수정하지 않은 이유 |
| --- | --- |
| SW-02 파편 조회 | Artificer element/piece 대응을 확정할 원본 구현과 실제 다수 파괴 프레임 측정이 확보되지 않았다. 잘못된 대응 캐시가 파괴 동작을 바꿀 위험이 있어 보류했다. |
| SW-05 공격 효과 후보 | 현재 8×6 그리드 규모에서 장비/그리드/효과 변경과 발동 중 재진입까지 다루는 새 캐시의 이득이 확인되지 않았다. |
| SW-07 정의 조회 | 기존 fallback과 정의 수명까지 포함한 실제 비용을 먼저 측정해야 한다. 이번 스냅샷 개선과 무관한 영구 캐시는 추가하지 않았다. |
| SW-08 보스 인트로 | 현재 최대 4명·짧은 인트로 구간이며 프레임 병목 근거가 없다. |
| SW-09 표적 정렬 | 현재 대상 규모와 동률·타격 순서의 의미를 고려하면 부분 선택 구현을 추가할 근거가 부족하다. |
| SW-11 교환 배치 | 현재 후보 수가 작고 실제 거래 병목을 확인하지 못했다. 롤백과 배치 선택 규칙을 유지했다. |
| SW-13 표시 문자열 | 사용자의 기존 수정이 진행 중인 파일이며, 실제 표시 갱신 비용도 확인되지 않았다. |
| SW-14/15/16 | 저빈도 초기화·개발 패널·작은 상점 재고에 대한 추가 캐시 복잡도가 현재 이득보다 크다고 판단했다. |
| ED-01~06 | `Assets/Editor/**` 부록은 이번 SW 런타임 범위에서 제외했다. |

### 10-3. 동일 조건 CPU 비교

Unity 6000.3.22 Editor에서 Assets 밖의 임시 검증 스크립트를 `run_script`로 실행했다. 해당 스크립트와 임시 캡처는 검증 후 사용자 요청으로 삭제했다. 아래 값은 전체 프레임이 아닌 격리된 함수 호출의 평균이다. 인벤토리·버프·쿨다운은 수정 전/후 같은 입력과 반복 수를 사용했다. 웨이브는 같은 실행에서 이전 반복문과 변경된 함수를 비교했다.

| 입력·동작 | 수정 전 | 수정 후 | 반복 수 |
| --- | ---: | ---: | ---: |
| 12개 소유 아이템 스냅샷 검사 | 316.7 μs | 62.0 μs | 각 80회 |
| 48개 소유 아이템 스냅샷 검사 | 4,597.5 μs | 227.5 μs | 각 80회 |
| 48개 소유 아이템 상태 비교 | 5,650.2 μs | 245.6 μs | 각 80회 |
| 48개 소유 아이템 동기화 | 12,263.9 μs | 440.7 μs | 각 80회 |
| 8종×8스택, 시간만 바뀌는 버프 적용 | 14.45 μs | 1.42 μs | 각 150회 |
| 과거 쿨다운 2,000키의 PublishState | 189.25 μs | 0.23 μs | 각 1,000회 |
| 640항목 중 사망/파괴/null 384항목 제거 | 73.86 μs | 18.44 μs | 각 250회 |

버프 테스트에서 워밍업 5회를 포함한 목록 변경 이벤트는 1,395회에서 0회로 줄었고 기존 BuffInstance가 유지됐다. 이 테스트에는 실제 StatManager/UI 구독자의 비용을 포함하지 않았다. 웨이브 입력은 리스트 동작을 비교하는 합성 데이터이며 640마리 실전 측정이 아니다. 파티클 처리 시간은 약 8~14 μs의 변동 범위로 유의미한 성능 개선을 주장하지 않는다. GC 측정 API가 0만 반환해 할당량은 미측정으로 처리했다. FPS 향상 수치로 해석하지 않는다.

### 10-4. 회귀 검증과 남은 범위

- **컴파일:** Unity CLI 재컴파일 완료, 컴파일 오류 0건.
- **인벤토리:** 스냅샷/소유 ID 중복·누락·개수 불일치, 강화·회전·장착·해제 차이와 동기화 결과, 동일 상태의 불필요한 SyncList 기록 없음 통과.
- **버프:** 시간 갱신 시 인스턴스 보존, 스택 감소·동적 효과값·출처 변경, 외부 목록 삭제 후 복구, null 스냅샷, 폐열 0스택 통과.
- **쿨다운:** 변경 키 1회 전파, 같은 값 재지정 무전파, 서버 종료 구독 해제·재시작 초기 게시, 늦은 접속용 SyncDictionary 전체 직렬화/역직렬화 통과.
- **VFX:** 소각기·설빙·이온 캐스케이드 실제 프리팹에서 반복 재생·반환과 거리 10→20→10·동일 거리·인스턴스 재생성을 확인했다. 고정 randomSeed로 이전 재귀 방식과 입자 수·위치·남은 수명을 비교해 일치했고 반환 후 잔여 입자는 0이었다. 프리팹 자산은 변경하지 않았다.
- **웨이브:** 살아 있음/사망/파괴 객체/null 혼합 목록의 생존자 순서와 마지막 사망 뒤 빈 목록을 확인했다.
- **실제 싱글 전투:** 열린 `Act1_BossStage`에서 기존 `WeekendSpatialEffectsValidation`의 실제 Gunner·적 풀 검증을 재사용했다. 샷건 15발/15회 피해, 누락·중복 0; 월드엔더 장착 후 약 8초 충전, 직접 피해 90+폭발 피해 135, 재충전과 1회 폭발 통과.
- **검증 도구 오류:** 최초 웨이브 테스트 픽스처가 NetworkIdentity를 부모·자식에 중첩해 Editor 오류 1건을 만들었다. 스포너와 적을 형제 오브젝트로 분리한 뒤 재검증했다. 운영 코드 오류와 구분한다.
- **기존 진입 조건 오류:** 보스 씬 직접 실행 후 원래 플레이어 사망 시 `YJ_PlayerDead`가 Start 씬의 `KY_RunStatsTracker`/`ResultPayload` 부재를 보고했다. 이번 수정 범위 밖이며 다른 담당 코드를 변경하지 않았다.
- **미검증:** 실제 원격 클라이언트 전송·재접속, 거래 실패/네트워크 중단 롤백 전체, 정식 웨이브 진행 전체, 다수 적 파괴 Profiler, 실제 UI 포함 버프 재계산 비용과 GC Alloc. 새 Player 빌드는 만들지 않았다.
- **보존:** 기존 사용자 변경과 열린 Dirty Scene은 저장하지 않았다. 검증 뒤 Edit Mode로 복귀했다. 개인 구현 로그 345번에 반영했으며 후속 사용자 요청으로 `코드 알고리즘 성능 개선` 커밋에 포함한다. Push는 수행하지 않는다.
