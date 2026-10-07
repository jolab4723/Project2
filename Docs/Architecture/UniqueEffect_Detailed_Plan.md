# 고유효과 상세 계획서

- 기준일: 2026-09-30 (과거 구현·검증 기록의 날짜와 범위는 보존)
- 진행 상태·전체 장비 목록: [고유효과 전체 계획서](UniqueEffect_Implementation_Plan.md)
- 기존 P0~P6-B, D1/Q1의 이력은 보존한다. **현재는 A1 싱글 회귀 / A2 싱글 기능·화면 / A3 폐열 코드·데이터·싱글 기능·수명·화면 통과, 최종 검증 대기**다. 신규 14종의 명세·진행 상태는 [최종 전환 계획](UniqueEffect_Final_Implementation_Plan_2026-09-30.md) §4·§12, 파일별 실행 절차와 남은 검증은 이 문서 §13을 따른다. A1~A3 멀티 검증은 사용자 지시에 따라 A3 이후 일괄 수행한다.
- 아래 `_MirrorTest` 시험·PASS 기록은 당시 근거다. 신규 구현을 그 경로에 다시 만들거나 과거 결과를 새 14종의 검증으로 대체하지 않는다. 미래 항목과 제안 수치는 구현 사실 또는 팀 확정 결정이 아니다.

## 1. 공통 실행 규칙 — P0/P1 완료

### 1.1 피해 종류와 발동 자격

| 구분 | 의미 | 장비 후속 공격 |
| --- | --- | --- |
| Direct | 직접 공격 피해 | 해당 장비가 요구하는 기본 공격·무기·표적 조건을 통과한 경우만 발동 |
| Skill | 스킬 피해 | 기본 공격 전용 효과를 발동하지 않음 |
| Effect | 장비가 만든 추가 피해 | 다른 공격형 장비 효과를 다시 발동하지 않음 |
| DoT | 상태이상의 시간별 피해 | 기본 공격·스킬 적중 이벤트를 다시 발동하지 않음 |

`DamageCause.Direct`만으로 Fighter 근접, Gunner 총탄, 치명타 여부를 판단하지 않는다. 기존 권한 공격 처리 구간과 실제 직접 대상 집합을 사용한다. 발사체 효과 자격은 발사 시 보존하지만, 현재 피해 수치는 **직접 피해 처리 진입 시점**의 Stat을 읽는다.

`AttackId`는 한 공격과 그 후속 피해의 연결 번호다. 같은 공격자·공격 번호·대상의 다중 Collider와 중복 요청을 구별한다. 현재 후속 피해 중복 키 `(AttackId, DamageCause, Target)`는 단일 장착 무기의 동기 효과 범위다. 같은 공격에서 여러 효과 생산자를 지원할 때에만 효과 ID 또는 실행 인스턴스까지 구분한다.

### 1.2 동기 큐와 스냅샷

직접 피해 처리 중 등록한 후속 피해를 기존 FIFO 큐에서 순서대로 해결한다. 공격력·치명·관통·속성 스냅샷은 직접 피해 처리 진입부터 그 큐가 끝날 때까지 유효하다. 발사 순간의 모든 능력치를 고정한다는 뜻이 아니며, 다음 프레임의 Burn 틱·지연 폭발·메아리에 그대로 재사용하지 않는다.

후속 피해는 허용된 직접 공격 처리 안에서만 등록한다. 등록된 후속타도 실행 시 대상 생존을 다시 확인하며, 죽은 대상의 피해·콜백은 건너뛴다. 효과가 처치하면 기존 사망·처치 귀속·보상 경로를 한 번만 사용한다. Effect/DoT의 공격 이벤트 차단과 `OnKill`의 전파 제한은 별개다. 향후 처치 전파에는 발동 출처·세대·상한을 명시해야 한다.

### 1.3 소유권·생명주기

런타임 상태는 플레이어와 아이템 인스턴스가 소유한다. 공유 SO에 플레이어별 쿨다운·대상·스택을 보관하지 않는다. Mirror 전투 판정은 서버가 확정하고 UI/VFX는 그 결과를 표시한다. 서버 제공자와 기존 전역 SO.OnEquip 경로를 겹쳐 실행하지 않는다.

장착 해제·유물 제거·제공자 비활성화·사망·씬 이동·새 런에서 구독·버프·지속 효과가 올바르게 정리되어야 한다. 표적이 풀로 돌아가면 이전 공격의 참조와 상태를 새 개체에 넘기지 않는다. 아이템 회전·이동 또는 실패한 거래의 롤백은 소유권을 잃은 것으로 간주하지 않는다.

### 1.4 주요 연결 파일

| 책임 | 진입점 |
| --- | --- |
| 권한 공격·후속 피해 | `Assets/SW/Scripts/Network/Player/PlayerCombatAuthority.cs`, `WBH_CombatResolver.cs` → `Assets/SW/Scripts/Player/PlayerDamageResolver.cs` |
| 싱글 기본 공격 | `Assets/WBHTest/Scripts/Player/T_PlayerCombat.cs` → `Assets/WBHTest/Scripts/Combat/WBH_CombatManager.cs` → 공통 Resolver |
| 적 상태·처치·표시 전송 | `Assets/SW/Scripts/Network/Combat/NetworkEnemyAuthority.cs` |
| 장비 발동 자격·Mirror 어댑터 | `Assets/SW/Scripts/Player/PlayerItemEffectState.cs`, `Assets/SW/Scripts/Network/Player/NetworkItemTriggerManager.cs` |
| 대표 무기 효과 데이터 | `Assets/SW/Scripts/Equipment/Effects/ChainLightningUniqueEffectSO.cs`, `InfernoExtraHitUniqueEffectSO.cs`, `GlassRailExtraHitUniqueEffectSO.cs` |
| 장착 장비·소지 유물 수명 | `Assets/SW/Scripts/Player/PlayerRelicEffectRuntime.cs`, `PlayerRelicEffectProvider.cs`, `Assets/SW/Scripts/Network/Player/NetworkPlayerRelicEffectProvider.cs` |
| 고유효과 표시·장판 | `Assets/SW/Scripts/Network/Player/UniqueEffectPresentation.cs`, `Assets/SW/Scripts/Player/PlayerGrenadeEffect.cs` |
| 원본 적 상태·피해·표시 | `Assets/WBHTest/Scripts/Enemy/**`, `Assets/WBHTest/Scripts/StatusEffect/**` |
| 실제 아이템·효과 원본 | `Assets/Resources/DataFiles/ItemData/**` |

팀원 파일을 고칠 때는 기존 담당 영역 규칙을 따른다. 역할 없이 매개변수만 중계하는 메서드나 한 줄에 여러 판정을 숨기는 코드를 늘리지 않는다.

### 1.5 고유효과 코드 작성 규칙

- 새 코드와 기존 코드의 변경 지점에는 `SW 수정`을 표시한다. 변경한 메서드의 XML `<summary>`에는 발동 조건, 처리 대상, 싱글/멀티 권한과 결과를 팀원이 바로 이해할 수 있게 설명한다.

```csharp
/// <summary>
/// SW 수정: 지정된 플레이어의 고유효과를 실행합니다.
/// 싱글에서는 기존 전투 흐름을 유지하며, 멀티에서는 서버가 결과를 확정합니다.
/// </summary>
```

- 공개 API는 플레이어 참조 연결, 여러 효과가 함께 쓰는 공통 규칙 실행, StageSelect 외부 제어에 실제로 필요한 범위만 추가한다. 한 효과의 내부 상태나 검증 편의를 공개 API로 노출하지 않는다.
- 기존 요청·결과 타입과 이벤트를 먼저 사용한다. 효과 하나만을 위한 인터페이스·Factory·Manager·전달 전용 메서드는 추가하지 않는다.
- 메서드는 역할이 드러나는 이름을 사용한다. 변수명과 용어는 같은 파일과 인접 시스템의 기존 어휘를 따르며, 새 효과만 다른 명명 체계를 만들지 않는다.
- 기존 직렬화 필드명과 공개 호출 계약은 가능한 유지한다. 변경이 불가피하면 Scene·Prefab·SO 참조와 기존 호출부 영향을 먼저 확인한다.
- 구현 완료 전 Diff에서 `SW 수정`, 변경 메서드의 XML `<summary>`, 공개 API 증가, 새 추상화, 명명·직렬화 호환성을 함께 확인한다.
- 기존 팀원 주석은 변경한 동작과 직접 충돌하지 않는 한 삭제하지 않는다. 이동·공통화한 코드의 설명과 작성 의도도 적합한 위치에 보존한다.
- 사용자 지시에 따라 **`$ponytail full`과 코드·주석 지침을 이후에도 별도 명시 없이 준수**한다. 실제 호출 흐름을 조사하고 기존 코드/API → Unity 기본 기능 → 설치 패키지 → 최소 신규 코드 순으로 선택한다. 효과별 차이는 필요한 지점에만 추가하고 범용 Manager·Factory·스케줄러를 선행 제작하지 않는다. 보안·입력 검증·생명주기·오류 처리·필요한 검증을 간결화 대상으로 삼지 않는다.
- 다른 담당 스크립트는 수정할 파일과 이유를 설명한 뒤 **`이 스크립트를 수정할까요?`**에 대한 승인을 받는다. 이 규약과 Ponytail은 단계 문서를 정리하는 과정에서도 삭제하지 않는다.

### 1.6 무기 고유효과 등급 기준

- 유일 무기는 밤의 칼날을 기준점으로 삼는다. 자신의 스탯, 조건부 강화 수치, 다음 공격 계수처럼 수치 변화가 중심인 효과를 우선한다.
- 장판 생성, 연쇄 타격, 실제 관통, 이전 공격 재생, 상태 전파, 아군이 소비하는 표식처럼 전장이나 공격 규칙을 크게 바꾸는 효과는 전설 무기에만 배치한다.
- 현재 구현·계획을 이 기준에 맞춰 아크 블레이드, 수호자의 정의, 폐열 절단 대검, 와일드파이어, 초냉매, 코어 브레이커, 중력 우물, 반물질 랜스, 스마일 시그널, 에코 챔버를 전설로 승격했다. 밤의 칼날은 유일로 유지하며, 특이점 박격포처럼 이미 전설인 무기는 유지한다.
- 유물은 이번 등급 조정 범위에서 제외한다. 이후 유물 등급 정책은 별도로 확정하기 전까지 현재 데이터를 유지한다.

## 2. 아크 블레이드 — P2-A 완료

- 실제 아이템: 전설 `item.weapon.greatsword.arcblade`; 효과: `UE_ArcBladeChainLightning`.
- 유효한 기본 직접 공격이 적중하면 공격력의 25%로 첫 연쇄 피해를 준다. 다음 연쇄는 직전의 80%이므로 25%→20%→16%다.
- 연쇄 반경 4m, 최대 3대, 재발동 대기 0.8초. 살아 있는 적만 찾고 벽에 막힌 대상, 이미 방문한 대상, 해당 직접 공격의 대상 집합을 제외한다.
- 후속 피해는 같은 AttackId의 비치명 Effect다. 다중 Collider, 중복 적중, 대상 사망으로 연쇄가 복제되지 않는다.
- 탐색·벽 검사·시각 결과는 실제 성공한 연쇄 경로를 공유한다. 피해와 무관한 가상의 연쇄 VFX를 추가하지 않는다.
- 크루세이더 등에 재사용할 때에도 치명 조건·공격 종류·쿨다운은 별도로 확정한다.

