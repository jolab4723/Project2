using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// 스킬 입력 키 안내.
// 옵션에서 설정한 스킬 키가 반영된다.
public class KY_SkillView : MonoBehaviour
{
    public KY_SkillSlot[] slots;

    [FormerlySerializedAs("skillController")]
    [Tooltip("ISkillController를 구현한 컴포넌트(FighterSkillController 등). Slot1~3(인덱스 0~2)의 실시간 " +
             "쿨타임을 여기서 읽어와 라디얼 필로 표시한다. slots는 Skill1~4+Dodge까지 5개가 있지만, " +
             "실제 쿨타임 데이터가 있는 건 Skill1~3뿐이라 그만큼만 갱신한다. 캐릭터 클래스와 무관하게 재사용할 " +
             "수 있도록 인터페이스만 바라보는데, 인스펙터가 인터페이스 필드를 직접 못 받아서 MonoBehaviour로 " +
             "받아 캐스팅한다(PlayerStatManager.equipManagerBehaviour와 같은 패턴).")]
    public MonoBehaviour skillControllerBehaviour;
    private ISkillController SkillController => skillControllerBehaviour as ISkillController;

    private GameInputActions inputActions;

    void Start()
    {
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

        int cooldownSlotCount = Mathf.Min(SkillController.SkillCount, slots.Length);
        for (int i = 0; i < cooldownSlotCount; i++)
        {
            float remaining = SkillController.GetRemainingCooldown(i);
            float total = SkillController.GetEffectiveCooldown(i);
            slots[i].SetCooldown(remaining, total);

            if (SkillController.TryGetStackInfo(i, out int stacks, out int maxStacks))
                slots[i].SetStacks(stacks);
            else
                slots[i].SetStacks(null);
        }
    }

    void OnSkillEquipped(int index, Sprite icon)
    {
        slots[index].SetIcon(icon);
    }

    void OnSkillUnequipped(int index)
    {
        slots[index].ClearIcon();
    }

    void RefreshAllKeyTexts()
    {
        Debug.Log("RefreshAllKeyTexts 호출됨");
        string skill1Key = KY_KeyTextUtil.GetKeyText(inputActions, "Skill1");
        Debug.Log("Skill1 키: " + skill1Key);
        slots[0].SetKeyText(skill1Key);
        slots[0].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Skill1"));
        slots[1].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Skill2"));
        slots[2].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Skill3"));
        slots[3].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Skill4"));
        slots[4].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Dodge"));
    }
}