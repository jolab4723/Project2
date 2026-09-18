# 고유효과 P3-B H3 저마나 회복형 인계 (Round-09)

- 작성일: 2026-09-18
- 브랜치: `codex/unity-6000-3-22-test`
- 상태: **Round-09 GPT 검토 완료 (정상 Host 1인 실행 기준선 수용, READY_FOR_USER_DECISION) / 저마나 투구 1종 정식 데이터 시범 연결(Stage B) 진행 중**
- 선택 범위: P3-B 저마나 회복 투구 고유효과 1인 로직 및 소유권/수명 경계 (정식 아이템 등록 및 다인 런은 후속 단계로 분리)

## 1. 이번 단계의 결정 및 반영 내역 (Round-09 검토 반영 및 정식 투구 시범 적용)

1. **`PlayerRuntimeStateSync_MirrorTest` 스탯 알림 책임 명시 및 결함 수정 (Code A)**
   - 원인: `HandleServerStatChanged`가 `context.Stats.Stat.OnStatChanged` 이벤트마다 `RecalculateServerStats()`를 호출하여 `context.Stats.SetPassiveStats(shopPlayerState?.ServerPassiveStats ?? StatSet.Zero)`로 패시브를 당시 0인 Shop 원본으로 덮어쓰던 결함 발견.
   - 조치: `HandleServerStatChanged`에서 패시브 덮어쓰기를 제거하고, 스탯 변동 시 `context.Health?.RefreshMaxHealth()`, `context.Mana?.RefreshMaxMana()`, `serverPublishQueued = true`만 수행하도록 정류. 패시브 원본 관리는 `ServerPassiveStatsChanged`에서만 수행함을 한글 docstring으로 명시.

2. **저장 자산 읽기 전용 검사기 추가 (Code B)**
   - `Assets/Editor/ArmorRelicP3BValidation_MirrorTest.cs`에 `ValidateSelectedLowManaHelmetAsset()` 메뉴 및 `RequireLowManaHelmetAsset(ItemDefinitionSO item)` 추가.
   - Project 창에서 선택된 실제 저장 `ItemDefinitionSO`를 읽기 전용으로 검사하여 ID 중복, 유효한 범위(0~100%), 버프 스펙(지속시간 0, Ignore, 1스택, mpRegenPercent 단일), 설명 계수 일치, UniqueEffectPool 내 유일성을 검증. 에셋 변경이나 삭제 없음.

3. **PlayMode 실서버 런타임 19단계 시퀀스 2회 반복 PASS**
   - 장착 전 베이스라인(100/10), 100% 비활성(10), 25% 경계 활성(13), 26% 비활성(10), 24.9% 활성(13), 25.1% 비활성(10), 최대마나 104 분모 변경 활성(13), 최대마나 80 비활성(10), 0/100 활성(13), 0/0 무효분모 비활성(10), 최대마나 100 복원 활성(13), V07 컴포넌트 Disable/Enable 수명주기, 동일 SO 교체(해제→복원) 버프 유지, 최종 해제(10)까지 전 단계 실측 PASS (1회차 frame 1805~1810, 2회차 frame 52868~52873).

4. **자원 회수 감시자(Release Verification Watcher) 및 안전/제어 경로 실측 상태**
   - 자원 회수 완제: Object.Destroy 확인, `NetworkServer.spawned` 등록 해제, `PlayerStatManager.All` / `PlayerManaManager.All` / `PlayerBuffManager.All` 리스트 잔류 제로 확인 완료 (`PASS Live Server PlayMode 검증 및 시험 자원 회수 완료`).
   - 제어 분기 실측: 가드(`isLiveValidationRunning`) 중복 실행 거절 확인, 회수 대기 중 취소 시뮬레이션 시 `result=CANCELLED` 보존 확인, 강제 환경 종료 시뮬레이션 시 `result=NOT_VERIFIED` 종결 로그 확인.
   - 준비 실패 경로: catch 블록 내 `StartReleaseVerification` 연결 및 가드 유지 정적 경로 확인 (실제 실패 주입은 후속 실서버 테스트에서 검증).

