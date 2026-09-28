# 새 UI에 언어 변환 넣는 방법

이 문서는 새로 만드는 UI 화면을 한국어·영어·일본어·중국어(KOR/ENG/JPN/CHN)로 바뀌게 하는 방법을 정리한 안내서다. 필요한 부품은 이미 프로젝트에 있으므로 새 매니저나 DB를 만들 필요가 없다.

## 가장 중요한 결론

- 언어 변환은 **문구**와 **폰트** 두 가지를 모두 바꿔야 한다. 문구만 바꾸면 일본어·중국어가 □로 깨지고, 폰트만 바꾸면 한국어가 그대로 나온다.
- 버튼·제목·탭처럼 **고정된 문구**는 엑셀에 한 줄 추가하고 텍스트 오브젝트에 `UILabelText`만 붙이면 끝이다. 코드가 필요 없고 폰트도 자동으로 바뀐다.
- 숫자가 들어가거나 상태에 따라 바뀌는 **코드 문구**는 `{0}`이 들어간 틀을 엑셀에 넣고 코드에서 `string.Format`으로 채운다. 이 텍스트에는 `YJ_LocalizedFont`를 붙인다.
- 아이템·스킬·적 이름 같은 **게임 데이터 이름**은 이미 있는 전용 DB를 쓴다. `ItemDefinitionSO.itemName` 같은 SO 필드는 한국어 사본이라 화면에 그대로 쓰면 안 된다.

```text
YJ_LanguageManager (현재 언어, 언어 변경 이벤트, 언어별 폰트)
├─ 고정 문구    UILabel.xlsx → UILabelDatabase → UILabelText 컴포넌트
├─ 코드 문구    UILabelDatabase.GetLabel(key) + string.Format → YJ_LocalizedFont
└─ 데이터 이름  아이템/고유효과/스탯/적/퀘스트/스킬/패시브 전용 라벨 DB
```

## 1. 구성 요소

| 부품 | 위치 | 역할 |
|---|---|---|
| `YJ_LanguageManager` | `Assets/Scripts/System/` | 현재 언어(`CurrentLanguage`), 언어 변경 이벤트(`LanguageChanged`), 현재 언어 폰트(`GetCurrentFont()`), 언어 변경(`SetLanguage`). 씬에 배치하지 않아도 `Instance` 접근 시 자동 생성된다. |
| `UILabel.xlsx` | `Assets/Resources/DataFiles/UIData/1. ExcelFile/` | 모든 화면이 함께 쓰는 고정 UI 문구 표. KOR/ENG/JPN/CHN 4개 시트, `key`/`label` 두 열. |
| `UILabelDatabase` | `Assets/Resources/DataFiles/UIData/3. GeneratedAssets/` | 위 엑셀에서 생성되는 DB(`UILabelDatabaseSO`). 반영 메뉴는 `DataLoader/UI Label/0. Run All Steps`. |
| `UILabelText` | `Assets/WJ_TestPlace/Script/Localization/` | 텍스트 오브젝트에 붙이고 `key`만 적으면 문구와 폰트를 알아서 바꾸고, 언어가 바뀔 때마다 다시 적용한다. |
| `YJ_LocalizedFont` | `Assets/Scripts/System/` | 문구는 코드가 넣고 **폰트만** 언어에 맞게 바꿔야 하는 텍스트에 붙인다. |

`UILabelDatabaseSO.GetLabel(key)`는 현재 언어 문구를 돌려준다. 현재 언어에 값이 없으면 한국어를, 한국어에도 없으면 **key 문자열 자체**를 돌려준다. 화면에 `forge_ui.title` 같은 글자가 보이면 엑셀에 key가 빠졌거나 파이프라인을 돌리지 않은 것이다.

## 2. 고정 문구 넣기 (코드 없음)

제목, 버튼, 탭 이름, 섹션 제목처럼 바뀌지 않는 문구에 쓴다.

1. `UILabel.xlsx`의 **4개 시트 모두**에 같은 key로 한 줄씩 추가한다.
   - key는 `화면이름_ui.항목` 형식으로 쓴다. 예: `forge_ui.title`, `forge_ui.confirm`
   - 이미 쓰고 있는 접두어: `settings_ui`, `chat_ui`, `upgrade_ui`, `passive_skill_ui`, `rest_ui`, `pause_ui`, `skill_ui`, `shop_ui`, `status_ui`, `common_ui`, `skill_evolution_ui`, `item_tooltip_ui`, `npc_ui`, `loading_ui`, `inventory_ui`
   - "확인", "예"처럼 여러 화면이 같이 쓰는 문구는 `common_ui.*`에 이미 있는지 먼저 찾아본다.
   - 번역 칸을 비워 두면 그 언어에서는 한국어가 나온다. 4개 언어를 한 번에 채운다.
2. Unity에서 `DataLoader/UI Label/0. Run All Steps`를 실행한다. **Play 모드가 아닐 때** 실행한다.
3. 해당 텍스트 오브젝트(TextMeshProUGUI)에 `UILabelText`를 붙이고 `Key`를 입력한다.
   - `Database` 칸은 비워 둬도 된다. 비어 있으면 Resources의 공용 DB를 자동으로 찾는다.
   - 폰트 교체도 이 컴포넌트가 함께 하므로 `YJ_LocalizedFont`를 따로 붙이지 않는다.

