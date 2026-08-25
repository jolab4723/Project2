# 눈싸움시간! 정식 재제작 — 이미지 승인 대기

기존 `ITM_WPN_GNR_0013` P1 메시를 재사용·보수하지 않고, 인벤토리 원본의 색·투명 얼음 챔버·눈·토끼 콘셉트만 참고해 새 4면도를 제작했다.

- 최종 4면도: `References/front.png`, `left.png`, `back.png`, `right.png`
- Tripo 준비본: `Prepared/TripoInput/`
- 자동 픽셀 검증: `Prepared/visual_validation.json`
- 입력 manifest: `Prepared/reference_manifest.json`
- H3용 프롬프트: `Prepared/final_prompt.txt`

모든 최종 PNG는 2048×1024 RGBA이며 배경과 모서리는 실제 alpha 0이다. 생성기의 출력 비율을 맞추기 위해 균일 리사이즈와 투명 패딩/이동만 사용했고, 배경 제거·색상 임계값 마스킹은 사용하지 않았다. RIGHT는 최종 LEFT의 픽셀 단위 수평 반전본이다.

Tripo API 제출은 root의 비용 제한 HOLD 지시에 따라 수행하지 않았다. 현재 상태는 root 육안 승인 대기이며, 승인 전에는 H3 제출·Blender 후처리·FBX 생성 단계로 진행하지 않는다.
