# Tripo API·웹 무기 제작·그립 전달 가이드

이 문서는 Codex가 Tripo API로 작업할 때와 팀원이 Tripo 웹에서 직접 작업할 때 함께 사용하는 무기 제작·전달 규격이다. Codex는 저장소의 `AGENTS.md`를 자동 작업 지침으로 적용하고, 그 문서의 Tripo 연결 규칙에 따라 이 가이드를 읽는다. `Docs/**`의 다른 문서까지 무조건 전부 읽는 것은 아니다.

현행 28종을 다시 모델링하기 위한 문서가 아니다. 현행 무기는 손가락·손잡이 형상을 바꾸지 않고 작은 겹침을 허용한 채 장착 트랜스폼만 보정했다. 아래 형상 지침은 공허 수확자, 정비 도구, 코어브레이커 이후의 **새 Tripo 모델에서 큰 장착 오차를 예방**하기 위한 기준이다.

## 가장 중요한 결론

- Tripo 프롬프트는 **손이 잡기 좋은 손잡이 형상**을 만드는 데 사용한다.
- `RightHandGrip`, `LeftHandGrip`, `Muzzle` 같은 Unity/Blender 기준점은 메시 외부의 Empty/Transform 데이터이므로, 프롬프트만으로 정확히 생성·보존된다고 가정하지 않는다.
- 이 프로젝트의 Grip은 손목 피벗이나 손바닥 표면점이 아니라 **각 손이 감싸는 손잡이 중심축 위의 기준점**이다. 런타임이 이 축을 Fighter 손가락 고리 중심에 맞춘다.
- `item.` Fighter 외형은 생성 프리팹에서 애니메이션에 맞게 방향을 뒤집되, root와 두 Grip은 각각 오른손 장착점과 왼손 IK 기준점으로 유지한다. 손잡이의 보이는 위치가 맞지 않을 때만 `Model`을 손잡이 축을 따라 무기별로 보정하고, 중심축은 계속 손가락 고리를 통과하게 한다.
- Blender의 Fighter `+Y` 주축과 Grip 로컬 `+X` 규격은 FBX 좌표 변환 뒤 Unity Inspector에서 다른 Euler 값으로 표시될 수 있다. 이 표시값을 맞추려고 Blender 축을 임의로 바꾸지 않으며, 생성기는 두 Grip의 **위치 벡터**를 사용해 Fighter `+Y` 장착축을 계산한다.
- 모든 무기에 같은 거리 보정을 적용하지 않는다. 모델마다 원본 피벗과 손잡이 장식 구간이 다르므로 실제 Fighter 장착 근접 화면에서 손잡이 끝과 중간 사이의 적절한 파지점을 찾는다. 이 Unity 프리팹 보정을 Tripo 결과나 원본 FBX에 중복 적용하지 않으며, 전설 무기의 직속 오라·글로우처럼 모델과 함께 움직여야 하는 VFX는 같은 거리로 함께 이동한다.
- 따라서 Tripo 결과에 그립이 없어도 생성 실패는 아니지만, **그립 기준점을 추가하고 실제 Fighter 장착을 확인하기 전에는 완성품이 아니다.**
- Gunner FBX에 `RightHandGrip`이 없어 외형 생성기가 메시 경계나 원본 피벗을 `자동 장착점`으로 사용했다면 이는 초깃값일 뿐이다. 실제 방아쇠 손잡이 중심을 다시 지정하고 Gunner 장착을 확인하기 전에는 최종 프리팹으로 승인하지 않는다.
- 프롬프트의 mm 수치는 설계 목표다. 생성형 모델이 치수를 정확히 지킨다고 보장할 수 없으므로 다운로드 뒤 Blender에서 재고, 부족하면 재생성하거나 후처리한다.

## 1. 생성 전에 확정할 내용

1. 최종 `itemId`
2. Fighter/Gunner 구분
3. Greatsword/Blunt/Axe/Gun 구분
4. 무기의 위아래와 진행 방향
5. 손잡이에 장식이 들어가면 안 되는 실제 양손 파지 구간
6. 인벤토리 크기 `itemWidth × itemHeight`
7. 모델·텍스처·아이콘에 공통으로 사용할 최종 파일명 `{itemId}`

