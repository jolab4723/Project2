# Tripo 웹 무기 제작·그립 전달 가이드

이 문서는 Codex나 `AGENTS.md`를 읽지 않고 Tripo 웹에서 직접 무기를 만드는 팀원이 사용하는 전달 규격이다.

현행 28종을 다시 모델링하기 위한 문서가 아니다. 현행 무기는 손가락·손잡이 형상을 바꾸지 않고 작은 겹침을 허용한 채 장착 트랜스폼만 보정했다. 아래 형상 지침은 공허 수확자, 정비 도구, 코어브레이커 이후의 **새 Tripo 모델에서 큰 장착 오차를 예방**하기 위한 기준이다.

## 가장 중요한 결론

- Tripo 프롬프트는 **손이 잡기 좋은 손잡이 형상**을 만드는 데 사용한다.
- `RightHandGrip`, `LeftHandGrip`, `Muzzle` 같은 Unity/Blender 기준점은 메시 외부의 Empty/Transform 데이터이므로, 프롬프트만으로 정확히 생성·보존된다고 가정하지 않는다.
- 이 프로젝트의 Grip은 손목 피벗이나 손바닥 표면점이 아니라 **각 손이 감싸는 손잡이 중심축 위의 기준점**이다. 런타임이 이 축을 Fighter 손가락 고리 중심에 맞춘다.
- 따라서 Tripo 결과에 그립이 없어도 생성 실패는 아니지만, **그립 기준점을 추가하고 실제 Fighter 장착을 확인하기 전에는 완성품이 아니다.**
- 프롬프트의 mm 수치는 설계 목표다. 생성형 모델이 치수를 정확히 지킨다고 보장할 수 없으므로 다운로드 뒤 Blender에서 재고, 부족하면 재생성하거나 후처리한다.

## 1. 생성 전에 확정할 내용

1. 최종 `itemId`
2. Fighter/Gunner 구분
3. Greatsword/Blunt/Axe/Gun 구분
4. 한손/양손 구분
5. 무기의 위아래와 진행 방향
6. 손잡이에 장식이 들어가면 안 되는 실제 파지 구간

Fighter 근접 무기는 최종 Unity 전달 시 손잡이에서 날·헤드로 향하는 방향을 `+Y`로 사용한다. 양손 무기는 오른손과 왼손이 나란히 들어갈 만큼 길고 곧은 파지 구간이 필요하다.

## 2. Tripo 웹 복사·붙여넣기 프롬프트

아래 대괄호 부분만 무기에 맞게 바꾼다. 배경 이야기보다 화면에 보이는 형상·재질·비율을 우선해서 쓴다.

### 양손 대검

```text
Game-ready stylized sci-fi two-handed greatsword, one single centered weapon, isolated. The weapon is perfectly straight and vertically aligned, blade tip at the top and pommel at the bottom. Create one continuous straight cylindrical or softly oval handle. The unobstructed two-hand grip corridor must be about 320 mm long and never shorter than 300 mm, roughly 8 to 10 grip diameters. Grip diameter must stay between 30 and 40 mm and must never exceed 45 mm in any direction. Leave at least 25 mm of clear straight handle between either hand zone and the guard or pommel. No spikes, rings, guards, cables, flanges, finger grooves, or decorations may cross the grip corridor. Decorative pommel geometry must begin only after the clean grip corridor ends. The blade, guard, handle, and pommel must form one coherent weapon with clean readable silhouettes from front, side, and back. [색상], [재질], [핵심 디자인 특징], detailed PBR game asset.
```

### 양손 도끼·둔기·정비 도구

```text
Game-ready stylized sci-fi two-handed [battle axe / hammer / maintenance tool], one single centered weapon, isolated. The main shaft is perfectly straight and vertically aligned, weapon head at the top and butt at the bottom. Reserve a continuous clean two-hand grip corridor about 320 mm long and never shorter than 300 mm, roughly 8 to 10 grip diameters. Use a cylindrical or softly oval grip 30 to 40 mm thick and never over 45 mm in any direction. Leave at least 25 mm of straight clearance before the head-side collar and the butt. No spikes, flanges, rings, cables, secondary guards, finger grooves, or ornaments may enter the grip corridor. Decorative butt or pommel geometry must begin only after the clean grip corridor ends. Keep the heavy head clearly separated from the grip and keep every part physically connected. [색상], [재질], [핵심 디자인 특징], detailed PBR game asset.
```

