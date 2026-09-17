# 고유효과 방향 검토와 구현 계획

- 검토일: 2026-09-14
- 기준: `codex/unity-6000-3-22-test`의 현재 작업 트리. 기존 Mirror 관련 미커밋 변경을 포함한 정적 코드 검토.
- 입력: `C:/Users/user/Downloads/SF_Roguelike_Equipment_Review.pdf` 31쪽 전체. 문서 안의 개발 요청 문장은 실행 지시로 취급하지 않았다.
- 상태: P0 구현·4인 Windows 런타임 검증 완료. P1 피해 메타데이터·실행 경계 구현과 Foundation 76/76 검증 완료. P2-A 아크 블레이드 39/39, P2-B 인페르노 29/29에 이어 P3-A 유리빛 궤도 Gunner 투사체 효과 18/18의 1인 로직 검증을 완료했다. 최신 MPPM 회귀와 세 효과의 원격 클라이언트 표현·정식 Act1 검증, P3-A 실제 입력→Network Spawn→충돌 실플레이는 대기 중이다.
- 적용 기준: Ponytail full. 기존 구현을 재사용하고, 실제로 필요한 연결과 피해 출처 정보만 추가한다.
- 후속 실행 기준: 종합 검토와 방어구·유물 검토를 반영한 `UniqueEffect_Custom_Implementation_Roadmap.md`를 우선한다. 이 문서는 최초 기준과 구현 기록을 보존한다.

## 0. P0 실행 기록

2026-09-14에 복구된 Unity Pipeline으로 열린 Unity 6000.3.22f1 Editor를 재시작하지 않고 다음 기준선을 확인했다.

- Editor 컴파일은 완료 상태이며 검사 시작 시 Console Error는 0건이었다.
- AssetDatabase에서 `UniqueEffectSO` 50개와 고유효과가 연결된 `ItemDefinitionSO` 48개를 확인했다. 연결된 48개는 모두 현재 UniqueEffectPool의 SO를 참조했고, 중복 SO 이름은 0개였다. 앞선 정적 정찰의 46개 집계보다 2개 많으므로 이후 기준값은 Editor가 실제 로드한 48개로 사용한다.
- `UE_SturdyArmor`는 남아 있는 이전 class identifier 문자열과 무관하게 Editor에서 `StatThresholdBuffUniqueEffectSO`로 정상 로드됐다.
- 실제 로드값은 양산형 코어 `OnDamageDealt / 이동속도 +20% / 3초 / RefreshDuration`, 라이트세이버 `OnDamageDealt / 공격속도 +6% / 5초 / 최대 5스택`, 사이버네틱 코어 `OnDodge / 이동속도 flat +77 / 4초 / 30초 공유 쿨다운`, 고철 압축기 `OnKill / 공격력 +0.5% / 최대 100스택 / 아이템 저장`, 중력장 생성 코어 `적 대상 / 반경 8 / 이동속도 -50%`다.
- 기존 4인 Mirror 전투 스모크는 영속 처치 스택 3종의 플레이어별 증가·최대치·드랍·재획득·복원·제거와 `ShareCooldown`/`PerItem`의 소유자 분리를 검사한다. 여기에 양산형 코어·라이트세이버·사이버네틱 코어·중력장 생성 코어의 실제 작성값과 발동 결과를 직접 검사하는 P0 단계를 추가했다.
- 최초 점검에서 `UniqueEffectTable.xlsx`, `UniqueEffectTableRow`, `UniqueEffectTableSOImporter.ApplyRow`에 `targetEnemies`와 `persistStackOnItem`이 없어 재임포트 시 기본값 `false`로 사라지는 문제를 재현했다. 이후 WJ 담당 수정 승인을 받아 두 Boolean 열과 임포트 경로를 추가했다. 완료 판정은 Excel→JSON→SO 재임포트 뒤 `UE_GravityFieldCore.targetEnemies=true`와 영속 처치 스택 3종의 `persistStackOnItem=true`가 그대로 유지되는지 확인하는 것으로 한다.

Windows64 Player 4인 실행에서 P0 작성값, 양산형·사이버네틱 반복 차단, 라이트세이버 5스택 상한·해제, 중력장 적 적용·해제, `ShareCooldown`/`PerItem` 소유자 분리와 네 클라이언트 복제를 확인했다. 서버와 세 클라이언트의 최종 스모크가 모두 통과했다. `HELLO WORLD`와 이동속도 flat `+77`은 데이터 손실 문제가 아니라 기획값 확정이 필요한 항목이므로 임의로 교체하지 않았다.

## 1. 판단

기획 방향은 적합하다. 무기에서 전투 행동을 바꾸고, 방어구·유물이 그 행동의 조건과 생존을 보완하는 구성이 현재 장비 구조와 잘 맞는다. 다만 문서는 Excel 기반 분석이므로, 현재 코드 기준으로 구현 순서를 보정해야 한다.

