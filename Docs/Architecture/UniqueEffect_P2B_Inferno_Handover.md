# 고유효과 P2-B 인페르노 인계

- 작업일: 2026-09-16
- 브랜치: `codex/unity-6000-3-22-test`
- 상태: **1인 로직·연출 결함 2종·화염 상태이상(Burn DoT) 및 Live Play Mode 실측 완료 (round-05)**
- 다음 단계: P3-A 실제 Gunner 공격형 효과 1종

## 1. 구현 결과

`item.weapon.axe.inferno`에 `UE_InfernoExtraHit`을 연결했다. 실제 Fighter 근접 기본 공격이 살아 있는 대상에 적중한 뒤, 같은 대상에 공격 시작 시점 공격력의 20%를 Fire `DamageCause.Effect`로 한 번 적용한다.

후속타는 치명타가 아니며 원래 직접 피해와 같은 `AttackId`를 사용한다. 직접타가 대상을 죽이면 실행하지 않고, 후속타가 처치하면 P1에서 확정한 기존 처치 귀속·보상 경로가 한 번만 실행된다.

## 2. 책임 경계

- `InfernoExtraHitUniqueEffectSO`: 20% 계수와 표시 설명만 보관한다.
- `ItemTriggerManager_MirrorTest`: 장착 효과, 서버, Direct, 살아 있는 대상, 실제 Fighter 권한 직접 대상이라는 최소 자격만 검사하고 후속 피해를 큐에 등록한다.
- `PlayerCombatAuthority_MirrorTest`: 기존 `directAttackTargets`로 현재 Fighter 기본 공격이 실제 처리 중인 대상을 알려 준다.
- `WBH_CombatResolver_MirrorTest`: P2-A에서 만든 공격 시작 스냅샷과 동기 FIFO 후속 피해 큐를 그대로 사용한다.
- `NetworkEnemyAuthority_MirrorTest`: 기존 피해 이벤트와 처치 보상 단일 경로를 유지한다.

이번 효과만을 위해 `AttackKind`, 공용 효과 인터페이스, Factory, 스케줄러, 새 Manager를 만들지 않았다. `DamageCause.Direct`만 확인하면 총기·기타 직접 피해도 잘못 발동할 수 있으므로, 이미 있는 Fighter 권한 공격의 직접 대상 집합을 동등한 분류 경계로 사용했다.

## 3. 데이터

- ItemTable 원본의 인페르노 행 `uniqueEffectId`: `UE_InfernoExtraHit`
- JSON과 생성 `ItemDefinitionSO`: 같은 ID와 실제 효과 자산 참조로 갱신
- 효과 설명: `근접 기본 공격 적중 시 같은 대상에게 공격력의 {0}% 화염 피해를 추가로 줍니다.`
- 표시 계수: `20`
- 실행 계수: `0.2`

아이템 연결은 ItemTable 원본에서 재현된다. 다만 `InfernoExtraHitUniqueEffectSO` 자체는 현재 EffectPool에 수동 생성한 시범 자산이다. 이 신규 타입을 UniqueEffectTable에서 삭제 후에도 완전 재생성하려면 WJ 담당의 데이터 모델·임포터에 타입 지원이 필요하며, 저장소 규칙에 따라 별도 수정 승인을 받아야 한다.

## 4. 검증 결과

Unity 6000.3.22f1의 연결된 Editor에서 다음을 확인했다.

- 컴파일 성공, 검증 직후 Console Error 0건
- P2-B `InfernoExtraHitValidation_MirrorTest`: **29/29 PASS**
- P2-A `ArcBladeChainLightningValidation_MirrorTest`: **39/39 PASS**
- P1 `MirrorCombatBoundaryValidation_MirrorTest`: **76/76 PASS**

P2-B 검증 항목은 실제 생성 효과와 SW Fighter/적 프리팹 연결, 같은 적의 다중 Collider에서 Direct+Effect 각 1회, 치명 직접타와 비치명 20% 추가타, 공격 중 스탯 변경에도 고정되는 스냅샷, 직접 처치 미발동, 효과 처치 보상 1회, Skill/Effect/DoT 및 Fighter 권한 밖 Direct 미발동이다.

검증기에서 발견한 두 실패는 제품 구현이 아니라 테스트 초기화 문제였다. 치명 피해 스탯은 프로젝트 규칙상 `50 = +50%`인데 처음에 `1.5`를 넣었던 단위를 수정했고, 실제 서버 생명주기에서 자동 연결되는 적 `OnDead → HandleDead`를 검증 프리팹에도 연결한 뒤 전 항목이 통과했다.

## 5. round-05 갱신 및 이번 단계 해결 완료 항목

round-05를 거치며 다음 항목들이 추가로 구현 및 Live Play 실측 검증되었습니다. 상세 내용은 `UniqueEffect_P2_ArcBlade_Inferno_Round05_Handover.md`를 참조합니다.

1. **표시 결함 2종 해결**:
   - `RpcShowDamage` 도입 및 `LateUpdate` 데미지 폴링 제거로 1타당 텍스트 2개 표출 (`PresentedDamageTextCount` 정상 증가, 텍스트 덮어쓰기 소실 제거).
   - `ItemTriggerManager_MirrorTest`의 후속타 성공 콜백(`onResolved`)에서만 `RpcPresentInfernoHit` 발행 및 `UniqueEffectPresentation_MirrorTest`를 통한 파티클/오디오 2초 수명 관리.
2. **화염 속성 상태이상(Burn DoT) 연계 완료**:
   - `PlayerCombatAuthority_MirrorTest`에 `GetStatusEffectForElement` 매핑 헬퍼(`Fire => Burn1`) 도입.
   - 근접 직접 공격 및 인페르노 화염 추가타에 `WBH_StatusEffectPresets.Burn1` 주입.
   - Live Play Mode 더미(HP 10,000)에서 타격 즉시 화상 부여 및 1초 주기 100 피해 DoT 틱 총 5회(500 피해 감소) 실측 검증 완료.

## 6. 후속 유지/검증 부채 범위

- R03-01 다중 Collider 전제 시험, R03-02 처치 수령자 대조 시험 보강
- R03-03 테스트 격리 및 잔여 객체 추적
- Host + 원격 Client 및 4인 Dedicated Server 실기 측정
- 신규 효과 타입의 UniqueEffectTable→JSON→SO 완전 자동 재생성 (WJ 데이터 모델 연계)
- 다음 권장 개발 단계: **P3-A 실제 Gunner 공격형 효과 1종 착수**

기본 Fighter 시작 무기를 아크 블레이드로 둔 테스트 설정은 사용자가 반복 고유효과 테스트를 위해 의도한 변경이므로 유지했다. 인페르노를 직접 플레이 테스트할 때는 해당 장비를 장착하거나 테스트용 기본 무기 ID만 일시적으로 인페르노로 바꾸면 된다.

