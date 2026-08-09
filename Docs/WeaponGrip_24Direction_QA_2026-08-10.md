# Fighter 무기 그립 24방향 최종 QA (2026-08-10)

## 최종 결론

- 실제 `Act1_BossStage`의 Fighter와 런타임 `PlayerWeaponVisualPresenter` 경로를 기준으로 검증했다.
- Fighter 내장 `default` 무기와 `WeaponVisualCatalogSO`의 `item.weapon.*` 28종은 서로 다른 대상으로 각각 검증했다.
- 합격 기준은 사용자가 승인한 Ice Crusher 상태다. 양손 손가락이 만드는 고리 내부를 손잡이 중심축이 지나야 하며, 손가락·손잡이 메시를 바꾸지 않고 소량의 겹침은 허용한다.
- 28종 모두 정책 `fighter-finger-ring-center-v4` 자동 판정 `Pass`, 무기당 24방향 이미지 완전 생성, 총 672장 수동 시각 검수를 통과했다.
- 내장 `default`도 최종 링 중심 코드 반영 뒤 별도로 다시 촬영했으며 `Pass`다.
- 무기 루트 또는 `LeftHandGrip`의 위치·회전만 보정했고 스케일은 바꾸지 않았다.

## 검증 환경과 판정 기준

- Scene: `Assets/Scenes/Maps/Act1_Maps/Act1_BossStage/Act1_BossStage.unity`
- Fighter: `Assets/Resources/Prefabs/Character/Player/Fighter.prefab`
- Catalog: `Assets/SW/SO/Equipment/WeaponVisualCatalog.asset`
- 시점: 방위각 8개 × 고도 3개 = 무기당 24개
- 링 중심: 오른손 `hand_R (0.010, 0.080, -0.040)`, 왼손 `hand_L (-0.010, 0.080, -0.040)` 로컬 좌표
- 자동 합격선: 왼손 IK 도달 오차 3.0mm 이하, 양손 손잡이 중심선 오차 각각 2.5mm 이하
- 표면 이격·침투는 기록용 진단값이다. 장식이나 무기 헤드를 잘못 잡은 상태를 수치만으로 합격시키지 않도록 24방향 시각 검수를 필수로 유지한다.
- 캡처 도구는 Scene이나 장비 상태를 저장하지 않았고, 촬영에 사용한 `timeScale`, Animator, 임시 Camera와 Layer를 복원했다.

## 내장 `default` 무기

| 대상 | 판정 | 왼손 IK 도달 오차 (mm) | 왼손 중심선 오차 (mm) | 오른손 중심선 오차 (mm) | 시점 |
|---|---:|---:|---:|---:|---:|
| Fighter built-in `default` | Pass | 0.006 | 0.006 | 0.000 | 24 |

`ShowDefaultVisual()`은 현재 손뼈 축과 오른손 고리 중심으로 내장 무기 루트를 보정하고, 전용 `LeftHandGrip`으로 왼손 IK를 적용한다. 따라서 장비 아이템이 없는 기본 상태도 카탈로그 무기와 같은 기준을 사용한다.

## 카탈로그 28종 결과

단위는 mm다.