1. **유지:** 기존 Passive/Triggered/Threshold/Aura와 단일 `uniqueEffectId` 연결. 기존 효과 50개를 갈아엎거나 아이템마다 전용 클래스를 만들 필요가 없다.
2. **보정:** OnDamageDealt는 현재 일반 전투와 Mirror 코드에 연결돼 있다. 신규 연결 작업으로 단정하지 않고 실제 발동 횟수·권한·피해 출처를 검증한다.
3. **보정:** Burn/Slow/Freeze/Electric 상태이상 코드가 이미 있다. 기존 구현의 의미가 문서의 제안과 다르므로, 재사용할 부분과 규칙을 바꿀 부분을 구분한다.
4. **우선:** 새 공격형 효과 전에 공격 단위 ID, 직접/효과 피해 구분, 원인 플레이어, 이벤트 순서를 확보한다. Mirror를 마지막 단계에 붙이는 방식은 현재 브랜치에 맞지 않는다.
5. **범위:** 공란 20개 전체의 임시 버프 적용을 신규 행동 구현의 선행 조건으로 만들지 않는다. 대표 기존형 검증과 새 효과 1종씩의 실전 검증을 먼저 한다.

### 기획에서 수정하면 좋은 부분

- **연쇄 번개는 첫 신규 기능으로 적절하다.** 첫 표적과 적 밀집 위치를 선택할 이유가 생기고, 여러 장비가 대상 수·조건만 달리해 재사용할 수 있다.
- **무조건 붙는 화염 추가타만으로는 행동 변화가 약하다.** 근접 기본 공격이라는 제약과 식별 가능한 피드백은 두되, 첫 버전은 피해 경로 검증용으로 본다. 화염 빌드의 최종 차별화는 점화 후 표적 전환·열 소모·연소 전파 중 하나로 평가한다.
- **속성이 있다고 모든 속성 행동을 자동 부여하지 않는다.** ElementType은 우선 기존 피해 보너스 계산에 사용하고, 연쇄·점화·냉각은 선택한 아이템 효과가 명시적으로 켠다. 그렇지 않으면 Rare 입문과 Unique/Legendary의 역할이 겹친다.
- **대가형 버프도 실제 공격 규칙을 확인한다.** 공격 속도 -10%가 애니메이션과 실제 공격 주기를 바꾸는지 확인하고, 툴팁 숫자만 대가로 보지 않는다.
- **회피 보상은 더 늘리지 않는다.** 현재 OnDodge는 회피 상태 진입이다. 성공 회피로 표현하지 않으며, 밤의 칼날·닌자의 움직임은 후속 단계에서 다음 유효 공격 1회 강화로 묶는다.
- **방어력과 보호막, penetration과 탄 관통을 구분한다.** 기존 스탯 버프를 쓰는 동안에는 현재 행동만 설명한다.
- 문서의 25%/20% 피해, 0.8초 쿨다운, 반경 4, 이동 속도 +35%는 실험값이다. 이번 검토에서 확정 밸런스로 채택하지 않는다.

## 2. 현재 코드에서 확인한 연결

파일 경로는 저장소 루트 기준이다. 아래는 실행 검증이 아니라 코드 근거다.

