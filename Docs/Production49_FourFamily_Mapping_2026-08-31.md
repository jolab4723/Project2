# Production49 공용 VFX 4계열 — 대상 54종

54개 독립 VFX 세트가 아니라 아래 4계열의 총구(M)·투사체(F)·명중(I)을 재사용한다. 무기별 크기·방향·emitter 위치 조절은 허용하지만 이 표가 실제 Prefab 연결 완료를 뜻하지는 않는다. 전투 코드·SO·카탈로그는 수정하지 않았다. 기존 `Production49` 경로와 GUID는 보존하며 범위 증가 때문에 폴더를 일괄 변경하지 않는다.

Root 직접 검증: `_consolidated/existing_body_flight_reuse_matrix.json`의 명시 49종을 `Assets/Resources/DataFiles/ItemData/2. JSONFile/ItemDataTable.json`의 `weaponDefinitions[].EnchantedElement`와 결합. 중복/누락 0. Importer는 이를 `weaponEnchantElement`로 복사하고, enum은 None/Fire/Ice/Electric 순서다. Scout의 생성 SO 49개 일치 보고는 별도이며, Root의 이번 직접 재검사는 JSON·목록·Importer·enum까지다.

최신 사용자 요청으로 저등급 유탄발사기 5종을 추가했다. Root가 같은 현재 JSON에서 거너 고유 ID 총54개(라이플21/샷건18/유탄15)를 직접 확인했다. 추가5개는 모두 None이며, 아래 일반 유탄 행에 포함한다. 정적 WeaponVisual 및 루트 직속 Muzzle 존재5/5는 확인됐지만 실제 Unity 총열 맞춤/합성 검증은 아직이다.

## 탄체 모델과 VFX 공용 수는 별개

2026-08-31 기존 세션(`01a040ab-9851-7552-a939-f54643cd881b`) 재인계로 누락됐던 탄체 배분을 복구했다. 사용자 지시대로 현재 작업과 충돌하지 않는 배분만 반영한다. **전체54종 = 공용 탄체 사용20종 / 전용 주 실루엣34종**, 공용 기본형은5계열이다. 직전의 `신규5만 확정 / 기존49 미분류` 기록은 재인계 이전 조사 결과로 대체된다. 이는 제작·Unity 최종 승인 완료 수가 아니다.

| 공용 탄체 그룹 | 무기 수 | 무기 ID (`item.weapon.` 생략) |
|---|---:|---|
| 일반탄 | 5 | rifle.dustsparrow, rifle.fieldmod, rifle.massproduced, rifle.rusthound, rifle.whitenoise |
| 정밀탄 | 3 | rifle.nightwarden, rifle.outdoorhunter, rifle.siegeframe |
| 전기탄 | 3 | rifle.arcvector, rifle.bluehorizon, rifle.ioncascade |
| 산탄 | 4 | shotgun.homesafety, shotgun.scrapdrum, shotgun.thermobarrel, shotgun.redlinebreacher |
| 신규 저등급 유탄 | 5 | grenadelauncher.shellcourier, grenadelauncher.bouncebuddy, grenadelauncher.scraphopper, grenadelauncher.clusterpop, grenadelauncher.smartfuse |
| 합계 | 20 | 공용 기본형5계열 |

| 전용 주 실루엣 | 무기 수 | 무기 ID (`item.weapon.` 생략) |
|---|---:|---|
| 개성형 라이플 | 3 | rifle.scrapline, rifle.workspark, rifle.smilesignal |
| 레일·에너지 라이플 | 7 | rifle.antimatterlance, rifle.glassrail, rifle.novalance, rifle.phaseorchid, rifle.railcarbine, rifle.emberline, rifle.redsingularity |
| 실탄·중량 샷건 | 3 | shotgun.goldentwinstar, shotgun.riotpipe, shotgun.conversationstarter |
| 에너지·속성 샷건 | 11 | shotgun.dockbreaker, shotgun.echovault, shotgun.embercoil, shotgun.starforgebreach, shotgun.flamethrower, shotgun.incinerator, shotgun.magmacrusher, shotgun.thundercoil, shotgun.voidbarrage, shotgun.sulbing, shotgun.whiterefrigerant |
| 기존 유탄 | 10 | grenadelauncher.apocalypse, grenadelauncher.halomortar, grenadelauncher.pulsecask, grenadelauncher.sunfallengine, grenadelauncher.worldender, grenadelauncher.gravitywell, grenadelauncher.singularitymortar, grenadelauncher.fireworks, grenadelauncher.snowballfight, grenadelauncher.glasscannon |
| 합계 | 34 | 개별 주 실루엣; 반드시 별도 FBX34개라는 뜻은 아님 |

전용 실루엣에는 Flamethrower처럼 독립 이동하는 VFX 자체가 주형태인 경우도 포함한다. 공용 그룹도 무기별 최종 `{itemId}_ProjectileVisual.prefab`은 별개로 제공해 총54개 wrapper를 유지하고, 메시·재질을 재사용하면서 비례·코어·후류 폭 등 시각 맞춤을 한다. 실제 전투·스탯은 바꾸지 않는다.

