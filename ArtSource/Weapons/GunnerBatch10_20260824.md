# Gunner 무기 우선 제작 10개 배치 (2026-08-24)

## 공통 규격

- 모든 최종 ID와 폴더명은 `item.weapon.{rifle|shotgun|grenadelauncher}.{slug}`를 사용한다.
- 프로젝트 참조 이미지와 Tripo H3 입력은 방향별 2048×1024 RGBA PNG를 사용한다. 생성 원본의 종횡비가 다르면 자르거나 늘리지 않고 비율 유지 `scale-to-fit` 뒤 투명 여백으로 중앙 정렬한다.
- API 뷰 순서는 `front`, `left`, `back`, `right`다.
- Tripo H3는 `multiview_to_model`, `model_version=v3.1-20260211`, `geometry_quality=standard`, texture/PBR/export UV 활성으로 고정한다.
- 기존 성공 기록 기준 예상 비용은 모델당 30 credits, 10개 총 300 credits다. 제출 직전 실제 잔액과 예상 비용이 다르면 중단한다.
- API 제출은 잔액 가드 충돌과 중복 과금을 막기 위해 부모 에이전트 승인 후 순차 실행한다.
- Blender 기준은 총구 `+Z`, 위쪽 `+Y`, 단일 root, root 직속 `RightHandGrip`, `LeftHandGrip`, `Muzzle`이다.
- `RightHandGrip`은 실제 방아쇠 손잡이 중심이며 root 원점이다.
- 실제 캐릭터 공간 양손 중심은 약 0.297m, 무기 로컬 `LeftHandGrip` 초깃값은 `(0, 0, 0.32625)`다. 마커를 공중에 맞추지 말고 실제 포어엔드 접촉면과 Gunner 장착을 기준으로 개별 검증한다.
- 렌더 재질은 현실적으로 표현하되 실루엣과 작동 구조는 명백한 허구의 게임용 SF 장비여야 한다. 현실 총기의 리시버·실탄 탄창·작동 구조를 그대로 닮은 입력은 사용하지 않는다.
- `솔라 블레이드`와 사용자 레퍼런스처럼 큰 주 실루엣을 단순하게 읽히게 하고, 핵심 코어·레일·총구 등 콘셉트 요소 1~2개에 집중한다. 울퉁불퉁한 외장, 반복 볼트, 케이블, 드럼과 기능 없는 부속을 등급 표현 수단으로 남발하지 않는다.
- 흰색·청색을 공통 팔레트로 강제하지 않는다. 각 무기의 이름, 타입, 등급과 콘셉트에 맞는 고유 팔레트를 유지하며 Gunner와의 조화는 손 비례, 패널 밀도, 실루엣과 에너지 표현 방식으로 맞춘다.
- 발광은 참조 이미지부터 별도 홈·튜브·코어처럼 물리적 경계가 선명한 연속 표면으로 설계한다. 발광색은 일반 도장면에 재사용하지 않고, 부드러운 오라·배경 번짐·떠 있는 발광 파츠는 입력에 넣지 않는다.
- Tripo 출력에 실제 emissive texture/factor가 있으면 보존한다. 색만 Base Color에 구워졌으면 메시를 억지로 분리하지 말고 고유 발광색과 UV 경계를 이용해 정렬된 Emission Map을 복원한다. 원본 형상과 무관한 발광 메시를 추가하지 않는다.
- Emission Map 복원은 발광부에서 샘플링한 정확한 sRGB RGB와 작은 허용 오차만 사용하고, 같은 색이 다른 표면에도 있으면 해당 UV 섬 또는 최소 구역으로 한정한다. 정확한 색 선택으로 해결되면 팽창·블러·레이캐스트 같은 추가 처리는 하지 않는다.
- H3 원본의 triangle 수가 게임 전달 수준을 넘으면 원본을 보존한 복제본에서 단계적으로 최적화하고, 실루엣·Grip·Muzzle·발광 UV 경계가 유지되는 최저 triangle 결과를 채택한다. 전후 수치를 검증 기록에 남긴다.
- Tripo `2008` 정책 거절은 같은 입력을 반복 제출하지 않는다. 비용과 frozen 상태를 확인한 뒤 현실 총기 유사 요소를 제거한 허구 디자인으로 다시 만들고 부모 승인부터 다시 받는다.
- 하위 에이전트는 `ArtSource/Weapons/{itemId}/**`만 소유하며 ItemTable, Unity Scene/Prefab, Catalog, 공용 문서와 다른 담당 폴더를 수정하지 않는다.