| 영역 | 현재 코드와 근거 | 계획에 미치는 영향 |
| --- | --- | --- |
| 플레이어 소유 상태 | `Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerContext.cs`가 Inventory/Equipment/Stats/Buffs/Health/ItemTriggers/CombatAuthority 참조 소유 | 새 PlayerContext나 전역 EffectManager를 만들지 않는다. |
| 효과 발동 | `.../ItemTriggerManager_MirrorTest.cs:61`의 서버 전용 Fire가 장착 장비와 보유 유물을 조회 | 가방의 무기는 비활성, 유물은 보유 활성이라는 기존 규칙 유지. |
| 효과 확장 경계 | 위 파일 `FireIfReady`는 TriggeredBuff만 처리. `PlayerRelicEffectProvider_MirrorTest.cs`도 타입별 switch 사용 | 신규 SO 생성만으로 동작하지 않는다. 신규 타입의 호출·해제 경로를 함께 연결해야 한다. |
| 일반 적중 이벤트 | `Assets/WBHTest/Scripts/Combat/WBH_CombatManager.cs:44` 이후 OnDamageDealt/OnCrit 발행. TakeDamage가 죽은 대상으로 인해 거부돼도 이후 발행 여부를 확인하지 않음 | PDF의 미연결 메모는 현재 코드에 그대로 적용할 수 없다. 실제 피해 적용 성공을 기준으로 발행하고 사망 뒤 남은 Collider의 허위 발동을 차단한다. |
| Mirror 적중 이벤트 | `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyAuthority_MirrorTest.cs:608` HandleDamaged에서 공격자의 ItemTriggers 호출 | 신규 resolver와 이 이벤트 양쪽에서 중복 발행하지 않는다. |
| Mirror 처치 | 같은 파일 `GrantKillRewardOnce`가 마지막 공격자 기준 OnKill/경험치/골드 처리 | 효과 피해에도 원인 플레이어가 필요하다. 공격형 재발동 차단과 처치 보상은 분리한다. |
| 피해 계산 | `.../WBH_CombatResolver_MirrorTest.cs`에 공격력×계수→속성→치명타→방어→받는 피해 배율→최소 피해 순서 | 결과 FinalDamage를 다시 기본 피해로 넣어 속성·방어를 이중 적용하지 않는다. |
| 피해 정보 | `Assets/WBHTest/Scripts/Combat/WBH_DamageRequest.cs`, `WBH_DamageResult.cs`에 공격 ID·효과 피해 원인 없음 | 필요한 필드만 확장하고, 직접 공격의 기존 생성 지점을 모두 연결한다. |
| 공격 요청 | `.../PlayerCombatAuthority_MirrorTest.cs:75` 부근 요청 ID와 타격 대상 HashSet 존재. Update는 예약을 지운 뒤 Resolve를 호출하며 `ClearServerAttackReservation`이 ID를 0으로 초기화 | 초기화 전에 확정 공격 ID를 캡처해 전달한다. 플레이어·스폰 수명과 묶고 산탄/투사체에도 동일 공격 ID 유지. |
| 후속 피해 순서 | `Assets/WBHTest/Scripts/Enemy/WBH_EnemyStatus.cs:105`에서 HP 감소→OnDamaged→OnDead | OnDamaged 안의 같은 대상 즉시 추가 피해는 재진입 위험. 원래 피해 처리가 끝난 뒤 후속 피해를 처리한다. |
| 상태이상 재사용 | `Assets/WBHTest/Scripts/StatusEffect/New Folder/WBH_BurnEffect.cs`, `WBH_FreezeEffect.cs` 등 | Burn은 대상 최대 HP 비례, Freeze는 이동/공격 속도 배율. 공격력 스냅샷 연소·스택 빙결과 동일하지 않다. |
| 오라 데이터 | `Assets/WJ_TestPlace/Script/Item/Data/DataLoad/UniqueEffectTableModel.cs`, `Editor/UniqueEffectTableSOImporter.cs:167`은 radius만 전달 | targetEnemies 및 필요한 지속 스택 설정을 표와 importer에서 재현 가능하게 만든다. |
| 저장 스택 | `Assets/WJ_TestPlace/Script/Buff/TriggeredBuffUniqueEffectSO.cs`에 persistStackOnItem, Mirror Trigger에 저장·동기화 경로 존재 | 누적 성장 저장을 새로 만들지 않는다. 런 종료/아이템 이동 정책을 먼저 정하고 기존 필드 사용. |
| 툴팁 | `Assets/SW/Scripts/ItemTooltip/TooltipUI.cs:403`은 라벨 DB와 coefficients 사용 | 효과 정의뿐 아니라 라벨 데이터도 함께 갱신해야 한다. |
| 드랍 | `Assets/SW/Scripts/WorldItem/DropRules/ItemDropRollService.cs`와 `DropRarityModifierUniqueEffectSO.cs` 존재 | 새 드랍 시스템 불필요. 후자는 전역 static 상태이므로 협동에서 소유자별/팀 공유 정책 확인 필요. |
| 상점 | `Assets/SW/Scripts/Shop/Stock/ShopStockRollService.cs`는 등급 가중치와 후보 아이템으로 추첨 | 첫 빌드 검증은 기존 풀을 활용한다. 자동 빌드 추천 엔진은 만들지 않는다. |
| 표시 한계 | `.../PlayerRuntimeStateSync_MirrorTest.cs:20`은 버프 스냅샷 최대 32개 | 다수 유물 검증에서 실제 효과와 표시 누락을 별도로 확인한다. |

### 일반 경로와 Mirror 경로를 섞지 않기

일반 효과 SO는 전역 PlayerBuffManager 및 SO 내부 쿨다운을 쓰는 부분이 있다. Mirror 경로는 플레이어 컴포넌트에 쿨다운과 런타임 객체를 분리했다. 이번 브랜치의 신규 기능은 Mirror의 서버·PlayerContext 경계를 기준으로 붙인다. 일반 경로의 지원 범위도 구현 전에 목록으로 남기고, 같은 한 번의 피해가 양쪽 이벤트에 동시에 들어가지 않게 한다. 기존 전체 시스템을 일괄 통합하는 리팩터링은 이 작업에 포함하지 않는다.

일반 Gunner 기본 탄의 Burn 설정과 Mirror Gunner 기본 탄의 상태이상 없음 등 경로 차이도 있다. 원본 전투를 호출하는 Mirror 스킬까지 포함해 이벤트의 단일 발행 여부를 검증한다. 이름이 Electric인 기존 상태이상은 공격력 감소이며 연쇄 번개가 아니다. Burn의 공격자 없는 피해는 Mirror의 마지막 공격자 기록에 의존할 수 있으므로, 기존 기록을 추측해서 쓰지 않고 원인 플레이어를 전달한다. 적의 원본/Mirror 처치 어댑터 활성 상태는 Editor에서 확인해야 한다.

### 현행 SO에서 재확인한 데이터

`Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/UniqueEffectPool/`에서 다음을 직접 확인했다.