5. **Stage B 정식 데이터 시범 대상 연결 및 생성 검증 완료**
   - **대상 선정**: 사용자 지침(규약상 ID에 `_` 미포함)에 따라 `item.armor.helmet.powersavingheadset` (절전모드 헤드셋, Rare, healthFlat 130, 2x2, 가격 2500)를 저마나 마나 재생 고유효과 정식 아이템으로 확정. 기존 `item.armor.helmet.militaryneuralstimulator`(군용 신경 자극기)는 `uniqueEffectId=""`로 원상 복원하여 베이스 아이템 순수성 보존.
   - **원천 엑셀 갱신**:
     - `UniqueEffectTable.xlsx` (행 53): `uniqueEffectId=UE_LowManaRecovery`, `effectType=StatThresholdBuffUniqueEffectSO`, `effectName=절전 모드`, `effectDescription=현재 마나가 최대 마나의 {0}% 이하일 때 마나 재생이 {1}% 증가합니다.`, `coefficients=25;30`, `statEffects=mpRegenPercent:30`, `duration=0`, `stackBehavior=Ignore`, `maxStack=1`, `referenceStat=CurrentManaPercent`, `comparisonOperator=LessOrEqual`, `thresholdValue=25`.
     - `ItemDataTable.xlsx` (행 43): `item.armor.helmet.powersavingheadset` 신규 추가 (`itemName=절전모드 헤드셋`, `uniqueEffectId=UE_LowManaRecovery`).
     - `ItemDataLabel.xlsx` (행 164): KOR, ENG, JPN, CHN 4개 국어 라벨 추가 (KOR: 절전모드 헤드셋, ENG: Power Saving Headset, JPN: 省電力ヘッドセット, CHN: 省电模式耳机).
     - `UniqueEffectLabel.xlsx` (행 52): KOR, ENG, JPN, CHN 4개 국어 고유효과 라벨 추가 (절전 모드 / Power Saving Mode / 省電力モード / 省电模式).
   - **아이콘 등록**:
     - `Assets/Resources/Images/Item/item.armor.helmet.powersavingheadset.png` 및 `.meta` 등록 완료.
   - **데이터 임포터 멱등성 검증 (`DataLoader/Item Data Table/0. Run All Steps`)**:
     - `item.armor.helmet.powersavingheadset_절전모드 헤드셋.asset` 생성 및 `UE_LowManaRecovery.asset` 연결, 아이콘 연결 완료.
   - **저장 자산 읽기 전용 검사 (`ValidateSelectedLowManaHelmetAsset`)**:
     - `Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/item.armor.helmet.powersavingheadset_절전모드 헤드셋.asset`에 대해 Code B 검사기 실행 결과 무결성 전수 통과 확인:
     - `[ArmorEffectValidation] 저장 자산 검사 통과: item.armor.helmet.powersavingheadset, 효과=UE_LowManaRecovery, 기준=25%, 재생 증가=30%`

## 2. 사용자 브리핑 — 현재 적용 범위와 정식 방어구 출시 경계

- **PlayMode 실서버 검증 (완료)**:
  - 폐기용 1인 SW TEST 세션(`Assets/SW/TEST/MirrorPlayerContext/Scenes/MirrorPlayerContextTest.unity`) Host에서 실서버 런타임 수명주기 및 저마나 투구 고유효과 19단계 전수 PASS.
- **사전 검증 (완료)**:
  - EditMode 20 checks PASS (자산 무결성 4 + 정책 조건 5 + 스탯 공식 5 + 분모/임계치 수학 6).
- **Stage B 정식 데이터 시범 연결 (완료)**:
  - `item.armor.helmet.militaryneuralstimulator`(군용 신경 자극기)에 `UE_LowManaRecovery`(절전 모드) 고유효과가 원천 엑셀/JSON/생성 SO 에셋에 정식 연결되었으며, `ValidateSelectedLowManaHelmetAsset` 검사 통과.
- **정식 방어구 출시 경계 (후속 단계 관리)**:
  - 실제 인벤토리 UI 획득/장착/자연 마나 재생 틱 회귀(Stage C), 사망/부활 런 수명주기(Stage D / V09), 4인 협동 동기화(Stage E)는 후속 단계로 관리.

## 3. Unity Editor 검증 결과

