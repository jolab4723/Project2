using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Core;

/// <summary>
/// 패시브 업그레이드 팝업 전체를 제어하는 컨트롤러 (기존 씬에 만들어진 UI 구조에 맞춰 코드로 바인딩).
///
/// 아이콘 12개(Row1~3 x PassiveIcon1~4)는 Core.PassiveSkillId의 enum 선언 순서와 1:1로 대응된다
/// (4개씩 3그룹: 체력/공격력/방어력/속도, 크리티컬확률/크리티컬피해/쿨감/속성보너스, 부활/캠프/상점강화/미정).
///
/// 아이콘 선택 -> -/+ 로 "적용해볼 레벨"(pendingLevel)을 조정 -> btn_Confilm으로 확정.
/// 이미 해금된 범위 안이면 즉시 무료 적용("OK"), 그 이상이면 골드를 써서 해금 후 적용(비용 숫자 표시,
/// 골드가 모자라면 빨간색). 다른 아이콘을 선택하면 확정 안 한 변경은 버려진다.
///
/// !! 작업 중 Panel이 여러 번 복제돼서 이 컴포넌트가 여러 GameObject에 붙어있을 수 있다.
///    "MainRow" 자식이 있는 인스턴스만 실제 팝업 루트로 보고 동작하며, 나머지는 아무것도 하지 않는다.
/// </summary>
public class PassiveSkillPanelUI : MonoBehaviour
{
    // enum 선언 순서가 Row1~3 x PassiveIcon1~4와 1:1로 대응됨 (4개씩 3그룹).
    private static readonly PassiveSkillId[] IconOrder = (PassiveSkillId[])System.Enum.GetValues(typeof(PassiveSkillId));

    // 상세 설명 텍스트의 "현재 레벨 / 최대 레벨" 색상 (TMP 리치 텍스트 <color> 태그용).
    private const string DefaultTextColor = "#FFFFFF";
    private const string LevelUpColor = "#ADFF2F";   // 연두색 - 설정하려는 값이 현재 레벨보다 큼(올릴 예정)
    private const string LevelDownColor = "#FFA500"; // 주황색 - 설정하려는 값이 현재 레벨보다 작음(내릴 예정)
    private const string MaxLevelReachedColor = "#FFFF00"; // 노란색 - 최대 레벨까지 전부 해금됨

    private struct IconRef
    {
        public PassiveSkillId id;
        public Button button;
        public TextMeshProUGUI levelBadge;
    }

    private bool isRootController;

    private TextMeshProUGUI goldText;
    private Button skillClearButton;
    private Button closeButton;

    private TextMeshProUGUI nameText;
    private TextMeshProUGUI descriptionText;
    private Button levelDownButton;
    private Button levelUpButton;
    private TextMeshProUGUI pendingLevelText;
    private Button confirmButton;
    private TextMeshProUGUI confirmButtonText;

    private readonly List<IconRef> icons = new List<IconRef>();

    private PassiveSkillId? selectedId;
    private int pendingLevel;

    private void BindReferences()
    {
        Transform headerControl = transform.Find("HeaderRow/ControlArea");
        goldText = headerControl.Find("GoldText").GetComponent<TextMeshProUGUI>();
        skillClearButton = headerControl.Find("btn_SkillClear").GetComponent<Button>();
        closeButton = transform.Find("btn_ClosePopup").GetComponent<Button>();

        Transform detailPanel = transform.Find("MainRow/ControlArea/Panel");
        nameText = detailPanel.Find("PassiveNameText").GetComponent<TextMeshProUGUI>();
        descriptionText = detailPanel.Find("PassiveDescriptionText").GetComponent<TextMeshProUGUI>();

        Transform detailControl = detailPanel.Find("ControlArea");
        levelDownButton = detailControl.Find("btn_LevelDown").GetComponent<Button>();
        levelUpButton = detailControl.Find("btn_LevelUp").GetComponent<Button>();
        pendingLevelText = detailControl.Find("Divider").GetComponent<TextMeshProUGUI>();

        confirmButton = detailPanel.Find("btn_Confilm").GetComponent<Button>();
        confirmButtonText = confirmButton.GetComponentInChildren<TextMeshProUGUI>();

        Transform listArea = transform.Find("MainRow/SkillListArea");
        icons.Clear();
        int index = 0;
        for (int row = 1; row <= 3 && index < IconOrder.Length; row++)
        {
            Transform rowT = listArea.Find("Row" + row);
            if (rowT == null)
                continue;

            for (int col = 1; col <= 4 && index < IconOrder.Length; col++)
            {
                Transform iconT = rowT.Find("PassiveIcon" + col);
                if (iconT == null)
                    continue;

                icons.Add(new IconRef
                {
                    id = IconOrder[index],
                    button = iconT.GetComponent<Button>(),
                    levelBadge = iconT.GetComponentInChildren<TextMeshProUGUI>(),
                });
                index++;
            }
        }
    }