Fighter 근접 무기는 모두 양손으로 제작한다. 최종 Unity 전달 시 손잡이에서 날·헤드로 향하는 방향을 `+Y`로 사용하고, 오른손과 왼손이 나란히 들어갈 만큼 길고 곧은 파지 구간을 확보한다.

## 2. Tripo API·웹 공통 프롬프트

아래 대괄호 부분만 무기에 맞게 바꾼다. 배경 이야기보다 화면에 보이는 형상·재질·비율을 우선해서 쓴다.

팀원에게 전달할 때는 프롬프트 위에 다음 작업 카드를 함께 보낸다. 이 값은 Tripo가 메시 안에 새기는 문구가 아니라 Unity 연결 실수를 막기 위한 작업 정보다.

```text
Item ID: [item.weapon...]
Character Class: [Fighter / Gunner]
Weapon Type: [Greatsword / Axe / Blunt / Rifle / Shotgun / GrenadeLauncher]
Handling: Two-handed
Inventory Size: [itemWidth] × [itemHeight]
Required clear grip zone: right + left hand
```

### 양손 대검

```text
Game-ready stylized sci-fi two-handed greatsword, one single centered weapon, isolated. The weapon is perfectly straight and vertically aligned, blade tip at the top and pommel at the bottom. Create one continuous straight cylindrical or softly oval handle. The unobstructed two-hand grip corridor must be about 320 mm long and never shorter than 300 mm, roughly 8 to 10 grip diameters. Grip diameter must stay between 30 and 40 mm and must never exceed 45 mm in any direction. Leave at least 25 mm of clear straight handle between either hand zone and the guard or pommel. No spikes, rings, guards, cables, flanges, finger grooves, or decorations may cross the grip corridor. Decorative pommel geometry must begin only after the clean grip corridor ends. The blade, guard, handle, and pommel must form one coherent weapon with clean readable silhouettes from front, side, and back. [색상], [재질], [핵심 디자인 특징], detailed PBR game asset.
```

### 양손 도끼·둔기·정비 도구

```text
Game-ready stylized sci-fi two-handed [battle axe / hammer / maintenance tool], one single centered weapon, isolated. The main shaft is perfectly straight and vertically aligned, weapon head at the top and butt at the bottom. Reserve a continuous clean two-hand grip corridor about 320 mm long and never shorter than 300 mm, roughly 8 to 10 grip diameters. Use a cylindrical or softly oval grip 30 to 40 mm thick and never over 45 mm in any direction. Leave at least 25 mm of straight clearance before the head-side collar and the butt. No spikes, flanges, rings, cables, secondary guards, finger grooves, or ornaments may enter the grip corridor. Decorative butt or pommel geometry must begin only after the clean grip corridor ends. Keep the heavy head clearly separated from the grip and keep every part physically connected. [색상], [재질], [핵심 디자인 특징], detailed PBR game asset.
```

### 거너 총기

```text
Game-ready realistic sci-fi two-handed [rifle / shotgun / grenade launcher], one single centered weapon, isolated. Use a clear production modeling orientation: muzzle pointing straight forward along +Z and weapon top aligned to +Y. Create a distinct right-hand trigger grip with a clean palm-sized contact area and an unobstructed trigger region. Create a separate straight left-hand support corridor on the fore-end, long enough for one full hand, with its contact center approximately 300 mm forward from the trigger-grip center in final character space. No spikes, cables, magazines, rails, moving parts, or decorations may cross either contact zone. Keep the muzzle opening centered, circular or mechanically symmetric, fully visible, and unobstructed so a Muzzle marker can be placed at its exact center. Keep the stock, receiver, barrel, magazine, trigger grip, and fore-end physically connected with no floating pieces. [색상], [재질], [핵심 디자인 특징], clean readable silhouette from front, side, top, and rear, detailed PBR game asset.
```

### Negative Prompt

