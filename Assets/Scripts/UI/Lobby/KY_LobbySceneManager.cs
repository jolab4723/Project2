using UnityEngine;
using UnityEngine.UI;

public class KY_LobbySceneManager : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button passiveSkillButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button returnToTitleButton;

    [Header("팝업 매니저")]
    [SerializeField] private KY_PopupManager popupManager;

    [Header("연출")]
    [SerializeField] private KY_SlideAnimator[] lobbySlideAnimators;

    public KY_LobbyCharacterButton[] buttons;
    public KY_LobbyCharacterSlot slot;

    void Awake()
    {
        foreach (var btn in buttons)
            btn.OnClicked += OnCharacterSelected;
    }

    void Start()
    {
        passiveSkillButton.onClick.AddListener(OnClickPassiveSkill);
        startButton.onClick.AddListener(OnClickStart);
        returnToTitleButton.onClick.AddListener(OnClickReturnToTitle);

        if (lobbySlideAnimators == null)
            return;

        foreach (var slideAnimator in lobbySlideAnimators)
        {
            if (slideAnimator != null)
                slideAnimator.SlideIn();
        }
    }

    void OnCharacterSelected(KY_LobbyCharacterData data)
    {
        slot.Show(data);
    }

    void OnClickPassiveSkill()
    {
        popupManager.Show(PopupType.PassiveSkill);
    }

    public void OnClickStart()
    {
        // TODO
    }

    void OnClickReturnToTitle()
    {
        // TODO
    }
}