## 제작 목록과 소유권

| Pair | itemId | 표시명 | 타입 | 등급 | 디자인 방향 |
|---|---|---|---|---|---|
| 1 | `item.weapon.rifle.scrapline` | 스크랩라인 | Rifle | Common | 재생 합금·검정 파지부·절제된 안전색의 단순한 허구 에너지 카빈 |
| 1 | `item.weapon.grenadelauncher.worldender` | 월드 엔더 | GrenadeLauncher | Legendary | 암적색·다크 브론즈의 단순한 공성 코어 발사기, 넓고 명확한 총구 |
| 2 | `item.weapon.shotgun.riotpipe` | 리엇 파이프 | Shotgun | Common | 검은 합금과 갈색 파지부의 단순한 허구 충격파 산탄 장비 |
| 2 | `item.weapon.grenadelauncher.singularitymortar` | 특이점 박격포 | GrenadeLauncher | Legendary | 흑연·보라색 장갑과 밀폐형 에너지 챔버를 가진 미래형 박격포 |
| 3 | `item.weapon.rifle.railcarbine` | 레일 카빈 | Rifle | Advanced | 네이비 건메탈과 제한된 청록 포인트의 소형 자기 레일 카빈 |
| 3 | `item.weapon.grenadelauncher.gravitywell` | 중력 우물 | GrenadeLauncher | Unique | 흑색·보라색, 수신기 위쪽에 연결된 원형 중력 코어가 있는 발사기 |
| 4 | `item.weapon.shotgun.thermobarrel` | 열압 산탄 | Shotgun | Advanced | 건메탈·구리색의 봉인형 열 챔버와 단순한 열차폐 외피를 가진 산탄 장비 |
| 4 | `item.weapon.rifle.antimatterlance` | 반물질 랜스 | Rifle | Unique | 검정·아이보리·자홍 코어의 단순한 장거리 에너지 랜스, 연결된 이중 장갑 총열 |
| 5 | `item.weapon.shotgun.flamethrower` | 불꽃 분사기 | Shotgun | Rare | 기존 ItemTable 항목. 적흑색의 일체형 화염 코어와 케이블 없는 단순 외피 |
| 5 | `item.weapon.rifle.novalance` | 노바 랜스 | Rifle | Rare | 백색·청색 장갑과 수신기 위 에너지 척추를 가진 정밀 플라즈마 소총 |

## 1차 10개 결과

- 10개 모두 Tripo H3 생성과 Unity 데이터 연결까지 진행했지만 오늘 최종 채택은 8개다. `railcarbine`은 장착 방향과 손 위치는 통과했지만 발광 마스크가 여전히 부자연스러워 내일 정확한 청록 RGB만 다시 추출한다. `worldender`는 총구 외형이 유실된 실패 모델이므로 현재 Unity 자산을 완료품으로 취급하지 않으며, 내일 네 방향 이미지부터 새로 제작해 교체한다.
- 실제 Tripo 비용은 모델당 30 credits, 총 300 credits였다.
- 기존 Fighter/Gunner 공용 손 리그와 캐릭터 프리팹은 수정하지 않았다. 현재 Gunner 프리팹에서 신규 외형 root만 `(-0.031290, -0.012951, 0.053416)`로 보정해 방아쇠 손잡이 중심을 오른손 손가락 안에 배치했다. 이 값은 현재 Gunner 리그 전용 Unity 프리팹 보정값이며 Blender 원본 root나 다른 캐릭터에 굽지 않는다.
- `LeftHandGrip`은 모델마다 실제 포어엔드 접촉면에 다시 맞췄다. Presenter가 이 접촉점을 `LeftHandIKTarget`의 손뼈 위치로 역산하므로, IK Target과 Grip의 좌표가 직접 같아야 하는 것은 아니다.
- 반물질 랜스의 발광은 흰색이 아니라 자홍색 HDR 발광으로 복원했다. 레일 카빈은 `Model Y=180°`로 방향을 바로잡았지만, 청록 레일의 Emission Map을 이어 붙인 결과는 사용자 검수에서 부자연스러운 것으로 판정되어 최종 채택하지 않았다. 내일 열압 산탄과 같은 정확한 RGB 추출 방식으로 다시 만든다. 열압 산탄은 실제 발광 표면색 `sRGB (204, 119, 27)`만 두 UV 구역에서 마스크에 추가해 양쪽 원형 코어와 직선 홈의 검은 결손을 복구하고 주황 HDR tint를 적용했다. 불꽃 분사기와 노바 랜스는 잘못 적용됐던 전체 모델 회전을 제거해 총기 자세로 되돌렸다.
- `worldender` 재생성 H3 원본도 총구 축방향 열린 깊이가 `0.000972m`로 최소 기준 `0.08m`에 못 미쳤고 24방향 × 3깊이 방사형 검사가 `[10, 24, 24]`로 실패했다. Blender 복구나 Unity 교체를 중단했으며, 원인은 깊은 환형 총구가 측면에서 자체 가림되고 축방향 암부가 막힌 판으로 해석된 것으로 정리했다.
- 이전 리엇 파이프처럼 손잡이가 손가락에서 완전히 벗어난 상태는 실패다. 마커나 손목 피벗 수치가 맞아도 손가락 안에 실제 손잡이 메시가 없으면 합격 처리하지 않는다.

