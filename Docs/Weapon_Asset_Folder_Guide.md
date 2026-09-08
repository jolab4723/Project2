# 무기·발사체 자산 폴더

2026-09-07 폴더 정리 기준. Fighter와 Gunner는 파일명으로 구분하며 제작 순서나 수정 이력에 따른 폴더를 만들지 않는다.

| 자산 | 저장 폴더 |
| --- | --- |
| 무기 FBX | `Assets/SW/Models/Weapons` |
| 무기 재질 | `Assets/SW/Models/Weapons/Materials` |
| 무기 텍스처 | `Assets/SW/Models/Weapons/Textures` |
| 무기 Unity Mesh | `Assets/SW/Models/Weapons/Meshes` |
| 발사체·효과 FBX | `Assets/SW/Models/ProjectileVisuals` |
| 발사체·효과 재질 | `Assets/SW/Models/ProjectileVisuals/Materials` |
| 발사체·효과 텍스처 | `Assets/SW/Models/ProjectileVisuals/Textures` |
| 발사체·효과 Unity Mesh | `Assets/SW/Models/ProjectileVisuals/Meshes` |
| 무기 외형 프리팹 | `Assets/SW/Prefabs/Equipment/WeaponVisuals` |
| 발사체·부품 프리팹 | `Assets/SW/Prefabs/Equipment/ProjectileVisuals` |
| 총구 효과 프리팹 | `Assets/SW/Prefabs/Equipment/MuzzleVisuals` |
| 명중 효과 프리팹 | `Assets/SW/Prefabs/Equipment/ImpactVisuals` |

- 새 파일은 가능하면 `itemId`를 사용한다. `Weapon.mat`, `Body.fbx`처럼 이름이 겹치면 무기 ID 또는 `Shared_Electric`, `Shared_Fire` 등 효과 종류를 붙인다.
- 같은 이름의 최종 FBX와 보존 원본은 원본에 `_Source`를 붙인다. 비교·참조용 발사체는 `Reference_`로 구분한다.
- `Batch10/11/12`, `Repairs`, `Astra20260907`, 무기별·검수 단계별 중간 폴더를 다시 만들지 않는다. `.blend`와 중간 렌더는 프로젝트 밖에 둔다.
- 기존 프리팹·SO·카탈로그·Addressables 연결은 GUID로 보존한다. 경로를 바꿀 때 `.meta`를 새로 만들지 않는다.
- `ProjectileVisualPalettePreview`는 일부 재질의 내부 이름을 읽는다. `Reference_GunnerProjectileRing.mat`의 내부 이름 `GunnerProjectileRing`을 파일명과 함께 바꾸지 않는다.
- 적·UI·인벤토리와 다른 용도의 모델 폴더는 이 정리 범위에 포함하지 않는다.

정리 시점의 이동 목록과 전후 검증은 프로젝트 밖 `Project2_BlenderWork/GunnerRemaining_20260907/folder_reorganization`에 보관했다. 과거 작업 기록에 남은 경로는 해당 이동 목록의 GUID로 현재 경로를 찾는다.