검사: `ArcBladeChainLightningValidation_MirrorTest.Validate()` 39개. 실제 Fighter Host의 AnimationEvent 직접 공격→연쇄도 확인했다. 원격 Player 4개의 공격·관찰은 Q1(§10.3)에서 통과했고, 최종 Act1 성능은 남았다.

## 3. 인페르노 — P2-B 완료

- 실제 아이템: `item.weapon.axe.inferno`; 효과: `UE_InfernoExtraHit`.
- 실제 Fighter 근접 기본 직접 공격이 살아 있는 대상에 적중한 뒤, 같은 대상에 공격력 20%의 비치명 Fire Effect를 1회 준다.
- 해당 직접 피해 진입 스냅샷을 쓴다. 같은 적의 다중 Collider는 Direct 1회와 Effect 1회로 끝난다.
- 스킬·총기·산탄·Effect·DoT·권한 공격 밖에서 임의로 발생한 Direct에는 발동하지 않는다.
- 직접타 처치에는 후속타가 없다. 효과타 처치에는 보상이 한 번만 발생한다.
- Fire 직접타와 Fire Effect가 기존 Burn1을 적용할 수 있다. 같은 대상의 Burn은 두 개가 쌓이지 않고 한 상태를 갱신한다. 초창기 문서의 “인페르노에는 Burn이 없다”는 중간 범위는 폐기했다.
- 숫자 분리와 전용 피격 표현은 기존 풀·VFX를 재사용한다. Burn 피해는 인페르노 추가타 계수와 별도다.

검사: `InfernoExtraHitValidation_MirrorTest.Validate()` 최신 30개. 검증 대상을 먼 격리 위치에 만들고 다중 Collider 전제도 검사한다. 실제 Fighter Host 직접타→추가타와 Burn 처치·보상 1회를 확인했다.

## 4. 유리빛 궤도 — P3-A 완료

- 실제 아이템: `item.weapon.rifle.glassrail`; 효과: `UE_GlassRailExtraHit`.
- 효과를 가진 무기로 발사한 탄이 유효한 기본 공격으로 적중하면 같은 대상에 공격력 15% 비치명 Ice Effect를 1회 준다.
- 발사 때의 효과 SO 자격을 투사체에 보존한다. 발사 후 무기 교체·해제·재장착에도 이미 발사한 탄의 자격은 유지한다.
- 일반 탄을 발사한 뒤 유리빛 궤도를 장착해도 이전 탄에 효과를 소급 부여하지 않는다. 장착 세대 불일치로 이미 발사한 효과 탄을 취소한다는 초기안은 폐기했다.
- 피해 수치는 현재 공통 규칙대로 명중 시 직접 피해 진입 스냅샷을 사용한다.
- Ice 추가타가 Freeze를 한 번 더 등록하지 않는다. 직접 속성 공격의 기존 Freeze 1회와 구분한다.
- 산탄 전체를 한 적중으로 뭉개거나, 타깃이 아닌 다른 적에게 Effect가 옮겨가게 하지 않는다.

검사: `GlassRailExtraHitValidation_MirrorTest.Validate()` 최신 40개. 실제 Gunner Host의 공격 요청→AnimationEvent→투사체 Network Spawn→충돌→직접·Effect 피해를 확인했다. 단순 resolver 호출만을 실제 입력 검증으로 기록하지 않는다.

## 5. 방어구·소지 유물 — P3-B 완료

### 5.1 H3 절전모드 헤드셋

실제 `item.armor.helmet.powersavingheadset`에 `UE_LowManaRecovery`를 연결한다. 마나가 최대의 25% 이하이고 살아 있을 때 마나 재생 +30%를 유지하며, 경계를 넘거나 해제·사망하면 제거한다. `militaryneuralstimulator`는 이 효과의 대상이 아니다.

현재 단독 검증 기준은 기본 재생 5→6.5다. Fighter 최대 마나 160에서 40→47, Gunner 215에서 53.75→60.75로 자연 회복한 뒤 버프가 해제됨을 확인했다. 0/20/24.9/25/25.1% 경계를 검사했다. 이 수치는 R2 오라를 끈 단독 시험값이다. 25% 이하 유지 조건을 “25%를 아래로 통과한 순간” 조건으로 바꾸지 않는다.

### 5.2 B1 폐기된 부스터 모듈

실제 `item.armor.boots.discardedboostermodule`에 `UE_DodgeMoveBoost`를 연결한다. 회피 상태 진입 시 기존 OnDodge 사건으로 이동속도 +20%를 2초 적용하고, 재발동 대기 6초를 둔다. 적 공격을 실제 피했다는 별도 성공 판정은 요구하지 않는다.

회피 요청만 보낸 경우와 실제 회피 상태에 들어간 경우를 구분한다. 현재 Host 시험은 실제 상태머신 진입·버프·만료·쿨다운을 확인했으며 키보드 조작의 회피 동작 전 구간을 새로 검수한 것은 아니다.

### 5.3 R2 마나 중계기

실제 `item.relic.manarelay`에 `UE_ManaRelay`를 연결한다. Rare, 가격 2500, 2×2 가방 칸을 사용한다. 소지 중 반경 6m의 자신과 아군에게 마나 재생 +15%를 준다. 초기 후보 문서의 10m는 현행 수치가 아니다.

- `PlayerRelicEffectProvider_MirrorTest`가 가방의 최종 소유 상태를 LateUpdate에 반영한다. 이동·회전·실패 롤백 중 잠깐 사라지는 사건 때문에 실행기를 재생성하지 않는다.
- `BuffFieldZone_MirrorTest`는 효과 출처와 플레이어 Collider를 구별한다. 같은 플레이어 Collider가 두 개여도 버프는 하나이며, 하나가 나갔다고 남은 Collider의 버프를 지우지 않는다.
- 같은 효과 유물 여러 개는 비중첩이다. 한 개를 버려도 나머지가 있으면 유지하고 마지막 소유가 끝날 때 제거한다.
- 죽은 플레이어에게는 버프를 유지하지 않는다. 범위 안 사망·부활도 갱신하며, 장판/제공자 해제는 즉시 버프를 정리한다.
- 소지형 유물은 Equipment 슬롯에 억지로 넣지 않는다.

아이콘은 `Assets/Resources/Images/Item/OriginalImage/item.relic.manarelay.source.png` 원본과 상위 폴더 `item.relic.manarelay.png`를 사용한다. 최종 256×256, Sprite/Single, 칸당 128px·8px 여백·왼쪽 10° 규격이며 아이템·효과 SO의 아이콘 참조를 연결했다. 기존 변환기의 전체 표시 구도를 사용한다.

### 5.4 실제 슬롯과 빌드

Helmet→Helmet, Armor→Chest, Boots→Boots의 실제 슬롯 매핑을 따른다. 빌드는 각각 아크 블레이드 또는 인페르노 또는 유리빛 궤도 하나에 H3/B1/R2를 조합한다. 실제 무기 3개의 별도 Host 빌드를 확인했다.

H3 합성 Assets & Formula 20개, B1/R2 Host 18개는 서로 다른 검사다. 별도의 서버 생성 아군으로 3m 진입·9m 이탈·재진입, 두 Collider, HP 0·회복, 가방 회전 후 실행기 인스턴스 보존도 확인했다. 같은 Host의 아군 객체를 원격 클라이언트 검증으로 세지 않는다.

## 6. 상태이상·화상 — P4 완료

### 6.1 공통 소유자와 적용 조건

`WBH_StatusEffectData`에 Attacker와 AttackId를 보존하고 공통 피해 resolver가 전달한다. 기존 상태 컨트롤러가 등록·재적용·틱·만료·해제를 소유한다.

`WBH_EnemyController.AddStatusEffect`는 비활성 오브젝트·죽은 적·비활성 상태 컨트롤러를 거절한다. 기존의 잘못된 `isActiveAndEnabled` 거절 조건을 수정해 활성 일반 적과 AI 컴포넌트만 끈 Mirror 적이 모두 상태이상을 받을 수 있다. NetworkIdentity가 있는 적은 서버만 상태와 DoT를 변경한다.

`ResetForPool`, 상태 컨트롤러 비활성화, 재초기화에서 이전 상태·이동 제약·VFX를 정리한다. 사망 뒤 시간 경과로 틱이나 보상이 다시 발생하지 않는다.

### 6.2 Burn 규칙

| 항목 | 현행 규칙 |
| --- | --- |
| 기본 Burn1 | 대상 최대 HP의 1%, 간격 1초, 지속 5초 |
| 재적용 | 상태는 하나. 마지막 강도·출처·지속 시간을 사용하고 진행 중인 틱 시계는 유지 |
| 긴 프레임 | 유효 지속 시간 안에서 지나간 틱을 처리 |
| 만료 | 남은 지속 시간으로 경과량을 제한해 만료 후 추가 틱 금지 |
| 입력 검사 | 유한한 값과 양의 지속 시간, 틱 간격 최소 0.01초 |
| 피해 메타데이터 | Fire, DoT, 비치명, 원래 Attacker/AttackId |
| 재귀 | 기본 공격·스킬·Fire DamageDealt 장비 이벤트 재발동 금지 |
| 처치 | 남아 있는 유효 출처에 귀속, 사망·보상 한 번 |
| 출처 소멸 | 이전 직접 공격자에게 DoT 처치를 임의로 넘기지 않음 |

HP 비례 피해를 공격력 비례로 바꾸지 않았다. 작은 간격 검사와 만료 제한은 틱 누락·무한 반복 방지용이다. 새 상태별 밸런스는 별도 후속 작업이다.

### 6.3 보스 저항·면역

`WBH_EnemyStatusEffectController.bossBurnDamageMultiplier`는 0~1이고 기본값은 1이다. 일반 적은 기존 피해를 받고 보스만 이 배율을 적용한다. 실제 보스 기본 자산의 배율을 임의로 낮추지 않았다.

- 0 < 배율 < 1: Burn은 등록하고 틱 피해만 줄이며 “화상 저항”을 표시한다.
- 배율 = 0: Burn을 등록하지 않고 “화상 면역”을 표시한다.
- 배율 = 1: 별도 문구를 띄우지 않는다.
- 저항은 화상 틱에만 적용한다. 직접 Fire/장비 Fire 추가타까지 줄이지 않는다.
- 잘못된 요청·죽은 적·클라이언트의 비권한 요청에는 문구를 만들지 않는다.

유효한 시도의 상태 반응 사건은 `OnBurnResponse(bool immune)` 하나다. 적별 최근 64개 `(공격자, AttackId)`를 보관해 같은 공격의 직접타·추가타 문구를 합친다. 다른 공격자의 같은 번호와 늦게 온 다른 번호는 구별한다. 번호가 0인 기존 호출은 같은 프레임에서만 합친다. 풀 비활성화·재초기화 시 기록을 비운다. 64개보다 오래된 공격을 무기한 기억하는 보장은 없으며, 장기 지연 효과가 추가될 때 그 수명과 함께 재검토한다.

