# 스마일 시그널 최종 전달

## 아이템

- `itemId`: `item.weapon.rifle.smilesignal`
- 표시명: KOR `스마일 시그널`, ENG `Smile Signal`, JPN `スマイルシグナル`, CHN `微笑信号`
- 등급: `Unique`
- 클래스: `Gunner`
- 무기 유형: `Rifle`
- 최종 축: 총구 `+Z`, 위 `+Y`

## 승인 참조 4면

`Prepared/TripoInput/`의 `front`, `left`, `back`, `right`를 사용했다. 네 장 모두 `2048×1024 RGBA`, 실제 alpha 0 투명 배경, 투명 픽셀 hidden RGB 0이며 `right`는 최종 `left`의 픽셀 exact horizontal mirror이다.

- 픽셀 게이트: `Prepared/QA/four_view_pixel_gate.json` — `PASS`
- 콘택트 시트: `Prepared/QA/four_view_contact_sheet.png`
- 입력 해시와 최종 산출물 해시: `Production/artifact_manifest.json`

## Tripo H3.1

첫 작업 `712651d2-dbc4-4e61-916c-f2a35897136c`은 100%에서 `expired`, 사용 크레딧 0이었다. 사용자 승인에 따른 단 한 번의 재제출만 별도 잠금 경로에서 수행했고 추가 재시도는 하지 않았다.

- 성공 작업 ID: `1fa3dd59-189c-4160-b050-36cd85e529d4`
- 유형: `multiview_to_model`
- 모델: `v3.1-20260211`
- 품질: `standard`
- 옵션: `texture=true`, `pbr=true`, `export_uv=true`
- 제출 직전 잔액/동결: `55 / 0`
- 완료 후 잔액/동결: `25 / 0`
- 실제 사용: `30 credits`
- API 상태: `success 100%`
- 원본: `Tripo/RetryAfterExpired20260825/Downloaded/item.weapon.rifle.smilesignal_raw.glb`
- 원본 구조 게이트: `Tripo/RetryAfterExpired20260825/RawQA/raw_gate.json` — `PASS`

원본 8면과 backface-culling 8면에서 열린 전방 총구, 막힌 중립 후면, 좌우 일치, 깨끗한 fore-end와 트리거 그립, 비정상 구멍 없음이 확인됐다.

## Blender / FBX 전달물

- 편집본: `Production/item.weapon.rifle.smilesignal.blend`
- FBX: `Production/item.weapon.rifle.smilesignal.fbx`
- 텍스처: `Production/Textures/`
  - BaseColor
  - Normal
  - ORM
  - Occlusion
  - MetallicSmoothness
  - Emission
- 전체 검증: `Production/validation.json` — `PASS`
- 빈 씬 FBX 재임포트: `Production/QA/Reimport/reimport_validation.json` — `PASS`
- QA 이미지: `Production/QA/` — PBR·발광·culling·감량 비교 포함 58장

원본 1,489,914 삼각형에서 60만·40만·25만 후보를 동일 구도로 비교했다. 249,999 삼각형 후보가 실루엣, 총구, 리시버 경계, 그립 개구부와 치비 얼굴 데칼을 보존해 최종 선택됐다. 고정 10만 목표는 사용하지 않았다.

최종 계층은 단일 루트 `item.weapon.rifle.smilesignal` 아래 메시 하나와 다음 Empty 세 개다.

- `RightHandGrip`: `(0, 0, 0)` — 트리거 그립 중앙이자 root 기준
- `LeftHandGrip`: `(0, -0.03, 0.27)` — 총구 쪽 27cm의 깨끗한 하부 fore-end 접촉점
- `Muzzle`: `(0, 0.0861, 0.6068)` — 열린 보어 끝, 로컬 `+Z` 발사 방향

빈 Blender 씬 재임포트 결과는 단일 루트, 단일 메시, 249,999 삼각형, 적용된 단위·양수 균일 Transform, 보존된 세 Empty, BaseColor/Normal/Emission FBX 슬롯, 외부 PBR 텍스처 6종을 확인했다. 앞손 마커의 최근접 표면 거리는 약 5.4mm, 총구는 약 1.0mm다.

## 발광

발광은 텍스처 전역의 어두운 적색 RGB 계열만 고른 이진 마스크다. 공간 좌표, UV 섬 예외, 팽창, 별도 발광 메시를 사용하지 않았다. 선택 비율은 atlas의 `1.849866%`이며 백색 본체와 스티커의 백색 머리는 선택될 수 없다. 자세한 임계값과 오버레이는 `Production/QA/emission_mask_report.json`, `Production/QA/emission_mask_overlay.png`에 있다.

## 범위 및 남은 검증

Unity 자산, 공용 문서, 엑셀, 기존 무기 자산은 수정하지 않았다. 따라서 Unity URP 머터리얼 remap, 프리팹/카탈로그/아이콘 연결, 실제 거너 장착과 Bloom 맵 검증은 루트 통합 작업에서 별도로 수행해야 한다.
