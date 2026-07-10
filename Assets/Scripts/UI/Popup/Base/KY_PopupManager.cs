using UnityEngine;
using System.Collections.Generic;

public class KY_PopupManager : MonoBehaviour
{
    public static KY_PopupManager Instance;

    [System.Serializable]
    public class PopupEntry
    {
        public PopupType type;
        public KY_PopupBase popup;
    }

    public List<PopupEntry> popupEntries;

    private Dictionary<PopupType, KY_PopupBase> popupDict;
    private Stack<KY_PopupBase> popupStack = new Stack<KY_PopupBase>();
    private KY_PopupBase currentSidePopup;

    void Awake()
    {
        Instance = this;

        popupDict = new Dictionary<PopupType, KY_PopupBase>();
        foreach (var entry in popupEntries)
        {
            popupDict[entry.type] = entry.popup;
        }
    }

    void OnEnable()
    {
        KY_GameEvents.OnEscPressed += OnEscPressed;
        KY_GameEvents.OnStatusRequested += OnStatusRequested;
        KY_GameEvents.OnSkillRequested += OnSkillRequested;
        KY_GameEvents.OnInventoryRequested += OnInventoryRequested;
        KY_GameEvents.OnQuestRequested += OnQuestRequested;
    }

    void OnDisable()
    {
        KY_GameEvents.OnEscPressed -= OnEscPressed;
        KY_GameEvents.OnStatusRequested -= OnStatusRequested;
        KY_GameEvents.OnSkillRequested -= OnSkillRequested;
        KY_GameEvents.OnInventoryRequested -= OnInventoryRequested;
        KY_GameEvents.OnQuestRequested -= OnQuestRequested;
    }

    public void Show(PopupType type)
    {
        KY_PopupBase popup = popupDict[type];
        popup.Open();
        popupStack.Push(popup);
    }

    public void Hide()
    {
        if (popupStack.Count == 0) return;

        KY_PopupBase top = popupStack.Pop();
        top.Close();
    }

    public void ShowSidePopup(PopupType type)
    {
        KY_PopupBase popup = popupDict[type];
        Debug.Log("ShowSidePopup 호출됨: " + type);

        if (currentSidePopup == popup)
        {
            HideSidePopup();
            return;
        }

        if (currentSidePopup != null)
            currentSidePopup.Close();

        currentSidePopup = popup;
        popup.Open();
    }

    public void HideSidePopup()
    {
        if (currentSidePopup == null) return;

        currentSidePopup.Close();
        currentSidePopup = null;
    }

    void OnEscPressed()
    {
        if (popupStack.Count > 0)
        {
            Hide();
            return;
        }

        if (currentSidePopup != null)
        {
            HideSidePopup();
            return;
        }

        Show(PopupType.Pause);
    }

    void OnStatusRequested()
    {
        ShowSidePopup(PopupType.Status);
    }

    void OnSkillRequested()
    {
        ShowSidePopup(PopupType.Skill);
    }

    void OnInventoryRequested()
    {
        ShowSidePopup(PopupType.Inventory);
    }

    void OnQuestRequested()
    {
        ShowSidePopup(PopupType.Quest);
    }

    public void ShowQuestDetail(KY_QuestData data)
    {
        KY_QuestDetailPopup detailPopup = popupDict[PopupType.QuestDetail] as KY_QuestDetailPopup;
        detailPopup.SetData(data);
        Show(PopupType.QuestDetail);
    }
}