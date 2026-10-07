# 결과 UI 연결

## 범위

ResultScene은 최종 게임 클리어/게임오버 공통 화면이다. 중간 스테이지 클리어에는 사용하지 않는다.
사망 연출, 부활 여부, 런 통계 집계, 보상 지급은 게임플레이 담당자의 책임이다.
earnedCredits에는 표시할 최종 지급액을 전달한다. UI는 지급 비율을 계산하거나 재화를 변경하지 않는다.

## 씬과 테스트

- 씬: Assets/Scenes/Test/KY/ResultScene.unity
- ResultCanvas의 KY_ResultScreen에 모든 표시 참조가 연결되어 있다.
- ResultCanvas 아래 Top_Header(상단), Middle_Statistics(중간), Bottom_Actions(하단), Overlay_Transition(전환)으로 구분한다. 각 통계 행의 라벨/수치/구분선은 해당 Row의 자식이다.
- 현재 Use Preview Data는 켜져 있다. 단독 Play 시 예시 결과를 표시한다.
- 실제 payload 데이터가 있으면 테스트 데이터보다 우선한다.
- 컴포넌트 컨텍스트 메뉴의 미리보기/클리어, 미리보기/게임오버로 두 레이아웃을 확인한다.
- 출시 연결 후 Use Preview Data를 끈다. 데이터가 없으면 누락 안내를 표시한다.
- 표 헤더와 최고 콤보는 표시하지 않는다. 기존 combo 데이터 필드는 기존 직렬화 호환을 위해 남겨 두었다.

## 결과 전달

게임플레이 호출부와 결과 씬이 같은 ResultPayload.asset을 참조한다.
새 런을 시작할 때 payload.Clear()를 호출한다.
최종 결과가 확정되면 payload.SetResult(resultData)를 호출한 뒤 기존 씬 전환 담당자가 결과 씬을 로드한다.
결과 씬은 Start에서 payload를 읽으며, Start 전에 ApplyResult로 직접 주입한 데이터도 덮어쓰지 않는다.
데이터는 메모리에만 유지되며 에셋/세이브 파일에 저장하지 않는다. 에디터 재실행 시 이전 세션 결과는 무효화된다.

## 검은색 가로 전환

KY_ResultWipe의 Black Panel에는 화면 전체를 덮는 검은 Image의 RectTransform을 연결한다.
왼쪽에서 화면을 덮는 Cover(), 오른쪽으로 빠져나가는 Reveal()은 IEnumerator다.
호출자가 yield return wipe.Cover()로 완료를 기다린 뒤 씬 전환을 시작해야 한다.
결과 씬에서는 검은 화면으로 시작한 뒤 자체 Reveal을 실행한다.
duration 기본값은 0.45초이며 Time.timeScale의 영향을 받지 않는다.
입력은 검은 Image의 Raycast Target으로 전환 중 차단한다.

## 버튼 계약 및 아직 연결되지 않은 부분

- 클리어/게임오버 공통: 다시 시작(Retry Requested), 타이틀로(Title Requested) 두 버튼만 사용한다.
- 현재 씬의 요청 이벤트는 미연결이다. 클릭 시 전환 연출만 실행하고 다시 결과를 보여준다.
- 게임플레이 담당자가 런 초기화/로비 복귀/멀티 권한 처리 후 실제 이동을 연결해야 한다.
- 다시 시작 요청을 받는 게임플레이 담당자가 새 런 초기화와 시작 위치를 처리한다.
- 현재 UI의 버튼 요청은 검은 화면에서 발생하고, 이벤트 반환 후 다시 Reveal한다.
  비동기 씬 전환 중에도 검은 화면을 유지하는 최종 연결은 기존 SceneLoader/Fader와 통합해야 한다.
- ResultScene은 Build Settings에 아직 등록하지 않았다.
- 기존 SceneLoader는 LoadingScene과 YJ_ScreenFader를 사용한다.
  검은 가로 전환만으로 결과 씬에 도달하도록 바꾸려면 해당 담당자 코드의 통합이 필요하다.
- 언어별 라벨은 씬의 별도 TMP 오브젝트로 되어 있다. 설명은 SetMessages로 교체 가능하다.
  실제 번역 테이블 연결은 아직 하지 않았다.

## 검증

Unity 6000.3.8f1에서 컴파일 오류 0, Play Mode 클리어/게임오버 실제 화면 캡처 확인.
timeScale 0에서 Cover/Reveal 완료, 연속 요청 2회에 이벤트 1회, 씬 재로드 후 payload 유지 확인.
시간 형식 01:02:03, 음수/NaN/Infinity 방어 확인. Missing Script 없음.
실제 사망/보상/멀티/게임플레이 씬 이동/빌드 실행은 미연결이므로 검증하지 않았다.
MCP 카메라 캡처 시 Unity 내부 PlayerLoop 재귀 오류가 발생하여 ScreenCapture 방식으로 변경했다.
