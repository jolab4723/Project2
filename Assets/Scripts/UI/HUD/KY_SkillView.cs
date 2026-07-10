using UnityEngine;
using UnityEngine.InputSystem;

public class KY_SkillView : MonoBehaviour
{
    public KY_SkillSlot[] slots;

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