### 6.4 일반 플레이·Mirror 표시 경로

일반 적의 `WBH_EnemyView`는 상태 반응 사건을 구독하고 기존 `WBH_FloatTextPoolManager`의 `WBH_DamageText`를 빌린다. Mirror에서는 원본 View를 끄고 서버 Authority가 ClientRpc를 보내 `NetworkEnemyCombatView_MirrorTest`가 표시한다. Host가 두 경로를 동시에 표시하지 않는다.

`WBH_DamageText.ShowBurnResponse`는 기존 위치·색·애니메이션·반납 흐름을 사용한다. 숫자는 기존 Oxanium 글꼴·머터리얼을 보존하고, 상태 문구는 기존 NanumHumanBold SDF를 사용한다. 공유 `Assets/WBHTest/Prefabs/Etc/DamageText.prefab`에는 statusFont 참조 하나를 추가했다. 프로젝트 전역 글꼴이나 별도 텍스트 풀은 만들지 않았다.

SW 테스트 일반 근접·일반 원거리·보스 프리팹은 원본 초기화가 요구하는 `WBH_EnemyEffect`를 갖추었다. 기존 GUID와 네트워크 assetId는 보존했다.

### 6.5 아직 남은 상태이상 확장

초냉매의 축적 빙결, 와일드파이어의 화상 처치 전파, 코어 브레이커의 방어 약화, 장판 출처 둔화 구별은 P4 Burn 완료와 별개다. 다음 상태 하나가 필요할 때 중첩 상한·갱신·면역·출처·해제·보스 규칙을 먼저 정한다. 현 `OnKill`만 연결해 무제한 전파시키거나 범용 상태 시스템을 통째로 교체하지 않는다.

## 7. 보호막과 다음 공격 준비 — 기존 P5-A/B 완료 이력

### 7.1 P5-A 첫 보호막

대표 후보는 실제 태양의 은혜 또는 C2 태양광 축전 외피 중 하나다. 후보 수치인 **HP 80% 이상, 8초 무피격 유지, 최대 HP 15% 보호막**은 밸런스 제안이며 확정값이 아니다.

2026-09-21 1차 구현은 기존 `태양의 은혜`를 사용한다. HP 80% 이상에서 8초간 유효 피해를 받지 않으면 최대 HP의 15% 보호막을 얻고 12초 유지한다. 보호막만 맞아도 무피격 시간을 다시 세며, 8초 무피격을 다시 충족하면 남은 보호막을 상한까지 갱신한다. 만료 뒤에는 8초를 새로 기다린다. 동일 효과는 중첩하지 않고 해제·사망 시 제거하며, 부활 뒤에는 처음부터 충전한다. 이 수치는 플레이 밸런스 검수 전의 시안이다. 서버의 `PlayerHealthManager.TakeDamage`에서 보호막을 먼저 흡수하고 실제 HP 손실을 별도 사건으로 구분한다. `PlayerArmorEffectProvider_MirrorTest`의 보호막 양을 Mirror로 복제하고 클라이언트에는 캐릭터를 감싸는 반투명 구체를 표시한다. 실제 Fighter 서버 플레이어의 장착·피격·초과 피해·HP 기준·만료·해제·사망과 일반 적 프리팹의 보호막 피격은 통과했다. 동일한 새 Windows Development 빌드로 전용 서버 1개와 별도 Player 2개를 KCP/127.0.0.1에 연결해 두 클라이언트가 자신과 상대의 생성·12초 만료·약 8초 뒤 재생성을 모두 동일하게 보는 것도 통과했다. 여러 PC와 지연·손실 환경은 아직 검증하지 않았다.

착수 전에 실제 Health의 피해 적용 지점에서 다음을 정한다.

1. 최종 피해 계산, 보호막 흡수, 실제 HP 감소의 순서.
2. 보호막 양·상한·유지 시간·재충전 조건. 같은 효과는 비중첩을 기본으로 한다.
3. 갱신과 누적의 차이, 장착 해제·죽음·새 런 시 정리.
4. “무피격”을 유효 공격 수신으로 볼지 실제 HP 감소로 볼지. 보호막만 맞은 상황을 명시한다.
5. 서버 상태 원본과 로컬/원격 UI 표시.

실제 HP 손실과 계산상 피해를 구분해야 C1 반격·C3 환류·C4 긴급 완화가 정확해진다. 방어력 증가로 보호막을 대신했다고 완료하지 않는다. 반격은 방어자의 피해 사건에서 시작하므로 현재 공격자의 동기 큐에 무리하게 끼우지 않는다.

검증은 실제 피격→흡수→HP 변화, 과잉 피해·동시 공격·보호막만 피해·만료·재충전·사망·해제·원격 표시 순서로 한다.

당시 Mirror 자동 검증은 `MirrorSmokeConfiguration_MirrorTest.cs`를 사용했다. **현재 신규 검증은 Assets 밖 Editor `run_script`와 개발 Player Pipeline 명령으로 수행한다.** 옛 시험 진입점·임시 네트워크 메시지·런타임 자동 실행 코드를 다시 넣지 않는다.

### 7.2 P5-B 다음 유효 기본 공격

2026-09-21 1차 구현은 `밤의 칼날`과 `UE_NightSwordDodgeStrike`를 사용한다. 서버가 생존·조작 가능·회피 대기 시간을 통과한 회피를 확정하면 준비 상태가 생기고, 다음 유효 Fighter 기본 직접 공격의 최종 피해 배율에 1.4를 곱한다. 준비 상태는 중첩하지 않는다.

- 빗나감, 스킬, Effect, DoT는 준비 상태를 소비하지 않는다.
- 한 기본 공격이 여러 Collider나 여러 적을 맞혀도 같은 AttackId 전체에 1.4배를 적용하고 소비 횟수는 한 번이다. 다음 AttackId부터는 일반 피해다.
- 무기 해제·교체, 사망, 공격 번호 수명 초기화에서 준비 상태를 지운다.
- 클라이언트에는 준비 중임을 알리는 보라색 원형 표시를 복제한다.
- 수치 40%는 실제 전투 난이도 검수 전의 밸런스 시안이다.

Excel→JSON→SO 재생성에서 `DodgePreparedAttackUniqueEffectSO`와 40%→1.4 변환을 지원한다. 재생성 후 밤의 칼날 아이콘과 GUID를 유지했고 Unity 컴파일 및 Console 오류 0건을 확인했다. 전용 자동 검증기(`NightSwordPreparedAttackValidation_MirrorTest`)를 통해 회피 준비, 40% 증폭, 단일 소비, 다중 피격 단일 소비, 스킬/DoT 미소비, 빗나감 보존, 무기 해제/교체/사망 초기화, 클라이언트 보라색 링 시각화 등 33개 검사 전원 PASS를 완료했다. 또한 Windows 전용 서버(`Builds/MirrorDedicatedServer`) 및 LAN 클라이언트(`Builds/MirrorLanTest`) 실기 빌드에서 UDP 7777 리슨, 2인 동시 접속, Fighter 선택 및 로비 세션 권한 부여 수명주기를 정상 검증했다.

빗나감, 무적 표적, 피해 0, 다중 Collider, 여러 대상, 산탄 펠릿에 대해 소비 시점을 먼저 정한다. 무기 교체·해제·사망·새 런의 준비 유지 여부도 명시한다. 공격 버튼 입력만으로 소비하거나 동일 공격의 두 번째 Collider에서 다시 소비하지 않는다.

H1/H4의 서로 다른 공격·대상, H2의 특정 스킬 남은 쿨다운 환급은 이 경계를 재사용하되 각각 별도 조건이다. 즉시 쿨다운 환급을 지속 CDR 스탯으로 대체하지 않는다. 마나 회복은 최대치와 실제 회복량을 구분해 회복 사건이 스스로 반복되지 않게 한다. C4 하향 통과는 H3의 경계 이하 유지와 다른 사건이다.

## 8. 장판·지연·관통·메아리 — P6-A~B 완료 / P6-C 계획

### 8.1 P6-A 중력 우물 고정 둔화 장판

2026-09-22 1차 구현은 전설 `item.weapon.grenadelauncher.gravitywell`과 `UE_GravityWellField`를 사용한다. 서버가 유탄 충돌과 직접 피해를 확정한 뒤 같은 네트워크 투사체를 충돌 위치의 고정 장판으로 전환한다. 별도 Manager와 네트워크 프리팹은 추가하지 않았다.

- 반경 3.5m, 지속 5초, 이동속도 45% 감소(`slowMultiplier=0.55`), 0.5초마다 짧은 Slow를 갱신한다. 범위를 벗어나거나 장판이 끝나면 마지막 갱신 뒤 0.1초 이내에 원래 속도로 돌아온다.
- 발사 시 공격자·아이템·효과·AttackId를 보존한다. 발사 후 무기 교체·해제에는 유지하고, 소유자 사망·씬 전환·5초 만료에는 네트워크 객체를 제거한다.
- 한 적의 여러 Collider는 장판 갱신마다 한 번만 처리한다. 같은 소유자는 최대 3개를 유지하며 초과 시 가장 오래된 장판부터 제거한다.
- 서버가 위치·반경·색·활성 상태를 복제하고 각 클라이언트가 `AreaRingVisual`로 바닥 링을 만든다. 늦게 Spawn 상태를 받은 관찰자도 현재 활성 장판을 표시한다.
- Excel `coefficients=3.5;5;45;0.5;3`을 반경·지속 초·둔화율%·갱신 초·동시 장판 수로 검사하고 JSON/SO에 재생성한다.

Editor Host 실제 네트워크 프리팹 검증에서 유탄 직격 피해, 장판 1개 생성, 적 이동속도 `10→5.5`, Slow 10회 갱신, Host 링 1회 관찰, 무기 해제 후 위치·효과 유지, 5초 만료와 이동속도 `5.5→10` 복귀를 확인했다. 검증 로직은 `MirrorSmokeConfiguration_MirrorTest.cs`에만 일시적으로 두고 완료 후 제거했다. 빌드는 이번 요청에 따라 실행하지 않았다.

실제 끌어당김, 서로 다른 플레이어의 장판 중첩 규칙, 별도 Player·지연/손실 네트워크와 4인 성능 수치는 P6-A 완료 범위에 포함하지 않는다.

### 8.2 P6-B 특이점 박격포 지연 폭발

2026-09-22 1차 구현은 `item.weapon.grenadelauncher.singularitymortar`와 `UE_SingularityDelayedExplosion`을 사용한다. 기존 유탄의 충돌 직접 피해를 처리한 뒤 같은 네트워크 투사체를 충돌 위치의 고정 예약체로 전환한다. 별도 Manager·Factory·네트워크 프리팹은 추가하지 않았다.