### 실제 Gunner 런타임 재장착 검수

아래 값은 저장된 Addressables 프리팹을 새 Gunner 인스턴스에 다시 로드해 측정한 결과다. `RH`는 오른손 실제 grip-center와 외형 root의 거리, `LH`는 `LeftWeaponPalmContact`와 외형 `LeftHandGrip`의 거리, `Muzzle dot`은 총구 `+Z`와 발사 방향의 내적이다.

| itemId | RH 오차 | LH 오차 | Muzzle dot | 방향 보정 | 판정 |
|---|---:|---:|---:|---|---|
| `item.weapon.rifle.scrapline` | 3.8 mm | 1.9 mm | 0.990 | 없음 | 통과 |
| `item.weapon.grenadelauncher.worldender` | 3.8 mm | 1.5 mm | 0.988 | 없음 | 장착 수치 통과 / 메시 외형 실패 |
| `item.weapon.shotgun.riotpipe` | 3.8 mm | 1.8 mm | 0.980 | 없음 | 통과 |
| `item.weapon.grenadelauncher.singularitymortar` | 3.8 mm | 0.9 mm | 0.956 | 없음 | 통과 |
| `item.weapon.rifle.railcarbine` | 3.8 mm | 2.4 mm | 0.989 | `Model Y=180°` | 장착 통과 / 발광 재작업 대기 |
| `item.weapon.grenadelauncher.gravitywell` | 3.8 mm | 1.8 mm | 0.983 | 없음 | 통과 |
| `item.weapon.shotgun.thermobarrel` | 3.8 mm | 1.2 mm | 0.983 | 없음 | 통과 |
| `item.weapon.rifle.antimatterlance` | 3.8 mm | 1.1 mm | 0.991 | 없음 | 통과 |
| `item.weapon.shotgun.flamethrower` | 3.8 mm | 1.0 mm | 0.991 | 없음 | 통과 |
| `item.weapon.rifle.novalance` | 3.8 mm | 1.0 mm | 0.993 | 없음 | 통과 |

Gunner 상태 검수에서는 `Locomotion`과 `Attack`에서 양손 파지가 유지되고, `Dodge`, `Hit`, `Dead`에서는 왼손 IK가 해제되는 것을 확인했다. 최종 화면 검수는 총기 방향, 양손 접촉, 총구, 콘셉트별 발광색을 함께 본다.

## 후속 제작 계획: 신규 40개 + `worldender` 교체

아래 40개는 사람이 최종 판단하기 위한 후보이며 아직 ItemTable이나 Tripo에 제출하지 않는다. 기존 1차 10개와 합치면 50개가 되고, 전체 타입 수는 Rifle 17 / Shotgun 17 / GrenadeLauncher 16이다. 내부 10개와 팀원 웹 제작 20개는 정했으며 마지막 10개는 담당 미확정이다. `worldender`는 새 ID가 아니라 1차 항목의 실패 교체 작업이므로 50개 집계에는 한 번만 포함한다.

### 다음 작업에서 내부 제작할 10개

아래 신규 10개와 별도로 두 건을 먼저 바로잡는다.

