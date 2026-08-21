# ITM_WPN_GNR_0013 — 눈싸움시간!

거너용 유탄발사기의 Tripo P1 정식 생성 및 Blender 후처리 산출물이다. Unity `Assets`에는 FBX·텍스처·외부 URP/Lit 머터리얼 6종을 임포트하고 FBX source material을 1:1 remap했으며, 무기 외형 프리팹 생성과 `WeaponVisualCatalogSO` 등록까지 완료했다. 정식 ItemTable SO에는 Gunner 클래스와 3×2 인벤토리 아이콘을 연결했다. 실제 Gunner 장착 보정은 아직 수행하지 않았다.

## 확정 데이터

- 제작 원본 ID: `ITM_WPN_GNR_0013`
- 정식 ItemTable itemId: `item.weapon.grenadelauncher.snowballfight`
- 이름: `눈싸움시간!`
- characterClass: `Gunner (1)`
- weaponType: `유탄발사기 (3)`
- inventory: `3 × 2`
- Blender 축: 총구 `+Z`, 위 `+Y`
- root: 방아쇠를 잡는 오른손 실제 파지 중앙

## Tripo 생성 기록

- 모델: `P1-20260311`
- 방식: `multiview_to_model`, 4면도 직접 업로드
- 옵션: detailed texture, PBR, UV export, face limit 20,000
- taskId: `656ff2e2-d243-48ff-b8b1-4a9ce2688fda`
- 상태: success, progress 100%
- 예상/실제 비용: 60 / 60 credits
- 잔액: 85 → 25 credits, frozen 0 → 0
- 원본 결과: `../Tripo/Downloaded/ITM_WPN_GNR_0013_P1_raw.glb`
- 비용 근거: `../Tripo/generation_cost.json`

P1 입력은 `../Prepared/TripoInput/front.png`, `left.png`, `back.png`, `right.png`의 투명 2048 × 2048 PNG 네 장이다. 좌우 참조의 토끼는 무기 측면에서 90도 프로필이 되도록 정리했다.

## 최종 산출물

- 편집 원본: `ITM_WPN_GNR_0013.blend`
- Unity 전달 후보: `ITM_WPN_GNR_0013.fbx`
- 재현 스크립트: `../build_weapon_blender.py`
- FBX 왕복 검증 스크립트: `../validate_weapon_blender.py`
- 최종 제작 브리프: `final_prompt.txt`
- P1 텍스처: `Textures/ITM_WPN_GNR_0013_Color.png`, `Normal.png`, `ORM.png` — 각 4096 × 4096
- Unity 변환 텍스처: `Textures/ITM_WPN_GNR_0013_MetallicSmoothness.png`, `ITM_WPN_GNR_0013_Occlusion.png`
- Unity 변환 재현 스크립트: `../prepare_unity_textures.py`

## 구조와 기준점

단일 `ITM_WPN_GNR_0013_root` 아래에 본체, 챔버, 토끼와 세 기준점이 직계 자식으로 보존된다.

| 기준점 | root-local 위치 | 용도 |
| --- | --- | --- |
| `RightHandGrip` | `(0.000, 0.000, 0.000)` | 방아쇠 오른손 및 root 원점 |
| `LeftHandGrip` | `(0.000, -0.110, 0.480)` | 전방 보조 손잡이 접촉점 |
| `Muzzle` | `(0.000, 0.140, 0.945)` | 총구 끝 중앙, local `+Z` 발사 방향 |

FBX에는 8 mesh, 27,247 triangles가 있다. Camera, Light, Armature, Collider는 포함하지 않았고 모든 객체 scale은 양의 unit scale이다.

## 챔버·토끼 제작 결과

P1의 뭉개진 불투명 챔버 내부 6,275 faces를 제거하고 아래를 별도 메시로 재구성했다.

- `CoolingChamber_Glass`: 실제 투과도를 가진 `M_IceGlass`
- `Rabbit_Solid`: 별도 고형 토끼 메시, `M_RabbitWhite`
- `Rabbit_Eyes`, `RabbitNose`, `Rabbit_Cheeks`: 얼굴 전용 별도 메시/재질

토끼 얼굴은 총구 방향 `+Z`에만 있다. `QA/rabbit_front.png`에서는 두 눈·코·양쪽 볼이 보이고, `rabbit_left.png`와 `rabbit_right.png`에서는 한쪽 눈만 보이는 프로필이며, `rabbit_rear.png`에는 얼굴 파츠가 없다. 따라서 정면/측면 얼굴이 중복 융합되지 않았다. 토끼 전체 bounds는 챔버 안쪽에서 최소 0.01 Blender unit 여백을 유지한다.

## 재질 이름

- `M_Weapon_P1_Base`
- `M_IceGlass`
- `M_RabbitWhite`
- `M_RabbitEyes`
- `M_RabbitNose`
- `M_RabbitCheeks`

