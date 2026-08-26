using UnityEngine;
using UnityEngine.UI;

public class KY_LobbySceneManager : MonoBehaviour
{
    public KY_LobbyCharacterButton[] buttons;
    public KY_LobbyCharacterSlot slot;
    public Button startButton;

    void Awake()
    {
        foreach (var btn in buttons)
            btn.OnClicked += OnCharacterSelected;

        startButton.interactable = false;
    }

    void OnCharacterSelected(KY_LobbyCharacterData data)
    {
        slot.Show(data);
        startButton.interactable = data.isUnlocked;
    }

    public void OnClickStart() { }
}