- `item.weapon.grenadelauncher.worldender`: 정면·후면·좌우 이미지 네 장을 처음부터 다시 만들고 H3 원본 총구 검사를 먼저 통과시킨다. 깊은 환형 총구 대신 네 방향에서 내부·외부 벽이 명확히 읽히는 단순한 열린 공성 총구로 재설계하며, 실패 원본과 이미지는 재사용하지 않는다.
- `item.weapon.rifle.railcarbine`: Base Color에서 실제 청록 발광 RGB를 다시 샘플링하고 해당 UV 구역 안의 같은 색만 Emission Mask로 만든다. 현재의 연속 구간 보완 결과는 재사용하지 않으며 양쪽 측면에서 삐져나옴·끊김·흐려짐이 없는지 다시 확인한다.

| itemId | 표시명 | 타입 | 등급 | 콘셉트와 주 팔레트 | 발광 |
|---|---|---|---|---|---|
| `item.weapon.rifle.dustsparrow` | 모래 참새 | Rifle | Common | 웜 그레이·탁한 머스터드, 가는 일체형 펄스 카빈 | 없음 |
| `item.weapon.shotgun.dockbreaker` | 부두의 철퇴 | Shotgun | Common | 차콜·세이프티 오렌지, 넓고 단순한 충격파 총구 | 없음 |
| `item.weapon.grenadelauncher.pulsecask` | 펄스 챔버 | GrenadeLauncher | Advanced | 슬레이트·코발트, 둥근 밀폐형 펄스 챔버 | 청록, 챔버 안쪽만 |
| `item.weapon.rifle.glassrail` | 유리빛 궤도 | Rifle | Advanced | 스모크 실버·아주르, 투명 덮개 안의 단일 에너지 도관 | 청색, 도관만 |
| `item.weapon.shotgun.embercoil` | 잔불 고리 | Shotgun | Rare | 무광 흑색·구리, 열차폐 안에 잠긴 짧은 코일 | 주황빛 적색, 코일만 |
| `item.weapon.grenadelauncher.halomortar` | 헤일로 박격포 | GrenadeLauncher | Rare | 짙은 인디고·앤티크 골드, 하나의 밀폐형 환형 투사기 | 보라색, 환형 홈만 |
| `item.weapon.rifle.phaseorchid` | 허공에 핀 난초 | Rifle | Unique | 다크 제이드·검정, 총구 뒤에 모인 절제된 꽃잎형 장갑 | 자홍색, 장갑 사이 채널만 |
| `item.weapon.shotgun.echovault` | 에코 챔버 | Shotgun | Unique | 흑연·딥 틸, 직선형 공명실과 매끈한 외피 | 청록, 공명실만 |
| `item.weapon.grenadelauncher.sunfallengine` | 일식 기관 | GrenadeLauncher | Legendary | 옵시디언·브라스, 큰 한 덩어리 태양 코어 발사기 | 태양빛 호박색, 코어와 총구 홈 |
| `item.weapon.shotgun.starforgebreach` | 스타 브리처 | Shotgun | Legendary | 다크 크림슨·텅스텐, 납작한 제련 코어와 넓은 총구 | 금빛 적색, 제련 코어만 |

### 팀원 웹 제작용 20개