- 현재 `2. JSONFile/UniqueEffectTable.json`은 50행이며 생성 SO도 50개다. JSON 타입 분포는 Passive 17, Triggered 22, Threshold 7, Aura 2, PeriodicLog 1, DropRarity 1로 PDF와 같다. 정찰의 아이템 JSON/SO 대조에서는 효과 연결 46개가 확인됐으며 참조 불일치는 발견되지 않았다. 이는 실전 발동 인증은 아니다.
- `UE_AdvancedCore.asset`은 여전히 PeriodicLogUniqueEffectSO이며 10초 간격 HELLO WORLD 설정이다. Mirror 유물 처리기의 switch에는 이 타입이 없어, 일반 경로의 로그 동작과 Mirror 경로의 미처리를 구분한다. 전투 효과로 교체하거나 실제 배포 풀에서 제외하는 것이 P0 작업이다.
- `UE_CyberneticCore.asset`에는 지속시간 4초, 쿨다운 30초, 스탯 값 77이 남아 있다. 테스트 잔재인지 확인하고 이동 속도 계산·실제 플레이를 기준으로 교정한다.
- `UE_GravityFieldCore.asset`은 현재 `targetEnemies: 1`, 반경 8, 감속 -50으로 저장돼 있다. 현재 SO의 대상이 틀렸다고 단정할 수는 없다. 문제는 importer가 이 옵션을 생성하지 않아 새로 생성할 때 같은 결과를 재현하지 못하는 점이다.
- `UE_ScrapCompactor.asset`은 `persistStackOnItem: 1`이다. 저장 기능이 없는 것이 아니라 importer 모델에 빠져 있어 재생성 재현성이 문제다.
- `UE_SturdyArmor.asset`에는 이전 `HealthThresholdBuffUniqueEffectSO` class identifier 문자열이 남아 있다. GUID만으로 Missing Script라고 단정하지 않고 Editor에서 실제 로드 타입을 확인한 후 연결한다.
- Mirror 유물 provider에는 `DropRarityModifierUniqueEffectSO`도 처리 분기가 없다. 행운의 부적의 실제 효과 누락 여부를 P0에서 확인하고, 전역 static 배율을 그대로 새 경로에 연결하지 않는다. 개인 드랍 보정인지 팀 공유 보정인지 결정한 뒤 기존 드랍 추첨에 전달한다.

## 3. 먼저 합의할 최소 동작 규칙

아래는 구현 기준으로 권하는 초안이며, 기존 데이터의 확정 의미를 바꾸었다는 뜻이 아니다.

| 항목 | 첫 구현의 규칙 |
| --- | --- |
| 판정 주체 | Mirror에서는 서버만 피해·확률·쿨다운·표적·스택 판정. 클라이언트는 결과 표시. |
| 발동 자격 | 유효한 직접 기본 공격. 근접/원거리 구분은 공격 정보로 전달. 스킬·DoT·효과 피해는 첫 버전 신규 공격형 효과 대상 제외. |
| 공격 단위 | 플레이어 식별 + 스폰/세션 수명 + 공격 번호. 산탄 한 번의 모든 펠릿은 같은 공격 번호. 다중 Collider는 적 런타임 객체로 합친다. |
| 효과 단위 | 연쇄는 효과별 공격당 1회, 화염 추가타는 효과별 공격·대상당 1회. 기존 발동형 버프 횟수는 자동으로 바꾸지 않는다. |
| 재발동 | Effect/DoT 피해는 신규 공격형 효과 및 OnCrit/OnDamageDealt 보너스를 다시 켜지 않도록 구분. 허용 시너지는 후속 명시 규칙으로만 추가. |
| 처치 | 효과 피해의 원인 플레이어를 보존. OnKill·경험치·골드는 기존 한 번 처리 경로 사용. 후속 처치 폭발은 초기 범위 제외. |
| 피해 기준 | 공격 확정 시 기준 공격력/속성 보너스 등 필요한 공격측 수치 저장. 각 대상 방어 계산은 타격 시 공용 공식으로 1회. 효과 자체의 치명타는 첫 버전 비활성. |
| 이벤트 순서 | 1차 HP/사망 확정 후 후속 피해 실행. 치명타로 죽은 최초 대상은 추가타 대상에서 제외. 연쇄는 저장한 최초 피격 지점을 시작점으로 사용할 수 있다. |
| 교체·지연 적중 | 발사 당시 무기 인스턴스와 장착 세대 기록. 무기 교체 후 도착한 원래 탄의 피해는 기존 규칙을 유지하되, 해제된 무기의 신규 고유효과는 발동하지 않는다. |
| 수명 | 전투용 쿨다운·공격 중복 판정은 플레이어 런타임 소유. 해제/사망/새 런/접속 종료 시 준비·영역·구독 정리. 중복 방지 기록은 공격 완료 후 폐기해 무한 증가 방지. |
| 성장 스택 | 기존 persistStackOnItem에 저장된 값은 보존. 런 내 장비 재획득 시 복원 여부와 새 런 초기화 위치를 명시하고, 임시 전투 스택과 분리. |
| 중복 효과 | ShareCooldown만으로 쿨다운 0인 중복 유물의 1회 발동이 보장되지 않는다. 동일 사건에서 효과 ID 중복 제거 여부를 별도 정의. |

구조는 기존 DamageRequest/Result의 최소 확장과 플레이어별 Trigger 처리 확장으로 시작한다. `공격 정보 → 자격 검사 → 효과 실행`을 코드에서 구분하되, 각각을 인터페이스·Factory·노드 그래프로 만들지 않는다. SO는 설정만, 플레이어/대상 컴포넌트는 변하는 상태를 소유한다.

## 4. 구현 순서와 완료 기준

