# Firebase 전환 계획

## 1. 현재 Firebase 파일 판정

현재 작업 경로는 `C:/Users/user/Desktop/Project2_test/Project2`이다. 사용자가 언급한 `C:/Users/user/Desktop/Project2/_test/Project2` 경로는 현재 존재하지 않는다.

`Assets/Firebase/Plugins`의 파일은 과거 공식 Firebase Unity SDK 13.12.0을 임포트했던 흔적은 맞지만, 현재 프로젝트에서 사용할 수 있는 완전한 SDK 설치 상태는 아니다.

- Git 이력의 `Stop tracking Firebase SDK files` 변경에서 Firebase 관리 DLL과 플랫폼별 Auth/Database 네이티브 파일 대부분이 제거되었다.
- 현재 `Assets/Firebase/Plugins/x86_64`에는 코어 네이티브 파일인 `FirebaseCppApp-13_12_0.so`, `FirebaseCppApp-13_12_0.bundle`만 남아 있다.
- C# 코드가 참조해야 하는 `Firebase.App.dll`, `Firebase.Auth.dll`, `Firebase.Firestore.dll`이 없다.
- 기존 설치 이력에는 Auth와 Realtime Database만 있고 Cloud Firestore는 포함되어 있지 않다.
- `.gitignore`가 `/Assets/Firebase/` 전체를 제외하므로 현재 파일을 기반으로 개발해도 팀원이 동일한 환경을 받을 수 없다.
- `Assets/ExternalDependencyManager`, `Assets/Plugins/iOS/Firebase`, `Assets/Plugins/tvOS/Firebase`에는 이전 Asset Package 방식의 일부 파일이 별도로 남아 있다.

따라서 이 파일들은 “공식 SDK에서 나온 잔여물”로는 볼 수 있지만, “Firebase가 정식으로 설치되어 팀에서 재현 가능한 상태”로 보기는 어렵다. 공식 SDK를 다시 구성할 때는 기존 Asset Package 잔여물과 새 Unity Package Manager 설치를 혼합하지 않고 한 방식으로 정리해야 한다.

## 2. 전환 목표

1. 기존 로컬 저장 기능을 한 번에 폐기하지 않는다.
2. 실시간 전투와 플레이어 권한은 Mirror가 담당한다.
3. Firebase는 로그인, 계정별 영구 데이터, 기기 간 동기화를 담당한다.
4. Firebase 장애나 일시적인 오프라인 상태에서도 최근 저장 데이터로 게임을 이어갈 수 있게 로컬 캐시를 유지한다.
5. 어떤 데이터를 Firebase에 저장할지 중앙 목록에서 쉽게 추가하거나 제외할 수 있게 한다.
6. 기존 DTO의 변수명과 의미를 유지하고, Firebase 때문에 게임 데이터 모델 전체를 다시 만들지 않는다.
7. 새 메서드에는 역할을 쉽게 이해할 수 있는 한글 XML `<summary>` 주석을 작성한다.
8. 기존 동기식 `GameManager` 초기화 계약은 유지하고, Firebase 준비 과정만 별도의 비동기 흐름으로 둔다.

## 3. 데이터 책임 분리

### 3-1. Firebase에 저장할 데이터

| 데이터 | 초기 저장 위치 | 이유 |
| --- | --- | --- |
| `PlayerProfileData` | Cloud Firestore + 로컬 캐시 | 계정 식별, 닉네임, 진행도처럼 기기 간 유지가 필요한 데이터 |
| `GameSaveData` | Cloud Firestore + 로컬 캐시 | 인벤토리, 재화, 장비, 성장처럼 영구 저장이 필요한 데이터 |
| `QuestSaveData` | Cloud Firestore + 로컬 캐시 | 퀘스트 진행 상태와 완료 여부를 계정 단위로 유지 |

### 3-2. 로컬에 유지할 데이터