## 3. 코드에서 만드는 문구

"강화비용 : 500", "보유 3 / 5"처럼 값이 들어가거나 상태에 따라 바뀌는 문구에 쓴다.

**엑셀에는 값 자리를 비운 틀을 넣는다.**

| key | KOR | ENG | JPN | CHN |
|---|---|---|---|---|
| `forge_ui.cost` | `강화비용 : {0}` | `Cost : {0}` | `強化費用：{0}` | `强化费用：{0}` |

- 언어마다 어순이 달라서 문장 조각을 코드에서 이어 붙이지 않는다. 값이 여러 개면 `{0}`, `{1}`을 쓴다.
- **파이프라인이 셀 앞뒤 공백을 잘라낸다.** `"진화 : "`처럼 끝 공백 뒤에 값을 붙이면 공백이 사라진다. 값 자리는 항상 틀 안에 `{0}`로 넣는다.
- 색 태그(`<color>`)와 숫자 서식은 코드에 두고, 문장 부분만 틀로 뺀다.

**코드는 아래 형태를 따른다.**

```csharp
private const string UILabelPath = "DataFiles/UIData/3. GeneratedAssets/UILabelDatabase";

[SerializeField] private UILabelDatabaseSO uiLabels;
[SerializeField] private TextMeshProUGUI costText;

private void Awake()
{
    // 씬에서 연결하지 않아도 다른 씬에서 동작하도록 자동 로드한다.
    if (uiLabels == null)
        uiLabels = Resources.Load<UILabelDatabaseSO>(UILabelPath);
}

private void OnEnable()
{
    if (YJ_LanguageManager.Instance != null)
        YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;

    Refresh(); // 창이 열릴 때마다 현재 언어로 다시 채운다.
}

private void OnDisable()
{
    if (YJ_LanguageManager.Instance != null)
        YJ_LanguageManager.Instance.LanguageChanged -= HandleLanguageChanged;
}

private void HandleLanguageChanged(GameLanguage _) => Refresh();

private void Refresh()
{
    costText.text = string.Format(uiLabels.GetLabel("forge_ui.cost"), cost);
}
```

- `costText` 같은 코드 문구 텍스트에는 **`YJ_LocalizedFont`를 붙인다.** 붙이지 않으면 일본어·중국어가 □로 나온다.
- `OnEnable`에서 `Refresh()`를 부르는 것이 중요하다. 부르지 않으면 창을 처음 열 때 프리팹에 넣어 둔 기본 글자가 그대로 보인다(아래 7번의 1).

## 4. 게임 데이터 이름

아이템·스킬·적 이름은 새 DB를 만들지 말고 아래를 쓴다. 모두 현재 언어를 알아서 읽고, 비어 있으면 한국어로 대신 보여준다.

| 표시할 것 | 쓸 곳 | Resources 경로 (`DataFiles/` 아래) |
|---|---|---|
| 아이템 이름·설명 | `ItemLabelDatabaseSO.TryGetName(itemId, out name)` / `GetDescription(itemId)` | `ItemData/3. GeneratedAssets/LabelData/ItemLabelDatabase` |
| 스탯 이름 + 단위(%) | `ItemDisplayNames.StatNames[type]` + `ItemDisplayNames.StatUnit(type)` | (정적 클래스, DB 자동 로드) |
| 등급·분류·무기·방어구 종류 | `ItemDisplayNames.GradeNames` / `CategoryNames` / `WeaponNames` / `ArmorNames` | (정적 클래스) |
| 고유 효과 이름 | `UniqueEffectLabelDatabaseSO.TryGetName(id, out name)` | `ItemData/3. GeneratedAssets/LabelData/UniqueEffectLabelDatabase` |
| 적 이름 | `EnemyLabelDatabaseSO.GetName(enemyId)` | `EnemyData/3. GeneratedAssets/LabelData/EnemyLabelDatabase` |
| 퀘스트 | `QuestLabelDatabaseSO.GetQuestName(questId)` 등 | `QuestData/3. GeneratedAssets/QuestLabelDatabase` |
| 액티브 스킬 | `SkillLabelDatabaseSO.GetSkillName(skillId)` 등 | `CharData/SkillData/3. GeneratedAssets/SkillLabelDatabase` |
| 패시브 스킬 | `PassiveSkillLabelDatabaseSO.GetName(id)` / `GetDescription(id)` | `PassiveSkillData/3. GeneratedAssets/PassiveSkillLabelDatabase` |
| 스탯 창 행 이름 | `StatLabelDatabaseSO.GetLabel(statKey)` | `CharData/ClassData/3. GeneratedAssets/StatLabelDatabase` |

