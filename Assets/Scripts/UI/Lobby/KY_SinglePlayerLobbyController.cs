using System;
using TMPro;
using UnityEngine;

/// <summary>3D 캐릭터 선택과 상세 정보, 싱글 게임 시작 요청을 담당한다.</summary>
public class KY_SinglePlayerLobbyController : MonoBehaviour
{
    [SerializeField] private KY_CharacterSelectionController selectionController;
    [SerializeField] private GameObject detailsPanel;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private KY_SlideAnimator detailsSlideAnimator;
    public event Action<KY_CharacterId> StartGameRequested;
    private bool detailsHasEntered;

    private void Awake()
    {
        selectionController.SelectionConfirmed += RequestStartGame;
        selectionController.CharacterSelected += HandleCharacterSelected;
    }

    private void Start()
    {
        detailsPanel.SetActive(false);
        selectionController.ShowSelection();
    }

    private void HandleCharacterSelected(KY_CharacterId characterId)
    {
        if (detailsHasEntered) return;
        detailsHasEntered = true;
        detailsPanel.SetActive(true);
        detailsSlideAnimator?.ReplayIn();
    }


    private void RequestStartGame(KY_CharacterId characterId)
    {
        if (StartGameRequested == null)
        {
            feedbackText.text = "게임 시작 기능은 아직 연결되지 않았습니다.";
            return;
        }
        feedbackText.text = string.Empty;
        StartGameRequested.Invoke(characterId);
    }

    private void OnDestroy()
    {
        if (selectionController != null) selectionController.SelectionConfirmed -= RequestStartGame;
        if (selectionController != null) selectionController.CharacterSelected -= HandleCharacterSelected;
    }
}