| itemId | 표시명 | 타입 | 등급 | 콘셉트와 주 팔레트 | 발광 |
|---|---|---|---|---|---|
| `item.weapon.rifle.tinwasp` | 양철 말벌 | Rifle | Common | 아연 회색·올리브, 짧고 가벼운 단일 실루엣 | 없음 |
| `item.weapon.shotgun.streetbell` | 골목의 경종 | Shotgun | Common | 다크 틸·검정 고무, 종 모양의 단순 충격파 총구 | 없음 |
| `item.weapon.grenadelauncher.driftpod` | 유랑 포드 | GrenadeLauncher | Common | 샌드 그레이·먹색, 둥근 포드형 몸체와 깨끗한 포어엔드 | 없음 |
| `item.weapon.grenadelauncher.stonehopper` | 현무암 도약포 | GrenadeLauncher | Common | 현무암색·탁한 브론즈, 낮고 짧은 포물선 발사기 | 없음 |
| `item.weapon.rifle.cobaltneedle` | 코발트 침 | Rifle | Advanced | 네이비·코발트, 가는 총열과 한 줄의 에너지 홈 | 옅은 청색, 홈만 |
| `item.weapon.grenadelauncher.orbitcask` | 궤도 탄심 | GrenadeLauncher | Advanced | 건메탈·청록, 외부 파츠 없이 장갑 안에 잠긴 환형 코어 | 청록, 코어만 |
| `item.weapon.shotgun.mossbreaker` | 이끼 낀 파쇄기 | Shotgun | Advanced | 포레스트 그린·브러시드 스틸, 두꺼운 일체형 방열 외피 | 없음 |
| `item.weapon.shotgun.shockreef` | 벼락 암초 | Shotgun | Advanced | 흑색·청록, 손 구간 밖의 낮은 리브형 공명판 | 청록, 리브 사이만 |
| `item.weapon.rifle.auroraspine` | 극광 척추 | Rifle | Rare | 차콜·보랏빛 회색, 상부의 하나로 이어진 에너지 척추 | 민트색, 척추만 |
| `item.weapon.rifle.thorncurrent` | 극성 가시 | Rifle | Rare | 다크 브라운·청동, 손 구간 밖에만 낮게 솟은 전도 핀 | 라임색, 전도 홈만 |
| `item.weapon.shotgun.smelterjaw` | 용광로의 아귀 | Shotgun | Rare | 검정·산화 구리, 맞물린 두 장의 총구 열차폐판 | 호박색, 총구 안쪽만 |
| `item.weapon.grenadelauncher.cometcradle` | 혜성 요람 | GrenadeLauncher | Rare | 딥 블루·은회색, 연결된 보호대 안의 혜성 코어 | 아주르, 코어만 |
| `item.weapon.rifle.voidreed` | 공허 갈대 | Rifle | Unique | 먹색·보라, 단순한 이중 튜브 사이의 공허 도관 | 보라색, 도관만 |
| `item.weapon.shotgun.mirrorvault` | 거울 감옥 | Shotgun | Unique | 거울 은색·검정, 반사 패널이 감싼 매끈한 공명실 | 없음, 반사 재질로 표현 |
| `item.weapon.grenadelauncher.tideanchor` | 심해 닻 | GrenadeLauncher | Unique | 딥 네이비·청동, 닻처럼 아래가 넓은 안정형 실루엣 | 짙은 청색, 중앙 홈만 |
| `item.weapon.grenadelauncher.prismtide` | 분광 해류 | GrenadeLauncher | Unique | 흑연·보랏빛 금속, 한 몸체 안에서 분리된 두 에너지 채널 | 청록·자홍, 서로 분리된 채널 |
| `item.weapon.rifle.eclipsechoir` | 일식 합창 | Rifle | Legendary | 검정·다크 골드, 한 줄로 겹친 공명판과 긴 코어 | 보라색, 코어와 공명 홈 |
| `item.weapon.rifle.stormcrown` | 폭풍 왕관 | Rifle | Legendary | 짙은 남색·은회색, 총구 근처의 낮은 왕관형 집속기 | 코발트색, 집속기 안쪽만 |
| `item.weapon.shotgun.crownfurnace` | 용융 왕관 | Shotgun | Legendary | 적갈색·흑철, 넓은 총구를 감싼 제련로형 외피 | 용융 호박색, 총구와 코어만 |
| `item.weapon.grenadelauncher.celestialanvil` | 항성 압쇄기 | GrenadeLauncher | Legendary | 미드나이트 블루·브라스, 크고 평평한 모루형 공성 몸체 | 금색, 중앙 코어만 |

### 담당 미확정 10개

기존 기획표에서 `우주 괴물 허파`를 제외하고 우선 반영한 목록이다. 등급별 2개씩이며 최종 담당자는 다음 작업 전에 정한다.