    private void OnEnable()
    {
        // !! isRootController/BindReferences를 Awake가 아니라 여기서 매번 다시 계산한다.
        // 에디터 도메인 리로드(스크립트 재컴파일)는 이미 씬에 있던 오브젝트의 Awake를 다시 안 불러서,
        // 유니티 실행 없이 Context Menu 등으로 테스트하면 예전에 캐싱된 값이 최신 계층 구조와 어긋날 수 있다.
        // OnEnable은 Play 진입/씬 로드/GameObject 재활성화마다 항상 다시 불리므로 여기서 매번 재판정한다.
        isRootController = transform.Find("MainRow") != null;
        if (!isRootController)
            return;

        BindReferences();

        foreach (var icon in icons)
        {
            PassiveSkillId id = icon.id;
            icon.button.onClick.AddListener(() => SelectSkill(id));
        }

        levelDownButton.onClick.AddListener(HandleLevelDownClicked);
        levelUpButton.onClick.AddListener(HandleLevelUpClicked);
        confirmButton.onClick.AddListener(HandleConfirmClicked);
        skillClearButton.onClick.AddListener(HandleSkillClearClicked);
        closeButton.onClick.AddListener(HandleCloseClicked);

        if (PassiveSkillManager.Instance != null)
            PassiveSkillManager.Instance.OnProfileChanged += RefreshAll;

        if (selectedId == null && icons.Count > 0)
            selectedId = icons[0].id;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (!isRootController)
            return;

        foreach (var icon in icons)
            icon.button.onClick.RemoveAllListeners();

        levelDownButton.onClick.RemoveListener(HandleLevelDownClicked);
        levelUpButton.onClick.RemoveListener(HandleLevelUpClicked);
        confirmButton.onClick.RemoveListener(HandleConfirmClicked);
        skillClearButton.onClick.RemoveListener(HandleSkillClearClicked);
        closeButton.onClick.RemoveListener(HandleCloseClicked);

        if (PassiveSkillManager.Instance != null)
            PassiveSkillManager.Instance.OnProfileChanged -= RefreshAll;
    }

    private void SelectSkill(PassiveSkillId id)
    {
        selectedId = id;
        pendingLevel = PassiveSkillManager.Instance != null ? PassiveSkillManager.Instance.GetCurrentLevel(id) : 0;
        RefreshDetailPanel();
    }

    private void HandleLevelDownClicked()
    {
        if (selectedId == null)
            return;

        pendingLevel = Mathf.Max(0, pendingLevel - 1);
        RefreshDetailPanel();
    }

    private void HandleLevelUpClicked()
    {
        if (selectedId == null)
            return;

        var definition = PassiveSkillManager.Instance.GetDefinition(selectedId.Value);
        int maxLevel = definition != null ? definition.maxLevel : 0;
        pendingLevel = Mathf.Min(maxLevel, pendingLevel + 1);
        RefreshDetailPanel();
    }

