# Production49 공용 VFX 4계열

49개 독립 VFX 세트가 아니라 아래 4계열의 총구(M)·투사체(F)·명중(I)을 재사용한다. 무기별 크기·방향·emitter 위치 조절은 허용하지만 이 표가 실제 Prefab 연결 완료를 뜻하지는 않는다. 전투 코드·SO·카탈로그는 수정하지 않았다.

Root 직접 검증: `_consolidated/existing_body_flight_reuse_matrix.json`의 명시 49종을 `Assets/Resources/DataFiles/ItemData/2. JSONFile/ItemDataTable.json`의 `weaponDefinitions[].EnchantedElement`와 결합. 중복/누락 0. Importer는 이를 `weaponEnchantElement`로 복사하고, enum은 None/Fire/Ice/Electric 순서다. Scout의 생성 SO 49개 일치 보고는 별도이며, Root의 이번 직접 재검사는 JSON·목록·Importer·enum까지다.

각 이름 앞에는 `item.weapon.`을 붙인다.

| 계열 | 개수 | 무기 ID (접두사 생략) |
|---|---:|---|
| 일반 / None | 7 | grenadelauncher.apocalypse, grenadelauncher.gravitywell, grenadelauncher.halomortar, grenadelauncher.pulsecask, grenadelauncher.singularitymortar, grenadelauncher.sunfallengine, grenadelauncher.worldender |
| 일반 / None | 16 | rifle.antimatterlance, rifle.dustsparrow, rifle.fieldmod, rifle.glassrail, rifle.massproduced, rifle.nightwarden, rifle.novalance, rifle.outdoorhunter, rifle.phaseorchid, rifle.railcarbine, rifle.rusthound, rifle.scrapline, rifle.siegeframe, rifle.smilesignal, rifle.whitenoise, rifle.workspark |
| 일반 / None | 11 | shotgun.conversationstarter, shotgun.dockbreaker, shotgun.echovault, shotgun.embercoil, shotgun.goldentwinstar, shotgun.homesafety, shotgun.redlinebreacher, shotgun.riotpipe, shotgun.scrapdrum, shotgun.starforgebreach, shotgun.thermobarrel |
| 불 / Fire | 6 | grenadelauncher.fireworks, rifle.emberline, rifle.redsingularity, shotgun.flamethrower, shotgun.incinerator, shotgun.magmacrusher |
| 얼음 / Ice | 3 | grenadelauncher.snowballfight, shotgun.sulbing, shotgun.whiterefrigerant |
| 전기 / Electric | 6 | grenadelauncher.glasscannon, rifle.arcvector, rifle.bluehorizon, rifle.ioncascade, shotgun.thundercoil, shotgun.voidbarrage |

일반 합계 34, 전체 합계 49. 일반은 백색 고정이 아니며 무기에 맞는 색을 허용한다. 불꽃·서리·노란 번개와 형태 및 움직임으로도 구분한다. 속성 이름을 무기 이름/기존 색상에서 추론하지 않는다.

M은 루트 직속 Muzzle에 남고 F는 독립 +Z 진행, I의 +Z는 피격면 바깥이다. Flamethrower도 움직이는 분리형 화염탄이다. 이전 긴 고정 제트는 이 요구의 최종 후보가 아니다. 런타임 발사/이동/명중 연동은 자산 작업 범위 밖이다.

최종 승인은 실제 무기별 Muzzle 맞춤, 공용 M/F/I 합성, 동일 조건 Gunner_Bullet/Fighter_Attack 비교, 반복·동시 재생 및 독립 평가 게이트 이후다. 개인 구현 로그는 전체 자산/Unity 검증 완료 전 갱신하지 않는다.