| itemId | 표시명 | 타입 | 등급 | 메모 |
|---|---|---|---|---|
| `item.weapon.rifle.massproduced` | 양산형 라이플 | Rifle | Common | 단순한 표준형 에너지 소총 |
| `item.weapon.shotgun.homesafety` | 가정용 안전 용품 | Shotgun | Common | 생활 장비를 개조한 비살상 안전 장비풍 산탄총 |
| `item.weapon.rifle.fieldmod` | 개조된 라이플 | Rifle | Advanced | 현장 개조 패널이 보이되 부속은 절제 |
| `item.weapon.rifle.outdoorhunter` | 아웃도어 헌팅 라이플 | Rifle | Advanced | 목재 모방 소재와 미래형 에너지 총열 |
| `item.weapon.grenadelauncher.fireworks` | 폭죽놀이 | GrenadeLauncher | Rare | 화려한 색은 발광 홈과 탄두 챔버에만 제한 |
| `item.weapon.shotgun.sulbing` | 설빙 | Shotgun | Rare | 얼음 속성의 짧고 넓은 냉각 총구 |
| `item.weapon.grenadelauncher.glasscannon` | 유리대포 | GrenadeLauncher | Unique | 투명 덮개 안의 취약한 고출력 코어 |
| `item.weapon.shotgun.incinerator` | 소각 산탄총 | Shotgun | Unique | 기존 Rare `flamethrower`와 합칠지 별도 제작할지 판단 필요 |
| `item.weapon.grenadelauncher.apocalypse` | 묵시록적 파괴 | GrenadeLauncher | Legendary | `worldender`와 겹치지 않는 다중 봉인 코어 방향 |
| `item.weapon.shotgun.conversationstarter` | 훌륭한 대화수단 | Shotgun | Legendary | 무겁고 단순한 일체형 충격파 산탄 장비 |

### 기존 기획표의 나머지 항목

- `눈싸움시간!`은 `item.weapon.grenadelauncher.snowballfight`로 4개 언어 라벨·ItemDefinitionSO·아이콘·프리팹까지 이미 존재하므로 신규 제작 수량에 넣지 않는다.
- `아크 블래스터`는 팀원 웹 제작 Rare GrenadeLauncher 교체 후보, `스캐빈저 개조 소총`은 Rare Rifle 교체 후보, `트러블 슈터`는 Unique Rifle 교체 후보, `매서운 눈총`은 Legendary Rifle 교체 후보로 보존한다.
- `소각 산탄총`은 기존 Rare `item.weapon.shotgun.flamethrower`와 다른 Unique 항목으로 만들지, 기존 항목의 이름·등급을 바꿀지 사용자가 판단한다.
- `묵시록적 파괴`는 `worldender`와 같은 전설 유탄발사기이므로 둘 다 채택할 경우 실루엣과 코어 구조를 명확히 구분한다.
- `우주 괴물 허파`는 이번 계획에서 제외한다.

## 에이전트별 완료 산출물

각 무기 폴더에 다음을 남긴다.

- `References/front.png`, `left.png`, `back.png`, `right.png` — 2048×1024 RGBA
- `Prepared/TripoInput/front.png`, `left.png`, `back.png`, `right.png` — 원본과 동일한 2048×1024 RGBA H3 입력
- `Tripo/` — upload manifest, task ID/result, 비용 기록, 원본 다운로드 GLB
- `Production/{itemId}.blend`
- `Production/{itemId}.fbx`
- `Production/Textures/` — Unity 전달용 PBR 텍스처
- `Production/QA/` — 정면·후면·좌우·상하·사시도 검증 이미지
- `Production/final_prompt.txt`
- `Production/validation.json` — root/marker/축/크기/triangle/material/FBX 재임포트 결과

## 완료 판정

1. 네 참조 이미지가 같은 무기이며 손 영역과 총구가 논리적으로 일치한다.
2. Tripo task가 성공했고 실제 비용과 결과 URL/파일이 manifest에 기록됐다.
3. 오른손 root가 실제 방아쇠 손잡이 중심에 있다.
4. `LeftHandGrip`이 실제 포어엔드 접촉면에 있고 양손 간격이 Gunner에서 도달 가능하다.
5. `Muzzle`이 실제 총구 중심이며 local `+Z`가 발사 방향이다.
6. 음수·비균일 scale, Camera, Light, Armature, Collider와 떠 있는 불필요 파츠가 없다.
7. 발광은 Tripo 원본 emissive 또는 발광부의 정확한 RGB와 제한된 UV 구역으로 만든 Emission Map으로 복원되며 비발광 표면과 분리되어 있다. 양쪽 측면에서 삐져나옴·구멍·흐려짐·백색화가 없어야 한다.
8. 최적화 전후 실루엣·Grip·Muzzle·발광 경계가 같고 triangle 수가 기록되어 있다.
9. 빈 Blender 씬 FBX 재임포트에서 단일 root, 세 marker, 축, 크기, triangle/material이 보존된다.
10. 실패 모델은 억지 마커 보정으로 합격 처리하지 않고 재생성 필요 사유를 보고한다.
