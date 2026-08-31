using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>정식 HUD와 Mirror HUD가 함께 사용하는 로컬 표시 프리팹.</summary>
[DefaultExecutionOrder(-1100)]
[DisallowMultipleComponent]
public sealed class ChatPanel : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private RectTransform content;
    [SerializeField] private TMP_Text rowTemplate;
    [SerializeField] private TMP_InputField input;
    [SerializeField] private TMP_Text hint;
    [SerializeField] private Button unreadButton;
    [SerializeField] private CanvasGroup recordInteraction;

    private readonly List<TMP_Text> rows = new();
    private ChatSession session;
    private Keyboard keyboard;
    private RectTransform rect;
    private Canvas canvas;
    private GameObject title;
    private Coroutine reactivate;
    private Coroutine scrollUpdate;
    private bool editing;
    private bool changingInput;
    private bool rebuildPending;
    private int unread;
    private int displayedCount;
    private long displayedRevision = -1;

    public bool IsEditing => editing;
    public ChatSession Session => session;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        title = transform.Find("Title")?.gameObject;
        // 입력 중이 아니어도 패널 전체를 UI로 판정해 뒤쪽 전투 클릭을 막는다.
        background.raycastTarget = true;
        input.richText = false;
        input.characterLimit = ChatSession.MaxTextLength;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.restoreOriginalTextOnEscape = false;
        input.onFocusSelectAll = false;
        rowTemplate.richText = false;
        rowTemplate.gameObject.SetActive(false);
        input.onSelect.AddListener(OnSelected);
        input.onSubmit.AddListener(OnSubmitted);
        input.onValueChanged.AddListener(OnDraftChanged);
        unreadButton.onClick.AddListener(JumpToLatest);
    }

    private void OnEnable()
    {
        TryBindSession();
        SetKeyboard();
        Refresh();
    }

    private void OnDisable()
    {
        keyboard = null;
        if (session != null)
        {
            session.Draft = input.text;
            session.SetInputFocused(false);
            session.Changed -= Refresh;
        }
        session = null;
        editing = false;
        reactivate = scrollUpdate = null;
        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        input.onSelect.RemoveListener(OnSelected);
        input.onSubmit.RemoveListener(OnSubmitted);
        input.onValueChanged.RemoveListener(OnDraftChanged);
        unreadButton.onClick.RemoveListener(JumpToLatest);
    }

    private void TryBindSession()
    {
        ChatSession candidate = (NetworkManager.singleton as MirrorTestNetworkManager)?.Chat;
        candidate ??= InventoryController.Instance?.SinglePlayerMessages;
        if (ReferenceEquals(candidate, session)) return;
        if (session != null) { session.Changed -= Refresh; session.SetInputFocused(false); }
        session = candidate;
        displayedCount = 0;
        rebuildPending = true;
        if (session != null) session.Changed += Refresh;
        Refresh();
    }

    private void SetKeyboard()
    {
        keyboard = Keyboard.current;
    }

    private void Update()
    {
        TryBindSession();
        SetKeyboard();
        ResizeWithinHud();
        if (session?.IsConnected != true)
        {
            if (editing) EndEditing(false);
            return;
        }
        if (keyboard == null) return;
        if (editing && keyboard.escapeKey.wasPressedThisFrame)
        {
            EndEditing(true);
            return;
        }
        if (!editing && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.GetComponent<TMP_InputField>() != null && selected != input.gameObject)
                return;
            BeginEditing();
            return;
        }
        if (editing && reactivate == null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
            !RectTransformUtility.RectangleContainsScreenPoint(rect, Mouse.current.position.ReadValue(),
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera))
            EndEditing(false);
    }

    private void ResizeWithinHud()
    {
        if (canvas == null || !(canvas.transform is RectTransform parent)) return;
        // 각 HUD의 CanvasScaler는 그대로 두고 이 패널만 중앙 HUD 왼쪽에 맞춘다.
        float available = parent.rect.width * 0.5f - 220f;
        float width = Mathf.Min(480f, Mathf.Max(260f, available));
        float height = Mathf.Min(260f, parent.rect.height * 0.38f);
        rect.sizeDelta = new Vector2(width, height);
        // 고정 픽셀 HUD가 차지하는 폭을 확보할 수 없는 화면에서는 HUD 위로 배치한다.
        rect.anchoredPosition = new Vector2(24f, parent.rect.width < 1600f ? 196f : 24f);
    }

    public void BeginEditing()
    {
        if (session?.IsConnected != true) return;
        editing = true;
        session.SetInputFocused(true);
        input.interactable = true;
        input.ActivateInputField();
        RefreshAppearance();
    }

    public void EndEditing(bool cancelDraft)
    {
        if (reactivate != null) { StopCoroutine(reactivate); reactivate = null; }
        editing = false;
        if (session != null)
        {
            session.Draft = cancelDraft ? string.Empty : input.text;
            session.SetInputFocused(false);
        }
        if (cancelDraft) input.SetTextWithoutNotify(string.Empty);
        input.DeactivateInputField();
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == input.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        RefreshAppearance();
    }

    private void OnSelected(string _) { if (!editing) BeginEditing(); }
    private void OnDraftChanged(string text)
    {
        if (!changingInput && session != null) session.Draft = text;
    }

    private void OnSubmitted(string text)
    {
        if (!editing) return;
        if (reactivate != null) StopCoroutine(reactivate);
        reactivate = StartCoroutine(ReactivateInput());
    }

    private IEnumerator ReactivateInput()
    {
        yield return null;
        if (editing)
        {
            // Enter 한 번으로 조합 확정과 전송. TMP의 확정 이벤트가 반영된 본문을 사용한다.
            session?.TrySend(input.text);
            input.ActivateInputField();
        }
        reactivate = null;
    }

    private void Refresh()
    {
        if (input == null) return;
        changingInput = true;
        input.SetTextWithoutNotify(session?.Draft ?? string.Empty);
        changingInput = false;
        IReadOnlyList<ChatEntry> entries = session?.Entries;
        int count = entries?.Count ?? 0;
        bool newEntry = count > 0 && displayedRevision != session.Revision;
        if (newEntry || rebuildPending || count < displayedCount)
        {
            bool atBottom = scroll.verticalNormalizedPosition <= 0.02f || displayedCount == 0;
            float offset = content.anchoredPosition.y;
            if (count == ChatSession.HistoryLimit && displayedCount == count && rows.Count > 0 && newEntry)
                offset -= rows[0].rectTransform.rect.height + 4f;
            for (int i = 0; i < count; i++)
            {
                if (i == rows.Count)
                {
                    TMP_Text row = Instantiate(rowTemplate, content);
                    row.gameObject.SetActive(true);
                    rows.Add(row);
                }
                rows[i].gameObject.SetActive(true);
                rows[i].richText = entries[i].Kind == ChatKind.Acquisition;
                rows[i].text = Prefix(entries[i].Kind) + entries[i].Text;
                rows[i].color = ColorFor(entries[i].Kind);
            }
            for (int i = count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
            if (newEntry && !atBottom) unread++;
            displayedCount = count;
            displayedRevision = session?.Revision ?? -1;
            rebuildPending = false;
            if (scrollUpdate != null) StopCoroutine(scrollUpdate);
            if (isActiveAndEnabled) scrollUpdate = StartCoroutine(AfterLayout(atBottom, offset));
        }
        RefreshAppearance();
    }

    private IEnumerator AfterLayout(bool followBottom, float offset)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (followBottom) JumpToLatest();
        else content.anchoredPosition = new Vector2(content.anchoredPosition.x,
            Mathf.Clamp(offset, 0f, Mathf.Max(0f, content.rect.height - scroll.viewport.rect.height)));
        scrollUpdate = null;
    }

    private void RefreshAppearance()
    {
        bool showInput = NetworkManager.singleton is MirrorTestNetworkManager;
        input.gameObject.SetActive(showInput);
        hint.gameObject.SetActive(showInput);
        if (title != null) title.SetActive(showInput);
        RectTransform recordRect = (RectTransform)scroll.transform;
        recordRect.offsetMin = new Vector2(recordRect.offsetMin.x, showInput ? 52f : 12f);
        recordRect.offsetMax = new Vector2(recordRect.offsetMax.x, showInput ? -34f : -12f);
        Color color = background.color;
        color.a = editing ? 0.8f : 0.25f;
        background.color = color;
        recordInteraction.blocksRaycasts = editing;
        recordInteraction.interactable = editing;
        scroll.enabled = editing;
        input.interactable = session?.IsConnected == true;
        input.targetGraphic.raycastTarget = input.interactable;
        hint.text = session?.IsConnected != true ?
            (NetworkManager.singleton is MirrorTestNetworkManager ? "접속 후 대화할 수 있습니다" : string.Empty) :
            session.IsSending ? "전송 중…" : editing ? "Enter 전송 · Esc 취소" : "Enter 키로 대화";
        unreadButton.gameObject.SetActive(unread > 0 && editing);
    }

    public void JumpToLatest()
    {
        scroll.verticalNormalizedPosition = 0f;
        unread = 0;
        RefreshAppearance();
    }

    private static string Prefix(ChatKind kind) => kind switch
    {
        ChatKind.Chat => "[채팅] ", ChatKind.Acquisition => "[획득] ",
        ChatKind.Warning => "[안내] ", _ => "[연결] ",
    };

    private static Color ColorFor(ChatKind kind) => kind switch
    {
        ChatKind.Acquisition => new Color32(129, 216, 176, 255),
        ChatKind.Warning => new Color32(242, 193, 107, 255),
        ChatKind.Connection => new Color32(169, 183, 198, 255),
        _ => new Color32(230, 237, 243, 255),
    };
}