웹 화면에 Negative Prompt 입력란이 있으면 아래 문장을 별도로 넣는다.

```text
character, person, hands, fingers, duplicate weapon, crossed weapons, weapon rack, pedestal, ground, background, floating parts, detached pieces, bent handle, curved grip, oversized grip, tiny grip, spikes or ornaments inside the grip zone, guard splitting the grip, blocked muzzle, obstructed trigger, text, logo, watermark
```

## 3. 참조 이미지 사용법

정확한 손잡이 비율이 중요하면 Text-to-3D 단독보다 Image-to-3D 또는 Multi-view를 우선한다. Tripo의 현재 공식 Multi-view 안내는 같은 대상을 찍은 2~4개 뷰를 사용하고, 프레임·조명·크기를 일치시키라고 설명한다.

- 정면·후면·좌우 측면 중 2~4장을 사용하고, 모든 이미지는 같은 무기·비율·크기여야 한다. 상단 형상이 중요하면 상단 뷰를 추가하되 다른 뷰와 축을 일치시킨다.
- 무기를 화면 중앙에 수직으로 세우고 날·헤드는 위, 손잡이 끝은 아래로 둔다.
- 원근이 강한 전투 구도 대신 정사영 제품 시트처럼 만든다.
- 손, 캐릭터, 받침대, 그림자, 오라, 잘린 칼끝을 넣지 않는다.
- 손잡이 전체와 가드·폼멜 사이의 빈 공간이 모든 뷰에서 보여야 한다.
- 측면 이미지에서 손잡이 두께가 과도하게 굵거나 납작하지 않은지 확인한다.

Tripo 공식 프롬프트 안내처럼 주 대상, 형상 특징, 재질, 스타일, 품질 조건을 짧고 구조적으로 쓰고, 제외할 형상은 Negative Prompt로 분리한다.

### Gunner 4-view 이미지 생성 규격

실제 Gunner 프리팹의 양손 파지를 기준으로 한 이미지 생성 규격이다. 아래 프롬프트는 Codex 이미지 생성과 Tripo 웹 스튜디오 Multi-view 입력 이미지 제작에 공통으로 사용한다. 먼저 파지 구간을 재기 쉬운 `LEFT SIDE 90°` 한 장을 만들고 합격한 뒤, 그 이미지를 참조로 첨부해 나머지 세 장을 만든다. 이후 생성에서도 이미 합격한 이미지를 모두 함께 첨부하고 `[VIEW]`만 바꾼다.

```text
Create one production multiview reference PNG of a realistic sci-fi [RIFLE / SHOTGUN / GRENADE LAUNCHER].
Design brief: [COLOR, MATERIAL, SILHOUETTE, AND KEY DESIGN FEATURES].
If approved reference views are supplied, reproduce that exact weapon without redesigning it.

Output requirements:
- Canvas: exactly 2048 × 1024 pixels.
- File format: PNG with a fully transparent background (alpha 0).
- View: [FRONT 0°, muzzle facing the camera / REAR 180° / LEFT SIDE 90° / RIGHT SIDE 270°].
- Orthographic product-reference view with no perspective distortion.
- One complete weapon only, centered, level, and fully inside the canvas.
- No hands, character, stand, floor, shadow, text, labels, floating effects, or detached parts.
- Use realistic game-asset materials and physically plausible connected construction.

Cross-view consistency:
- All four images must depict the exact same physical weapon at the exact same scale and center.
- Receiver, stock, barrel, muzzle, trigger, grips, magazine, seams, colors, decals, and every asymmetric detail must match logically between opposite views.
- Do not mirror text or invent, remove, resize, or relocate parts between views.

Gunner grip-fit requirements:
- In both side views, keep the right-hand trigger grip clearly exposed and its visible thickness at or below 55 pixels.
- Leave enough unobstructed trigger-grip length and clearance for a full gloved hand.
- In both side views, place the center of the clean front support contact area 620 to 680 pixels toward the muzzle from the center of the trigger grip, measured along the weapon's main axis.
- Make the clean front support corridor 160 to 220 pixels long and continuous.
- Do not put magazines, cables, rails, guards, spikes, ornaments, moving parts, or abrupt thickness changes inside either hand-contact area.
- Keep the muzzle opening clear, centered, mechanically symmetric, and unblocked.
- The overall weapon length may vary by weapon type. Fit the complete silhouette inside the canvas without cropping, but do not shorten or stretch the grip-center spacing to normalize the total weapon length.

Preserve the approved first-view design exactly. Change only the camera direction required for [VIEW].
```