- 지연 2초, 반경 4m, 기폭 시 현재 서버 Stat을 읽는 공격력 100%의 `DamageCause.Effect` 피해다. 직접 공격 이벤트를 다시 발동하지 않으며 한 적의 여러 Collider는 한 번만 처리한다.
- 발사 시 공격자·아이템·효과·원본 AttackId를 보존하고 지연 피해에는 원본에서 파생한 별도 Effect AttackId를 사용한다. 발사 후 무기 교체·해제에는 유지하고 소유자 사망·씬 전환에는 취소한다.
- 폭발은 표적 추적이 아니라 충돌 위치 고정이다. 원래 표적이 죽거나 사라져도 예약은 남고, 기폭 순간 범위 안의 살아 있는 적만 판정한 뒤 정리한다.
- 같은 소유자는 최대 3개를 예약하며 초과 시 가장 오래된 예약부터 취소한다. 서버가 위치·반경·색·기폭 시각·활성 상태를 복제하고 클라이언트는 `AreaRingVisual` 경고 링을 표시한다.
- Excel `coefficients=2;4;100;3`을 지연 초·반경·피해%·동시 예약 수로 검사해 JSON/SO에 재생성한다.

현재 열려 있던 Mirror 로비 씬의 Editor Host에서 실제 네트워크 프리팹으로 충돌 직접 피해 1회, 경고 링, 1초 시점 미기폭, 무기 해제 후 고정 유지, 2초 뒤 Effect 피해 1회, 다중 Collider 중복 제거, 대상 소멸 뒤 안전 정리, 소유자별 3개 상한과 가장 오래된 예약 취소, 소유자 사망 시 남은 예약 취소를 포함한 8개 조건을 통과했다. 검증 로직은 `MirrorSmokeConfiguration_MirrorTest.cs`에만 일시적으로 두고 완료 후 제거했다. 빌드는 이번 요청에 따라 실행하지 않았다.

여러 PC LAN/WAN·지연/손실 환경, 별도 Player 관찰자, 4인 동시 폭발 성능과 최종 밸런스는 P6-B 완료 범위에 포함하지 않는다.

### 8.3 구현 순서

기존 고정 장판·지연 폭발 구현을 재사용하고, 현재는 **A1 처치 파동 → A2 근거리 폭발 → A3 축열**부터 진행한다. B/C는 최종 전환 계획 §12 순서를 따른다. 중력 우물의 실제 끌어당김은 별도 힘·위치 권한 검증이 필요한 범위 밖 작업이다. 단순 Slow를 끌어당김 완료로 표시하지 않는다.

일식 기관의 지속 화염은 P4 Burn과 장판의 접촉/틱 정책을 조합한다. 와일드파이어는 자신이 적용한 화상의 처치만 대상으로 전파 세대와 수를 제한한다. 에코 챔버는 이전 공격의 위치·방향·종류를 기록해야 하며, 현재 캐릭터 위치로 새 공격을 내는 것으로 대체하지 않는다.

### 8.4 첫 비동기 효과에 필요한 최소 계약

| 내용 | 정할 사항 |
| --- | --- |
| 출처 | 공격자·아이템 인스턴스·효과 종류·공격 번호 |
| 시점 | 서버 기준 시작/종료 시간, 지연 실행 시 능력치를 언제 읽을지 |
| 공간 | 충돌 위치·방향 고정 또는 대상 추적 여부 |
| 취소 | 무기 교체·해제·사망·씬 전환·새 런·출처 소멸 정책 |
| 풀 재사용 | 같은 GameObject가 새 적으로 재사용돼도 옛 예약이 맞지 않도록 생애 구분 |
| 상한 | 동시 장판·예약·재생·세대 수, 유효하지 않은 작업 조기 정리 |
| 관찰자 | 일회성 사건 RPC와 늦게 들어온 관찰자에게 필요한 지속 상태 구별 |

기존 동기 FIFO는 다음 프레임 예약기가 아니다. 첫 지연 효과가 요구할 때에만 소유자와 수명이 명확한 작은 실행기를 추가한다. 지연 효과마다 전역 Manager를 만들지 않는다.

### 8.5 개별 효과의 핵심 검증

- 장판: 경계 진입/이탈·중복 Collider·죽음/부활·소유자 제거·관찰자 재진입.
- 지연 폭발: 표적 사망/소멸·풀 재사용·예약 취소·최대 동시 수·같은 타깃 중복.
- 관통: 충돌 순서·최대 관통 수·벽·같은 적 여러 Collider·탄 소멸.
- 근거리 폭발: Gunner 원거리 공격 분류를 유지하고 거리 조건만 추가.
- 메아리: 원래 위치·방향·공격 한 번, 재귀 메아리 금지, 소유자 생명주기.
- 아군 소비 표식: 누가 만들고 누가 소비하는지 서버에서 한 번 확정. 오라 적용과 소비권은 별개다.

## 9. 데이터·아이콘·툴팁·기존 효과 — D1 이력 / 신규 전환 검수

**D1 완료(2026-09-20), P6-B까지 확장(2026-09-22).** WJ 임포터 승인 범위에서 완료 효과를 효과 표 60개 데이터 행에 편입했다. 기존 SO를 같은 경로에서 갱신하여 GUID와 아이콘을 유지한다. JSON 변환 실패는 이전 JSON이 있어도 성공으로 반환하지 않으며, SO를 쓰기 전에 모든 행의 ID·타입과 기존 SO 타입을 검사한다.

| 타입 | 표와 실제 값의 대응 |
| --- | --- |
| ChainLightningUniqueEffectSO | `coefficients=3;4;25;80;0.8`: 대상 수, 반경, 첫 피해%, 이후 피해%, 쿨다운. `cooldownSeconds=0.8`과 마지막 계수가 일치해야 함 |
| InfernoExtraHitUniqueEffectSO | `coefficients=20`을 `damageMultiplier=0.2`로 변환 |
| GlassRailExtraHitUniqueEffectSO | `coefficients=15`를 `damageMultiplier=0.15`로 변환 |
| SingularityDelayedExplosionUniqueEffectSO | `coefficients=2;4;100;3`: 지연 초, 반경, 피해%, 동시 예약 수. 피해%는 `damageMultiplier=1.0`으로 변환 |

위 필드는 이제 표가 원본이다. SO에서 해당 값만 수동 변경하면 다음 가져오기에서 표 값으로 돌아온다. 아이콘, GUID와 표에 없는 다른 타입의 수동 필드는 기존 정책을 유지한다. 계수의 누락·NaN/Infinity·잘못된 범위와 툴팁/쿨다운 불일치는 가져오기 전에 거절한다.

효과 통합 실행 후 아이템의 고유효과 연결 단계를 다시 실행하여 219개 ItemDefinitionSO/UniqueEffectSO의 GUID와 전체 직렬화 값을 시작 기준과 비교했고 모두 동일했다. 알 수 없는 타입, 잘못된 계수, 쿨다운 불일치, 기존 SO와 다른 타입, 잘못된 Excel이 기존 JSON을 덮어쓰는 경우의 5개 실패 검사를 통과했다. Excel 기존 셀·서식·관련 없는 ZIP 구성도 보존했다.

| 기존 검토 항목 | 확인 결과와 처리 |
| --- | --- |
| AdvancedCore | 당시 P0는 HELLO WORLD 시험값을 사용했으나 현재는 최대 HP +20 Passive다. 옛 시험값으로 복원하지 않음 |
| CyberneticCore | 실제 `item.relic.cybernetic_core`와 P0 검사가 회피 이동속도 Flat +77/4초/30초 대기를 사용. 밸런스 결정 없이 변경하지 않음 |
| 미참조 4종 | TestHelmet/TestGreatSword/SturdyArmor/HasteAura는 9월 22일 당시의 미참조 목록이다. 이후 정리 이력과 현재 표를 대조하고 신규 전환의 삭제 목록으로 재사용하지 않음 |
| LuckyCharm | 당시 전역 드롭 배율·Mirror 미지원 이력과 현재 `PlayerRelicEffectRuntime`의 소유자별 처리를 구분한다. 새 전환의 회귀 항목이며 이번 조사만으로 실제 드롭 시험 PASS를 선언하지 않음 |

- GravityFieldCore의 targetEnemies 및 persistStack 관련 열은 현재 존재한다. “열이 없다”는 과거 지적은 다시 할 일이 아니다.
- CyberneticCore 수치의 제품 의도와 LuckyCharm의 실제 플레이 회귀는 신규 14종 기믹 구현과 구분해 다룬다.
- 과거의 56개라는 연결 수를 현행 총수로 사용하지 않는다. 현재 데이터 기준은 §13.1이며 연결 개수와 실제 장착/발동/다인 완료는 별개다.

새 아이템은 itemId·캐릭터·장비 종류·가방 크기를 먼저 확정한다. 기존 아이콘을 우선 쓰고, 없으면 기존 규격의 원본 이미지를 만든 뒤 변환한다. 출력은 `Assets/Resources/Images/Item/{itemId}.png`, 원본은 `OriginalImage`다. 칸당 128px, 8px 여백, 왼쪽 10°를 유지한다. SO의 아이콘 참조·Sprite/Single·슬롯 실제 화면을 확인한다. 원본과 기존 .meta를 보존한다.

툴팁에는 발동 조건, 대상, 실제 수치, 지속·대기 시간, 중첩 여부와 필요한 비용을 적는다. 내부 클래스명이나 개발용 동기화 용어는 플레이어 설명에 넣지 않는다. VFX/음향은 실제 성공 결과를 표시하고 화면의 핵심 정보와 피해 숫자를 가리지 않는다.

## 10. 검증 결과와 재실행 기준

### 10.1 이번 완료에 사용한 증거

| 검사 | 결과와 해석 |
| --- | --- |
| P1 Foundation | 기존 76개 통과. 과거 다인 검사 기록을 이번 신규 P4의 원격 검사로 재사용하지 않음 |
| 아크/인페르노/유리빛 궤도 | 최신 39/30/40개 및 별도 실제 Host 공격 흐름 통과 |
| H3 | 합성 20개 외 실제 Fighter/Gunner의 마나 경계·자연 회복, Fighter 장착 UI 조작 확인 |
| B1/R2 | Host 18개 및 별도 아군·다중 Collider·생존·가방 회전 검사 통과 |
| 실제 단일 무기 빌드 | Fighter 인페르노, Fighter 아크, Gunner 유리빛 궤도 각각 H3/B1/R2와 함께 검사 |
| Burn 처치 | 두 클래스에서 실제 Update DoT 1회 처치, 원래 출처·공격 번호, 사망·보상 1회 확인 |
| P4 규칙 | 25개 통과. 실제 프리팹의 틱·갱신·만료·일반/Mirror 수신·풀 정리·보스 배율·문구 중복·한글 글꼴 검사 |
| P4 보스 Host | 실제 보스 프리팹에 12개 검사 통과. 0.25 배율 피해, 0 면역, RPC 문구 1회, 무효 요청 차단, 풀 반환 해제 |
| P4 표시 | MCP 화면에서 화상 면역·화상 저항 한글 확인. 같은 풀 객체의 숫자 글꼴 복원 확인 |
| 자산 | SW 적 3프리팹 필수 Effect·Missing·기존 assetId, R2 256×256 Sprite·아이콘 연결 확인 |
| P6-A 중력 우물 | Editor Host 실제 유탄 직격 후 반경 3.5m 장판, 이동속도 10→5.5, Slow 10회 갱신, Host 링 1회, 무기 해제 후 고정 유지, 5초 만료·속도 10 복귀 PASS |
| P6-B 특이점 박격포 | Editor Host 실제 유탄 직격, Host 경고 링, 1초 미기폭, 무기 해제 후 고정 유지, 2초 Effect 단일 피해, 다중 Collider 제거, 대상 소멸 정리, 예약 3개 상한, 소유자 사망 취소 8개 조건 PASS |