| 데이터 | 초기 저장 위치 | 이유 |
| --- | --- | --- |
| `SystemOptionsData` | 로컬 전용 | 해상도, 음량, 품질 등 기기별 설정 |
| 키 바인딩 | `PlayerPrefs` 또는 기존 로컬 방식 | 입력 장치와 사용자 PC별 설정 |
| 테스트/벤치마크 데이터 | 로컬 전용 | 운영 계정 데이터에 섞이면 안 됨 |
| 정적 SO와 데이터 테이블 | 프로젝트 에셋 | 클라이언트 콘텐츠이며 저장 데이터가 아님 |
| `StageMapSaveData` | 초기에는 로컬/Mirror 세션 | 맵 상태 전체를 매 순간 클라우드에 기록하지 않음 |

`StageMapSaveData`는 나중에 스테이지 체크포인트나 세션 종료 시점에 필요한 최소 정보만 별도 DTO로 추려 Firebase에 저장한다. 현재 구조 전체를 그대로 업로드하지 않는다.

`MultiplayerSlotData`는 Mirror 정식 전환 이후 소유권 모델을 먼저 확정한다. 현재처럼 호스트가 게스트 프로필까지 복사해 저장하는 구조는 Firebase 계정 소유권과 맞지 않으므로 초기 Firebase 전환 대상에서 제외한다.

## 4. 권장 구성

```text
게임 코드 / 기존 DataManager
          |
          v
   SaveDataService
     |          |
     |          +---- LocalJsonSaveDataStore
     |
     +--------------- FirestoreSaveDataStore
                          |
                          v
                    FirebaseService
                    - SDK 초기화
                    - 로그인 사용자 확인
                    - 준비/실패 상태 제공
```

### 4-1. 역할

- `FirebaseService`
  - Firebase SDK 의존성 확인과 초기화를 한 번만 수행한다.
  - `FirebaseAuth`의 현재 사용자와 UID를 제공한다.
  - 로그인되지 않은 상태에서 클라우드 저장을 시도하지 못하게 한다.
- `ISaveDataStore`
  - 로컬과 Firestore가 같은 저장/불러오기/삭제 계약을 사용하게 한다.
  - 호출부가 Firebase SDK 세부 구현을 직접 알지 않게 한다.
- `LocalJsonSaveDataStore`
  - 기존 로컬 저장을 대체하거나 복구 캐시로 사용한다.
  - 파일을 임시 파일에 쓴 뒤 교체하고, 이전 파일을 백업한다.
- `FirestoreSaveDataStore`
  - Firestore 문서 읽기/쓰기와 예외 변환을 담당한다.
  - 게임 코드에 Firestore 타입과 예외를 노출하지 않는다.
- `SaveDataCatalog`
  - 데이터 종류별 문서 ID, 로컬 파일명, 저장 위치, 스키마 버전을 한 곳에서 관리한다.
  - Firebase 대상 추가/제외 시 호출부를 수정하지 않고 이 목록과 DTO 매핑만 변경한다.
- `SaveDataService`
  - 카탈로그에 따라 로컬 전용 또는 클라우드+캐시 저장소로 요청을 전달한다.
  - 로드 우선순위, 실패 복구, revision 충돌 처리를 담당한다.

초기에는 실제 교체 경계가 분명한 위 구성만 사용한다. Factory, Service Locator, 별도 전역 Manager처럼 아직 필요하지 않은 계층은 추가하지 않는다.

## 5. Firestore 문서 구조

초기 경로는 다음처럼 단순하게 유지한다.

```text
players/{uid}/saveData/profile
players/{uid}/saveData/gameplay
players/{uid}/saveData/quest
```

각 문서는 다음 공통 봉투 구조를 사용한다.

```json
{
  "schemaVersion": 1,
  "revision": 1,
  "updatedAtUtc": "2026-09-22T00:00:00.0000000Z",
  "payloadJson": "{ ... 기존 DTO JSON ... }"
}
```

- `schemaVersion`: DTO 구조 변경과 마이그레이션 판단에 사용한다.
- `revision`: 여러 기기나 재시도에서 저장 순서를 판단한다.
- `updatedAtUtc`: 사용자 표시와 진단을 위한 UTC 시각이다. 서버 저장 시각은 Firestore 전용 필드로 별도 유지할 수 있다.
- `payloadJson`: 기존 DTO 이름과 JSON 구조를 최대한 유지한다.

