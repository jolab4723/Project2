using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Core;

/// <summary>
/// 연출용 : 캐릭터 선택 화면의 입력, 상세 정보, 미리보기와 진입 연출을 관리한다.
/// 선택 확정 뒤의 로비 전환은 KY_LobbyFlowController에 알린다.
/// </summary>
public class KY_CharacterSelectionController : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button passiveSkillButton;
    [SerializeField, FormerlySerializedAs("startButton")] private Button confirmSelectionButton;
    [SerializeField, FormerlySerializedAs("returnToTitleButton")] private Button backToTitleButton;
    [SerializeField] private Button optionsButton;
    [Header("팝업 매니저")]
    [SerializeField] private KY_PopupManager popupManager;
    [Header("연출")]
    [SerializeField, FormerlySerializedAs("lobbySlideAnimators")] private KY_SlideAnimator[] selectionSlideAnimators;
    [SerializeField] private KY_CurtainEffect radarChartCurtain;
    [Header("캐릭터 프리뷰")]
    [SerializeField] private KY_CharacterSelectionPreviewController previewController;
    [Header("캐릭터 선택")]
    [SerializeField] private KY_CharacterSelectionButton[] buttons;
    [SerializeField] private KY_CharacterSelectionDetailView slot;

    private KY_CharacterId? selectedCharacterId;

    /// <summary>선택한 캐릭터를 확정했을 때 호출한다.</summary>
    public event Action<KY_CharacterId> SelectionConfirmed;
    public event Action<KY_CharacterId> CharacterSelected;

    /// <summary>버튼과 캐릭터 카드의 이벤트를 등록한다.</summary>
    private void Awake()
    {
        passiveSkillButton?.onClick.AddListener(OnClickPassiveSkill);
        confirmSelectionButton?.onClick.AddListener(OnClickConfirmSelection);
        backToTitleButton?.onClick.AddListener(OnClickReturnToTitle);
        optionsButton?.onClick.AddListener(OnClickOptions);
        if (confirmSelectionButton != null) confirmSelectionButton.interactable = false;
        if (buttons == null) return;
        foreach (KY_CharacterSelectionButton button in buttons)
            if (button != null) button.OnClicked += OnCharacterSelected;
    }

    /// <summary>선택 화면을 열고 이전 선택값이 있으면 다시 표시한다.</summary>
    public void ShowSelection(KY_CharacterId? previousSelection = null)
    {
        if (previousSelection.HasValue) OnCharacterSelected(previousSelection.Value);
        PlayEntryEffects();
    }

    /// <summary>연출용 : 선택 화면의 패널과 레이더 커튼 진입 연출을 재생한다.</summary>
    private void PlayEntryEffects()
    {
        if (selectionSlideAnimators != null)
            foreach (KY_SlideAnimator slideAnimator in selectionSlideAnimators) slideAnimator?.ReplayIn();
        radarChartCurtain?.Open();
    }

    /// <summary>캐릭터 카드를 눌렀을 때 상세 정보와 3D 미리보기를 갱신한다.</summary>
    private void OnCharacterSelected(KY_CharacterId characterId)
    {
        KY_CharacterInfoData data = KY_CharacterDatabase.GetById(characterId);
        if (data == null)
        {
            Debug.LogWarning("[KY_CharacterSelectionController] 선택한 캐릭터 데이터를 찾을 수 없습니다.", this);
            return;
        }
        selectedCharacterId = characterId;
        CharacterSelected?.Invoke(characterId);
        slot?.Show(data);
        previewController?.ShowCharacter(characterId);
        if (confirmSelectionButton != null) confirmSelectionButton.interactable = true;
    }

    /// <summary>패시브 스킬 팝업을 연다.</summary>
    private void OnClickPassiveSkill() => popupManager?.Show(PopupType.PassiveSkill);

    /// <summary>현재 선택한 캐릭터를 확정하고 로비 전환 요청을 보낸다.</summary>
    public void OnClickConfirmSelection()
    {
        if (!selectedCharacterId.HasValue)
        {
            Debug.LogWarning("[KY_CharacterSelectionController] 캐릭터를 선택한 뒤 선택을 완료할 수 있습니다.", this);
            return;
        }
        SelectionConfirmed?.Invoke(selectedCharacterId.Value);
    }

    /// <summary>기존 Inspector 버튼 연결을 유지하며 선택 확정 처리로 전달한다.</summary>
    public void OnClickStart() => OnClickConfirmSelection();

    /// <summary>옵션 팝업을 연다.</summary>
    private void OnClickOptions() => popupManager?.Show(PopupType.Settings);

    /// <summary>타이틀 복귀 버튼의 외부 연결 지점이다.</summary>
    private void OnClickReturnToTitle()
    {
        SceneLoader loader = SceneLoader.Instance;
        if (loader == null || loader.IsLoading)
            return;

        loader.LoadScene("TitleScene");
    }

    /// <summary>등록한 버튼과 캐릭터 카드의 이벤트를 해제한다.</summary>
    private void OnDestroy()
    {
        passiveSkillButton?.onClick.RemoveListener(OnClickPassiveSkill);
        confirmSelectionButton?.onClick.RemoveListener(OnClickConfirmSelection);
        backToTitleButton?.onClick.RemoveListener(OnClickReturnToTitle);
        optionsButton?.onClick.RemoveListener(OnClickOptions);
        if (buttons == null) return;
        foreach (KY_CharacterSelectionButton button in buttons)
            if (button != null) button.OnClicked -= OnCharacterSelected;
    }
}
