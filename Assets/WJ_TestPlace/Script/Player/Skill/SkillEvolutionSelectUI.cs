using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// 스킬 진화(3형태 중 선택) + 강화(위력/쿨타임/범위 중 선택)를 게임 중에 직접 고를 수 있는 간단한 패널.
/// K키로 열고 닫는다. 원래 진화 전용이었는데, 사용자 요청으로 강화 선택도 같은 패널에 합쳤다(따로
/// L키 패널로 분리했던 SkillEnhancementSelectUI는 이 패널로 흡수되면서 폐기함).
/// 아직 진화/강화 해금·재화 개념이 없어서 지금은 제한 없이 자유롭게 바꿀 수 있다.
/// 슬롯마다 버튼 하나를 클릭할 때마다 없음→1→2→3→없음 순으로 돌아간다
/// (드롭다운 대신 클릭 한 번으로 순환하는 방식이라 더 간단하게 만들 수 있었다).
/// </summary>
public class SkillEvolutionSelectUI : MonoBehaviour
{
    [SerializeField] private FighterSkillController skillController;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button[] cycleButtons = new Button[3];
    [SerializeField] private TextMeshProUGUI[] cycleButtonLabels = new TextMeshProUGUI[3];

    [Header("강화 선택 (진화와 같은 패널, 슬롯별 별도 버튼)")]
    [SerializeField] private Button[] enhanceCycleButtons = new Button[3];
    [SerializeField] private TextMeshProUGUI[] enhanceCycleButtonLabels = new TextMeshProUGUI[3];

    private static readonly string[] EvolutionLabels = { "없음", "진화1", "진화2", "진화3" };
    private static readonly string[] EnhancementLabels = { "없음", "강화1(위력)", "강화2(쿨타임)", "강화3(범위)" };

    private void Awake()
    {
        for (int i = 0; i < cycleButtons.Length; i++)
        {
            int index = i; // 클로저 캡처용 지역 변수
            if (cycleButtons[i] != null)
                cycleButtons[i].onClick.AddListener(() => CycleEvolution(index));
        }

        for (int i = 0; i < enhanceCycleButtons.Length; i++)
        {
            int index = i;
            if (enhanceCycleButtons[i] != null)
                enhanceCycleButtons[i].onClick.AddListener(() => CycleEnhancement(index));
        }
    }

    private void OnEnable() => RefreshLabels();

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            TogglePanel();
    }

    private void TogglePanel()
    {
        if (panelRoot == null)
            return;

        bool next = !panelRoot.activeSelf;
        panelRoot.SetActive(next);

        if (next)
            RefreshLabels();
    }

    private void CycleEvolution(int index)
    {
        if (skillController == null)
            return;

        int current = (int)skillController.GetEvolution(index);
        int next = (current + 1) % EvolutionLabels.Length;
        skillController.SetEvolution(index, (SkillEvolutionId)next);
        RefreshLabels();
    }

    private void CycleEnhancement(int index)
    {
        if (skillController == null)
            return;

        int current = (int)skillController.GetEnhancement(index);
        int next = (current + 1) % EnhancementLabels.Length;
        skillController.SetEnhancement(index, (SkillEnhancementId)next);
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (skillController == null)
            return;

        for (int i = 0; i < cycleButtonLabels.Length && i < skillController.SkillCount; i++)
        {
            if (cycleButtonLabels[i] != null)
                cycleButtonLabels[i].text = EvolutionLabels[(int)skillController.GetEvolution(i)];

            if (i < enhanceCycleButtonLabels.Length && enhanceCycleButtonLabels[i] != null)
                enhanceCycleButtonLabels[i].text = EnhancementLabels[(int)skillController.GetEnhancement(i)];
        }
    }
}