### 1) EditMode 사전 검증 (`SW/Mirror Test/Validate Armor Relic P3-B H3 (Assets & Formula)`)
```
[ArmorEffectValidation] case=V02-Asset-FighterPrefabExists passed=True detail=Fighter PlayerContext 프리팹 로드
[ArmorEffectValidation] case=V02-Asset-FighterRequireArmorProvider passed=True detail=Fighter 어댑터 부착, Missing Script 0건, 5대 참조 및 useLowManaHelmetEffect 일치
[ArmorEffectValidation] case=V02-Asset-GunnerPrefabExists passed=True detail=Gunner PlayerContext 프리팹 로드
[ArmorEffectValidation] case=V02-Asset-GunnerRequireArmorProvider passed=True detail=Gunner 어댑터 부착, Missing Script 0건, 5대 참조 및 useLowManaHelmetEffect 일치
[ArmorEffectValidation] case=V03-Policy-ValidHelmet passed=True detail=H3 투구 아이템에 대해 opt-in 정책 true
[ArmorEffectValidation] case=V03-Policy-InvalidArmorType passed=True detail=Armor(갑옷) 슬롯은 H3 투구 정책 false
[ArmorEffectValidation] case=V03-Policy-InvalidEffectType passed=True detail=Threshold 외 다른 효과는 H3 투구 정책 false
[ArmorEffectValidation] case=V03-Policy-InvalidOperator passed=True detail=GreaterOrEqual 연산자는 저마나 H3 정책 false
[ArmorEffectValidation] case=V03-Policy-InvalidStatRef passed=True detail=CurrentHealthPercent 참조는 H3 정책 false
[ArmorEffectValidation] case=V04-Formula-EffectSpecVerified passed=True detail=실제 SO에 mpRegenPercent +30% 버프 스펙 설정 확인
[ArmorEffectValidation] case=V04-Formula-BaseMaxMana passed=True detail=기본 최대 마나 100 (실제: 100)
[ArmorEffectValidation] case=V04-Formula-BaseMpRegen passed=True detail=기본 마나 재생 10 (실제: 10)
[ArmorEffectValidation] case=V04-Formula-BuffedMpRegen passed=True detail=SO 30% 버프 적용 시 마나 재생 13 (실제: 13)
[ArmorEffectValidation] case=V04-Formula-AlteredBuffDivergence passed=True detail=20% 버프 시 12로 산출되어 30%(13)와 차이 감지 (실제: 12)
[ArmorEffectValidation] case=V05-Math-26PercentInactive passed=True detail=26/100 비율 26.00% > 25% 비활성
[ArmorEffectValidation] case=V05-Math-25PercentActive passed=True detail=26/104 비율 25.00% <= 25% 활성
[ArmorEffectValidation] case=V05-Math-32.5PercentInactive passed=True detail=26/80 비율 32.50% > 25% 비활성
[ArmorEffectValidation] case=V05-Math-24.9PercentActive passed=True detail=24.9/100 비율 24.90% <= 25% 활성
[ArmorEffectValidation] case=V05-Math-25.1PercentInactive passed=True detail=25.1/100 비율 25.10% > 25% 비활성
[ArmorEffectValidation] case=V05-Math-ZeroDenominatorInvalid passed=True detail=최대 마나 0일 때 분모 무효 판정
[ArmorEffectValidation] PASS 20 checks. 자산 무결성(Fighter/Gunner) 및 H3 정책·스탯 공식 사전 검증 완료.
```