실제 Host 시험은 인증 로비→StageSelect→Act1 테스트 전투 맵에 들어가 수행했다. 적은 실제 프리팹을 사용했지만 안정적인 측정을 위해 AI를 멈추고 HP 등을 시험값으로 둔 구간이 있다. 전체 웨이브·보스 전투 또는 원격 4인 완료를 뜻하지 않는다. 최초 보스 시험의 RPC 도착 전 판정은 대기 시간을 보완해 다시 실행했으며 최종 결과만 위에 기록했다.

### 10.2 다시 실행할 때

1. 작업 중인 Scene/Prefab Stage가 Dirty이면 저장하거나 덮어쓰지 않는다. 컴파일이 끝난 Edit Mode에서 시작한다.
2. 해당 효과의 기존 검증 코드·메뉴가 현재 존재하는지 먼저 확인한다. 옛 Foundation/ArcBlade 등 이름만으로 호출하지 않는다. 필요한 새 검사는 Assets 밖 `run_script`에 두고 공통 코드와 실제 플레이를 나누어 확인한다.
3. H3의 Assets & Formula와 Live Server 검사는 별개다. 실제 powersavingheadset 장착 상태의 마나 변화도 검사한다.
4. 실제 로비→클래스 선택→전투 진입을 거친 뒤 장착·소지한다. 자동화가 임의로 사용자 씬을 저장하거나 기본 장비를 덮어쓰지 않게 한다.
5. 고유효과 검증용 Stage C 도구는 자동 씬 저장·자동 장착을 하지 않는다. Scene 전환 완료와 네트워크 생성·RPC 처리를 기다린다.
6. 이번 검증이 만든 시험 객체·시험 데이터만 정리하고 사용자 원래 씬으로 돌아간다. 기존 승인된 Build Settings 씬 목록과 팀 설정은 임의 복원/삭제하지 않는다. 임시 Editor 자동화는 Assets 밖에 둔다.
7. Console과 Diff를 확인하고 실제로 수행한 범위만 기록한다.

### 10.3 Q1 — 서버·별도 Player 4개 기능 검수 완료

2026-09-20, 한 PC의 서버 전용 Editor와 별도 Windows Development Player 4개를 KCP/127.0.0.1로 연결했다. Fighter 2명·Gunner 2명 모두 실제 소유자 입력 경로로 참여했고, 최종 실행은 서버와 네 클라이언트 모두 107단계를 통과했다. 이는 반복 관찰을 포함한 실행 단계 수이며 독립된 단위 테스트 107개를 뜻하지 않는다.

| 범위 | 확인 결과 |
| --- | --- |
| H3·B1·R2 | 네 명의 장착/소지, H3 저마나 진입·해제, B1 회피 후 이동속도 20%·2초 만료, R2 마나 회복 15%와 중복 방지. 모든 클라이언트가 서버 Stat과 일치 |
| 아크·인페르노·유리빛 궤도 | 개별 실제 공격 11회와 네 명의 같은 대상 동시 공격 2회. 혼합/동일 무기 구성에서 AnimationEvent·투사체·직접/Effect 피해와 관찰자 VFX 확인 |
| 화상 처치·보상 | 실제 Burn Update로 일반 적 3회 처치. 공격자 귀속, 사망/보상 1회, 네 관찰자의 사망 표시, 처치자 골드 +7·다른 세 명 유지 및 상점 골드 일치 |
| 생명주기 | R2 월드 드롭→Raycast 재획득 시 같은 인스턴스 유지. 네 명 사망→개발용 부활 후 조작/효과 복구. 실제 연결 종료→프로필 재접속 후 장비/런타임 유지 및 재공격 성공 |
| 실제 보스 씬 | 저항 배율 0.25와 면역 0 각각 직접/Effect 1회. 저항 DoT는 최대 HP×1%×0.25, 면역 DoT는 0. 네 관찰자 모두 화상 응답 표시 카운터 증가 |
| 새 런 | 리더의 로비 복귀→전원 Ready→시작. 새 Context, 이전 아이템 인스턴스 제거, 인벤토리/골드/쿨타임/트리거 초기화 및 캐릭터 기본 장비 복원 |

검수 중 원격 B1이 발동하지 않는 원인을 수정했다. `PlayerRuntimeStateSync_MirrorTest`가 클라이언트의 `ItemTriggers`를 끄면서 로컬 회피 이벤트 구독도 사라지고 있었다. 구독은 유지하고, `ItemTriggerManager_MirrorTest`의 회피 Command가 서버에서 생존·조작 가능 상태·회피 쿨타임을 확인한 뒤 기존 효과 발동 경로를 호출하도록 연결했다. 클라이언트가 효과 수치나 임의 트리거를 전달하지 않는다.

당시 재현 도구는 `MirrorCombatSmoke_MirrorTest`, `MirrorUniqueEffectSmoke_MirrorTest`, `MirrorLanTestBuilder`와 `--mirror-smoke-*` 명령행이었다. 이 문단은 과거 Q1의 실행 근거이며 **현재 실행 지침이 아니다.** 신규 검증은 §10.2의 Editor/개발 Player Pipeline 경로를 따른다. 기존 운영 프로필·승인된 Build Settings를 시험 준비라는 이유로 변경하지 않는다.

시험은 Camp의 NavMesh/시야가 확보된 위치에 적을 두고 AI·HP·보상을 고정하며, 회피는 기존 소유자 이벤트 처리기를 호출한다. Camp에는 시험 중에만 기존 파괴 연출 서비스를 배치해 실제 풀과 사망 표시를 검증한다. 부활은 개발용 API를 사용한다. 보스 시험은 중간 층을 빠르게 넘겨 실제 보스 씬으로 이동한 뒤 AI를 정지하므로 전체 경로·보스 패턴 전투나 난이도 검증을 대신하지 않는다. 네 Player의 실제 화면은 확인했지만 원격 보스 문구 자체의 글꼴 판독은 이번 카운터 검증과 구분한다. 한글 문구 화면은 §10.1의 Host 검수 근거를 유지한다.

최종 실행 Console 오류는 0건이다. 최초 전체 Windows 빌드는 성공했지만 기존 BH 보스 AdvancedDissolve 셰이더의 D3D11 `uv0`/`ObjectSpacePosition` 오류 10건이 보고되었다. 이후 스크립트 빌드는 오류 0건으로 성공했다. 셰이더 수정과 보스 전체 렌더 품질 확인은 이번 범위에 포함하지 않았다.

남은 네트워크 검수는 여러 PC의 LAN/WAN·지연/손실 환경이다. Q2 성능은 같은 맵·적 수·빌드·시간 구간으로 서버 프레임 시간, GC, 활성 장판/VFX, 큐·예약 길이, RPC 호출 수와 전송 바이트를 측정한다. RPC 호출 수를 실제 네트워크 패킷 수로 보고하지 않는다. 측정된 병목이 있을 때 성공한 연쇄 구간의 RPC 묶음 등 작은 변경부터 검토한다. ECS, 전면 NonAlloc, 범용 실행기 전환을 선행 조건으로 삼지 않는다.

## 11. 최신 리뷰에서 보존한 별도 후속 작업

### 11.1 실제 공격 종료·다음 입력 시간

아래는 9월 22일의 `GunnerSkills_MirrorTest`와 당시 Animator 측정 기록이다. 현행 클래스·Animator 상태는 새 성능 검증 시작 시 다시 확인한다. 당시 공격 종료 이벤트와 코드 상수는 대응하며 Gunner exitTime 0.91549295, 복귀 블렌드 0.25초였다. Fighter Short는 종료 이벤트와 출구가 대응하고 블렌드 0.15초였다.

Gunner 단독 Host에서 속도 0.425/0.85/1.7의 상태 종료는 약 0.937/0.484/0.252초, 다음 요청 승인·전환 종료는 약 1.351/0.815/0.535초였다. 각 회차 승인 증가 1·미확인 이벤트 0을 확인했다. 남는 체감 지연은 복귀 블렌드와 구분해 보아야 하며 이번에 값을 바꾸지 않았다.

Fighter 동등 측정, 저프레임, 원격, 공격 중 공격속도 변경은 남았다. 위 측정만으로 공통 공격 지연 수정 완료를 선언하지 않는다.

### 11.2 보스 파이프라인 검증

보스 검증기는 같은 프리팹·Timeline·플레이어 트랙 연결과 4개 서로 다른 위치, 타입이 맞는 바인딩을 엄격하게 확인한다. 원본에서 의도적으로 사용하지 않는 Activation Track (2)는 명시적으로 제외한다. 이름이 비슷한 다른 프리팹이나 비어 있는 바인딩으로 통과시키지 않는다.

9월 22일 전체 Boss Pipeline은 CampStatusPopup.fireRow 참조 문제로 완주하지 못했고 엘리베이터 앵커 오류도 남았다. 이번 문서 조사에서는 현재 재현 여부를 시험하지 않았다. Q1의 짧은 보스 씬 검수와 전체 Pipeline 완주는 구분하고 실제 통합 검수에서 재확인한다.

## 12. 완료 판단

각 다음 파트는 실제 장비 데이터 연결, 서버 권한, 발동·중복·해제·사망·재사용 규칙, 실제 캐릭터/적 결과, 필요한 화면 검사까지 끝내고 완료로 바꾼다. 완료 전 §1.5의 변경 표시·XML 설명·공개 API·기존 타입 재사용·명명과 직렬화 호환성도 Diff에서 확인한다. 자동 규칙 검사·1인 Host·원격·4인·전체 런의 검증 수준은 서로 대신할 수 없다.

P4까지의 기존 보류 코드 제안은 구현에 흡수했다. 앞으로 리뷰 결과는 이 문서의 해당 파트에 반영하고, 전체 장비의 진행 상태는 전체 계획서에서 갱신한다.

## 13. 2026-09-30 단계별 착수 — A1 싱글 회귀 / A2 싱글 기능·화면 / A3 싱글 기능·수명·화면 통과, 최종 검증 대기