| 단계 | 실제 작업 | 완료 조건 |
| --- | --- | --- |
| P0 데이터·기존 동작 기준선 | 실제 JSON/SO 연결 수 재집계, 테스트 효과·77·오라 대상·영속 스택·중복 유물 점검. 양산형 코어/라이트세이버/처치 성장/회피 버프를 실전에서 확인 | 현재 값과 PDF 차이 기록. 재가져오기 후 결과 재현. 의도하지 않은 다중 발동 0건. |
| P1 피해 정보와 실행 순서 | **구현 완료:** 요청 ID를 근접·투사체·산탄에 전달. 직접/스킬/효과/DoT 원인 구분. 소유자 전달. 단일 이벤트 발행과 플레이어별 후속 피해 처리 순서 보장 | Foundation 76/76 PASS. 과거 MPPM 63/63 PASS 이력은 보존되며, 보강된 최신 67-check 러너의 실기 회귀는 재실행 대기. |
| P2-A 연쇄 번개 1개 | **1인 시범 구현·보완 완료:** 아크 블레이드에 `UE_ArcBladeChainLightning`을 연결하고 실행기·표현·공용 피해 책임을 분리했다. 직접/효과 중복 경계, 공격 시작 스탯 스냅샷, 공격 번호 수명 초기화를 보완했다 | 1인 자동 검증 39/39 PASS. 실제 데이터·Fighter/적 프리팹에서 추가 3명, 25%→20%→16% 피해, 비치명, 벽, 쿨다운, 후보 없음, 동일 대상 후속타, 쿨다운 0 다중 직격의 공격당 1회를 확인했다. 원격 ClientRpc 표현은 미검증. |
| P2-B 화염 추가타 1개 | **1인 시범 구현 완료:** 인페르노의 실제 Fighter 근접 기본 공격이 처리 중인 직접 대상에만 같은 대상 20% Fire Effect를 1회 등록한다. 기존 직접 대상 집합으로 분류해 새 `AttackKind`는 만들지 않았다. | P2-B 29/29, P2-A 39/39, P1 76/76 PASS. 다중 Collider, 치명/비치명, 스냅샷, 직접/효과 처치, Skill/Effect/DoT/권한 밖 Direct 제외를 확인했다. 원격·4인·정식 Act1은 미검증. |
| P3-A Gunner 투사체 대표 | **1인 로직 구현 완료:** 유리빛 궤도 Rifle 한 발의 유효 직접타에 정확한 발사 무기 인스턴스·장착 세대가 유지될 때만 15% 비치명 Ice Effect를 적용한다. 직접 피해는 명중 시점 Stat 정책을 유지한다. | 18/18 PASS. 다중 Collider, 다른 무기 교체, 같은 정의의 다른 인스턴스, 해제·재장착, 두 탄 역순 도착을 확인했다. 실제 solo Host 입력·Network Spawn·물리 충돌과 원격·4인·정식 Act1은 미검증. |
| P3-B 기존형 방어구·유물 대표 | H3/B1/R2 중 현재 사건으로 표현 가능한 한 종류부터 검증하고, 실제 장착 슬롯 변화가 필요한 방어구는 작은 활성 어댑터 책임으로 분리한다. | 실제 장착·해제·드랍·거래·사망·새 런 수명을 확인하고, 다른 담당자 파일이 필요하면 사전 승인 후 진행한다. |
| P4 상태이상 정비 | 기존 Slow/Burn/Freeze를 재사용. Burn의 귀속·공격력 기반 여부·refresh tick 동작 보정. 냉각 우선, 스택 빙결과 보스 저항은 이어서 구현 | 상태 겹침·재적용·적 풀 재사용·보스 저항·서버 판정 검증. 기존 상태이상과 이중 적용 없음. |
| P5 방어구·후속 공격 | 실제 보호막과 다음 공격 토큰을 각각 별도 작은 기능으로 구현. HP 처리·상태 동기화·UI까지 한 묶음 | 최초 피해는 보호막 발동으로 소급 취소하지 않음. 다음 공격 준비는 1회만 소모. 장비 해제 후 잔류 없음. |
| P6 나머지 장비 전개 | 검증된 효과 타입을 나머지 공란과 Rare 입문에 적용. 탄착 영역/지연 폭발은 필요 장비를 묶어 추가 | 데이터 재가져오기·실제 획득·툴팁·장착·전투까지 연결. 연결률과 플레이 차이를 별도 기록. |

P0의 정상 동작 대표 검증에는 상시/피격/적중/오라/회피/체력 조건 각 1개면 충분하다. 공란 20개 모두에 임시 버프를 넣었다가 다시 교체하는 두 번의 데이터 작업을 필수로 하지 않는다. 실제 코드 수정에 들어갈 때에는 P0, P1, P2-A를 각각 리뷰 가능한 변경 묶음으로 나눈다.

### 첫 연쇄·화염 실험 사양

