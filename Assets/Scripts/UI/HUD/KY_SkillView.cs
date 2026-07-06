using UnityEngine;
using UnityEngine.InputSystem;

public class KY_SkillView : MonoBehaviour
{
    public KY_SkillSlot[] slots;

    private GameInputActions inputActions;

    void Awake()
    {
        inputActions = new GameInputActions();
        inputActions.Enable();
    }

    void Start()
    {
        RefreshAllKeyTexts();
    }

    void OnEnable()
    {
        KY_GameEvents.OnSkillEquipped += OnSkillEquipped;
        KY_GameEvents.OnSkillUnequipped += OnSkillUnequipped;
    }

    void OnDisable()
    {
        KY_GameEvents.OnSkillEquipped -= OnSkillEquipped;
        KY_GameEvents.OnSkillUnequipped -= OnSkillUnequipped;
        inputActions.Disable();
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
        slots[0].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Skill1"));
        slots[1].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Skill2"));
        slots[2].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Skill3"));
        slots[3].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Skill4"));
        slots[4].SetKeyText(KY_KeyTextUtil.GetKeyText(inputActions, "Dodge"));
    }
}