### 한손 무기

```text
Game-ready stylized sci-fi one-handed [weapon type], one single centered weapon, isolated. The weapon is perfectly straight and vertically aligned, head or blade at the top and pommel at the bottom. Create one continuous unobstructed handle at least 110 mm long. Use a cylindrical or softly oval grip 30 to 40 mm thick and never over 45 mm in any direction. Leave at least 20 mm of clear straight handle before the guard and pommel. No spikes, rings, cables, finger grooves, or ornaments may enter the hand grip zone. Keep every part physically connected. [색상], [재질], [핵심 디자인 특징], detailed PBR game asset.
```

### Negative Prompt

웹 화면에 Negative Prompt 입력란이 있으면 아래 문장을 별도로 넣는다.

```text
character, person, hands, fingers, duplicate weapon, crossed weapons, weapon rack, pedestal, ground, background, floating parts, detached pieces, bent handle, curved grip, oversized grip, tiny grip, spikes or ornaments inside the grip zone, guard splitting the grip, text, logo, watermark
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

## 4. 웹 생성 설정 권장값

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
3. root 원점을 오른손이 감싸는 **손잡이 중심축 위의 점**에 둔다. 손바닥 표면, 손목 관절, 가드나 폼멜 중심을 원점으로 쓰지 않는다.
4. root 직속 Empty를 정확한 이름으로 만든다.
   - 한손: `RightHandGrip`
   - 양손: `RightHandGrip`, `LeftHandGrip`
5. `LeftHandGrip`은 두 번째 손이 감싸는 **동일한 손잡이 중심축 위의 점**에 둔다. 마커의 로컬 `+X`가 손잡이 축과 평행하도록 회전하고, 오른손 root와 `LeftHandGrip` 사이에 가드·링·폼멜이 끼지 않게 한다.
6. Transform을 적용하고 음수·비균일 scale을 남기지 않는다.
7. Camera, Light, 촬영용 오브젝트, 불필요한 Collider를 빼고 FBX로 내보낸다.
8. 빈 Blender 씬에 FBX를 다시 가져와 root와 두 Grip Empty가 보존되는지 확인한다.

### Unity

1. `SW/Equipment/무기 외형 프리팹 생성기`에서 ItemDefinition과 FBX를 연결한다.
2. 자동 배치는 초깃값으로만 사용한다.
3. 실제 Fighter에 장착해 무기 root에서 뻗는 손잡이 중심축이 오른손 손가락 고리 내부를 통과하는지 확인한다. 손목 피벗이나 손바닥 표면 마커 일치만으로 합격 처리하지 않는다.
4. 양손 무기는 `LeftHandGrip`에서 뻗는 같은 중심축이 왼손 손가락 고리 내부도 통과하고, 두 손 모두 장식이 아닌 깨끗한 파지 구간을 감싸는지 확인한다.
5. 정면 한 장만 보지 말고 8방위 × 3고도 24방향과 손 근접 각도를 확인한다.
6. 작은 손가락·손잡이 겹침은 허용한다. 다만 손 전체가 뜨거나, 손이 가드·폼멜·무기 헤드를 잡거나, 손목이 손잡이를 크게 관통하면 실패다.
7. 런타임 보정 저장 시 팀원이 선택한 root와 Model scale이 그대로 보존되는지 확인한다. scale 변경 자체는 허용한다.

## 7. 웹 작업자 전달 묶음

웹에서 생성만 담당하는 팀원은 다음을 한 묶음으로 전달한다.

- 최종 `itemId`
- 한손/양손 표시
- 사용한 Prompt와 Negative Prompt
- 사용한 정면·측면·후면 참조 이미지
- 원본 GLB/FBX와 텍스처
- Tripo 360° 미리보기 캡처
- 아직 `RightHandGrip`/`LeftHandGrip`을 추가하지 않았다면 그 사실을 명시한 메모

## 공식 참고 자료

- [Tripo Text-to-3D 프롬프트 엔지니어링 가이드](https://www.tripo3d.ai/blog/text-to-3d-prompt-engineering)
- [Tripo Multi-view to 3D 안내](https://www.tripo3d.ai/blog/multi-view-to-3d)
- [Tripo 내보내기 형식 안내](https://www.tripo3d.ai/tutorials/tripo-ai-export-formats)