프롬프트만으로 치수를 합격 처리하지 않는다. 전체 길이는 무기 종류에 따라 달라도 되며 무잘림만 확인한다. 측면 두 장에서는 방아쇠 손잡이 두께, 두 파지 중심 간 픽셀 거리와 앞손 파지 구간을 실제로 재고, 서로 다른 값이면 Tripo에 넘기기 전에 재생성한다. Tripo 웹 스튜디오에 네 장을 업로드한 뒤에도 아래 `거너 총기` 프롬프트와 Negative Prompt를 함께 사용한다.

## 4. 생성 설정 권장값

- Style: 프로젝트 아트 방향에 맞는 하나의 스타일만 선택
- PBR: 켬
- HD/Detailed Texture: 켬
- Geometry/Face Limit: 원형 디테일을 확인할 수 있는 중간 이상으로 생성하고 Unity 전달 전에 최적화
- Pose: 무기에는 사용하지 않음
- 여러 결과가 나오면 외형보다 먼저 손잡이 형상과 파지 공간이 가장 좋은 결과를 선택

## 5. Tripo 미리보기 합격 기준

- 무기가 한 개만 존재한다.
- 360° 회전에서 날·헤드·손잡이가 물리적으로 연결돼 있다.
- 손잡이 축이 곧고 급격히 꺾이거나 비틀리지 않는다.
- 양손 파지 구간에 두 손을 연속으로 놓을 수 있다.
- 파지 구간 사이에 가드, 링, 스파이크, 케이블, 굵은 장식이 없다.
- 손잡이가 사람 손보다 지나치게 굵거나 가늘지 않다.
- 정면뿐 아니라 측면에서도 손잡이 두께와 여유가 유지된다.
- 떠 있는 파츠와 내부를 관통하는 파츠가 없다.

하나라도 실패하면 Blender에서 억지로 맞추기 전에 프롬프트·참조 이미지를 고쳐 다시 생성하는 편이 낫다.

## 6. 다운로드 후 필수 후처리

### Fighter 근접 무기

1. 무기 root를 하나로 정리한다.
2. 손잡이에서 날·헤드 방향이 Blender `+Y`가 되도록 회전한다.
   - 도끼는 넓은 날 면이 Blender `YZ` 평면에 놓이고 두께 방향이 `X`가 되도록 맞춘다. 정면 검증 카메라는 `±X`, 측면 검증 카메라는 `±Z`에서 본다.
   - `+Y` 장축을 맞춘 뒤 도끼를 `Y`축 주위로 임의 회전하지 않는다. 넓은 날 면이 기울면 Unity 장착 시 외형 생성기가 파지축만 바로잡아도 도끼 머리의 롤 방향은 복구되지 않는다.
3. root 원점을 오른손이 감싸는 **손잡이 중심축 위의 점**에 둔다. 손바닥 표면, 손목 관절, 가드나 폼멜 중심을 원점으로 쓰지 않는다.
   - Unity에서 무기별 장착 외형을 보정하더라도 원본 모델이나 Grip에 그 보정값을 미리 더하지 않는다.
4. root 직속 Empty로 `RightHandGrip`과 `LeftHandGrip`을 모두 만든다.
5. `LeftHandGrip`은 두 번째 손이 감싸는 **동일한 손잡이 중심축 위의 점**에 둔다. 마커의 로컬 `+X`가 손잡이 축과 평행하도록 회전하고, 오른손 root와 `LeftHandGrip` 사이에 가드·링·폼멜이 끼지 않게 한다.
6. Transform을 적용하고 음수·비균일 scale을 남기지 않는다.
7. Camera, Light, 촬영용 오브젝트, 불필요한 Collider를 빼고 FBX로 내보낸다.
8. 빈 Blender 씬에 FBX를 다시 가져와 root와 두 Grip Empty가 보존되는지 확인한다.