- 연쇄: 문서의 최대 추가 3명·반경 4·0.8초·첫 전이 25%·후속 80%를 튜닝 출발값으로 사용 가능. 한 번의 공격에서 어느 직접 적중을 최초 표적으로 삼을지 결정론적인 순서를 사용한다. 원래 공격이 여러 적을 맞힌 경우에도 연쇄가 여러 번 시작하지 않는다.
- 화염: 근접 기본 공격의 기준 공격력 20%를 출발값으로 사용 가능. `FinalDamage × 20%`를 다시 resolver에 넣지 않는다. 기존 resolver가 현재 스탯을 읽고 치명타를 굴리므로, 스냅샷·비치명 옵션을 먼저 지원한다.
- 효과 피해 0 또는 무효 대상은 resolver 호출 전에 제외한다. 기존 최소 피해 1 처리 때문에 계수 0이 실제 피해로 바뀌지 않게 한다.
- 시각 효과는 기존 VFX 풀을 우선 사용하고, 서버가 정한 경로/결과만 클라이언트에 표시한다. 추가 타격마다 NetworkObject를 생성하는 구조는 먼저 만들지 않는다.

### P2-A 1인 시범 구현 기록 (2026-09-16)

- 실제 `item.weapon.greatsword.arcblade`의 `uniqueEffectId`를 `UE_ArcBladeChainLightning`으로 연결하고 ItemTable 원본→JSON→생성 ItemDefinitionSO 재생성까지 확인했다.
- 신규 설정 SO는 최대 추가 대상 3명, 반경 4m, 쿨다운 0.8초, 첫 전이 공격력 25%, 다음 전이마다 직전 피해 계수의 80%를 튜닝 출발값으로 보관한다.
- 직접 기본 공격만 시작점이 되며 같은 공격의 원래 직접 대상, 이미 연쇄된 대상, 사망 대상과 벽 뒤 대상은 제외한다. 효과 피해는 `DamageCause.Effect`로 처리하고 치명타와 재귀 발동을 막았다.
- 클라이언트 표현은 서버가 확정한 각 구간만 짧은 LineRenderer로 표시하며 별도 NetworkObject를 생성하지 않는다. 툴팁 라벨 DB에 항목이 없을 때는 SO의 한국어 설명으로 안전하게 대체한다.
- 책임 분리 뒤 `ItemTriggerManager_MirrorTest`는 서버 자격·쿨다운·얇은 RPC를, `ChainLightningExecutor_MirrorTest`는 탐색·벽·감쇠·후속 큐 등록을, `UniqueEffectPresentation_MirrorTest`는 로컬 선 표현 수명을 담당한다.
- 공용 리졸버는 직접 피해와 후속 효과 피해의 중복 기록을 분리하고, 직접 피해 처리 진입 시점의 공격력·치명타·관통·속성 보너스를 해당 동기 후속 큐까지 고정한다. 이는 투사체 발사 시점 스냅샷이나 지연 실행 스케줄러 계약을 의미하지 않는다.
- Unity 컴파일 오류와 Console 오류는 0건이었다. `Validate Arc Blade Chain Lightning P2`는 실제 생성 SO와 SW Fighter/적 프리팹으로 39/39 PASS, P1 Foundation 회귀는 76/76 PASS였다.
- 사용자 요청에 따라 이번 완료 판정은 1인 로직 검증까지만 포함한다. 실제 Play Mode에서 번개 선의 화면 품질, Host/원격 ClientRpc 일치, 4인·정식 Act1·성능은 후속 검증이다.

### P2-B 1인 시범 구현 기록 (2026-09-16)

- 실제 `item.weapon.axe.inferno`의 ItemTable `uniqueEffectId`를 `UE_InfernoExtraHit`으로 연결하고 JSON과 생성 ItemDefinitionSO 참조까지 갱신했다.
- `InfernoExtraHitUniqueEffectSO`는 공격력 20% 계수와 한국어 설명만 보관한다. 실행 상태나 범용 계층은 추가하지 않았다.
- 발동 자격은 `DamageCause.Direct` 단독 판정이 아니라 `PlayerCombatAuthority_MirrorTest`가 실제 Fighter 공격 처리 중 등록한 직접 대상인지까지 확인한다. 따라서 스킬·총기·기타 임의 Direct를 위한 새 `AttackKind` 없이 현재 범위를 정확히 좁혔다.
- 원래 직접 피해가 끝난 뒤 대상이 살아 있으면 같은 공격 번호로 Fire `DamageCause.Effect`를 기존 동기 큐에 등록한다. 후속타는 비치명이며 직접 피해 진입 시점의 공격력 스냅샷을 사용한다.
- 직접타 처치에는 후속타를 등록하지 않고, 후속타 처치는 기존 적 권한의 보상 경로를 한 번만 사용한다. 다중 Collider도 같은 적 객체 기준 Direct 1회와 Effect 1회로 끝난다.
- Unity 자동 검증은 실제 생성 SO와 SW Fighter/적 프리팹으로 P2-B 29/29 PASS였다. 이어 P2-A 39/39와 P1 76/76 회귀가 통과했고 컴파일 성공 및 Console Error 0건을 확인했다.
- 신규 실행 타입 SO는 현재 EffectPool의 수동 시범 자산이다. 아이템 연결은 ItemTable 원본에 남지만, 효과 자체를 삭제 후 표에서 완전 재생성하려면 WJ 데이터 모델·임포터의 신규 타입 지원이 필요하므로 별도 승인 범위로 남긴다.
- 실제 Play Mode 화면 피드백, Host/원격 클라이언트, 4인 MPPM, 정식 Act1 및 성능은 검증하지 않았다.

