using UnityEngine;

// 스킬 입력 키 안내.
// 옵션에서 설정한 스킬 키가 반영된다.
public class KY_SkillView : MonoBehaviour
{
    [SerializeField] private KY_SkillSlot[] skillSlots; // Slot1~4
    [SerializeField] private KY_SkillSlot dodgeSlot;
    [SerializeField] private KY_SkillSlot itemSlot;

    // ISkillController를 구현한 컴포넌트(FighterSkillController/GunnerSkillController 등)에서 Slot1~3
    // (인덱스 0~2)의 실시간 쿨타임을 읽어와 라디얼 필로 표시한다. slots는 Skill1~4+Dodge까지 5개가 있지만,
    // 실제 쿨타임 데이터가 있는 건 Skill1~3뿐이라 그만큼만 갱신한다.
    //
    // 예전엔 인스펙터에 MonoBehaviour 필드로 직접 연결해뒀는데(캐릭터를 바꿀 때마다 손으로 재연결해야
    // 했고, 안 바꾸면 비활성 캐릭터의 컨트롤러를 계속 보다가 초기화 안 된 값 - 예: 스택 -1 - 을 그대로
    // 표시하는 문제가 있었다), 지금은 ActiveSkillControllerLocator로 지금 활성 캐릭터의 컨트롤러를
    // 그때그때 찾아서 쓴다(SkillEvolutionSelectUI와 같은 방식, 116/124번).
    private ISkillController SkillController => ActiveSkillControllerLocator.Find();

    private GameInputActions inputActions;
    private T_PlayerController playerController;
    private WBH_PlayerStatus playerStatus;

    void Start()
    {
        playerController = FindFirstObjectByType<T_PlayerController>();
        playerStatus = FindFirstObjectByType<WBH_PlayerStatus>();

        inputActions = KY_RebindManager.Instance.GetInputActions();
        Debug.Log("inputActions 인스턴스: " + inputActions.GetHashCode());
        RefreshAllKeyTexts();
    }

    void OnEnable()
    {
        KY_GameEvents.OnSkillEquipped += OnSkillEquipped;
        KY_GameEvents.OnSkillUnequipped += OnSkillUnequipped;
        KY_GameEvents.OnKeyBindingChanged += RefreshAllKeyTexts;
    }

    void OnDisable()
    {
        KY_GameEvents.OnSkillEquipped -= OnSkillEquipped;
        KY_GameEvents.OnSkillUnequipped -= OnSkillUnequipped;
        KY_GameEvents.OnKeyBindingChanged -= RefreshAllKeyTexts;
        //inputActions.Disable();
    }

    void Update()
    {
        if (SkillController == null)
            return;

        int cooldownSlotCount = Mathf.Min(SkillController.SkillCount, skillSlots.Length);
        for (int i = 0; i < cooldownSlotCount; i++)
        {
            float skillRemaining = SkillController.GetRemainingCooldown(i);
            float skillTotal = SkillController.GetEffectiveCooldown(i);
            skillSlots[i].SetCooldown(skillRemaining, skillTotal);

            if (SkillController.TryGetStackInfo(i, out int stacks, out int maxStacks))
                skillSlots[i].SetStacks(stacks);
            else
                skillSlots[i].SetStacks(null);
        }

        float dodgeRemaining = playerController.currentDodgeCooltime;
        float dodgeTotal = playerStatus.DodgeCooltime;
        dodgeSlot.SetCooldown(dodgeRemaining, dodgeTotal);
    }

    void OnSkillEquipped(int index, Sprite icon)
    {
        skillSlots[index].SetIcon(icon);
    }

    void OnSkillUnequipped(int index)
    {
        skillSlots[index].ClearIcon();
    }

    void RefreshAllKeyTexts()
    {
        Debug.Log("RefreshAllKeyTexts 호출됨");
        string skill1Key = KY_KeyTextUtil.GetKeyText(inputActions, "Skill1");
        Debug.Log("Skill1 키: " + skill1Key);
        skillSlots[0].SetKeyText(skill1Key);
        skillSlots[0].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Skill1"));
        skillSlots[1].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Skill2"));
        skillSlots[2].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Skill3"));
        skillSlots[3].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Skill4"));
        dodgeSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Dodge"));
        itemSlot.SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Potion"));
    }
}