- 스탯 수치는 `$"{StatNames[type]} {sign}{value:F1}{StatUnit(type)}"`처럼 쓴다. 퍼센트 스탯은 이름이 아니라 수치 뒤에 %가 붙는다(예: "공격속도 -45.0%").
- `ItemDefinitionSO.itemName`, `EnemyDefinitionSO.enemyName` 같은 SO의 이름 필드는 **데이터를 가져올 때 저장한 한국어 사본**이다. 언어를 바꿔도 한국어로 남으므로 화면 표시에 쓰지 않는다.
- 이름을 한 번만 받아 두고 계속 표시하는 화면(보스 이름표 등)은 `LanguageChanged`를 구독해서 다시 받아와야 한다.
- 이 DB들은 각자 엑셀과 파이프라인이 따로 있다(`Assets/Resources/DataFiles/<분류>/1. ExcelFile/`). 새 이름을 추가할 때는 해당 엑셀과 파이프라인을 쓴다.

## 5. 폰트

기본 폰트(`Pretendard-Medium SDF`)에는 한자와 가나가 없다. 일본어·중국어에서는 `YJ_LanguageManager`가 가진 `NotoSansJP` / `NotoSansSC` 폰트로 바꿔야 글자가 나온다.

| 텍스트 종류 | 폰트 처리 |
|---|---|
| `UILabelText`를 붙인 텍스트 | 자동 교체. 추가 작업 없음 |
| 코드로 문구를 넣는 텍스트 | `YJ_LocalizedFont`를 붙인다 |
| 숫자·영문만 나오는 텍스트(레벨, 수량 등) | 붙이지 않아도 된다 |

- `YJ_LocalizedFont`는 `Start()`에서 폰트를 적용한다. 꺼져 있는 오브젝트는 `Start()`가 돌지 않으므로, 켜진 뒤에 확인해야 한다.
- 일본어로 처음 그리는 글자는 폰트 에셋(`NotoSansJP-Medium SDF.asset`)에 자동 추가되어 Git에 수정으로 잡힐 수 있다. 기능과 무관한 자동 변경이다.

## 6. 확인 방법

- **Play 모드 화면으로 확인한다.** DB에 번역이 들어 있다는 것만으로는 부족하다. 실제로 화면의 컴포넌트가 연결됐는지 봐야 한다.
- 한국어와 **다른 언어 하나 이상**(일본어 권장, 폰트 문제까지 드러남)으로 확인한다.
- 언어는 설정 창의 언어 선택이나 `YJ_LanguageManager.Instance.SetLanguage(GameLanguage.JPN)`로 바꾼다.
- 창을 **처음 열 때**와 **열린 상태에서 언어를 바꿀 때**를 모두 확인한다.
- 부모 창까지 켜진 상태에서 확인한다. 꺼져 있는 오브젝트는 `Awake`/`Start`가 돌지 않아서 바뀌지 않은 것처럼 보인다.

## 7. 자주 걸리는 문제

1. **창을 처음 열 때 한국어 기본 글자가 보이거나 □로 깨진다.**
   프리팹에 넣어 둔 기본 글자가 초기화 전에 보이는 것이다. 코드로 채우는 텍스트는 `OnEnable`에서 갱신한다. 2026-09-28 강화 팝업(`UpgradeController`)이 이 경우였다.
2. **만든 씬에서는 되는데 다른 씬에서는 한국어로 나온다.**
   DB 참조를 씬에서만 연결한 경우다. 3번 예시처럼 `Resources.Load` 자동 로드를 넣는다.
3. **캠프 한 곳에서만 되고 다른 캠프·스테이지에서는 안 된다.**
   공유 프리팹의 씬 인스턴스에만 컴포넌트를 붙인 경우다. 프리팹 원본(`Assets/Resources/Prefabs/UI/...`, `Assets/SW/Prefabs/...`)에 붙인다.
4. **화면에 key 문자열이 그대로 보인다.**
   엑셀에 key가 없거나, 오타가 있거나, 파이프라인을 돌리지 않았다.
5. **엑셀을 고쳤는데 반영이 안 된다.**
   파이프라인을 다시 돌린다. 엑셀이 열려 있으면 저장이 막히므로 먼저 닫는다.
6. **일본어·중국어만 □로 나온다.**
   폰트 교체가 빠졌다. 5번 표를 확인한다.

## 8. 새 화면 체크리스트

- [ ] 화면의 모든 텍스트를 고정 문구 / 코드 문구 / 데이터 이름 / 숫자로 나눴다.
- [ ] 고정 문구: 엑셀 4개 시트에 key 추가 → `DataLoader/UI Label/0. Run All Steps` → `UILabelText` 부착.
- [ ] 코드 문구: 틀(`{0}`)을 엑셀에 추가, DB 자동 로드, `OnEnable`에서 갱신, `LanguageChanged` 구독·해제, `YJ_LocalizedFont` 부착.
- [ ] 데이터 이름: 4번 표의 전용 DB 사용, SO 이름 필드 직접 표시 안 함.
- [ ] 공유 프리팹이면 프리팹 원본에 작업했다.
- [ ] Play 모드에서 한국어 + 일본어로, 처음 열 때와 언어 변경 시 모두 확인했다.