초기에는 기존 DTO를 Firestore 필드 구조로 모두 분해하지 않는다. JSON payload 방식은 현재 코드와의 차이를 줄이고 로컬/클라우드 직렬화 결과를 일치시키기 쉽다. 특정 필드 검색이나 서버 측 검증이 실제 요구될 때만 해당 문서를 구조화 필드로 분리한다.

Firestore 문서 크기 제한 때문에 `GameSaveData`가 커질 가능성을 측정해야 한다. 1 MiB에 가까워지면 인벤토리, 성장, 장비를 별도 문서로 분리한다. 초기부터 근거 없이 여러 문서로 나누지는 않는다.

## 6. 저장과 불러오기 정책

### 6-1. 저장

1. DTO를 JSON으로 직렬화한다.
2. 다음 revision을 계산한 봉투를 만든다.
3. 로컬 캐시를 원자적으로 저장한다.
4. 로그인과 Firebase 연결이 준비되었으면 Firestore 저장을 시도한다.
5. Firestore 저장이 실패하면 로컬 봉투에 업로드 대기 상태를 남긴다.
6. 다음 로그인, 저장, 명시적 동기화 시 재시도한다.

### 6-2. 불러오기

1. 로그인 전에는 로컬 전용 데이터와 마지막 캐시만 읽는다.
2. 로그인 후 Firestore 문서와 로컬 캐시의 `revision`을 비교한다.
3. 한쪽만 존재하면 존재하는 데이터를 사용하고 다른 쪽을 보완한다.
4. 양쪽 revision이 같으면 Firestore를 기준으로 확정한다.
5. revision이 다르고 로컬에 업로드 대기 표시가 있으면 자동 덮어쓰기 전에 충돌 정책을 적용한다.
6. 성공적으로 결정된 데이터를 기존 DTO로 역직렬화하여 현재 `DataManager` 흐름에 전달한다.

초기 충돌 정책은 “더 높은 revision 우선”으로 하되, 서로 다른 장치에서 동시에 수정된 정황이 있으면 조용히 덮어쓰지 않고 오류 결과와 진단 로그를 남긴다. 재화와 아이템 복제 위험이 있으므로 단순히 마지막 시각만 비교하지 않는다.

## 7. 인증과 보안

- 개발 첫 단계는 익명 인증으로 UID와 저장 흐름을 검증할 수 있다.
- 팀 요구사항에 이메일/비밀번호 로그인이 포함되면 같은 `FirebaseService` 경계 안에서 인증 방식만 확장한다.
- Firebase 프로젝트는 대표자 한 명의 Google 계정으로 만들되 계정 자체를 공유하지 않는다. 대표자는 프로젝트 소유와 결제를 관리하고, 팀원은 각자의 Google 계정을 `프로젝트 설정 > 사용자 및 권한`에서 필요한 역할로 초대한다.
- 일상 개발에는 Firebase 프로젝트 편집 권한만 부여하고, 소유자·결제·IAM 변경 권한은 대표자와 백업 관리자처럼 최소 인원에게만 둔다.
- Firestore 보안 규칙은 로그인 사용자가 자신의 `players/{uid}` 아래 문서만 읽고 쓸 수 있게 제한한다.
- 클라이언트가 보내는 재화와 아이템 값을 신뢰할 수 없다는 점은 별도 문제다. 팀 프로젝트 필수 구현 범위가 클라우드 저장이라면 우선 사용자별 접근 제어와 데이터 손실 방지를 완료하고, 치팅 방지는 서버 권한 구조가 필요할 때 확장한다.
- `google-services.json`과 `GoogleService-Info.plist`는 앱을 Firebase 프로젝트에 연결하는 공개 클라이언트 설정이므로 팀 저장소에서 공유할 수 있다. 서비스 계정 키와 관리자 비밀 키는 전혀 다른 자격 증명이므로 저장소에 넣거나 팀 메신저로 공유하지 않는다.
- 현재 Android 앱 식별자는 `com.PLAYERTWO.arpgproject`다. Firebase Android 앱 등록 시 이 값을 대소문자까지 동일하게 사용하며, 식별자를 바꾸려면 Firebase 앱 등록과 Unity Player Settings를 함께 갱신한다.