## 5. 공란 20종의 역할 배치

최종 itemId는 실제 ItemTable 값으로 유지한다. PDF의 `_Draft` 효과 ID는 자동 확정하지 않는다. 검증된 타입과 연결할 때 최종 효과 ID를 정해 재명명과 저장 데이터 영향을 줄인다.

| 장비 | 권장 역할 | 구현 묶음 |
| --- | --- | --- |
| 인페르노 | 근접 화염 추가타 → 점화 | P2-B, P4 |
| 크루세이더 | 치명타 조건의 강한 연쇄 | P2-A 검증 후 데이터 변형 |
| 수호자의 정의 | 피격 반격 버프 → 실제 보호막 | 기존 Triggered, P5 |
| 폐열 절단 대검 | 적중 열 누적 → 소모 추가타 | 기존 Stack 검증 후 소비 규칙 추가 |
| 와일드파이어 | 연소 대상 처치 후 제한 전파 | P4 이후. 처치 원인·전파 횟수 제한 필요 |
| 초냉매 | 적중 냉각 → 제한 빙결 | P4. 착용자 오라는 별도 임시안 |
| 밤의 칼날 | 회피 후 다음 유효 기본 공격 강화 | P5 |
| 공허의 수확자 | 처치 지점의 제한 후속 파동 | P6 영역 피해 |
| 아크 블레이드 | 기본형 연쇄 | P2-A 대표 |
| 코어 브레이커 | 관통 특화 → 같은 표적 장갑 약화 | 기존 Passive 우선, 대상별 누적은 후속 |
| 중력 우물 | 탄착 감속 영역 | P6. 착용자 오라로 최종 기능을 대체하지 않음 |
| 반물질 랜스 | 장갑 대응 → 실제 직선 관통 | 기존 Passive 우선, 투사체 관통은 별도 작업 |
| 월드 엔더 | 제한 빈도의 후속 폭발 | P6 영역 피해 |
| 특이점 박격포 | 탄착 지연 폭발 | P6 영역 피해와 지연 수명 |
| 스마일 시그널 | 지원 오라 → 적중 표식 | 기존 Aura 및 팀 판정, 표식은 후속 |
| 에코 챔버 | 공격 잔향 1회 | 공격 정보·재발동 규칙 검증 후. 당장은 기존 피격 버프 가능 |
| 일식 기관 | 탄착 화염 영역 | P4 연소 + P6 영역 |
| 스타 브리처 | 가까운 거리 적중 추가 폭발 | P6. 근접 공격 태그와 거리 조건을 구분 |
| 닌자의 움직임 | 회피 후 다음 공격 준비 | 밤의 칼날과 P5 공유 |
| 태양의 은혜 | 높은 체력 유지 → 무피격 보호막 | 기존 Threshold, P5 |

Common/Advanced와 모든 Rare 공란을 채우지는 않는다. Rare는 볼트/솔라/제로 블레이드 같은 소수 입문 장비부터 같은 기능의 약한 버전으로 연결한다. 이미 고유 역할이 있는 데브리스 심장 도끼에 냉기라는 이유로 빙결까지 추가하지 않는다.

## 6. 파일별 구현 경계

| 책임 | 기존 수정 후보 | 변경 목적 |
| --- | --- | --- |
| SW 서버 발동 | `Assets/SW/TEST/MirrorPlayerContext/Scripts/ItemTriggerManager_MirrorTest.cs` | 피해 정보 수신, 신규 효과 타입 실행, 소유자별 쿨다운·중복 판정 |
| SW 공격 | 같은 폴더 `PlayerCombatAuthority_MirrorTest.cs`, `WBH_CombatResolver_MirrorTest.cs` 및 실제 연결 투사체 | 기존 공격 ID 전달, 스냅샷/효과 피해 계산, 후속 순서 |
| SW 적 이벤트 | `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyAuthority_MirrorTest.cs` | 결과 출처 전달, 발동 중복 차단, 처치 귀속 유지 |
| BH 공통 피해 | `Assets/WBHTest/Scripts/Combat/WBH_DamageRequest.cs`, `WBH_DamageResult.cs`, 필요 시 `WBH_CombatManager.cs` | 기존 구조체의 필수 정보 전달 및 일반 경로 일치 |
| BH 공격·상태 | 직접 호출이 확인된 공격 생성부 및 `Assets/WBHTest/Scripts/StatusEffect/**` | 산탄/다중 히트 단위, 연소 귀속, 냉각·빙결 재사용 |
| WJ 데이터 | `Assets/WJ_TestPlace/Script/Item/Data/DataLoad/UniqueEffectTableModel.cs`, `Editor/UniqueEffectTableSOImporter.cs`와 해당 Excel 변환기 | 대상 설정·저장 스택·새 타입의 명시 필드. 알 수 없는 타입/스탯은 조용한 성공으로 처리하지 않음 |
| WJ 버프·체력 | 필요한 `Assets/WJ_TestPlace/Script/Buff/**`, `Script/Player/**` 파일만 | 기존 버프 수명 및 보호막 피해 흡수 지점 |
| SW 표시 | `Assets/SW/Scripts/ItemTooltip/TooltipUI.cs`, 기존 Mirror 상태 스냅샷 | 툴팁/계수 일치, 준비·보호막 표시 데이터 |
| KY 표시 | 필요 시 `Assets/Scripts/UI/Popup/Buff/**`, 실제 HUD 스크립트 | 기존 UI에서 소유 플레이어 상태·남은 시간·보호막 표시 |

