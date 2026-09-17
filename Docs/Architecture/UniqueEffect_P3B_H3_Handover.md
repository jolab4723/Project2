# 고유효과 P3-B H3 저마나 회복형 인계

- 작성일: 2026-09-17
- 브랜치: `codex/unity-6000-3-22-test`
- 상태: **구현 초안 / Unity 기준선 컴파일 차단으로 런타임 검증 대기**
- 선택 범위: P3-B에서 실제 데이터 경계가 가장 명확한 H3 하나를 먼저 진행

## 1. 이번 단계의 결정

P3-B 전체 후보(H3/B1/R2)를 한 번에 새 시스템으로 만들지 않고 H3를 첫 대표로 선택했다. H3는 기존 `StatThresholdBuffUniqueEffectSO`와 `CurrentManaPercent` 평가기를 재사용하며, 장착 슬롯 변화만 별도 `PlayerArmorEffectProvider_MirrorTest`가 소유한다.

- 조건: 현재 마나가 최대 마나의 25% 이하
- 효과: `mpRegenPercent +30%`
- 경계: 25%에서 활성, 26%에서 비활성, 0%에서도 활성
- 제외: 최대 마나 변경, 스킬 무료화, 쿨다운 환급

제안 이름과 수치는 `Armor_Relic_UniqueEffect_Review_Round01_Handover.md`의 시제품 기준이며, 원천 Excel 행과 최종 itemId/effectId 확정은 별도 데이터 작업으로 남긴다. 이번 검증기는 런타임 경계를 확인하기 위해 메모리 SO를 사용한다.

## 2. 구현

### 방어구 활성 어댑터

`Assets/SW/TEST/MirrorPlayerContext/Scripts/PlayerArmorEffectProvider_MirrorTest.cs`는 `PlayerRelicEffectProvider_MirrorTest`를 확장하지 않고 `EquipmentSystem.OnEquipmentChanged`를 구독한다. 현재 장착된 `ItemCategory.Armor`만 reconcile하여 다음 수명을 관리한다.

- `StatThresholdBuffUniqueEffectSO`: `StatThresholdRunner_MirrorTest`를 해당 PlayerContext에 바인딩
- `PassiveBuffUniqueEffectSO`: 효과별 보유 수를 세어 첫 장착/마지막 해제에서만 적용·제거
- `FieldAuraUniqueEffectSO`: 기존 `BuffFieldZone_MirrorTest`를 재사용
- `TriggeredBuffUniqueEffectSO`: 발동은 기존 `ItemTriggerManager_MirrorTest`에 맡기고 장착 복원·해제 정리만 수행

Threshold 해제 시에는 `Destroy` 지연을 기다리지 않고 효과를 먼저 제거해 장비 변경 직후 잔류하지 않게 했다.

### 검증기

`Assets/Editor/ArmorRelicP3BValidation_MirrorTest.cs`의 메뉴:

`SW/Mirror Test/Validate Armor Relic P3-B H3`

실제 SW Fighter PlayerContext 프리팹을 복제하고 메모리 H3 투구 정의를 장착해 25%/26%/0%/해제와 `mpRegen` 10→13→10을 확인한다.

## 3. 현재 차단과 미검증

현재 열린 Unity 6000.3.22f1 Editor의 재컴파일 결과는 기존 사용자 작업 파일의 다음 오류 하나로 실패한다.

`Assets/SW/Scripts/Enemy/Telegraph/BossAttackTelegraphPreview.cs(14,12): CS0246 TelegraphDirection`

이 파일과 보스 인디케이터 작업은 P3-B 범위가 아니므로 수정하지 않았다. 해당 오류가 해결되어 Assembly-CSharp가 다시 로드된 뒤 다음 순서를 수행해야 한다.

1. 양 PlayerNetwork 프리팹에 `PlayerArmorEffectProvider_MirrorTest`를 Unity Editor로 추가하고 참조를 확인한다.
2. 위 H3 검증 메뉴를 실행해 PASS 수를 기록한다.
3. 실제 `EquipmentTransaction` 장착·교체·해제와 드랍/거래/사망/새 런 수명을 추가 확인한다.
4. 원천 Excel→JSON→SO→ItemDefinitionSO에 확정된 H3 itemId/effectId를 연결한다.

원격 Client·4인·정식 Act1 플레이는 이 초안에서 완료로 표시하지 않는다. B1/R2는 H3의 기준선 검증 뒤 별도 범위로 선택한다.