### Gunner 총기

1. 방아쇠를 잡는 오른손 위치를 root와 `RightHandGrip` 기준으로 사용한다. 기준점은 방아쇠 구멍, 손목, 손바닥 표면이나 개머리판이 아니라 중지·약지·소지가 감싸는 권총 손잡이의 실제 중심축 위에 둔다.
2. 총구 방향은 Blender `+Z`, 무기 위쪽은 `+Y`로 맞춘다.
3. Project2 Gunner의 최종 캐릭터 공간 양손 파지 중심 거리는 약 `0.297 m`다. 현재 캐릭터 스케일을 반영한 무기 로컬 기준에서는 `RightHandGrip=(0, 0, 0)`, `LeftHandGrip=(0, 0, 약 0.326 m)`를 초깃값으로 사용한다.
4. `LeftHandGrip`은 앞손이 실제로 닿는 포어엔드 중심에 둔다. 메시 형상 때문에 기준 위치를 바꿔야 한다면 실제 Idle·Run·Attack 장착 검증을 다시 수행하고 무기별 값으로 저장한다.
5. `Muzzle` Empty는 총구 끝의 정확한 중심에 두고 로컬 `+Z`가 발사 방향을 향하게 한다.
6. root 직속에 `RightHandGrip`, `LeftHandGrip`, `Muzzle`을 두고 Transform을 적용한다.
7. Camera, Light, 촬영용 오브젝트와 불필요한 Collider를 제외한 뒤 FBX로 내보낸다.
8. 빈 Blender 씬 재임포트에서 세 Empty, `+Z` 총구 방향, `+Y` 위쪽, 크기와 scale을 다시 확인한다.
9. 이 작업은 Empty 보존, 축 정리, Transform 적용, FBX 재임포트 검증이 단순한 Blender를 기본 후처리 도구로 사용한다. 3ds Max를 사용해도 결과 규격은 같아야 하며, 두 도구를 한 무기에 중복 적용하지 않는다.

### Unity

1. ItemTable의 최종 `itemId`, `characterClass`, `weaponType`, `itemWidth`, `itemHeight`를 확정하고 `DataLoader/Item Data Table/0. Run All Steps`로 ItemDefinitionSO를 생성·갱신한다.
2. FBX와 텍스처를 임포트하고 외부 URP/Lit 머터리얼을 source material에 1:1 remap한다.
3. `SW/Equipment/무기 외형 프리팹 생성기`에서 ItemDefinition과 FBX를 연결해 `{itemId}_WeaponVisual.prefab`과 `WeaponVisualCatalogSO` 항목을 만든다.
4. Unity FBX Inspector에서 Blender의 Grip 로컬 `+X`가 예를 들어 `Z=270°`처럼 보이는 것은 좌표계 변환 결과일 수 있다. Euler 숫자만 보고 Empty를 다시 돌리지 말고, 생성 프리팹에서 `RightHandGrip → LeftHandGrip` 위치 벡터가 root의 `+Y`와 평행한지 확인한다.
   - 표준 Fighter 대검의 예시는 `RightHandGrip=(0, 0, 0)`, `LeftHandGrip=(0, 약 0.22282, 0)`이다.
   - 생성 결과가 `(약 -0.22282, 0, 0)`처럼 `-X`로 눕는다면 원본 Blender 축을 바꾸지 않는다. 위치 벡터가 아닌 Grip 회전을 정렬축으로 사용한 구형 생성기인지 먼저 확인한다.