신규 동작·수치는 [최종 전환 계획 §4](UniqueEffect_Final_Implementation_Plan_2026-09-30.md#4-출시용-개별-동작-명세), 단계 순서와 완료 상태는 그 문서 §12를 따른다. 아래 착수 기준선과 실행 절차를 보존하고 A1/A2/A3의 실행·미실행 검증을 구분한다. 구현 3/14이며 최종 완료 판정은 0/14다.

### 13.1 원본·생성 데이터 기준선

원본 위치는 `Assets/Resources/DataFiles/ItemData/1. ExcelFile/`, JSON은 `2. JSONFile/`, 생성 자산은 `3. GeneratedAssets/`다. 주 에이전트가 OpenPyXL의 읽기 전용 모드로 원본 ID 집합을 JSON과 독립 대조했다. 표의 헤더·타입 힌트 행은 데이터 개수에서 제외했다.

| 원본 | 실제 시트·필드 | 원본 ↔ JSON 대조 결과 |
|---|---|---|
| `ItemDataTable.xlsx` | `WeaponDefinitions`, `ArmorDefinitions`, `PotionDefinitions`, `RelicDefinitions`; `itemId`, `uniqueEffectId` | 무기 102 / 방어구 41 / 소비품 5 / 유물 16, 총 **164**. ID 집합 차이 0, 효과 연결 **85** |
| `UniqueEffectTable.xlsx` | `UniqueEffectDefinitions`; `uniqueEffectId`, `effectType`, `coefficients`, `cooldownSeconds`, `displayKind` 등 기존 22열 | 착수 시 86행, A1 후87행, A2 후88행, A3 후 **89**행. 기존 Passive/Triggered 행 보존 |
| `UniqueEffectLabel.xlsx` | `KOR/ENG/JPN/CHN`; `uniqueEffectId`, `effectName`, `effectDescription` | 착수 시 언어별 86행, A1 후87행, A2 후88행, A3 후 각 언어 **89**행. 새 설명 행은 내용 높이·위 정렬 적용 |
| `ItemDataLabel.xlsx` | `KOR/ENG/JPN/CHN`; `itemId`, `itemName`, `description` | 각 언어 **164**행, ID 집합 차이 0 |

착수 시 전환 14종은 Passive 6 / Triggered 6 / Threshold 2였고 실제 SO 14/14의 참조·아이콘 non-null을 확인했다. A1 후 파동 1 / Passive 5 / Triggered 6 / Threshold 2이며 새 `UE_PhaseHarvesterWave`는 기존 Passive SO/GUID를 보존한다. 원본과 JSON의 전체 값·번역 의미까지 동일함을 검증한 것은 아니다. A1은 실제 변환기 재생성 2회에서 동일 아이템 GUID·새 파동 수치·아이콘·4개 언어 라벨을 확인했다.

A2 후 전환 14종의 연결 타입은 파동 1 / 폭발 1 / Passive 5 / Triggered 5 / Threshold 2다. 기존 `UE_StarforgeBreach` Triggered SO/GUID와 4언어 라벨은 보존했으며, 같은 스타 브리처 아이템 GUID의 연결만 새 `UE_StarforgeBreachExplosion`으로 바꿨다. A2는 ItemTable RunAll 2회와 별도 효과 라벨 RunAll 1회에서 새 SO 수치·GUID·아이콘·라벨 DB 계수 치환을 확인했다.

A3 후 연결 타입은 파동 1 / 폭발 1 / 폐열 1 / Passive 4 / Triggered 5 / Threshold 2다. 기존 `UE_WasteHeatCleaver` Passive SO/GUID·4언어 라벨을 보존하고 동일 아이템의 연결만 새 `UE_WasteHeatDischarge`로 바꿨다. ItemTable RunAll 2회·효과 라벨 RunAll 1회 후 새 SO GUID `59f6c4e1d3824ef48ba903c1c9444b1d`, 계수 `4;4;70;50;4;5`, 기존 아이콘·아이템 GUID와 언어별89 라벨을 확인했다. 원본의 기존 셀·수식·스타일·native ZIP 항목은 보존했다.

| A 기준 아이템 | 보존할 아이템 GUID | 기존 효과 ID / GUID |
|---|---|---|
| 공허의 수확자 | `db8efb52ecce6db45bf450d075a4f5e2` | `UE_PhaseHarvester` / `5e5d8fd40e15bc84383f0ee85efebd8b` |
| 스타 브리처 | `139db515956b9cf4fbec78b50734d207` | `UE_StarforgeBreach` / `8fc7dfc06b300df46b376b3ea5b2befa` |
| 폐열 절단 대검 | `5991e5ed5d205a3428594214c1bc9e90` | `UE_WasteHeatCleaver` / `d71393ac0bd46b64f9bcc9735f6a753c` |

### 13.2 조사 보고의 주 에이전트 재검토 결과

사용자의 요청에 따라 기존 조사 보고를 원본으로 한 번 더 검토했다. 이후 이 브랜치의 조사는 `AGENTS.md` §1-2에 따라 **GPT-6.1 Sol / xhigh**에 위임한다.

| 주장 | 직접 대조한 근거 | 판정 |
|---|---|---|
| 14종은 임시 스탯/발동형 효과에 연결되어 있음 | 원본 효과 타입·아이템 연결 → JSON → Unity 로드 SO 14개 | 사실. 새 행동형 완료는 0/14 |
| 원본/JSON 총수가 어긋날 가능성 | 원본과 JSON의 실제 ID 집합 비교, 헤더/힌트 제외 | **데이터 누락 없음. 초기 조사 보고의 행 수 집계·표현을 정정**. 효과 86, 아이템 164, 연결 85 |
| 기존 `OnKill`만으로 A1을 처리할 수 없음 | `NetworkEnemyAuthority.GrantKillRewardOnce`, `PlayerDamageResolver.ExecuteDamageInternal` | 사실. 일반 처치 이벤트에는 Cause/AttackId/위치/방향 인자가 없음 |
| 방향과 공격당 발동 기록을 추가로 보존해야 함 | `T_PlayerCombat.SectorAttack`, `PlayerCombatAuthority.ResolveServerAttack`, Resolver의 호출별 스냅샷/FIFO | 사실. 피격 연출 역방향과 공격 forward가 다르며 큐 상태는 다음 직접 표적 호출까지 유지되지 않음 |
| 새 타입의 표시가 자동 지원되지 않음 | `PlayerItemEffectState.GetRemainingCooldown`, `MirrorCooldownHud.TryCollectUniqueEffect`, `NetworkItemTriggerManager`, `UniqueEffectPresentation` | 사실. 조회는 Triggered/Chain 중심, HUD 수집은 Triggered 전용, 현재 사건은 연쇄/인페르노/준비 링 중심 |
| 새 장판·신발 효과에 기존 분기를 그대로 쓸 수 없음 | `RegisterGrenadeEffect`의 IsGravity, `TryGetPreparedAttackEffect`의 Fighter 무기 슬롯 제한 | 사실. 각각 B1/B4의 좁은 확장이 필요 |
| 변환기 2개를 반드시 수정해야 함 | Excel 변환기의 `Convert`가 SO importer의 `ValidateRows`를 호출 | 필수 범위로 단정하지 않음. A1은 SO importer의 타입/계수/검사 확장을 먼저 검토하고 Excel converter 수정은 필요성이 드러날 때만 추가 |
| 옛 미참조 4종의 현재 상태 | 현재 효과 JSON의 정확 ID 조회 | `test` 문자열 검색만으로 네 항목의 정리를 판단할 수 없음. `UE_TestHelmet/UE_TestGreatSword`는 없고 `UE_SturdyArmor/UE_HasteAura`는 남아 있음 |
| 생성 실패 후 이전 결과로 계속 연결할 위험 | `ItemDataTableSOImporter.InsertUniqueEffects` 391~431행 | 사실. 효과 생성 실패 뒤 기존 SO 연결을 시도하고, 새 ID SO가 없으면 이전 직접 참조를 지우지 않고 건너뜀. 실제 실패 재현은 아직 하지 않음 |

현재 `UE_AdvancedCore`는 `healthFlat:20;` Passive이고 `UE_CyberneticCore`는 `moveSpeedFlat:77`, 4초, 쿨다운 30초 Triggered다. 조사에서 확인한 값이며 밸런스를 변경하거나 검증한 결과가 아니다.

### 13.3 A1 — 공허의 수확자 구현 파일과 순서

**현재 결과:** 기본 공격의 실제 정면·Fighter 출처를 저장하고, 공통 Resolver의 생존→사망 확정에서 원점을 보존해 기존 FIFO에 무속성·비치명타 Effect 파동을 등록했다. 싱글 Presenter/네트워크 Reliable RPC·쿨다운 복제와 기존 HUD 슬롯을 확장했다. `PlayerContext.BindSinglePlayerInventory`에서 실제 싱글 Presenter를 연결했다. BH 기본 공격, WJ 효과·아이템·라벨 importer 및 쿨다운 Container/Slot의 총 6개 스크립트는 사용자가 명시적으로 승인했다. 아래 순서는 이후 동일한 경계의 기준으로 유지한다.

| 변경 후보 | 필요한 책임 | 경계 |
|---|---|---|
| `Assets/SW/Scripts/Player/PlayerDamageResolver.cs` | `TakeDamage` 전 위치/출처 확보, 확정 생존→사망 뒤 A1 통지, 활성 FIFO에 비치명 Effect 등록 | 기존 계산·권한·예외 정리·보상 유지. 일반 `OnKill`을 재발행하지 않음 |
| `Assets/SW/Scripts/Player/PlayerItemEffectState.cs` | 장착 자격, 플레이어별 AttackId 1회 제한·1초 쿨다운·수명 정리·표시 사건 | 현재 쿨다운 사전 재사용. 교체/재장착 우회 금지와 새 런 Context 수명을 함께 검사 |
| `Assets/SW/Scripts/Network/Player/PlayerCombatAuthority.cs` | 실제 기본 공격의 원점·수평 방향·출처를 같은 AttackId 동안 값으로 보존 | 요청의 피격 연출 방향을 바꾸지 않고 실제 공격 방향을 전달 |
| `Assets/SW/Scripts/Network/Player/NetworkItemTriggerManager.cs`, `UniqueEffectPresentation.cs` | 서버 결과 사건 → 소유자/관찰자 즉시 절단 표시, 싱글 사건 구독 | 판정과 같은 공간을 표시. Host 이중 표시·사망 Transform 추적 금지 |
| `Assets/SW/Scripts/Network/Combat/MirrorCooldownHud.cs` | 새 효과의 실제 쿨다운을 기존 슬롯에서 수집할 수 있는지 연결 | 불필요한 새 UI/Manager 없이 조회 경계만 확장. 슬롯 실제 화면 확인 필요 |
| `Assets/SW/Scripts/Equipment/Effects/`의 작은 신규 SO | 길이/폭/계수/대상 수/쿨다운 설정 | `UE_PhaseHarvesterWave`용 설정만. SO에 플레이어 런타임 상태를 저장하지 않음 |
| `Assets/WBHTest/Scripts/Player/T_PlayerCombat.cs` | 싱글 `SectorAttack`의 이미 계산한 origin/forward를 공통 상태로 전달 | **BH 스크립트 사전 승인 필요**. `SetDirectTargets` 연결 주변의 최소 변경 후보 |
| `Assets/WJ_TestPlace/Script/Item/Data/DataLoad/Editor/UniqueEffectTableSOImporter.cs` | `ResolveEffectType` / `ApplyRow` / `ValidateRows`의 새 타입·계수 지원 | **WJ 스크립트 사전 승인 필요**. 기존 타입 불일치 차단을 해제하지 않음 |

1. BH/WJ 파일과 수정 이유를 제시해 승인 범위를 확인한다. A1의 기본 공격·효과/아이템 importer 3개 및 라벨 importer·쿨다운 HUD 3개는 두 차례의 명시적 승인 후 수정했다. `WBH_DamageRequest/Result`와 적 사망 보상 코드는 수정하지 않았다.
2. 실제 싱글/서버 기본 공격 → 공통 Resolver → 생존/사망 전이의 출처를 연결한다. 피격 전 표적 위치와 AttackId의 공격 방향을 값으로 보존한다.
3. A1은 즉시 통로 판정으로 구현한다. 6m·전체 폭 2m·최대 6체, 벽/단차/다중 Collider를 걸러 동기 FIFO에 계수 0.60·무속성·`canCrit=false`를 전달한다. 추가 이동 파동 객체나 예약 시스템은 만들지 않는다.
4. 동일 AttackId 최초 처치만 발동한다. 후속 Effect/DoT/스킬로 파동을 재생성하지 않고 기존 사망·경험치·골드·드롭 경로를 유지한다. 기존 보상형 유물은 별도로 회귀한다.
5. 아래 데이터 절차로 **이 한 종만** 새 ID에 연결하고 옛 Passive의 관통 +25/공격력 +15%/공격속도 -10%가 함께 남지 않는지 확인한다.
6. 정식 맵의 실제 Fighter 기본 공격 요청→애니메이션 이벤트와 실제 풀 적에서 다중 처치 1파동, 최대 6체, 사망 위치·정면, 중복 Collider, 벽·폭·단차 차단, 소유자 쿨다운·수명 초기화, Skill/Effect 재발동 방지를 통과했다. 이후 치명타 100% 조건의 비치명 파동 회귀도 통과했다. 교체/사망/씬/새 런 전체 조합·보상 중복 전용 검사·실제 화면 및 Host/다인·원격은 남았다. 기존 대표 효과 전체 회귀와 마지막 일괄 원격 검사까지 통과해야 A1 완료다.

### 13.4 한 종씩 적용할 데이터 전환 절차

**A1 데이터 연결의 선행 확인·반영:** 승인 후 `ItemDataTableSOImporter`가 효과 생성 실패·누락 참조에서 전체 연결을 중단하고 자신이 변경한 아이템/버프/DB만 저장하도록 했다. 새 SO 연결 뒤 무기 아이콘도 연결한다. `UniqueEffectLabelSOImporter` 역시 라벨 DB만 저장한다. `UniqueEffectTableSOImporter`는 길이·폭·피해%·최대 대상·쿨다운의 5개 계수와 cooldown 열 일치를 검증한다. 실패를 강제로 성공 처리하거나 기존 JSON을 삭제해서 우회하지 않는다.

`ItemDataTableExcelToJson.ConvertPreferringDefaultPaths`와 `UniqueEffectLabelExcelToJson.ConvertWithDefaultPaths`가 `File.Exists`로 결과 경로를 반환하는 것도 확인했다. 다만 모든 예외가 삼켜져 이전 JSON으로 진행한다고 단정할 수는 없다. 각 실패 사례를 재현한 뒤 필요한 경우에만 해당 WJ 파일을 추가 수정한다.

1. 기존 아이템/효과 GUID·아이콘·표 값을 기준으로 저장하고 **새 효과 행과 4개 언어 라벨만 먼저** 준비한다. 이후 단계 13종의 미구현 ID를 일괄 연결하지 않는다.
2. `DataLoader/Unique Effect/0. Run All Steps`로 효과 원본→JSON→SO를 생성한다. 새 타입 등록·계수 검사가 먼저 컴파일되어 있어야 한다. 이미 있는 임시 SO의 타입을 같은 ID에서 바꾸지 않는다.
3. `DataLoader/Unique Effect Label/0. Run All Steps`로 효과 라벨 원본→JSON→라벨 SO를 갱신한다. 아이템 설명 변경도 필요하면 별도의 `DataLoader/Item Label/1. Convert Excel To JSON` 및 `2. Generate SO From JSON`을 사용한다. Item Data Table Run All은 이 라벨 절차를 포함하지 않으므로 번역 수치/자리표시자를 별도 확인한다.
4. `ItemDataTable.xlsx`의 해당 아이템 `uniqueEffectId`만 전환하고 `DataLoader/Item Data Table/0. Run All Steps`로 아이템·아이콘·효과를 연결한다. 이 메뉴의 마지막 효과 연결 단계는 효과 원본→SO 변환도 다시 실행한다.
5. 완료 로그만 믿지 말고 실제 아이템 참조, 새 SO 클래스, 이전 버프 제거, 아이콘·아이템 GUID·4개 언어 설명을 읽어 검증한다. 메뉴 전체가 단일 트랜잭션으로 되돌아간다고 가정하지 않는다.
6. 두 번 실행해 같은 결과인지 비교한다. 실패하면 해당 아이템의 연결과 설명을 기존 동작으로 복구하고 원인을 기록한다. 구형 SO·`.meta`는 참조와 롤백 필요를 확인하기 전까지 보존한다.

`UniqueEffectTableExcelToJson.cs`는 공통 `ValidateRows`를 이미 호출한다. 새 열/읽기 규칙이 실제로 필요하지 않으면 수정하지 않는다. 필요할 경우에만 WJ 승인 목록에 추가한다.

### 13.5 후속 단계가 처음 요구하는 확장

| 단계 | 현재 재사용 지점 | 해당 단계에서 추가할 경계 |
|---|---|---|
| A2 | 기본 산탄 범위 공격·공통 FIFO·A1 공격별 발동 | 실제 원점·표면 적중점 보존 및 3m·공격당 1폭발 구현. 정식 Gunner 싱글 경계·피해·보상·스냅샷·탈착/비활성화·링/HUD 검사 통과. 공통 수명 전체·정상 Stage 준비·성능 미검증, 멀티는 A3 이후 |
| A3 | 플레이어별 상태·기존 장착/죽음 정리·BuffSnapshot | 열0~4·5초 만료·다음 유효 공격 소비·공격 세대 검사, 기존 버프 HUD와 실제 칼날 MPB 발광·부채꼴 표시 구현. 실제 Fighter 싱글 충전/공간/피해/보상/수명 통과. 서버·관찰자·다인 검증 대기 |
| B1/B2 | `PlayerGrenadeEffect`·Burn1 / 기존 연쇄 탐색 | 종류별 상한 / 치명 조건 후 중복 기록, 단일 표적에서 대기 미소비 |
| B3/B4 | 기존 보호막 / 준비 공격 | 실제 HP 손실·단일 보호막 출처 / 신발·Gunner 승인 소비와 +40%/+20% 합산 |
| C1/C2/C3 | `NetworkEnemyProjectile`, 싱글 `WBH_Projectile`, 승인 기본 공격 | 관통 수명 / 과거 공간 예약·실행별 스냅샷·명시적 비치명 / 발사 탄두 자격 |
| C4/C5/C6/C7 | 기존 적 상태·저항·풀 복구·Burn·서버 권한 | 적 방어 약화 / 대상 공유 빙결 제한 / 실제 화상 출처·세대 / 시전자·소비자 분리와 표식 소비권 |

적/플레이어 Health·상태 수신·UI 등 다른 담당 스크립트가 필요한 B/C 단계는 착수 시 실제 파일을 좁혀 새로 승인받는다. A1 승인으로 해당 파일까지 허용됐다고 해석하지 않는다. 유지할 기존 파괴 연출은 적 본체와 별도 풀 수명을 따르며 새 파동이 파편 객체를 공격 표적으로 선택하지 않아야 한다.

A1은 Unity 컴파일·데이터 재생성 2회·실제 싱글 전투 검사와 `MirrorStage4EffectValidation`의 `PASS 1 actual player boundaries`를 확인했다. 김성우 로그 332번은 RAM 부족으로 중단한 당시 실행 범위를 보존한다. 이후 사용자가 단독 모드로 전환한 뒤 `Tools/Validation/PhaseHarvesterWaveValidation.cs`의 `RunSingle`, `RunSingleNoCritical`, `RunSingleBoundaries`를 실제 Fighter와 풀 적에서 다시 실행해 통과했다. 치명타 100% 기본 공격은 치명타지만 파동은 비치명·무속성 Effect로 59.8000031 피해를 주는 것을 확인했다. 다중 처치 1파동·최대 6체·중복 Collider·벽·단차·빈 파동 쿨다운·Skill/Effect 거부도 통과했다. HUD 슬롯·아이콘·번역 텍스트·파동 Renderer 확인은 유지하되 A1 전용 화면·보상 검사, 공통 수명 전체와 Host/원격은 남겼다.

### 13.6 A2 구현·데이터 검증과 남은 실행

- 공통 상태와 FIFO를 재사용했다. 싱글·서버 Shotgun 범위 공격은 피해 전에 실제 FirePoint 원점과 본체별 유효한 `Collider.ClosestPoint(origin)`를 확정한다. 첫 3m 이내 유효 Direct/Normal 적중만 반경 2.5m·Fire 35%·최대 5체·비치명타 Effect 폭발을 등록한다. 벽·0.5m 초과 본체 단차·중복 Collider를 제외하며 큰 본체는 중심 거리보다 실제 표면으로 반경을 판정한다. 소유자 1.2초 쿨다운, 최초 적중점 보존, 추가 Burn 미예약, 싱글 사건/네트워크 Reliable RPC·기존 HUD를 연결했다.
- 데이터: ItemTable `WeaponDefinitions!K73`, 효과 90행·ComboBox A16, 4언어 라벨 89행만 전환·추가했다. 기존 아이템 164개와 연결 85개, 이전 효과·A1 값·수식·native features·무관 ZIP 항목을 보존했다. 사용자 사진 피드백에 따라 A1/A2 새 설명 행의 큰 빈 공간을 내용 높이·위 정렬로 줄이고 A2 효과 이름의 줄바꿈으로 잘림을 막았다. Unity 컴파일과 두 차례 아이템/효과 재생성, 별도 라벨 재생성·아이콘·DB 계수 치환을 확인했다.
- 정식 원본은 `Assets/Resources/Prefabs/Character/Player/Gunner.prefab`이며 `Gunner 1`이 아니다. Stat·Buff·Health·Mana가 포함돼 있고 Missing Script가 없다. 추가 Editor가 없는 단독 모드에서 Assets 밖 `Tools/Validation/StarBreacherExplosionValidation.cs`를 실행했다. 기존 Fighter를 런타임에서 비활성화하고 정식 Gunner를 인벤토리·장비·지갑에 바인딩한 격리 전투 검사로, 저장 클래스와 Spawner 선택은 바꾸지 않았다. 외형 준비는 고정 지연 대신 실제 Addressables 준비 완료와 장착 itemId 일치를 기다리도록 검증 코드를 보정해 재실행했다.
- 싱글 통과: `RunBoundaries`는 실제 발사 원점에서 Collider 표면 2.99/3.01m, 먼 적 이후 최초 유효 적중, AttackId당 1회, 1.2초 쿨다운과 수명 초기화 후 유지, Skill/Effect/DoT/Fighter 출처 거부, 시작 표적 즉사 및 공격자 20m 이동 후 저장 적중점 사용을 확인했다. `RunTargetsAndDamage`는 최대 5체·살아 있는 시작 표적의 Direct+Effect·중복 Collider·벽·0.6m 단차·치명타 100% 조건의 비치명 Fire Effect·추가 Burn 없음, `RunSurfaceAndEmpty`는 본체 중심이 반경 밖인 큰 Collider 표면과 대상 0체의 쿨다운/표시를 확인했다.
- 싱글 통과: `RunActualAttackAndHud`는 실제 `TryAttack`→애니메이션 Shotgun 이벤트와 실제 소유자 HUD 아이콘·쿨다운·번역 텍스트를 확인했다. `RunSnapshotAndLifetime`은 Direct 콜백에서 공격력이 147→647로 변해도 세 폭발 피해가 모두 최초 스냅샷의 26.3125로 유지되고, 정식 장비 탈착/재장착과 실제 플레이어 비활성화/재연결이 쿨다운을 지우지 않음을 확인했다. `RunRewardsOnce`는 직접 처치 1체와 폭발 처치 2체가 각각 보상 이벤트 1회·총 60크레딧을 지급하고 경험치가 진행하며, 죽은 대상의 추가 피해 요청은 정산을 반복하지 않음을 확인했다.
- 화면·최종 상태: `PrepareSingleVisual`의 32점 LineRenderer·실제 아이콘/쿨다운·활성 한글 툴팁 텍스트를 확인하고, Overlay를 포함하는 `capture_game_view --source screen`에서 주황 폭발 링과 HUD 아이콘/남은 시간 표시를 눈으로 확인했다. 툴팁의 화면 위치까지 통과로 확대하지 않는다. Unity 컴파일 완료·새 컴파일 오류 없음. Console에는 기존 pending 노드 오류 2건과 검증 도구의 프로젝트 밖 캡처 경로 거부 1건이 남아 있어 전체 오류 0은 주장하지 않는다. 전투 검사에서 새 런타임 예외는 없었다. Play 종료 후 일시정지 해제·Dirty Act1_Stage1·startScene=null·단독 모드(추가 Editor 0)를 보존했으며 Scene/Prefab을 저장하지 않았다.
- 미검증·다음 작업: 직접 Act1 Play의 저장맵 pending 노드가 비어 있어 정상 Stage 준비가 중단된다. 기존 directScene 설정으로 다시 진입하면 `needsPlayerInitialization=true` 때문에 운영 저장을 쓰는 조건이어서 실행하지 않았다. 격리 전투/화면 통과를 정상 Stage 준비 완료로 기록하지 않는다. 실제 플레이어 사망/부활·씬 전환·새 런·풀 세대 전환 등 공통 수명 전체, A1 전용 화면/보상 및 성능 검사는 남았다. A3 후 A 회귀에서 남은 싱글 수명과 Host/관찰자/원격/다인 검증을 함께 수행하며 A2를 최종 완료로 표시하지 않는다.

### 13.7 A3 — 폐열 절단 대검 코드·데이터·싱글 기능/수명/화면 검사

- 구현: `WasteHeatDischargeUniqueEffectSO`와 새 `UE_WasteHeatDischarge`를 연결했다. 열의 원본은 기존 `BuffInstance.stackCount` 하나이며 SO·ItemInstance·UI에 별도 열을 저장하지 않는다. 서로 다른 유효 Fighter Direct/Normal AttackId가 한 번씩 충전하고 4번째에는 준비, 5번째에는 열0과 방출을 확정한다. 같은 5번째 광역 공격의 다음 표적은 재충전하지 않는다. Skill/Effect/DoT/빗나감은 충전·소비·무적중 시각 갱신에서 제외한다.
- 피해·공간: TakeDamage 전에 실제 원점·정면·장착 세대를 값으로 보존하고 콜백 뒤 생존·활성·장착 생애를 검사한다. 기존 FIFO/호출별 스냅샷으로 Fire50%·비치명·추가 Burn 없는 Effect를 등록한다. 수평 본체 원점 기준 4m·총70도(반각35도)·최대4체·높이차0.5m·Wall/Prop/Ground 시야 검사를 선택했다. 큰 Collider 표면만 범위 안인 적은 제외한다. 본체 중복을 제거하고 거리/netId/InstanceId로 정렬한다. 이 본체 기준은 이번 구현 선택이며 최종 명세에 Collider 표면 기준이 강제되어 있다고 설명하지 않는다.
- 수명·표시: 5초 무적중, 장비 교체/탈착, 사망, 비활성화, 활성 씬 변경에서 열·준비·이전 공격 기록을 정리한다. DDOL Mirror 플레이어도 기존 `PlayerContext`의 `activeSceneChanged` 구독으로 처리하고 같은 생애의 A1/A2 쿨다운은 보존한다. 활성 씬이 바뀌지 않는 Additive 로드는 이 초기화 이벤트의 대상이 아니다. Remove/Apply 버프 콜백의 재진입에는 세대 검사를 적용한다. 기존 BuffSnapshot은 폐열0을 그대로 복제하며 준비 bool SyncVar와 Reliable RPC는 관찰자의 칼날·부채꼴 표시를 연결한다. 네트워크 연결은 구현했지만 실제 Host/원격 검증은 남았다.
- 승인·최소 변경: WJ `UniqueEffectTableSOImporter.cs`의 새 타입/6계수 검증, `BuffIconSlot.cs`의 폐열0/1 숫자, `BuffTextComposer.cs`의0 툴팁은 각각 사용자의 명시적 A3 승인 후 수정했다. BH는 A1/A2가 연결한 실제 원점·Fighter 출처를 재사용했다. 기존 무기 외형 Presenter의 현재 외형을 읽어 발광 MaterialPropertyBlock만 일시 변경·원상복구하고 공유 Material을 수정하지 않는다. 새 HUD·Manager·범용 실행기나 런타임 시험 진입점을 만들지 않았다.
- 데이터: Excel 아이템 `WeaponDefinitions!K5`, 효과 새 `A91:V91`/타입 목록, 네 언어 새 `A90:C90`만 전환·추가했다. 이전 Passive의 관통18/화염15를 새 SO 계수에 옮기지 않았다. 아이템/옛 효과 GUID·아이콘·기존 셀/수식/스타일/native 항목을 보존하고 새 설명은 위 정렬·내용 높이로 렌더 확인했다. Unity ItemTable RunAll2회·라벨 RunAll1회는 명령 timeout이 있었지만 실제 완료 로그와 로드 SO/JSON을 별도로 대조했다. 효과89·언어별89·아이템164/연결85를 확인했다.
- 실제 싱글 통과: `Tools/Validation/WasteHeatDischargeValidation.cs`는 Assets 밖 `run_script`에서 원본 Fighter, 기존 장비 거래, 실제 적 Provider/Pool을 사용한다. `RunChargeAndExpiry`는0→4/5번째 방출·8체 광역1충전·Skill/Effect/DoT/비Fighter/빗나감 거부·5.1초 후0을 확인했다. `RunGeometryAndDamage`는3.99/4.01m·34.99/35.01도·큰/중복 Collider·최대4체·벽/0.6m 단차·Direct+Effect·표적0소비·치명100% 조건에서 Fire Effect 비치명/추가 Burn 없음을 확인했다. 실제 피해는 첫 세션33.5, 마지막 세션51.1로 각각 그 세션의 실제 스탯을 반영했다.
- 실제 싱글 통과: `RunActualAttackAndFlash`는 TryAttack→SectorAttack AnimationEvent·실제 발광/원래 MPB 복구·34점 부채꼴/Collider 없음·수명 종료와 기존 HUD0/1/4·아이콘·번역 툴팁을 확인했다. `RunLifetimeAndSnapshot`은 Direct 콜백에서20m 이동/180도 회전 후 저장 원점·방향, 첫 Effect 콜백에서 공격력+500 후에도4체 모두 같은 최초 스냅샷 피해(첫 세션33.5, 마지막 세션51.1), 정식 탈착/재장착·비활성화의 이전 세대 방출 차단을 확인했다. `RunReentrancyAndDeath`는 Apply/Remove 콜백 재진입·실제 피해로 사망·기존 부활 API 후 열0/새 적중1을 확인했다. 이 부활 검사를 운영 저장/부활 횟수 흐름 통과로 확대하지 않는다.
- 표시 재진입 통과: `RunReadinessReentrancy`는 실제 `PlayerStat.OnStatChanged` 콜백의 재장착 뒤 이전 소비/Direct 적중 내부 만료가 false 표시·방출을 추가하지 않음을 확인했다. 내부 만료 시각만 외부 fixture에서 앞당겼으며, Update 만료는 실제5초를 기다렸다. Tick 콜백에서 재장착·유효 Direct4회로 새 생애의 열4/준비true를 만든 뒤에도 이전 false로 덮이지 않았다. 세 곳의 스택 변경 직후 세대 검사를 보완하고 충전/공간/실제 공격·발광/사망/스냅샷/씬·신규 배우 검사를 다시 통과했다. 이후 Console 신규 오류0을 확인했다.
- 보상·씬·신규 배우 통과: `RunRewardsOnce`의 실제 Direct1/Effect3 처치가 보상 사건 각각1회·80크레딧·레벨1→2/경험치 진행으로 정산되고 죽은 적의 추가 요청은 거부됐다. `RunSceneAndNewActor`는 살아 있는 같은 배우의 활성 씬 변경4→0·원씬 복원, 이전 배우 파괴 후 동일 장비 인스턴스를 가진 신규 Fighter0→첫 적중1·persistedStack0을 확인했다. 신규 배우의 실제 Health.Start 완료를 기다리는 fixture로 보정해 통과했다. 정식 런 시작/저장은 실행하지 않았다. `RunCooldownSceneBoundary`는 A1 .981274545→.977409065초, A2 .7705487초 유지로 씬 전환의 쿨다운 보존을 확인했다.
- 화면·회귀·Editor: 실제 Act1 화면 캡처에서 준비 칼날/HUD4와 방출 부채꼴/HUD0을 확인했다. 기존 Stage 준비 오류 Overlay는 이 화면 검사 동안 런타임 Canvas 표시만 숨겼으며 준비 상태나 저장을 우회하지 않았다. A1 RunSingle/RunSingleNoCritical 및 A2 RunTargetsAndDamage도 재실행해 통과했다. 강제 재컴파일 완료·C# 오류0, 마지막 씬/쿨다운 검사 뒤 Console 새 오류0을 확인했다. 이전 도구 timeout·기존 pending 노드/Preview 오류와 보정 전 외부 fixture 실패 이력까지 전체 오류0으로 보고하지 않는다. Play/일시정지를 종료하고 Dirty Act1_Stage1·startScene=null·PrefabStage 없음·단독 모드를 유지했다. Scene/Prefab 저장·빌드·Commit/Push는 실행하지 않았다.
- 남은 관문: 정상 Stage 준비, 정식 새 런/저장 수명, 기존10종 전체 회귀·모든 공통 수명 조합, Host/관찰자/원격/다인, 성능은 미검증이다. RAM/디스크 부족 조건에서 새 Player를 빌드하지 않았고 기존 빌드도 A3 증거로 사용하지 않는다. 사용자 지시의 A1~A3 일괄 멀티 검증을 다음 A 회귀에서 수행한다. 현재 구현3/14·최종0/14를 유지하며 B 단계 진입 관문 통과로 기록하지 않는다.
