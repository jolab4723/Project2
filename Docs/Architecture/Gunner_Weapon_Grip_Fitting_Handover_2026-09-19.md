# 거너 무기 54종 그립/파지 1:1 세부조정 및 실기 Play Mode 검증 인계

- **작성일**: 2026-09-19
- **작성자 / 담당**: 김성우 / Antigravity (Codex 인계용)
- **브랜치**: `codex/unity-6000-3-22-test`
- **대상**: 거너 무기 총 54종 (소총 21종, 산탄총 18종, 유탄발사기 15종)
- **핵심 상태**: 
  - **버그 원천 해결**: `PlayerWeaponVisualPresenter.cs`의 `FindDescendant` 자식 마커 우선순위 탐색 수정 완료
  - **대표 3종 세부조정 및 에셋 영구 저장 완료**: 스크랩라인, 개조된 라이플, 헤일로 박격포 (기작업 설빙, 폭죽놀이 포함 총 5종 완료)
  - **잔여 49종 전수조사 가이드 및 규격 확립**: 라이플 19종, 산탄총 17종, 유탄발사기 13종

---

## 1. 작업 배경 및 사용자 요구사항

### 1-1. 사용자 피드백 요약
1. **대표 3종 결함 지적**:
   - `item.weapon.rifle.scrapline` (스크랩라인): 피스톨 손잡이 부분 오른손이 완전히 어긋남, 왼손 언더레일 파지 위치 불일치.
   - `item.weapon.rifle.fieldmod` (개조된 라이플): 왼손이 앞쪽 핸드가드를 잡지 못하고 허공에 붕 뜨며 손가락이 펴진 채 처짐.
   - `item.weapon.grenadelauncher.halomortar` (헤일로 박격포): 오른손과 왼손 모두 완전히 어긋남.
2. **전수조사 및 검증 방식 명령**:
   - 일괄적인 단일 규격을 기계적으로 적용하면 무기마다 크기와 형상이 달라 파지가 어긋나므로, 나머지 49종도 전수조사하여 무기 1종당 **전면·좌측면·우측면 3각 스크린샷**을 캡처하여 육안 검토 후 1:1 세부조정을 수행할 것.
   - 실제 들고 있는 모션을 정확히 검증하기 위해 반드시 **Play Mode** 상태에서 진행할 것.
   - 무기 교체 시 이전 무기나 기본 총기(`DefaultGun`)가 남아 겹치는 현상을 방지하고, **단독 1종만 엄격히 활성화**하여 검증할 것.

---

## 2. 핵심 원인 분석 및 규격 한계 (Key Findings)

### 2-1. `FindDescendant` DFS 우선순위 버그 규명 및 수정 (공통 시스템)
- **원인**:
  - `PlayerWeaponVisualPresenter.cs` 내부의 `FindDescendant`가 `GetComponentsInChildren` 방식의 깊이 우선 탐색(DFS)을 수행하고 있었음.
  - 이로 인해 프리팹 루트 직속 자식으로 배치된 정밀 보정 마커(`root/LeftHandGrip`)보다 첫 번째 자식인 `root/Model/LeftHandGrip`(FBX 내부 원본 더미 Empty)을 먼저 반환하여, 프리팹 루트에서 수정한 `LeftHandGrip`의 위치/회전이 런타임에 전혀 반영되지 않던 심각한 원천 버그가 존재했음.