신규 코드 후보는 `Assets/SW/Scripts/Equipment/Effects/`의 연쇄/추가 피해 설정 SO 정도로 시작한다. 변하는 쿨다운·공격 기록은 기존 플레이어 발동 처리기에 둔다. 행동 구현이 커질 때만 해당 알고리즘을 작은 일반 C# 클래스로 분리한다. 새로운 공용 Manager, DI 컨테이너, 효과 그래프, 외부 패키지는 필요하지 않다.

타 담당자 스크립트의 실제 수정 시에는 저장소 규칙에 따라 정확한 파일·이유를 제시하고 `이 스크립트를 수정할까요?` 승인 후 진행한다. 이번 검토와 계획 작성에는 해당 수정이 없어 승인 단계가 필요하지 않다. Scene/Prefab은 구현 단계에서 연결된 Unity 인스턴스와 Dirty 상태를 확인한 뒤 필요한 대상만 변경한다.

## 7. 검증 계획

| 시험 | 반드시 확인할 결과 |
| --- | --- |
| 데이터 재생성 | 원본 표→JSON→SO→ItemDefinitionSO 참조 재현, 기존 GUID 보존, 라벨과 계수 일치, 오라 대상·저장 옵션 유지 |
| 실제 기본 공격 | 양산형 코어 발동, 라이트세이버 1→5스택, 회피 상태 진입 보상, 피격/치명타/처치 조건 분리 |
| 피해 단위 | 근접 다중 Collider, 산탄 여러 펠릿, 투사체 관통, 스킬 다중 히트 각각 의도 횟수. 추가 피해는 재귀 0건 |
| 순서와 사망 | 1차 사망·추가타 사망·연소 사망 각각 처치/보상 1회. OnDamaged 재진입으로 OnDead 중복 없음 |
| 장비 수명 | 장착/해제/빠른 교체/날아가는 탄/유물 버리기·재획득/사망·부활/씬 이동/새 런에서 규칙 유지 |
| 중복과 소유자 | 같은 유물 2개, 다른 효과의 같은 스탯, 2인 동일 무기에서 쿨다운과 스택 혼선 없음 |
| 체력 경계 | 29.9/30/30.1%, 79.9/80/80.1%, 최대 체력 장비 변경 직후 조건 재평가 |
| 상태이상 | Burn 재적중으로 tick이 계속 미뤄지는지, 적 풀 재사용 후 이전 효과 제거, Slow·오라 중복, 보스 저항 |
| 보호막·준비 | 피해 흡수 순서·상한·만료·동기화, 산탄 한 번에서 준비를 여러 번 소비하지 않음 |
| 협동 | Host+Client 이후 4인. 서버 피해·스택과 각 화면 일치, 공격자 접속 종료·대상 소멸에도 안전 |
| 체감·성능 | 같은 맵·적 수에서 전후 처치 시간/받은 피해/추가 피해 비중/프레임 시간/발동 수 측정. 타격마다 로그로 측정을 왜곡하지 않음 |

자동 검증은 효과 조건·공격 중복 제거·사망 보상 같은 실제 오류를 잡는 소수 회귀 검사로 시작한다. 단순 데이터 복사마다 별도 테스트 클래스를 만들지 않는다. 실제 플레이는 Mirror 테스트 맵의 플레이어와 적으로 먼저 검증한 뒤 정식 Act1 연결을 확인한다. 연결되지 않은 정식 맵을 완료로 표시하지 않는다.

성능 목표는 측정한 기준선을 보고 정한다. 연쇄의 대상 수 제한은 처음부터 둔다. 대규모 적 탐색 캐시·공간 분할 시스템은 실제 병목이 확인될 때만 추가한다. 파괴 연출을 변경하게 된다면 AGENTS의 보스 부위 분리·다수 적 동시 파괴 측정 규칙을 별도로 적용한다.

## 8. 이번 검토의 한계

PDF 전체 텍스트와 공통 구현 규칙 페이지의 렌더를 확인하고, 관련 C# 호출 경로 및 데이터 변환 구조를 정적으로 조사했다. Unity Editor의 현재 Scene/Prefab 배치, 컴파일·Console, 플레이, 네트워크 실행, 성능은 이번에 검증하지 않았다. 따라서 코드 존재를 런타임 정상 동작으로 표현하지 않는다.

기존 작업 변경은 보존했다. P1 구현과 검증 현황은 `UniqueEffect_P1_Handover.md` 및 김성우 개인 구현 로그에 기록했다. 다음 실제 구현은 사용자 승인 후 P2-A 아크 블레이드 연쇄 1종부터 진행한다.

2026-09-16에 P2-A 아크 블레이드 연쇄 1종의 1인 시범 구현, 책임 분리, P2-B 선행 전투 계약 보완과 자동 검증을 완료했다. 다음 단계는 사용자의 별도 진행 지시 뒤 `UniqueEffect_Custom_Implementation_Roadmap.md`의 P2-B 착수 게이트부터 진행한다.
