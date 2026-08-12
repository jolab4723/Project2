# Fighter 신규 무기 18종 양손 Grip QA (2026-08-11)

## 최종 결과

- 검증 씬: `Assets/Scenes/Maps/Act1_Maps/Act1_BossStage/Act1_BossStage.unity`
- 검증 캐릭터: `Assets/Resources/Prefabs/Character/Player/Fighter.prefab`
- 기준 17종 증거 세션: `Temp/WeaponGripQA/BossStage_New18_20260811_222016_473`
- `managerriotbaton` 최종 보정 세션: `Temp/WeaponGripQA/BossStage_New18_20260811_231305_851`
- 매니페스트 SHA-256: 기준 세션 `ff2740a802e7363ef7a4fee606261c27ff161c15be0808e44eca3c60190e9585`, 최종 보정 세션 `e1a5aeffb704f252395052249a7be2e8c96b5144914f67e3a319087e41fe5662`
- 결과: **18/18 Pass**, 합격 증거 PNG **432/432**(기준 17종 408장 + 최종 보정 1종 24장), 누락 및 실패 사유 0
- 현재 저장된 프리팹 dependency hash는 기준 17종 및 최종 `managerriotbaton` 매니페스트와 각각 일치한다. `managerriotbaton` 최종 hash는 `bcd40adf187bbb2cb32922b8c56791e2`다.
- 공유 이미지: [Fighter_WeaponGrip_New18_Grid_BossStage_2026-08-11.png](Images/Fighter_WeaponGrip_New18_Grid_BossStage_2026-08-11.png)

## 적용값

모든 값은 Fighter 무기 외형 프리팹 루트 기준이다. 루트 위치·회전·스케일과 손가락/손잡이 형상은 변경하지 않았다. 후속 육안 검수에서 `managerriotbaton`만 손이 무기 중앙을 잡고 있는 문제가 확인되어 `Model.localPosition.y`를 개별 보정했다.

| 분류 | 대상 | 최종 `LeftHandGrip.localPosition` | 최종 `LeftHandGrip.localRotation` | 변경 |
| --- | --- | --- | --- | --- |
| Greatsword | 6종 | `(0, 0.22282, 0)` | `(-0.6625993, -0.6625993, 0.2469054, 0.2469054)` | 기존 합격값 유지 |
| Axe | 6종 | `(0, 0.22282, 0)` | `(-0.6625993, -0.6625993, 0.2469054, 0.2469054)` | 잘못된 Z 90° 회전을 합격 회전으로 교정 |
| Blunt | 6종 | `(0, 0.22282, 0)` | `(-0.6625993, -0.6625993, 0.2469054, 0.2469054)` | Grip 위치 `Y 0.31 → 0.22282`, 회전 교정. `managerriotbaton`만 `Model Y 0.31 → 0.19` 추가 보정 |

### `managerriotbaton` 손잡이 구간 보정

- 방향·루트·Grip 간격은 유지하고 보이는 모델만 축 방향으로 이동했다.
- 최종값은 `Model.localPosition = (0, 0.19, 0)`이다. 이 값에서 오른손은 검은 손잡이의 위쪽 구간, 왼손은 무기 하단 금속 장식 직전의 검은 손잡이를 잡는다.
- 최종 단일 항목 세션은 자동 Pass, 24/24 PNG, 왼손 IK 0.009593mm, 왼손 중심선 0.009464mm, 오른손 중심선 0.000232mm였고 FRONT·좌45°·SIDE를 포함한 방위각 8개 × 고도 3개를 직접 확인했다.

### 대상 Item ID

- Greatsword: `conveyercleaver`, `sharpgreatsword`, `ceremonialsword`, `magneticrailblade`, `simonslastcontract`, `planetdeedbreaker`
- Axe: `emergencyrescueaxe`, `scrapsorter`, `excavatortooth`, `maintenancedronewing`, `antimattersplitter`, `finalinvoice`
- Blunt: `torquewrench`, `managerriotbaton`, `hydraulicpiledriver`, `magneticstormhammer`, `refunddenied`, `lastwarningbell`

## 검수 기준과 수치

- 오른손과 왼손 모두 손잡이 중심선이 실제 손가락 고리 중심을 통과해야 한다.
- 작은 메시 겹침은 허용하지만 손이 뜨거나 손잡이가 고리 밖으로 빠지면 불합격이다.
- 주 손은 손잡이 하단부를 잡되 실제 끝 장식/폼멜 위의 손잡이 구간에 남아야 한다.
- 정면 한 장만 보지 않고 방위각 8개 × 고도 3개, 총 24방향을 전수 확인한다.

| 분류 | 최대 왼손 IK 도달 오차 | 최대 왼손 중심선 오차 | 최대 오른손 중심선 오차 |
| --- | ---: | ---: | ---: |
| Greatsword | 0.009593 mm | 0.009464 mm | 0.000232 mm |
| Axe | 0.010379 mm | 0.010050 mm | 0.000232 mm |
| Blunt | 0.009593 mm | 0.009464 mm | 0.000232 mm |

자동 합격 한계는 IK 도달 3 mm, 손잡이 중심선 2.5 mm이며, 최종 결과는 모두 이보다 충분히 작다. 자동 수치 통과 뒤에도 기준 17종의 기존 24방향 접촉 시트와 `managerriotbaton` 최종 24방향을 직접 확인했다.

## 기각한 시험값

둔기 왼손 간격을 0.17 m로 더 줄인 시험은 왼팔 가동 한계를 넘어 IK 도달 오차 15.5798 mm, 손잡이 중심선 오차 13.4201 mm가 발생해 불합격 처리했다. 이 값은 프리팹과 생성기 어디에도 남기지 않았으며, 최종 자산은 합격한 0.22282 m Grip 간격만 사용한다.

`managerriotbaton`은 `Model Y=0`과 `Y=0.08718`도 24방향으로 시험했다. 두 값은 손이 검은 손잡이를 지나 금속 장식/케이지 안으로 들어가 육안 불합격으로 기각했고, 최종 프리팹과 공유 이미지에는 `Y=0.19`만 남겼다.

## 재발 방지

- `WeaponVisualPrefabGeneratorWindow`의 Fighter 기본 `LeftHandGrip` 회전을 합격한 손바닥 방향으로 교정했다.
- `WeaponGripQaCaptureWindow`에서 신규 18종과 기존 28종을 선택해 동일한 BossStage 24방향 검증을 반복할 수 있게 했다.
- 생성 프리팹을 다시 만들 때 기존 수동 캘리브레이션 보존 옵션을 사용하더라도, 최종 완료 판단은 실제 BossStage 24방향 촬영으로 한다.