신규5용 Halo R15 공용후보 .42m는 실제5무기·10뷰에서 너무 커 Root 크기FAIL이다. 같은10카메라의 transient .12m를 Root가 모두 확인해 크기를 채택했다. 귀가 시점에 새 .12m 영구저장·대표2뷰 검증 코드는 준비/검토만 완료했으며 실제 실행0이다. 이미 제작한 ScrapDrum 등 현재 후보를 이 표 때문에 삭제하거나 되돌리지 않는다. 기존 후보를 해당 공용군의 출발점으로 사용할 수 있는지 검수한다.

### 재인계 충돌 처리

- 현재 채택한 집/학교 작업물과 최신 시각 판정은 유지한다. 과거49종 계획은 누락 배분만 보완하며 자산 롤백 근거가 아니다.
- 일반 냉백색 고정은 복원하지 않는다. 아래 일반/불/얼음/전기 **4공용 VFX**를 유지하며 과거 총구8·명중10계열 또는 무기별 전용 VFX 대량 제작은 재개하지 않는다.
- Flamethrower의 고정 근접 화염 제트는 복원하지 않는다. 총구와 분리되어 날아가는 화염탄 요구가 우선한다.
- 레일 카빈은 한 발 판정을 유지하며 두 시각 emitter의 x±.105/y±.035는 실제 총열에서 재검증한다. root 직속 Muzzle 및 +Z 계약, 시각 전용·외부 원본 보존 규칙은 유지한다.

## VFX 계열 배정

각 이름 앞에는 `item.weapon.`을 붙인다.

| 계열 | 개수 | 무기 ID (접두사 생략) |
|---|---:|---|
| 일반 / None | 12 | grenadelauncher.apocalypse, grenadelauncher.bouncebuddy, grenadelauncher.clusterpop, grenadelauncher.gravitywell, grenadelauncher.halomortar, grenadelauncher.pulsecask, grenadelauncher.scraphopper, grenadelauncher.shellcourier, grenadelauncher.singularitymortar, grenadelauncher.smartfuse, grenadelauncher.sunfallengine, grenadelauncher.worldender |
| 일반 / None | 16 | rifle.antimatterlance, rifle.dustsparrow, rifle.fieldmod, rifle.glassrail, rifle.massproduced, rifle.nightwarden, rifle.novalance, rifle.outdoorhunter, rifle.phaseorchid, rifle.railcarbine, rifle.rusthound, rifle.scrapline, rifle.siegeframe, rifle.smilesignal, rifle.whitenoise, rifle.workspark |
| 일반 / None | 11 | shotgun.conversationstarter, shotgun.dockbreaker, shotgun.echovault, shotgun.embercoil, shotgun.goldentwinstar, shotgun.homesafety, shotgun.redlinebreacher, shotgun.riotpipe, shotgun.scrapdrum, shotgun.starforgebreach, shotgun.thermobarrel |
| 불 / Fire | 6 | grenadelauncher.fireworks, rifle.emberline, rifle.redsingularity, shotgun.flamethrower, shotgun.incinerator, shotgun.magmacrusher |
| 얼음 / Ice | 3 | grenadelauncher.snowballfight, shotgun.sulbing, shotgun.whiterefrigerant |
| 전기 / Electric | 6 | grenadelauncher.glasscannon, rifle.arcvector, rifle.bluehorizon, rifle.ioncascade, shotgun.thundercoil, shotgun.voidbarrage |

일반 합계 39, 전체 합계 54. 일반은 백색 고정이 아니며 무기에 맞는 색을 허용한다. 불꽃·서리·노란 번개와 형태 및 움직임으로도 구분한다. 속성 이름을 무기 이름/기존 색상에서 추론하지 않는다.

추가5개는 사용자 승인에 따라 공용 탄체와 일반 M/F/I를 우선 재사용한다. 구체적인 탄체 채택과 크기는 실제 무기 비교 후 확정한다. 이름/설명에 등장하는 바운스·분열·신관 기능은 이번 시각 자산 작업에서 구현하지 않는다.

| 추가 ID (grenadelauncher. 이하) | 이름 | 등급 | 속성 |
|---|---|---|---|
| shellcourier | 탄두 배달부 | Common | None |
| bouncebuddy | 바운스 버디 | Common | None |
| scraphopper | 고철 호퍼 | Common | None |
| clusterpop | 클러스터 팝 | Advanced | None |
| smartfuse | 스마트 퓨즈 | Advanced | None |

M은 루트 직속 Muzzle에 남고 F는 독립 +Z 진행, I의 +Z는 피격면 바깥이다. Flamethrower도 움직이는 분리형 화염탄이다. 이전 긴 고정 제트는 이 요구의 최종 후보가 아니다. 런타임 발사/이동/명중 연동은 자산 작업 범위 밖이다.

최종 승인은 실제 무기별 Muzzle 맞춤, 공용 M/F/I 합성, 동일 조건 Gunner_Bullet/Fighter_Attack 비교, 반복·동시 재생 및 독립 평가 게이트 이후다. 개인 구현 로그는 전체 자산/Unity 검증 완료 전 갱신하지 않는다.