| itemId | 판정 | 왼손 IK 오차 | 왼손 중심선 | 오른손 중심선 | 시점 |
|---|---:|---:|---:|---:|---:|
| `item.weapon.greatsword.basic` | Pass | 0.008 | 0.008 | 0.000 | 24 |
| `item.weapon.blunt.treebranch` | Pass | 0.009 | 0.009 | 0.000 | 24 |
| `item.weapon.blunt.mechanicaldestroyer` | Pass | 2.453 | 2.185 | 0.000 | 24 |
| `item.weapon.greatsword.crusader` | Pass | 0.010 | 0.010 | 0.001 | 24 |
| `item.weapon.greatsword.guardiansjustice` | Pass | 0.012 | 0.011 | 0.000 | 24 |
| `item.weapon.blunt.baseballbat` | Pass | 0.010 | 0.010 | 0.000 | 24 |
| `item.weapon.blunt.noentrysign` | Pass | 0.009 | 0.009 | 0.000 | 24 |
| `item.weapon.blunt.ironpipe` | Pass | 0.006 | 0.005 | 0.000 | 24 |
| `item.weapon.blunt.lightsaber` | Pass | 0.011 | 0.010 | 0.001 | 24 |
| `item.weapon.axe.inferno` | Pass | 0.019 | 0.020 | 0.001 | 24 |
| `item.weapon.axe.icecrusher` | Pass | 0.010 | 0.010 | 0.000 | 24 |
| `item.weapon.greatsword.solarblade` | Pass | 0.007 | 0.007 | 0.000 | 24 |
| `item.weapon.greatsword.voltblade` | Pass | 0.011 | 0.011 | 0.001 | 24 |
| `item.weapon.greatsword.zeroblade` | Pass | 0.011 | 0.011 | 0.001 | 24 |
| `item.weapon.axe.heartofdebris` | Pass | 0.011 | 0.011 | 0.000 | 24 |
| `item.weapon.greatsword.wasteheatcleaver` | Pass | 0.011 | 0.010 | 0.000 | 24 |
| `item.weapon.axe.wildfire` | Pass | 0.010 | 0.010 | 0.000 | 24 |
| `item.weapon.blunt.magicstaff` | Pass | 0.010 | 0.010 | 0.000 | 24 |
| `item.weapon.greatsword.rusty` | Pass | 0.007 | 0.007 | 0.000 | 24 |
| `item.weapon.greatsword.nightsword` | Pass | 0.009 | 0.009 | 0.001 | 24 |
| `item.weapon.blunt.thighbone` | Pass | 0.010 | 0.009 | 0.000 | 24 |
| `item.weapon.blunt.superrefrigerant` | Pass | 0.008 | 0.008 | 0.001 | 24 |
| `item.weapon.blunt.morningstar` | Pass | 0.008 | 0.008 | 0.000 | 24 |
| `item.weapon.axe.phaseharvester` | Pass | 0.009 | 0.009 | 0.001 | 24 |
| `item.weapon.greatsword.corebreaker` | Pass | 0.007 | 0.007 | 0.000 | 24 |
| `item.weapon.blunt.shovel` | Pass | 0.009 | 0.009 | 0.000 | 24 |
| `item.weapon.greatsword.arcblade` | Pass | 0.011 | 0.010 | 0.000 | 24 |
| `item.weapon.greatsword.chainsaw` | Pass | 0.008 | 0.008 | 0.001 | 24 |

`Mechanical Destroyer`는 현행 28종 중 오차가 가장 크지만 왼손 중심선 2.185mm, IK 도달 2.453mm로 합격선 안이며 24방향에서도 굵은 손잡이가 손가락 고리 안을 통과한다.

## 이번 최종 재보정 항목

- 루트 축 방향 위치: `phaseharvester`, `baseballbat`, `corebreaker`, `crusader`, `solarblade`, `voltblade`, `zeroblade`
- `baseballbat`: 양손 축은 유지하면서 왼손 손목 롤을 Ice Crusher 기준으로 정렬
- 내장 `default`: 현재 손뼈 축·실제 링 중심을 사용하는 런타임 자동 보정과 왼손 IK 적용
- 그 외 21종도 동일한 24방향·링 중심 기준으로 재검수했으며 추가 이탈이 없었다.

## 증거 파일

- 최종 28종 매니페스트: `Temp/WeaponGripQA/BossStage_20260810_055756_684/manifest.json`
- 매니페스트 SHA-256: `B5812F79670D52524DF3DEAB12AAD1196568F55E4EF9B54D1BB25E120062985A`
- 최종 28종 24방향 시트: `Temp/WeaponGripQA/BossStage_20260810_055756_684/_contact_sheets/`
- 내장 `default` 수치·24장: `Temp/WeaponGripQA/Default_BossStage_20260810_RingV4/`
- 28종 공유 그리드: `Docs/Images/Fighter_WeaponGrip_28Grid_BossStage_2026-08-10.png`
- 내장 `default` 24방향 시트: `Docs/Images/Fighter_DefaultWeapon_Grip_24View_BossStage_2026-08-10.png`

매니페스트는 자동 수치만으로 최종 승인을 주장하지 않도록 `visualReviewRequired=true`, `finalAcceptanceClaimed=false`를 유지한다. 이 문서의 최종 판정은 자동 검사 뒤 672장과 내장 `default` 24장을 직접 시각 검수한 결과다.