### 2) PlayMode 실서버 검증 (`SW/Mirror Test/Validate Armor Relic P3-B H3 (Live Server Play Mode)`)
```
[ArmorEffectValidation] case=Live-PreEquip-Baseline frame=1805 passed=True buffs=0/0 mana=100.0/100.0 maxMana=100.0/100.0 regen=10.0/10.0
[ArmorEffectValidation] case=Live-V04-Mana100-Inactive frame=1805 passed=True buffs=0/0 mana=100.0/100.0 maxMana=100.0/100.0 regen=10.0/10.0
[ArmorEffectValidation] case=Live-V04-Mana25-Active frame=1805 passed=True buffs=1/1 mana=25.0/25.0 maxMana=100.0/100.0 regen=13.0/13.0
[ArmorEffectValidation] case=Live-V04-Mana26-Inactive frame=1805 passed=True buffs=0/0 mana=26.0/26.0 maxMana=100.0/100.0 regen=10.0/10.0
[ArmorEffectValidation] case=Live-V04-Mana24.9-Active frame=1805 passed=True buffs=1/1 mana=24.9/24.9 maxMana=100.0/100.0 regen=13.0/13.0
[ArmorEffectValidation] case=Live-V04-Mana25.1-Inactive frame=1805 passed=True buffs=0/0 mana=25.1/25.1 maxMana=100.0/100.0 regen=10.0/10.0
[ArmorEffectValidation] case=Live-V05-MaxMana104-Active frame=1805 passed=True buffs=1/1 mana=26.0/26.0 maxMana=104.0/104.0 regen=13.0/13.0
[ArmorEffectValidation] case=Live-V05-MaxMana80-Inactive frame=1805 passed=True buffs=0/0 mana=26.0/26.0 maxMana=80.0/80.0 regen=10.0/10.0
[ArmorEffectValidation] case=Live-V04-Mana0-Active frame=1805 passed=True buffs=1/1 mana=0.0/0.0 maxMana=100.0/100.0 regen=13.0/13.0
[ArmorEffectValidation] case=Live-V05-MaxMana0-Inactive frame=1805 passed=True buffs=0/0 mana=0.0/0.0 maxMana=0.0/0.0 regen=10.0/10.0
[ArmorEffectValidation] case=Live-V05-RestoreMaxMana-Active frame=1805 passed=True buffs=1/1 mana=0.0/0.0 maxMana=100.0/100.0 regen=13.0/13.0
[ArmorEffectValidation] case=V07-before-disable frame=1805 passed=True buffs=1/1 mana=0.0/0.0 maxMana=100.0/100.0 regen=13.0/13.0
[ArmorEffectValidation] case=V07-disabled-immediate frame=1805 passed=True buffs=0/0 mana=0.0/0.0 maxMana=100.0/100.0 regen=10.0/10.0
[ArmorEffectValidation] case=V07-disabled-next-frame-mana-event frame=1806 passed=True buffs=0/0 mana=0.0/0.0 maxMana=100.0/100.0 regen=10.0/10.0
[ArmorEffectValidation] case=V07-enabled-immediate frame=1806 passed=True buffs=1/1 mana=0.0/0.0 maxMana=100.0/100.0 regen=13.0/13.0
[ArmorEffectValidation] case=V07-enabled-next-frame frame=1807 passed=True buffs=1/1 mana=0.0/0.0 maxMana=100.0/100.0 regen=13.0/13.0
[ArmorEffectValidation] case=Live-SameSO-UnequipFirst frame=1807 passed=True buffs=0/0 mana=0.0/0.0 maxMana=100.0/100.0 regen=10.0/10.0
[ArmorEffectValidation] case=Live-SameSO-RestoreSecond frame=1808 passed=True buffs=1/1 mana=0.0/0.0 maxMana=100.0/100.0 regen=13.0/13.0
[ArmorEffectValidation] case=Live-FinalUnequip frame=1809 passed=True buffs=0/0 mana=0.0/0.0 maxMana=100.0/100.0 regen=10.0/10.0
[ArmorEffectValidation] 검사 단계 종료. 자원 제거 및 확인 진행.
[ArmorEffectValidation] PASS Live Server PlayMode 검증 및 시험 자원 회수 완료. (frame=1810)
```

### 3) 저장 자산 읽기 전용 검증 (`SW/Mirror Test/Validate Selected Low Mana Helmet Asset`)
```
[ArmorEffectValidation] 저장 자산 검사 통과: item.armor.helmet.powersavingheadset, 효과=UE_LowManaRecovery, 기준=25%, 재생 증가=30%
```

## 4. 남은 작업 및 후속 단계

1. 실제 인벤토리 UI 획득/장착/자연 마나 재생 틱 회귀 (Stage C).
2. 사망/부활 런 수명주기 연동 (Stage D / V09).
3. 4인 협동 네트워크 동기화 검증 (Stage E).
4. 제한 양산 및 추가 품목 확장 (Stage F / G).
