using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class YJ_PopupKeyGuide : MonoBehaviour
{
    [Serializable]
    private sealed class PopupButtonBinding
    {
        [SerializeField] private Button button;
        [SerializeField] private PopupType popupType;

        public Button Button => button;
        public PopupType PopupType => popupType;
    }

    private sealed class RegisteredListener
    {
        public Button Button { get; }
        public UnityAction Listener { get; }

        public RegisteredListener(Button button, UnityAction listener)
        {
            Button = button;
            Listener = listener;
        }
    }

    [Header("추가 팝업 버튼")]
    [Tooltip("기본 4개 버튼 외에 버튼이 추가되면 버튼과 팝업 종류를 등록합니다.")]
    [SerializeField] private List<PopupButtonBinding> popupButtons = new();

    private readonly List<RegisteredListener> registeredListeners = new();
    private InventoryPartView inventoryPartView;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeSceneHook()
    {
        SceneManager.sceneLoaded -= RegisterLoadedSceneKeyGuides;
        SceneManager.sceneLoaded += RegisterLoadedSceneKeyGuides;
    }

    private static void RegisterLoadedSceneKeyGuides(Scene scene, LoadSceneMode loadMode)
    {
        KY_KeyGuideView[] keyGuideViews = FindObjectsByType<KY_KeyGuideView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (KY_KeyGuideView keyGuideView in keyGuideViews)
        {
            if (keyGuideView.GetComponentInParent<YJ_PopupKeyGuide>(true) == null)
                keyGuideView.gameObject.AddComponent<YJ_PopupKeyGuide>();
        }
    }

    private void OnEnable()
    {
        RegisterButtons();
    }

    private void OnDisable()
    {
        UnregisterButtons();
    }

    private void RegisterButtons()
    {
        UnregisterButtons();

        HashSet<Button> registeredButtons = new();

        foreach (PopupButtonBinding binding in popupButtons)
        {
            if (binding == null)
                continue;

            RegisterButton(binding.Button, binding.PopupType, registeredButtons);
        }

        KY_KeyGuideView keyGuideView = GetComponent<KY_KeyGuideView>();
        if (keyGuideView == null)
            keyGuideView = GetComponentInChildren<KY_KeyGuideView>(true);

        if (keyGuideView == null)
            return;

        RegisterSlot(keyGuideView.inventorySlot, PopupType.Inventory, registeredButtons);
        RegisterSlot(keyGuideView.statusSlot, PopupType.Status, registeredButtons);
        RegisterSlot(keyGuideView.skillSlot, PopupType.Skill, registeredButtons);
        RegisterSlot(keyGuideView.questSlot, PopupType.Quest, registeredButtons);
    }

    private void RegisterSlot(
        KY_KeyGuideSlot slot,
        PopupType popupType,
        HashSet<Button> registeredButtons)
    {
        if (slot == null)
            return;

        RegisterButton(slot.GetComponent<Button>(), popupType, registeredButtons);
    }

    private void RegisterButton(
        Button button,
        PopupType popupType,
        HashSet<Button> registeredButtons)
    {
        if (button == null || !registeredButtons.Add(button))
            return;

        // BossStage처럼 Inspector 이벤트가 이미 정상 연결된 경우 중복 실행하지 않는다.
        if (HasValidPersistentListener(button))
            return;

        UnityAction listener = () => RequestPopup(popupType);
        button.onClick.AddListener(listener);
        registeredListeners.Add(new RegisteredListener(button, listener));
    }

    private static bool HasValidPersistentListener(Button button)
    {
        int listenerCount = button.onClick.GetPersistentEventCount();

        for (int i = 0; i < listenerCount; i++)
        {
            if (button.onClick.GetPersistentTarget(i) != null &&
                !string.IsNullOrEmpty(button.onClick.GetPersistentMethodName(i)))
            {
                return true;
            }
        }

        return false;
    }

    private void RequestPopup(PopupType popupType)
    {
        switch (popupType)
        {
            case PopupType.Inventory:
                ToggleInventory();
                break;
            case PopupType.Status:
                KY_GameEvents.StatusRequested();
                break;
            case PopupType.Skill:
                KY_GameEvents.SkillRequested();
                break;
            case PopupType.Quest:
                KY_GameEvents.QuestRequested();
                break;
            case PopupType.Pause:
                KY_GameEvents.EscPressed();
                break;
            default:
                Debug.LogWarning($"{popupType} 팝업은 KeyGuide 요청 방식이 등록되지 않았습니다.", this);
                break;
        }
    }

    private void ToggleInventory()
    {
        if (inventoryPartView == null)
            inventoryPartView = FindFirstObjectByType<InventoryPartView>();

        if (inventoryPartView != null)
        {
            inventoryPartView.ToggleInventory();
            return;
        }

        KY_GameEvents.InventoryRequested();
    }

    private void UnregisterButtons()
    {
        foreach (RegisteredListener registeredListener in registeredListeners)
        {
            if (registeredListener.Button != null)
                registeredListener.Button.onClick.RemoveListener(registeredListener.Listener);
        }

        registeredListeners.Clear();
    }
}