5. 자동 배치와 방향 반전은 초깃값으로 사용한다. 위치 보정이 필요하면 공통 거리를 일괄 적용하지 말고, root를 움직이지 않은 채 무기별 `Model`을 실제 손잡이 중심으로 이동한다. `Model`을 이동·회전·스케일했다면 `LeftHandGrip`, `Muzzle`과 모델 직속 VFX도 같은 모델 상대 위치를 유지하도록 함께 보정한다.
6. 실제 Fighter에 장착해 무기 root에서 뻗는 손잡이 중심축이 오른손 손가락 고리 내부를 통과하고, 손 아래쪽에 짧은 손잡이 여유가 남는지 확인한다. 손목 피벗이나 손바닥 표면 마커 일치만으로 합격 처리하지 않는다.
7. Fighter 무기는 `LeftHandGrip`에서 뻗는 같은 중심축이 왼손 손가락 고리 내부도 통과하고, 두 손 모두 장식이 아닌 깨끗한 파지 구간을 감싸는지 확인한다.
8. Gunner 무기는 실제 Gunner의 Idle·Run·Attack에서 오른손 손바닥과 중지·약지·소지가 방아쇠 손잡이를 감싸는지 확인한다. 손가락이 수신기·탄창·방아쇠울만 통과하거나, 손바닥 표면 마커만 맞고 손잡이와 손 전체가 떨어져 있으면 실패다.
9. Gunner에서 `Model`을 보정한 뒤에는 `LeftHandGrip`이 실제 앞손 접촉면에, `Muzzle`이 실제 총구 끝과 발사 방향에 남아 있는지 다시 확인한다.
10. 정면 한 장만 보지 말고 8방위 × 3고도 24방향과 손 근접 각도를 확인한다.
11. 작은 손가락·손잡이 겹침은 허용한다. 다만 손 전체가 뜨거나, 손이 가드·폼멜·무기 헤드를 잡거나, 손목이 손잡이를 크게 관통하면 실패다.
10. 런타임 보정 저장 시 팀원이 선택한 root와 Model scale이 그대로 보존되는지 확인한다. scale 변경 자체는 허용한다.
11. 인벤토리 원본은 모델링을 위해 처음 GPT Image로 만든 0° 정면 샷을 우선 사용한다.
   - 배경이 있으면 형상과 색을 바꾸지 않는 범위에서 한 번 투명 처리한다.
   - 가장자리 찌꺼기·색 번짐·배경 잔상이 남거나 이를 없애기 위해 반복 보정이 필요하면 해당 투명화를 폐기하고, 동일한 디자인·정면 구도의 투명 배경 이미지를 새로 생성한다.
   - 최종 원본에는 배경·바닥 그림자·프레임 밖으로 번지는 오라가 없어야 하며 무회전·무잘림 상태를 유지한다.
12. 마지막에 `SW/Equipment/인벤토리 무기 아이콘 변환기`로 `OriginalImage/{itemId}.png`를 변환하고 ItemDefinitionSO의 아이콘 연결과 `itemWidth × 128`·`itemHeight × 128` 출력 크기를 확인한다.

## 7. 생성 작업 전달 묶음

API나 웹에서 생성만 담당한 작업자는 다음을 한 묶음으로 전달한다.

- 최종 `itemId`
- Character Class와 양손 규격 확인
- 사용한 Prompt와 Negative Prompt
- 사용한 정면·측면·후면 참조 이미지
- 원본 GLB/FBX와 텍스처
- Tripo 360° 미리보기 캡처
- 빈 Blender 씬 재임포트에서 `RightHandGrip`, `LeftHandGrip`, Gunner의 `Muzzle`이 보존된 계층 확인
- 실제 대상 캐릭터의 Idle·Run·Attack 손 근접 검증 이미지
- 아직 `RightHandGrip`/`LeftHandGrip`을 추가하지 않았다면 그 사실을 명시한다. 이 상태는 후처리 대기 산출물이며 최종 완료품으로 전달하지 않는다.

## 공식 참고 자료

- [Tripo Text-to-3D 프롬프트 엔지니어링 가이드](https://www.tripo3d.ai/blog/text-to-3d-prompt-engineering)
- [Tripo Multi-view to 3D 안내](https://www.tripo3d.ai/blog/multi-view-to-3d)
- [Tripo 내보내기 형식 안내](https://www.tripo3d.ai/tutorials/tripo-ai-export-formats)
