using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>3D 캐릭터 선택과 상세 정보, 싱글 게임 시작 요청을 담당한다.</summary>
public class KY_SinglePlayerLobbyController : MonoBehaviour
{
    [SerializeField] private KY_CharacterSelectionController selectionController;
    [SerializeField] private GameObject detailsPanel;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private KY_SlideAnimator detailsSlideAnimator;
    [Header("게임 시작 전환 연출")]
    [SerializeField] private SpriteRenderer focusRing;
    [SerializeField, Min(0f)] private float startTransitionDuration = 0.45f;
    public event Action<KY_CharacterId> StartGameRequested;
    private bool detailsHasEntered;
    private bool isStarting;

    private void Awake()
    {
        selectionController.SelectionConfirmed += RequestStartGame;
        selectionController.CharacterSelected += HandleCharacterSelected;
    }

    private void Start()
    {
        detailsPanel.SetActive(false);
        selectionController.ShowSelection();
        YJ_BgmPlayer.Instance.Play(YJ_BgmPlayer.YJ_BgmType.LobbyBgm);
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
        if (isStarting) return;

        if (StartGameRequested == null)
        {
            feedbackText.text = "게임 시작 기능은 아직 연결되지 않았습니다.";
            return;
        }

        feedbackText.text = string.Empty;
        StartCoroutine(PlayStartTransition(characterId));
    }

    private IEnumerator PlayStartTransition(KY_CharacterId characterId)
    {
        isStarting = true;
        selectionController.PlayExitEffects();
        detailsSlideAnimator?.SlideOut();

        if (focusRing != null)
        {
            Color originalColor = focusRing.color;
            Vector3 originalScale = focusRing.transform.localScale;
            focusRing.DOKill();
            focusRing.transform.DOKill();
            focusRing.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
            focusRing.transform.localScale = originalScale * 0.8f;
            focusRing.DOFade(originalColor.a, startTransitionDuration * 0.35f).SetUpdate(true);
            focusRing.transform.DOScale(originalScale * 1.15f, startTransitionDuration).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(startTransitionDuration);
        StartGameRequested.Invoke(characterId);
        YJ_BgmPlayer.Instance.Stop();
    }

    private void OnDestroy()
    {
        if (selectionController != null) selectionController.SelectionConfirmed -= RequestStartGame;
        if (selectionController != null) selectionController.CharacterSelected -= HandleCharacterSelected;
    }
}
