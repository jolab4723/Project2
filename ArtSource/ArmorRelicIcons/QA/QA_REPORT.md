# 신규 방어구·유물 아이콘 QA

## 대상과 생성 방식

- 대상: 신규 방어구 24종, 신규 유물 8종, 합계 32종
- 생성: Codex 내장 `image_gen` (`gpt-image-2`)을 아이템별 1회 호출
- 투명화: 균일 `#00ff00` 크로마 배경 생성 후 `remove_chroma_key.py`의 soft matte/despill 적용
- 공통 프롬프트: 기존 Project2 방어구·유물 아이콘을 스타일 참조로 삼아, 단일 아이템·정면 또는 약한 3/4 시점·좌상단 중립 키라이트·우측의 약한 차가운 림라이트·산업 SF 재질·선명한 외곽선·바닥/그림자/문자/프레임 없음으로 생성했다.
- 개별 프롬프트: 각 ItemData의 영문 이름, rarity, 장비 부위와 설정을 기준으로 소재·형태·발광색을 구분했다.
- 생체 표현 보정: `item.armor.helmet.alienskullcrown`은 뼈 얼굴·입·치열을 제거하고 보라/청록 금속, 흰 세라믹 장식, HUD 바이저로 이루어진 왕관형 헬멧으로 재생성했다.
- `item.relic.luckycharm` 후속 보정: 긴 하단 기둥을 제거하고, 4개의 녹색 에나멜/에메랄드 잎·황동 프레임·짧은 빨간 고리·작은 보석 드롭의 컴팩트한 네잎클로버 펜던트로 교체했다. 해당 소스는 녹색 주제와 겹치지 않도록 magenta chroma를 사용했다.
- `item.armor.helmet.unbearablestimulation` 후속 보정: 벌어진 입과 괴물 머리로 읽히던 기존 실루엣을 폐기하고, 검정/건메탈·흰 세라믹 장갑·좁은 자홍 HUD 바이저·대칭 귀 모듈·신경 파형 회로로 구성된 완전 폐쇄형 신경 자극 헬멧으로 교체했다. 얼굴·입·이빨·생체 조직·촉수 표현은 사용하지 않았다.
- `item.armor.boots.alienclaws` 후속 보정: 실제 생물 발과 긴 발톱을 제거하고, 검정/건메탈 전투 부츠 한 쌍·보라/청록 에너지 인레이·짧고 매끄러운 은색 3분할 toe-guard로 구성된 착용형 SF 전투화로 교체했다.
- `item.armor.helmet.incomprehensiblefear` 후속 보정: 괴물 얼굴·다중 눈·뽔 두개골 인상을 제거하고, 검정/건메탈·흰 세라믹 패널·보라/청록 단일 HUD·층진 차원 센서로 구성된 착용 가능한 전설 헬멧으로 교체했다.
- `item.relic.alienheart` 후속 보정: 살점 심장·혈관·촉수를 제거하고, 금속/흰 세라믹 격납 프레임 안의 청록 결정 챔버 2개·보라 중앙 코어·기계식 맥동 링으로 구성된 봉인형 외계 생체에너지 반응로로 교체했다.

## 최종 규격

- 픽셀 규칙: `itemWidth × 128` × `itemHeight × 128`
- 실제 크기 집합: `128×128`, `128×256`, `256×256`, `256×384`, `384×256`, `384×384`
- 최종 알파 경계 여백: 최소 8~23px
- 불투명 점유율: 26.5~64.6%
- 반투명 AA 픽셀 비율: 2.5~11.7%
- 불투명 영역 평균 휘도: 0.121~0.509
- 불투명 영역 평균 채도: 0.146~0.548
- Unity 임포트: Sprite (2D and UI), Single, sRGB, Alpha From Input, Alpha Is Transparency, mipmap off, Clamp, Bilinear, max texture size 512

## 검증 결과

- PNG 존재/디코드/알파/캔버스 비율: 32/32 통과
- Unity Sprite/Single/max 512/샘플링 설정: 32/32 통과
- PNG GUID: 32개 모두 유효하며 중복 0
- ItemDefinitionSO 검색: 32/32, 중복 0
- ItemDefinitionSO.icon이 동일 ItemID Sprite를 참조: 32/32 통과
- 누락 importer, Sprite, SO, icon property: 모두 0
- Unity Console 오류·경고: 0
- 열린 `Act1_BossStage`는 기존 Dirty 상태를 유지했고 저장하지 않았다.
- 열린 `item.weapon.greatsword.corebreaker_WeaponVisual.prefab` Prefab Stage는 Dirty=false이며 저장하거나 수정하지 않았다.

## 육안 비교

- `new_icons_contact_sheet.png`: 신규 32종 전체의 잘림, 배경 잔여, 알파 가장자리, 카테고리별 실루엣 비교. ItemLabel KOR 표시명과 현행 UI 등급명(일반/고급/희귀/유일/전설)을 Malgun Gothic으로 출력했고, `ItemDisplayNames.GradeColorHex`의 색을 테두리와 등급 글자에 적용했다.
- `existing_new_mixed_comparison.png`: 기존 대표 16종과 신규 대표 16종을 교차 배치해 광원 방향, 색온도/채도, 대비, 외곽선, 재질 묘사, 슬롯 점유율 비교. 기존/신규 표기, KOR 표시명, 현행 등급명·색을 같은 한글 폰트로 재출력했다.
- 혼합 비교에서 신규 아이콘은 기존 세트의 어두운 산업 SF 금속, 제한된 cyan/orange/violet 발광, 좌상단 키라이트와 시각적으로 일치했다.
