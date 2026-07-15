# Architecture Decision Log

> 생성일: 2026-07-13  
> 프로젝트 배경: 각 팀원이 별도 브랜치에서 개발한 시스템을 최근 처음으로 병합했다. Mirror는 설치만 되어 있고 네트워크 연동 전이다. 병렬 구현과 미연결 기능은 현재 단계에서 자연스러운 상태이며, 이 문서는 팀이 실제로 합의한 아키텍처 결정을 기록하기 위한 원장이다.

## 목차

1. [사용 규칙](#1-사용-규칙)
2. [결정 상태](#2-결정-상태)
3. [결정 목록](#3-결정-목록)
4. [검토 대기 항목](#4-검토-대기-항목)
5. [결정 기록 템플릿](#5-결정-기록-템플릿)
6. [변경 이력](#6-변경-이력)

## 1. 사용 규칙

- 이 문서는 **팀이 합의한 결정**과 **아직 검토 중인 제안**을 명확히 구분한다.
- `02_ArchitectureReview.md`의 권장안은 자동으로 확정 결정이 되지 않는다.
- 결정에는 가능한 대안, 선택 근거, 영향, 후속 작업과 재검토 조건을 기록한다.
- 결정을 변경할 때 이전 기록을 삭제하지 않는다. 기존 항목을 `대체됨`으로 바꾸고 새 결정 ID를 연결한다.
- 구현이 결정과 다를 경우 코드가 자동으로 진실이 되는 것이 아니라, 코드 또는 결정 문서를 명시적으로 정합화한다.
- 날짜는 `YYYY-MM-DD`, ID는 `ADR-0001` 형식을 사용한다.

## 2. 결정 상태

| 상태 | 의미 |
|---|---|
| 제안 | 리뷰나 팀원이 제시했으나 공식 검토 전 |
| 검토 중 | 대안과 영향 범위를 팀이 논의 중 |
| 승인 | 팀이 적용하기로 합의한 결정 |
| 보류 | 정보 또는 선행 작업 부족으로 판단 연기 |
| 기각 | 검토 후 적용하지 않기로 결정 |
| 대체됨 | 새로운 ADR이 기존 결정을 대체 |

## 3. 결정 목록

현재 승인된 아키텍처 결정은 아직 기록되지 않았다.

| ID | 제목 | 상태 | 결정일 | 대체 관계 | 문서 위치 |
|---|---|---|---|---|---|
| — | 승인된 결정 없음 | — | — | — | — |

## 4. 검토 대기 항목

다음 항목은 `01_ProjectInventory.md`와 `02_ArchitectureReview.md`에서 도출된 **제안**이다. 아직 팀 결정이 아니며, 합의 후에만 ADR로 승격한다.

| 후보 ID | 주제 | 현재 제안 | 대안 | 필요한 확인 | 관련 우선순위 | 상태 |
|---|---|---|---|---|---|---|
| CAND-001 | 정식 플레이어 기준 | `WBH_PlayerStateMachine` 기반 컴포넌트 구조를 canonical로 사용하고 TestPlayer는 프로토타입으로 격리 | TestPlayer 상속 구조 유지; 제3의 통합 구현 | 실제 게임 prefab/조작 요구, 팀 소유권 | 시스템 통합 전 | 제안 |
| CAND-002 | 플레이어별 의존성 경계 | 플레이어 prefab에 얇은 `PlayerContext`를 두고 Inventory/Equipment/Stat/Buff/Health/Mana/Input을 Inspector로 묶음 | 기존 static 유지; DI 컨테이너; 서비스 로케이터 | prefab 생성 방식, 로컬 플레이어 UI 연결 | 시스템 통합 전 | 제안 |
| CAND-003 | 입력 액션 소유권 | 로컬 플레이어당 `GameInputActions` 인스턴스 1개 | UI 클래스별 개별 인스턴스; 전역 단일 인스턴스 | 리바인딩 적용 범위, 로컬 멀티플레이 요구 | 시스템 통합 전 | 제안 |
| CAND-004 | 플레이어 HP 소유자 | `PlayerHealthManager`를 런타임 HP 단일 소유자로 사용 | `T_PlayerCombat` HP 유지; 새 공통 Health 컴포넌트 | 적과 플레이어 공통화 수준, 저장/UI 요구 | 시스템 통합 중 | 제안 |
| CAND-005 | 최종 전투 수치 전달 | 공격 시작 시 `CombatSnapshot`을 생성 | 타격 순간 StatManager 직접 조회; 이벤트로 캐시된 수치 사용 | 공격 도중 장비 변경 정책, 스킬 수치 정의 | 시스템 통합 중 | 제안 |
| CAND-006 | 인벤토리와 UI 경계 | Inventory 모델이 결과/이벤트를 반환하고 Presenter가 Item UI 생성 | 현재 Controller에서 모델/UI 함께 처리 | 저장 복원, 서버/헤드리스 실행 요구 | 시스템 통합 중 | 제안 |
| CAND-007 | 장비 거래 경계 | `EquipmentTransaction`이 InventoryGrid와 EquipmentSystem 변경을 원자적으로 조정 | `ItemEquipHandler` 유지; EquipmentSystem이 전부 조정 | 장비 교체 실패 정책, 자동 장착 요구 | 시스템 통합 중 | 제안 |
| CAND-008 | 장비/버프/스탯 갱신 | provider가 Changed를 발행하고 StatManager가 재계산 | provider가 StatManager 직접 호출; Update polling | 최대 HP/MP 변경 정책 | 시스템 통합 중 | 제안 |
| CAND-009 | UI 이벤트 구분 | `KY_GameEvents`는 로컬 UI 내비게이션, 플레이어 데이터는 인스턴스 도메인 이벤트 | 전역 버스 하나로 통합; 직접 참조만 사용 | HUD가 바인딩할 플레이어 선택 방식 | 시스템 통합 중 | 제안 |
| CAND-010 | 아이템 상태 계층 | Definition → Instance → InventoryItem → SaveData 구분 유지, 강화 레벨은 Instance 소유 | 하나의 Item 모델로 통합; InventoryItem에도 강화 복제 | 직렬화된 기존 데이터 사용 여부 | 시스템 통합 전 | 제안 |
| CAND-011 | UniqueEffect 대상 전달 | 효과 호출에 `PlayerEffectContext`/수신자를 전달 | `PlayerBuffManager.Instance` 유지; 효과별 별도 컴포넌트 | 효과 종류와 발동 주체 요구 | 멀티플레이 연동 전 | 제안 |
| CAND-012 | 피해 권한 경계 | 물리 판정과 HP 변경 사이에 `DamageRequest`/Resolver를 둠 | 투사체가 직접 `T_IDamageable.TakeDamage` 호출 | 서버 판정 범위, 예측 요구 | 멀티플레이 연동 전 | 제안 |
| CAND-013 | 드랍 RNG와 아이템 ID | 영속 아이템 생성 경계에 RNG/ID 공급자 주입 | Unity Random/Guid 직접 사용 | 서버 권한, 저장 ID 정책, 재현 테스트 | 멀티플레이 연동 전 | 제안 |
| CAND-014 | Assembly Definition | canonical/runtime/test/editor 경계를 정한 뒤 프로젝트 asmdef 도입 여부 검토 | 기본 Assembly-CSharp 유지 | 팀 빌드 시간, 순환 의존성, 폴더 정리 | 통합 후 별도 검토 | 보류 |
| CAND-015 | 풀 구현 | 적/투사체/이펙트 개별 풀과 고갈 정책 유지 | 공통 제네릭 풀로 통합 | 실제 성능/운영 지표 | 현재 유지 | 제안 |

## 5. 결정 기록 템플릿

아래 템플릿을 복사해 이 문서 하단 또는 팀이 정한 별도 `ADR-xxxx_*.md` 파일에 기록한다. 별도 파일을 사용할 경우 [결정 목록](#3-결정-목록)에 링크를 추가한다.

```markdown
## ADR-0001 — 결정 제목

- 상태: 제안 / 검토 중 / 승인 / 보류 / 기각 / 대체됨
- 제안일: YYYY-MM-DD
- 결정일: YYYY-MM-DD 또는 미결
- 결정 참여자: 이름 또는 역할
- 관련 후보: CAND-xxx
- 대체/대체됨: 해당 ADR 또는 없음

### 맥락

어떤 문제와 제약 때문에 결정이 필요한지 기록한다.

### 고려한 대안

1. 대안 A
2. 대안 B
3. 현재 구조 유지

### 결정

승인 전에는 비워 두거나 “미결”로 작성한다. 승인된 경우 선택한 방향과 적용 범위를 명확히 기록한다.

### 근거

- 변경 비용
- 테스트 가능성
- 시스템 연결 용이성
- 멀티플레이 전환 가능성
- Unity Inspector 사용 편의성
- 팀 이해 및 유지보수성

### 영향

- 긍정적 영향
- 부정적 영향/트레이드오프
- 영향을 받는 시스템과 파일

### 최소 적용 범위

현재 프로젝트 규모에서 과도한 설계를 피하기 위한 최소 변경을 기록한다.

### 후속 작업

- [ ] 독립 작업 1
- [ ] 독립 작업 2

### 완료 조건

검증 가능한 완료 조건을 기록한다.

### 재검토 조건

어떤 요구나 지표가 변하면 이 결정을 다시 검토할지 기록한다.
```

## 6. 변경 이력

### 6.1 Git 구현 이력

> 아래 표는 Git에서 확인한 구현·문서 변경 내역이다. 커밋이 존재한다는 사실만으로 해당 구조가 팀의 승인된 ADR이 되는 것은 아니다. 관련 후보는 추적 편의를 위한 연결이며, 팀 합의 전에는 [결정 목록](#3-결정-목록)에 승인 항목으로 등록하지 않는다.

| 일시 (KST) | 커밋 | 작성자 | 확인된 변경 내용 | 관련 후보/범위 | 기록 상태 |
|---|---|---|---|---|---|
| 2026-07-13 15:30 | `89bfbdab` | 김성우 | 팀원 작업 계획표 수정 | 일정·문서 | 반영 완료 |
| 2026-07-13 16:14 | `552eba28` | 김성우 | `EquipmentSystem` 미사용 메서드와 관련 잔여 필드 제거 | CAND-007 장비 거래 경계 | 반영 완료, ADR 미승인 |
| 2026-07-13 16:39 | `fc29f107` | 김성우 | `ItemEquipHandler`의 고유 효과 중복 호출 제거, 장비 상태 변경과 UI 갱신 책임 분리 | CAND-007, CAND-011 | 반영 완료, ADR 미승인 |
| 2026-07-13 18:30 | `9ef1af4c` | 김성우 | 프로젝트 문서용 폰트 추가 | 문서 디자인 | 반영 완료 |
| 2026-07-13 18:32 | `e5fa84b8` | 김성우 | 장비 장착·해제·교환을 `EquipmentTransaction`으로 통합하고 실패 시 상태 복구 처리 | CAND-007 | 구현 반영, 팀 확인 필요 |
| 2026-07-14 11:21 | `488faaa3` | 김성우 | `InventoryGrid`, 교환 서비스, 드래그 및 `ItemUI`의 배치·복구 흐름 수정 | CAND-006 | 구현 반영, 전체 경계 결정은 미완료 |
| 2026-07-14 11:21 | `28e0100f` | 김성우 | `ShopTradeService`의 구매·판매·아이템 이동 결과 및 실패 복구 흐름 보강 | CAND-006, 상점 거래 경계 | 구현 반영, ADR 미승인 |
| 2026-07-14 12:50 | `c217128c` | 김성우 | 강화 서비스·결과 코드·강화 드롭 슬롯·장비 변경 알림 및 테스트 UI 구현 | CAND-008, CAND-010 | 구현 반영, 정책 일부 검토 필요 |
| 2026-07-14 14:07 | `1d08941f` | 김성우 | 상점·강화 결과 enum을 `byte` 기반 명시 값으로 고정하고, `ShopMessageMapper`·`UpgradeMessageMapper`를 통해 컨트롤러의 사용자 메시지 출력을 분리 | 상점·강화 네트워크 응답 준비 | 구현 반영, ADR 미승인 |
| 2026-07-14 | 본 문서 반영 커밋 | 김성우 | 강화창을 닫거나 선택 아이템이 바뀔 때 선택 상태와 결과 메시지를 초기화하고, 강화 비용 증가 배율을 Inspector 필드로 분리. `UpgradeSlot`의 드롭 판정, 슬롯 테두리, 아이템 미리보기 이미지를 분리하고 `UpgradeDropSlot`을 하나로 정리 | CAND-008, CAND-010, 강화 UI 수명주기 및 표시 책임 | 커밋 완료, ADR 미승인 |
| 2026-07-14 | 본 문서 반영 커밋 | 김성우 | 저장 데이터의 인벤토리·장비 복원을 `Try*` API와 `EquipmentTransaction` 경계로 전환하고, 실패 시 모델과 UI를 복구하도록 정리 | CAND-005, CAND-007, 플레이어별 상태 경계 | 커밋 완료, ADR 미승인 |
| 2026-07-15 | 본 문서 반영 커밋 | 김성우 | 인벤토리 아이템의 월드 드롭·복구 경계를 `WorldItemDropService`로 통합하고, 월드 아이템 등급 표시·근거리 툴팁·범위 내 클릭 획득·빈자리 탐색을 구현. 테스트 Drop 버튼도 같은 서비스 경로를 사용하도록 정리 | CAND-006, CAND-010, 월드 아이템 드롭·획득 경계 | 커밋 완료, ADR 미승인 |

두 2026-07-14 상점·강화 항목 모두 구현 및 커밋이 완료되었다. 다만 Mirror 요청·응답, 서버 권한 판정, 네트워크 DTO 및 상태 동기화 방식을 구현하거나 확정한 결정은 아니다.

### 6.2 상점·강화 구현 메모 (2026-07-14)

구현에 반영된 범위와 팀 결정이 필요한 범위를 구분해 기록한다.

- 구현 반영: 상점·강화 결과 코드는 명시적인 `byte` 값으로 유지하고, 화면에 표시할 문구는 `ShopMessageMapper`와 `UpgradeMessageMapper`에서 변환한다.
- 구현 반영 완료: `UpgradeController.OnDisable()`에서 선택 아이템과 결과 메시지를 초기화하며, 다른 아이템을 강화 슬롯에 올렸을 때 이전 결과 메시지를 제거한다.
- 구현 반영 완료: 강화 비용 공식의 증가 배율 `1.15`를 `upgradeCostMultiplier` 직렬화 필드로 분리했다. 공식 자체와 수치 변경 권한은 아직 팀 정책으로 확정하지 않았다.
- 구현 반영 완료: 강화 슬롯의 드롭 대상은 상위 `UpgradeSlot`의 단일 `UpgradeDropSlot`이 판정하고, 슬롯 테두리와 아이템 미리보기는 각각 별도 `Image`가 담당한다. 미리보기 이미지는 드롭 판정을 가로막지 않도록 Raycast 대상에서 제외한다.
- 팀 결정 필요: 강화 성공 판정, 비용 계산, 골드 차감 및 아이템 변경 중 어느 범위까지 서버 권한으로 둘지는 Mirror 연동 전에 결정한다.
- 현재 범위 제외: Mirror 통신 코드, 네트워크 DTO, 서버 상태 동기화, 실제 휴식맵 강화 NPC의 창 열기·닫기 연결.

### 6.3 인벤토리·장비 저장 복원 구현 메모 (2026-07-14)

- 커밋 완료: `EquipmentTransaction.TryRestoreEquippedItem()`을 추가해 저장된 장비도 `EquipmentSystem`을 직접 변경하지 않고 거래 경계를 통과하도록 했다.
- 커밋 완료: `DataManager`의 `PlaceItem()`과 `EquipmentSystem.Equip()` 직접 호출을 각각 `TryPlaceItem()`과 장비 복원 Transaction 호출로 교체했다.
- 커밋 완료: 그리드 배치·UI 생성·장비 복원 실패를 확인하고, UI 생성이나 장비 복원이 실패하면 이미 변경한 상태를 되돌리도록 했다.
- 커밋 완료: `InventoryController.Instance`는 메서드 시작 시 지역 변수 `controller`에 한 번 저장하고 보조 메서드에도 전달해 같은 플레이어의 인벤토리를 끝까지 사용하도록 했다.
- 적용 이유: 지역 변수는 새 인스턴스가 아니라 동일한 객체 참조이며, 반복적인 전역 조회를 줄이고 null 검사·의존성 확인·향후 플레이어별 상태 분리를 명확하게 한다.

### 6.4 인벤토리 모델·Item UI 생성 경계 구현 메모 (2026-07-14)

- 구현 및 현재 씬 검증 완료: `InventoryGrid.RemoveItem()` 직접 변경 경로를 제거하고, 제거·배치·이동 결과는 `TryRemoveItem()`과 `TryPlaceItem()`의 실제 성공 여부를 확인하도록 통일했다.
- 구현 및 현재 씬 검증 완료: `InventoryAddResult`와 `InventoryMoveResult`는 기존 의미를 유지한 명시적 `byte` 코드로 고정하고, 인벤토리 획득 문구는 `InventoryMessageMapper`에서 변환한다.
- 구현 및 현재 씬 검증 완료: `InventoryController.TryAddItemAt()`을 일반 획득과 저장 불러오기의 공통 배치 경계로 사용하고, 성공 후 `OnItemAdded`를 한 번 발행한다.
- 구현 및 현재 씬 검증 완료: Item UI prefab 생성과 `ItemUI.Setup()` 책임은 `InventoryItemUISpawner`로 이동했다. 일반 아이템은 `OnItemAdded` 구독으로, 장착 아이템 불러오기는 `DataManager`가 같은 Spawner를 명시적으로 사용해 UI를 생성한다.
- 씬 적용 범위: 현재 `ItemUpgradescene`의 언팩된 `InventoryController` 오브젝트에만 `InventoryItemUISpawner`를 배치하고 `ItemPrefab.prefab`을 연결했다. 원본 `StatTestPrefeb.prefab` 적용 여부는 팀 회의 전까지 확정하지 않는다.
- 확인 결과: 현재 씬에서 `InventoryController`와 `InventoryItemUISpawner`가 동일 GameObject에 각각 1개 존재하고, Item UI prefab 참조가 연결되어 있다. Unity 컴파일 완료 및 Console 오류·경고 0개를 확인했다.
- 후속 확인: 일반 획득, 회전 아이템 저장·불러오기, 장착 아이템 불러오기에서 Item UI가 한 번만 생성되는지 플레이 모드로 확인한다.
- 후속 정리: `DataManager`의 Spawner 누락 경고 문구에 남아 있는 이전 명칭 `InventoryItemPresenter`를 `InventoryItemUISpawner`로 통일한다.
- 결정 상태: CAND-006의 구현 검증 단계이며 팀 승인 ADR은 아니다. Mirror 요청·응답, 서버 권한 및 네트워크 동기화 방식도 아직 결정하거나 구현하지 않았다.
- 기록 상태: 구현 및 Editor 검증 완료.

### 6.5 장비 결과 코드·메시지 매핑 구현 메모 (2026-07-15)

- 구현 완료: `EquipResult`를 명시적인 `byte` 값으로 고정해 장착·교환·해제 결과 코드의 의미와 번호가 코드 순서 변경에 따라 달라지지 않도록 했다.
- 구현 완료: `ItemEquipHandler.GetEquipMessage()`의 결과→사용자 문구 변환 책임을 정적 `EquipMessageMapper`로 이동했다.
- 구현 완료: 장착 해제, 드래그 장착, 우클릭 교환, 장비·인벤토리 교환 실패 등 기존 7개 호출 경로가 `EquipMessageMapper.GetMessage()`를 사용하도록 통일했다.
- 유지 범위: 사용 가능한 슬롯 전체 탐색 실패 안내와 회전 보관 안내는 단일 `EquipResult` 변환이 아닌 UI 흐름 문구이므로 `ItemEquipHandler`에 유지했다. 상태 복구 실패를 알리는 `Debug.LogError`도 개발 진단용이므로 Mapper로 이동하지 않았다.
- 적용 이유: 장비 상태 처리 결과와 화면 표시 문구를 분리해 결과 코드는 게임 로직·저장·향후 네트워크 응답 경계에서 재사용하고, 사용자 문구는 게임 상태를 변경하지 않는 한 위치에서 관리하기 위함이다.
- 네트워크 관련 범위: 결과 코드를 전송 가능한 작은 정수로 고정한 단계이며, Mirror 요청·응답, 서버 권한 판정, 네트워크 DTO 또는 동기화 방식은 구현하거나 확정하지 않았다.
- 확인 결과: 기존 `GetEquipMessage()` 제거, Mapper 호출 교체 및 새 스크립트의 Unity `.meta` 생성까지 코드 검사로 확인했다. Unity 컴파일과 장착·해제·교환 플레이 검증은 추가 확인 대상으로 남아 있다.
- 기록 상태: 구현 완료. 팀 승인 ADR은 아니다.

### 6.6 월드 아이템 드롭·획득 구현 메모 (2026-07-15)

- 커밋 완료: `PlayerItemDropOrigin`이 플레이어의 드롭 기준점을 제공하고, `WorldItemDropService`가 월드 픽업 생성과 `ItemInstance` 전달을 담당하도록 경계를 정리했다.
- 커밋 완료: 인벤토리 밖으로 드래그하면 `ItemDropHandler`가 `WorldItemDropService.TryDrop()`을 호출한다. 성공 시 Item UI를 제거하고, 생성 실패 또는 빈자리 부족 시 기존 인벤토리 위치로 복구한다.
- 커밋 완료: `WorldItemDropResult`를 명시적인 `byte` 값으로 구성하고 `InvalidItem`, `DropOriginUnavailable`, `PickupPrefabUnavailable`, `SpawnFailed`, `NoAvailablePosition`을 구분했다. 기존 코드 값은 유지한 채 빈자리 부족 결과를 마지막에 추가했다.
- 커밋 완료: 월드 픽업은 `TemporaryWorldItemCube`의 `ItemDataStorage`에 기존 `ItemInstance`를 그대로 보관한다. 따라서 강화 수치 등 런타임 데이터가 인벤토리 획득과 재드롭을 거쳐도 같은 인스턴스에 유지된다.
- 커밋 완료: `WorldItemRarityColorView`가 아이템 등급에 따라 큐브와 Loot 빔 이펙트의 색상을 적용하도록 했다.
- 커밋 완료: `WorldItemTooltipScanner`, `WorldItemTooltipView`, `WorldItemPickupInteractor`를 통해 플레이어 감지 범위 안의 가장 가까운 아이템에 이름·종류·등급 툴팁을 표시하고, 현재 감지 대상으로 확인된 큐브만 클릭 획득할 수 있도록 했다.
- 커밋 완료: `WorldItem` 전용 레이어를 추가하고, 픽업 프리팹 루트와 스캐너·클릭 획득 마스크를 해당 레이어로 통일했다.
- 커밋 완료: 테스트 Drop 버튼의 기존 `ItemGenerator.Drop()` 직접 생성 경로를 제거하고 `WorldItemDropService`를 사용하도록 변경했다. 버튼을 반복해서 눌러도 기존 픽업을 지우지 않고 새로운 픽업을 누적 생성한다.
- 커밋 완료: `WorldItemDropService`는 플레이어 드롭 기준점 주변 0.9~2.2 범위에서 최대 20회 후보 위치를 찾고, 반경 0.6의 `Physics.CheckSphere` 검사로 `WorldItem`, `Wall`, `Prop`, `Player`와 겹치는 위치를 제외한다. `Ground`와 `Terrain`은 바닥 자체가 장애물로 판정되지 않도록 검사 마스크에서 제외했다.
- 실패 처리: 빈자리를 찾지 못하면 `NoAvailablePosition`을 반환한다. 인벤토리 드래그는 아이템을 원래 위치로 복구하고, 테스트 버튼은 실패 결과를 로그로 알리며 새 픽업을 생성하지 않는다.
- 씬 연결 확인: `Act1_DropTest`에서 테스트 드롭 스크립트의 `WorldItemDropService` 참조, 스캐너·클릭 획득의 `WorldItem` 마스크, 드롭 서비스의 충돌 마스크가 직렬화되어 있다.
- 현재 범위: `Act1_DropTest`를 중심으로 월드 드롭·획득의 기본 구조를 구현한 단계다. 최종 본편 플레이어 입력 연결, 실제 맵별 배치 규칙, 픽업 풀링 및 시각 에셋 확정은 이후 통합 범위다.
- 멀티플레이 관련 범위: 향후 서버가 드롭 결과와 생성 위치를 결정할 수 있는 단일 서비스 경계는 마련했지만, Mirror 서버 권한 실행, 픽업 동기화 및 네트워크 생성은 아직 구현하거나 팀 결정으로 확정하지 않았다.
- 기록 상태: 구현 및 커밋 완료. 팀 승인 ADR은 아니다.

### 6.7 문서 변경 이력

| 날짜 | 변경 내용 | 작성자 |
|---|---|---|
| 2026-07-13 | Decision Log 초기 생성. 승인된 결정 없음. 아키텍처 리뷰의 권장안을 검토 대기 후보로 등록. | Codex |
| 2026-07-14 | 2026-07-13 이후 Git 커밋과 상점·강화 작업을 구현 이력으로 추가. Git 계정의 작성자 표기를 팀원 이름 `김성우`로 통일하고, 강화 UI 및 인벤토리·장비 저장 복원 작업을 커밋 완료 상태로 기록. 구현 반영과 팀 승인 ADR을 분리해 기록. | 김성우 / Codex |
| 2026-07-14 | 인벤토리 `Try*` 결과 경계, 추가·불러오기 경로 통합, `InventoryItemUISpawner` UI 생성 분리 및 `ItemUpgradescene` 한정 적용 상태를 구현 메모로 추가. | 김성우 / Codex |
| 2026-07-15 | `EquipResult` 명시적 `byte` 코드 고정과 `EquipMessageMapper` 분리·적용 내용을 구현 메모로 추가. 구현 범위와 추가 검증 항목을 구분해 기록. | 김성우 / Codex |
| 2026-07-15 | 월드 아이템 드롭 서비스, `ItemInstance` 유지, 등급별 큐브·빔 표시, 근거리 툴팁과 클릭 획득, `WorldItem` 레이어 및 `Physics.CheckSphere` 빈자리 탐색 구현 내용을 추가. 구현 완료 범위와 Mirror·본편 통합의 미확정 범위를 구분해 기록. | 김성우 / Codex |

---

참조 문서:

- `Docs/Architecture/01_ProjectInventory.md`
- `Docs/Architecture/02_ArchitectureReview.md`