## 8. 이름과 주석 기준

- 기존 데이터 타입 이름은 유지한다: `PlayerProfileData`, `GameSaveData`, `QuestSaveData` 등.
- 비동기 메서드는 `SaveAsync`, `LoadAsync`, `DeleteAsync`, `InitializeAsync`처럼 `Async` 접미사를 사용한다.
- 축약형인 `Mgr`, `Svc`, `Repo`, `Fb`를 새 공개 API 이름에 사용하지 않는다.
- `FirebaseService`, `FirestoreSaveDataStore`, `LocalJsonSaveDataStore`, `SaveDataService`처럼 역할이 드러나는 이름을 사용한다.
- 공개 메서드뿐 아니라 역할 판단이 어려운 내부 메서드에도 쉬운 한글 `<summary>`를 작성한다.
- 주석은 코드 동작을 그대로 반복하지 않고, 호출 시점, 책임, 실패 시 결과처럼 팀원이 알아야 할 내용을 설명한다.

예시:

```csharp
/// <summary>
/// 저장 데이터 종류에 맞는 저장소를 선택해 데이터를 저장하고 결과를 반환합니다.
/// </summary>
public Task<SaveDataOperationResult> SaveAsync(...)
```

## 9. SDK 설치 방향

Unity Package Manager 방식으로 공식 Firebase Unity SDK를 재구성하는 것을 우선한다.

1. 현재 Asset Package 방식 잔여 파일의 정확한 목록을 확정한다.
2. 팀과 함께 삭제 범위를 확인한 뒤 `Assets/Firebase`, `Assets/ExternalDependencyManager`, `Assets/Plugins/iOS/Firebase`, `Assets/Plugins/tvOS/Firebase`의 구형 잔여물을 정리한다.
3. 공식 External Dependency Manager, Firebase App, Firebase Auth, Firebase Firestore 패키지를 같은 버전으로 설치한다.
4. `Packages/manifest.json`, 패키지 잠금 파일, Firebase 설정 파일이 새 clone에서도 재현되는지 확인한다.
5. Unity 재실행과 의존성 해석 후 Windows Editor에서 `FirebaseApp.CheckAndFixDependenciesAsync()`를 검증한다.

현재 확인된 공식 최신 계열은 Firebase Unity SDK 13.17.0이다. 실제 설치 시점에 공식 릴리스 노트에서 버전을 다시 확인하고 App/Auth/Firestore의 버전을 통일한다. Firebase의 데스크톱 Editor 지원은 개발 워크플로용 베타라는 제약이 있으므로 최종 배포 플랫폼에서도 별도로 검증한다.

공식 참고 자료:

- <https://firebase.google.com/docs/unity/setup>
- <https://firebase.google.com/docs/unity/setup-alternative>
- <https://firebase.google.com/support/release-notes/unity>
- <https://developers.google.com/unity/archive>

## 10. 단계별 구현 순서

### 1단계: 저장소 기반과 공식 SDK 재구성

- `SaveDataCatalog`, 공통 결과 타입, `ISaveDataStore`를 만든다.
- `LocalJsonSaveDataStore`와 왕복 테스트를 먼저 만든다.
- 구형 Firebase 잔여물을 정리하고 공식 App/Auth/Firestore 패키지를 설치한다.
- `FirebaseService.InitializeAsync()`로 준비/실패 상태를 확인한다.

완료 조건:

- 새 clone에서 패키지 의존성이 복원된다.
- Unity 컴파일 오류가 없다.
- SDK 준비 성공/실패가 명확한 결과로 반환된다.
- 로컬 저장소 왕복과 손상 파일 오류가 테스트된다.

### 2단계: 인증과 프로필 전환

- 임시 로그인 우회와 실제 Firebase 인증을 연결한다.
- `PlayerProfileData`만 먼저 Firestore에 저장한다.
- 첫 로그인 시 기존 로컬 프로필을 1회 업로드하고 마이그레이션 표시를 남긴다.
- 재로그인 후 같은 UID의 프로필이 복구되는지 확인한다.

완료 조건:

- 로그아웃/재로그인 후 프로필이 유지된다.
- 다른 UID의 데이터에 접근할 수 없다.
- 네트워크 실패 시 로컬 캐시로 진입하고 실패 이유를 확인할 수 있다.

### 3단계: 게임 저장과 퀘스트 전환

- `GameSaveData`, `QuestSaveData`를 카탈로그의 클라우드 대상으로 추가한다.
- 기존 `DataManager`의 저장/불러오기 진입점을 `SaveDataService`에 연결한다.
- 인벤토리, 재화, 장비, 퀘스트 완료 상태를 실제 플레이 흐름에서 검증한다.
- 저장 크기와 요청 횟수를 측정한다.

완료 조건:

- 기존 로컬 저장과 같은 DTO 결과가 나온다.
- 중복 아이템이나 재화 롤백이 없다.
- 오프라인 저장 후 재연결 동기화가 동작한다.

### 4단계: Mirror 체크포인트 연동

- Mirror가 실시간 런타임 상태의 권한을 유지한다.
- 방 생성, 매치 중 프레임 단위 상태를 Firebase에 기록하지 않는다.
- 스테이지 완료, 체크포인트, 정상 종료 같은 확정 시점에만 권한 있는 결과를 Firebase 저장 DTO로 변환한다.
- 게스트 데이터는 각 사용자 UID 소유 문서에 저장하는 구조로 전환한다.

완료 조건:

- 호스트와 게스트의 저장 소유권이 분리된다.
- 비권한 클라이언트가 다른 사용자의 결과를 저장하지 못한다.
- 연결 종료와 재접속 후 확정된 체크포인트에서 복구된다.

## 11. 테스트 범위

- 카탈로그의 모든 데이터 종류가 고유한 문서 ID와 파일명을 갖는지 검사한다.
- 로컬 JSON 저장/불러오기/백업/손상 파일 처리를 검사한다.
- 로그인 전, 로그인 성공, 인증 실패, Firebase 의존성 실패를 검사한다.
- Firestore 문서 없음, 정상 로드, 권한 거부, 네트워크 실패를 검사한다.
- 기존 로컬 데이터가 한 번만 업로드되고 재실행 때 중복 마이그레이션되지 않는지 검사한다.
- revision 충돌에서 데이터가 조용히 유실되지 않는지 검사한다.
- 실제 플레이어의 인벤토리, 장비, 재화, 퀘스트 상태를 저장하고 재접속 후 비교한다.
- Mirror 호스트와 게스트 각각 자신의 UID 데이터만 갱신하는지 검사한다.
- Firebase Emulator Suite를 사용할 수 있으면 보안 규칙과 실패 흐름을 자동 테스트한다.

## 12. 1차 구현 보완 상태 (2026-09-23)

### 액트 클리어 크레딧

- 싱글플레이는 보스 노드를 완료하기 전에 현재 `PlayerWallet.Gold`를 `PlayerProfileData.credit`에 더한다.
- 프로필 로컬 저장이 시작된 뒤에만 런 지갑을 0으로 초기화하므로 파일 저장 실패 시 크레딧을 잃지 않는다.
- `SaveSinglePlayerSlot`의 기존 경로를 사용하므로 로그인 상태에서는 사용자별 로컬 캐시에 먼저 기록하고 Firestore 업로드를 요청한다.
- 멀티플레이는 서버가 확정한 각 플레이어의 지갑 값만 해당 소유 클라이언트에 `TargetRpc`로 전달한다. 클라이언트는 자신의 활성 프로필과 Firebase UID에만 저장한다.
- 음수와 `int` 범위를 넘는 크레딧은 `PlayerProfileData.TryApplyCredit`에서 거절한다.

### 오프라인 자동 진행

- 로그인 씬 시작 시 `FirebaseAuth.CurrentUser`로 기기에 보존된 이전 인증 세션을 확인한다.
- 이전 로그인 세션이 있으면 이메일과 비밀번호를 다시 요구하지 않고 같은 UID의 `FirebaseMigration/players/{uid}` 로컬 캐시를 불러온다.
- Firestore 연결 실패 시 `SaveDataService`가 같은 사용자 로컬 캐시를 반환하며, 로그인 팝업은 인증 세션을 임의로 로그아웃하지 않는다.
- 비밀번호를 프로젝트 파일이나 별도 로컬 JSON에 저장하지 않는다. Firebase Auth가 보존한 인증 세션만 사용한다.
- 한 번도 로그인하지 않은 기기 또는 사용자가 명시적으로 로그아웃한 상태에서는 계정 확인을 위해 온라인 로그인이 필요하다.