    private void HandleConfirmClicked()
    {
        if (selectedId == null || PassiveSkillManager.Instance == null)
            return;

        // 성공하면 OnProfileChanged -> RefreshAll이 이미 화면을 갱신해준다.
        // 골드 부족으로 실패해도 pendingLevel은 그대로 둬서 사용자가 다시 시도할 수 있게 한다.
        PassiveSkillManager.Instance.TryApplyLevel(selectedId.Value, pendingLevel);
        RefreshDetailPanel();
    }

    private void HandleSkillClearClicked()
    {
        PassiveSkillManager.Instance?.ResetAllCurrentLevels();
        if (selectedId != null)
            SelectSkill(selectedId.Value);
    }

    private void HandleCloseClicked()
    {
        gameObject.SetActive(false);
    }

    private void RefreshAll()
    {
        var manager = PassiveSkillManager.Instance;
        var profile = manager != null ? manager.CurrentProfile : null;

        if (goldText != null)
            goldText.text = profile != null ? $"골드 {profile.gold}" : "골드 -";

        foreach (var icon in icons)
        {
            int level = manager != null ? manager.GetCurrentLevel(icon.id) : 0;
            if (icon.levelBadge != null)
                icon.levelBadge.text = level.ToString();
        }

        RefreshDetailPanel();
    }

    private void RefreshDetailPanel()
    {
        if (selectedId == null)
            return;

        PassiveSkillId id = selectedId.Value;
        var definition = PassiveSkillManager.Instance.GetDefinition(id);
        if (definition == null)
            return;

        var manager = PassiveSkillManager.Instance;
        int currentLevel = manager != null ? manager.GetCurrentLevel(id) : 0;
        int unlockedLevel = manager != null ? manager.GetUnlockedLevel(id) : 0;

        if (nameText != null)
            nameText.text = definition.displayName;

        if (descriptionText != null)
        {
            // "현재 효과"/"다음 레벨"은 확정된 currentLevel이 아니라 지금 -/+로 미리보는 pendingLevel 기준으로 표시.
            float previewEffect = definition.GetValue(pendingLevel);
            float nextEffect = definition.GetValue(Mathf.Min(pendingLevel + 1, definition.maxLevel));

            // 설정하려는 값(pendingLevel)이 현재 레벨보다 크면 연두색(올릴 예정), 작으면 주황색(내릴 예정), 같으면 기본색.
            string currentLevelColor = pendingLevel > currentLevel ? LevelUpColor
                : pendingLevel < currentLevel ? LevelDownColor
                : DefaultTextColor;
            // 최대 레벨까지 전부 해금했으면 MaxLevel 표시를 노란색으로.
            string maxLevelColor = unlockedLevel >= definition.maxLevel ? MaxLevelReachedColor : DefaultTextColor;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"현재 레벨: <color={currentLevelColor}>{pendingLevel}</color> / <color={maxLevelColor}>{definition.maxLevel}</color>");
            sb.AppendLine($"현재 효과: <color={currentLevelColor}>+{previewEffect:0.#}%</color>");
            if (pendingLevel < definition.maxLevel)
                sb.Append($"다음 레벨: +{nextEffect:0.#}%");

            descriptionText.text = sb.ToString();
        }

        if (pendingLevelText != null)
            pendingLevelText.text = pendingLevel.ToString();

        if (levelDownButton != null)
            levelDownButton.interactable = pendingLevel > 0;

        if (levelUpButton != null)
            levelUpButton.interactable = pendingLevel < definition.maxLevel;

        if (confirmButtonText != null)
        {
            if (pendingLevel <= unlockedLevel)
            {
                confirmButtonText.text = "OK";
                confirmButtonText.color = Color.white;
            }
            else
            {
                int cost = manager != null ? manager.GetUnlockCostToLevel(id, pendingLevel) : 0;
                bool canAfford = manager != null && manager.CurrentProfile != null && manager.CurrentProfile.gold >= cost;
                confirmButtonText.text = cost.ToString();
                confirmButtonText.color = canAfford ? Color.white : Color.red;
            }
        }
    }
}