- **수정 내용**:
  - [`Assets/SW/Scripts/Equipment/Visuals/PlayerWeaponVisualPresenter.cs`](file:///c:/Users/user/Desktop/Project2_test/Project2/Assets/SW/Scripts/Equipment/Visuals/PlayerWeaponVisualPresenter.cs)의 `FindDescendant` 메서드 시작 지점에 `Transform directChild = root.Find(objectName); if (directChild != null) return directChild;`를 추가하여 프리팹 루트 직속 마커를 최우선으로 취득하도록 수정 완료.

### 2-2. 거너 왼팔 해부학적/IK 가동 범위(거리 한계) 규격 도출 (매우 중요)
- **측정치**:
  - 거너 캐릭터 왼팔의 최대 물리 가동 길이는 어깨 본(`Arm_L`)부터 손목 본(`hand_L`)까지 정확히 **0.386m (38.6cm)** 임.
- **파지 붕괴 메커니즘**:
  - 무기의 `LeftHandGrip` 월드 위치가 거너의 어깨(`Arm_L`)로부터 **36cm를 초과**하면(기존 자동 세팅에서 45~51cm에 위치했던 경우 다수), `TwoBoneIKConstraint`가 팔을 한계까지 팽팽하게 펴버리며 애니메이션 포즈가 붕괴되고, 손가락이 펴진 채 허공에 처지는 현상이 발생함.
- **표준 피팅 가이드라인**:
  - 모든 거너 무기의 `LeftHandGrip`은 어깨로부터 **33~35cm 이내(무기 로컬 Z = 0.20m ~ 0.26m 부근)**의 총열 하단/핸드가드/언더레일에 배치해야 팔꿈치가 자연스럽게 굽혀진 안정적인 파지 포즈가 형성됨.

### 2-3. 무기 겹침 및 비동기 타이밍 방지
- Addressables 비동기 로딩 스왑 시 `ShowDefaultVisual()`이 실행되어 기본 총(`DefaultGun`)이나 직전 무기가 `WeaponSocket` 하위에 남아 겹쳐 찍히는 문제가 발생함.
- 테스트 시 `WeaponSocket` 하위의 모든 기존 무기 인스턴스를 즉시 파괴하거나 `SetActive(false)` 처리한 뒤, 단독 1종만 활성화하는 엄격한 격리 절차 필수.

---

## 3. 대표 3종(+ 기작업 2종) 정밀 세부조정 결과

### 3-1. `item.weapon.rifle.scrapline` (스크랩라인)
- **오른손 피스톨 그립**:
  - 손잡이 피벗 정렬: `standardLocalRot = (271.414f, 245.681f, 24.312f)`
  - 손잡이 중심 오프셋: `exactGripPos = (-0.008f, 0.028f, 0.078f)` -> `gripOffset = (-0.0262f, -0.0785f, 0.0088f)`
  - 검지손가락이 황금색 방아쇠 레버에 완벽 거치되고 손바닥이 손잡이를 감싸 쥠.
- **왼손 언더레일 파지**:
  - `LeftHandGrip`: `localPos = (-0.02f, 0.12f, 0.26f)`, `localEulerAngles = (0, 0, 0)`
  - 손바닥으로 총열 밑면 직사각형 언더레일을 받치고 엄지가 상단으로 올라오는 완벽한 파지 구현.
- **검증 스크린샷**: `Assets/Screenshots/scrapline_clean_front.png`, `_left.png`, `_right.png`

### 3-2. `item.weapon.rifle.fieldmod` (개조된 라이플)
- **스케일 정규화**:
  - 소총 기준 규격(1.05m) 및 거너 신체 비율에 맞춰 `localScale = (0.82f, 0.82f, 0.82f)` 적용.
- **오른손 피스톨 그립**:
  - 손잡이 및 방아쇠울 완벽 밀착.
- **왼손 핸드가드 파지**:
  - `LeftHandGrip`: `localPos = (-0.02f, 0.14f, 0.24f)`, `localEulerAngles = (15f, 0f, 0f)`
  - 탄창 앞쪽 전방 초록색 핸드가드 레일을 엄지와 손가락으로 감싸 쥐는 전술 C-Clamp 파지 구현.
- **검증 스크린샷**: `Assets/Screenshots/fieldmod_clean_front.png`, `_left.png`, `_right.png`

### 3-3. `item.weapon.grenadelauncher.halomortar` (헤일로 박격포)
- **오른손 손잡이**:
  - `exactGripPos = (-0.005f, 0.025f, 0.065f)` -> `gripOffset = (-0.0235f, -0.0655f, 0.0057f)`
  - 손잡이를 손바닥 중심 링에 도킹하고 검지손가락을 방아쇠에 자연스럽게 배치.
- **왼손 바렐 지지**:
  - `LeftHandGrip`: `localPos = (-0.02f, 0.095f, 0.24f)`, `localEulerAngles = (0, 0, 0)`
  - 총열 아래 황금색 링 후방 바렐 몸통을 손바닥으로 안정감 있게 받쳐 듦.
- **검증 스크린샷**: `Assets/Screenshots/halomortar_clean_front.png`, `_left.png`, `_right.png`

### 3-4. 기작업 완료 무기 (2종)
- `item.weapon.shotgun.sulbing` (설빙 샷건)
- `item.weapon.grenadelauncher.fireworks` (폭죽놀이 유탄발사기)

---

## 4. 잔여 49종 전수조사 현황 및 인계 로드맵

현재 거너 무기 총 54종 중 5종 완료, **잔여 49종** 대상:

| 무기군 | 총 수량 | 기작업 완료 | 잔여 수량 | 대상 아이템 ID 목록 예시 |
|---|---|---|---|---|
| **라이플 (Rifle)** | 21종 | 2종 (`fieldmod`, `scrapline`) | **19종** | `glassrail`, `laser`, `massproduced`, `plasma`, `railgun`, `sniper` 등 |
| **산탄총 (Shotgun)** | 18종 | 1종 (`sulbing`) | **17종** | `breacher`, `doublebarrel`, `magma`, `sawedoff`, `tactical` 등 |
| **유탄발사기 (GrenadeLauncher)** | 15종 | 2종 (`fireworks`, `halomortar`) | **13종** | `cluster`, `cryo`, `micro`, `quad`, `vortex` 등 |

### 4-1. 전수조사 작업 절차 (Codex 후속 작업용)
1. **Play Mode 진입 및 단독 활성화**:
   - `Assets/SW/Scenes/Act1_Camp_MergeTest.unity` 씬을 Play Mode로 실행.
   - `Player.WeaponSocket` 하위의 모든 자식 오브젝트를 제거/비활성화.
   - 대상 프리팹을 단독으로 인스턴스화하여 `WeaponSocket`에 도킹.
2. **무기 기하학적 분석 및 1:1 피팅**:
   - 무기의 메시 버텍스 Bounds를 분석하여 피스톨 그립의 중심점과 방아쇠 위치 추출.
   - 오른손 그립 오프셋(`gripOffset`)을 도킹하여 방아쇠울에 검지손가락 안착.
   - `LeftHandGrip`을 무기 하부(Z = 0.20m ~ 0.26m)의 핸드가드/언더레일 지점에 배치.
3. **3각 뷰 스크린샷 캡처 및 육안 검토**:
   - 메인 카메라 또는 검증 전용 카메라로 전면(Front), 좌측면(Left), 우측면(Right) 캡처.
   - 스크린샷 저장 경로: `Assets/Screenshots/{weaponId}_clean_front.png`, `_left.png`, `_right.png`.
   - 오른손 손잡이 관통/이탈 여부, 왼손의 레일 파지 및 팔꿈치 굽힘 자연스러움 검토.
4. **프리팹 에셋 영구 저장**:
   - 조정된 `localPosition`, `localRotation`, `localScale` 및 `LeftHandGrip` 트랜스폼을 프리팹 에셋에 저장 (`PrefabUtility.SaveAsPrefabAsset`).

---

## 5. 변경 파일 목록

- **공통 런타임 스크립트**:
  - `Assets/SW/Scripts/Equipment/Visuals/PlayerWeaponVisualPresenter.cs` (`FindDescendant` 최우선 탐색 수정)
- **프리팹 에셋 (영구 보정 저장)**:
  - `Assets/SW/Prefabs/Equipment/Weapons/Visuals/item.weapon.rifle.scrapline_WeaponVisual.prefab`
  - `Assets/SW/Prefabs/Equipment/Weapons/Visuals/item.weapon.rifle.fieldmod_WeaponVisual.prefab`
  - `Assets/SW/Prefabs/Equipment/Weapons/Visuals/item.weapon.grenadelauncher.halomortar_WeaponVisual.prefab`
- **검증 스크린샷 산출물**:
  - `Assets/Screenshots/scrapline_clean_front.png`, `_left.png`, `_right.png`
  - `Assets/Screenshots/fieldmod_clean_front.png`, `_left.png`, `_right.png`
  - `Assets/Screenshots/halomortar_clean_front.png`, `_left.png`, `_right.png`

---

## 6. 절대 엄수 원칙 (Constraints)
- **거너 캐릭터 본체 수정 절대 금지**:
  - 거너 프리팹, 리그, 본 계층 구조(`hand_R`, `WeaponSocket` 등), `TwoBoneIKConstraint` 파라미터는 일절 수정하지 않음.
  - 모든 맞춤은 **각 무기 프리팹 내부의 루트 트랜스폼 보정값과 `LeftHandGrip` 마커**로만 해결해야 함.