모든 재질은 Blender 원본에서 Principled BSDF 기반이다. FBX 재임포트에서도 이름과 non-null slot이 모두 보존됐다. Unity에서는 같은 이름의 외부 URP/Lit 재질 6종으로 1:1 remap했다. `M_IceGlass`는 별도 투명 얼음 재질이고, `M_Weapon_P1_Base`는 Base Color·Normal·Metallic/Smoothness·Occlusion 맵을 사용한다.

## 검증 결과

- `QA/validation.json`: Blender 원본 구조·축·bounds·QA 렌더 검사 `pass: true`
- `QA/reimport_validation.json`: 완전히 빈 Blender 5.1.1 씬에 최종 FBX를 재임포트한 왕복 검사 `pass: true`
- `QA/unity_texture_conversion.json`: Tripo ORM의 AO·roughness·metallic 채널을 Unity용 Metallic/Smoothness와 Occlusion으로 변환한 검사
- `QA/unity_material_visual_validation.json`: Unity 외부 머터리얼·FBX remap·외형 프리팹·카탈로그·기준점 검사 `pass: true`
- FBX SHA-256은 `QA/reimport_validation.json`에 기록된다.
- QA 렌더: `front.png`, `left.png`, `right.png`, `rear.png`, `isometric.png`
- 토끼 방향 증명: `rabbit_front.png`, `rabbit_left.png`, `rabbit_right.png`, `rabbit_rear.png`
- Unity 머터리얼 QA: `unity_material_overall.png`, `unity_material_chamber_closeup.png`

왕복 검사에서 단일 top-level root, root 직계 기준점, RightHandGrip 원점, Muzzle local `+Z`, 예상 mesh/triangle/material 수, 모든 material slot, 챔버와 토끼 분리, 토끼가 챔버 안에 있음, 얼굴 파츠가 `+Z`에만 있음, 금지 객체 부재, 양의 unit scale을 전부 확인했다.

## Unity 외형 생성 상태와 남은 검증

Unity 6000.3.22의 김성우 영역에 아래 자산을 생성·연결했다.

- FBX: `Assets/SW/Models/Weapons/ITM_WPN_GNR_0013/ITM_WPN_GNR_0013.fbx`
- Base Color: `Assets/SW/Textures/Weapons/ITM_WPN_GNR_0013/ITM_WPN_GNR_0013_BaseColor.png`
- Normal: `Assets/SW/Textures/Weapons/ITM_WPN_GNR_0013/ITM_WPN_GNR_0013_Normal.png`
- ORM: `Assets/SW/Textures/Weapons/ITM_WPN_GNR_0013/ITM_WPN_GNR_0013_ORM.png`
- Metallic/Smoothness: `Assets/SW/Textures/Weapons/ITM_WPN_GNR_0013/ITM_WPN_GNR_0013_MetallicSmoothness.png`
- Occlusion: `Assets/SW/Textures/Weapons/ITM_WPN_GNR_0013/ITM_WPN_GNR_0013_Occlusion.png`
- 외부 머터리얼 6종: `Assets/SW/Materials/Weapons/ITM_WPN_GNR_0013/`
- 외형 프리팹: `Assets/SW/Prefabs/Equipment/WeaponVisuals/item.weapon.grenadelauncher.snowballfight_WeaponVisual.prefab`
- 카탈로그: `Assets/SW/SO/Equipment/WeaponVisualCatalog.asset`
- 정식 ItemDefinitionSO: `Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/item.weapon.grenadelauncher.snowballfight_눈싸움시간!.asset`
- 인벤토리 원본: `Assets/Resources/Images/Item/OriginalImage/item.weapon.grenadelauncher.snowballfight.source.png`
- 인벤토리 Sprite: `Assets/Resources/Images/Item/item.weapon.grenadelauncher.snowballfight.png`

Base Color는 Default·sRGB 활성, Normal은 NormalMap·sRGB 비활성, ORM·Metallic/Smoothness·Occlusion은 Default·sRGB 비활성으로 임포트했다. 기존 외형 생성기의 Gunner 규칙을 적용해 오른손 기준으로 정렬하고 1.00m 기준 길이로 정규화했으며, 프리팹 root 직계 `LeftHandGrip`·`Muzzle`을 생성했다. 8 Renderer·8 material slot·27,247 triangles, 모든 slot non-null, 예상 머터리얼 6종 사용, root unit scale, 세 기준점, 프리팹 GUID와 카탈로그 GUID 일치를 확인했다. 정식 SO의 `characterClass`는 Gunner(1)이며, 투명 측면 원본을 GUID 매핑한 뒤 기존 변환기로 왼쪽 10°·공통 8px 여백을 적용한 384×256 Sprite를 생성해 `icon`에 연결했다. 최종 Unity Console 오류는 0건이다.

외형 프리팹 이름과 `WeaponVisualCatalogSO` 키는 정식 ItemTable itemId `item.weapon.grenadelauncher.snowballfight`로 통일했다. 다음 단계에서는 사용자 지시에 따라 실제 Gunner 양손 장착의 Grip/Muzzle 위치와 정식 맵 조명에서의 얼음 투명도를 검증해야 한다. 적색 부위는 비발광 기계 포인트로 제한했다.
