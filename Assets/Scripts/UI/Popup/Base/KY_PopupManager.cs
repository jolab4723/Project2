using UnityEngine;
using System.Collections.Generic;

// 팝업을 관리.
// 키 입력을 이벤트로 받아 해당하는 팝업을 여는 코드입니다.
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

    /// <summary>일반 팝업이 열려 있는지 알려준다. 멀티플레이 입력은 게임 시간을 멈추지 않고 이 상태로 차단한다.</summary>
    public bool HasOpenModalPopup => popupStack.Count > 0; // SW 수정

    [Header("일반 팝업 뒤 입력 차단 대상")]
    [SerializeField] private CanvasGroup[] modalInputTargets;

    [Header("Dialog")]
    [SerializeField] private KY_ConfirmDialog confirmDialog;
    [SerializeField] private KY_AlertDialog alertDialog;
    [SerializeField] private KY_ToastDialog toastDialog;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("KY_PopupManager가 씬에 중복으로 존재합니다!");
            Destroy(gameObject);
            return;
        }
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
        if (!popupDict.TryGetValue(type, out KY_PopupBase popup) || popup == null)
            return;
        if (popupStack.Contains(popup))
            return;
        popup.transform.SetAsLastSibling(); // SW 수정
        popup.Open();
        popupStack.Push(popup);
        SetBackgroundInput(false);
    }

    public void Hide()
    {
        if (popupStack.Count == 0) return;

        KY_PopupBase top = popupStack.Pop();
        top.Close();
        if (popupStack.Count == 0) SetBackgroundInput(true);
    }

    private void SetBackgroundInput(bool enabled)
    {
        if (modalInputTargets == null) return;
        foreach (CanvasGroup target in modalInputTargets)
        {
            if (target == null) continue;
            target.interactable = enabled;
            target.blocksRaycasts = enabled;
        }
    }

    public void ShowSidePopup(PopupType type)
    {
        if (!popupDict.TryGetValue(type, out KY_PopupBase popup) || popup == null)
            return;
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
        KY_GameEvents.SidePopupOpened();
    }

    public void HideSidePopup()
    {
        if (currentSidePopup == null) return;

        currentSidePopup.Close();
        currentSidePopup = null;
        KY_GameEvents.SidePopupClosed();
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

    private KY_QuestData displayedQuestData;

    /// <summary>열려 있는 같은 의뢰의 내용만 갱신하며 팝업을 다시 열지 않는다.</summary>
    public void RefreshQuestDetail(KY_QuestData previous, KY_QuestData updated)
    {
        if (previous == null || !ReferenceEquals(displayedQuestData, previous)) return;
        displayedQuestData = updated;
        if (popupDict.TryGetValue(PopupType.QuestDetail, out var popup) &&
            popup is KY_QuestDetailPopup detail && detail.gameObject.activeInHierarchy)
            detail.SetData(updated);
    }

    public void ShowQuestDetail(KY_QuestData data)
    {
        displayedQuestData = data;
        KY_QuestDetailPopup detailPopup = popupDict[PopupType.QuestDetail] as KY_QuestDetailPopup;
        detailPopup.SetData(data);
        Show(PopupType.QuestDetail);
    }

    public void ShowConfirm(KY_DialogData data)
    {
        RemoveClosedPopups();
        if (confirmDialog == null || popupStack.Contains(confirmDialog))
            return;

        confirmDialog.Show(data);
        popupStack.Push(confirmDialog);
    }

    private void RemoveClosedPopups()
    {
        if (popupStack.Count == 0) return;

        var activeEntries = new List<KY_PopupBase>();
        while (popupStack.Count > 0)
        {
            KY_PopupBase popup = popupStack.Pop();
            if (popup != null && popup.gameObject.activeSelf)
                activeEntries.Add(popup);
        }

        for (int i = activeEntries.Count - 1; i >= 0; i--)
            popupStack.Push(activeEntries[i]);
    }

    public void ShowAlert(KY_AlertData data)
    {
        alertDialog.Show(data);
    }

    public void ShowToast(string message)
    {
        toastDialog.Show(message);
    }
}