현재 Unity 6000.3.22 컴파일과 Edit Mode 테스트 11개가 통과했다. 사용자별 로컬 프로필·Firebase 캐시 파일 존재도 확인했다. 실제 네트워크 어댑터를 끈 상태의 로그인 씬 자동 전환과 멀티 호스트/원격 클라이언트별 크레딧 반영은 수동 플레이 검증 항목으로 남긴다.

## 12. 현재 구현 상태와 다음 승인 지점

2026-09-22 기준으로 김성우 담당 영역과 패키지 기반에는 아래 항목이 구현되어 있다.

- `SaveDataTypes`, `SaveDataCatalog`, `ISaveDataStore`
- 원자적 파일 교체와 `.bak` 복구를 포함한 `LocalJsonSaveDataStore`
- Firebase 초기화, 이메일 로그인, 이메일 계정 생성을 담당하는 `FirebaseService`
- Firestore 문서 읽기/쓰기/삭제를 담당하는 `FirestoreSaveDataStore`
- 로컬 전용 데이터와 클라우드 캐시 데이터를 구분하는 `SaveDataService`
- 로컬 저장 왕복, 사용자별 캐시 분리, 경로 검증, 카탈로그 분류, 손상 파일 백업 복구 Edit Mode 테스트
- Firebase App/Auth/Firestore 13.17.0과 External Dependency Manager 1.2.189 공식 UPM tarball 설치
- Firebase 프로젝트 `project2-d063b`의 Android 앱 ID `com.PLAYERTWO.arpgproject` 설정 파일 연결
- `firestore.rules`, `firebase.json`, `.firebaserc`로 사용자 본인의 저장 경로만 허용하는 규칙을 버전 관리

패키지 tarball은 저장소 루트의 `GooglePackages`에 두고 `*.tgz`를 Git LFS로 관리한다. `Packages/manifest.json`은 `file:../GooglePackages/...` 상대 경로를 사용하므로 팀원은 별도 ZIP을 전달받지 않고 clone/pull만으로 같은 패키지를 복원한다. 현재 Firebase tarball에는 Unity가 검증할 수 있는 서명이 없어 Package Manager의 노란 경고가 표시되지만, 설치나 컴파일 실패를 뜻하지는 않는다.

담당자 승인에 따라 아래 로그인·기존 저장 연결을 구현했다.

- `Assets/WBHTest/Scripts/0. Core/Manager/DataManager.cs`: 기존 프로필 저장 진입점을 `SaveDataService`에 연결하고 최초 로컬 데이터 이관 후 Firebase UID 소유자를 기록
- `Assets/Scripts/UI/Login/KY_LoginPopup.cs`: 임시 로그인 우회를 Firebase Auth 결과로 교체
- `Assets/Scripts/UI/Login/KY_CreateAccountPopup.cs`: 이메일/비밀번호 계정 생성과 기본 프로필 준비를 Firebase Auth에 연결

설정 파일은 추가되었으며 Unity가 `Assets/StreamingAssets/google-services-desktop.json`을 정상 생성했다. 실제 프로젝트 `project2-d063b`에서 Firebase 초기화, 임시 이메일 계정 생성, 본인 UID Firestore 쓰기·읽기, `SaveDataService` 저장·재로드, 다른 UID 경로 쓰기 거부를 확인했다. 기존 공용 로컬 프로필의 소유 UID가 다른 경우 새 계정으로 업로드하지 않고 새 프로필을 만드는 격리 흐름도 검증했으며, 검증용 계정·문서·로컬 캐시는 모두 제거하고 기존 로컬 파일은 원상 복구했다. 남은 실제 플레이 검증은 로그인 씬의 버튼 입력, 타이틀 씬 전환, 플레이 후 저장, 앱 재실행 뒤 복구 흐름이다.
