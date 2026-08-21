using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// production 미지 UI를 서버 Run Snapshot의 고정 이벤트와 방장 선택 요청에 연결한다.
/// 이벤트의 실제 효과 시스템은 production에 아직 없으므로 선택 확정과 노드 완료만 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MirrorUnknownStageAdapter_MirrorTest : MonoBehaviour
{
    private const string StageDatabaseResourcePath =
        "DataFiles/UnknownStageData/3. GeneratedAssets/AllUnknownStages";
    private const string LabelDatabaseResourcePath =
        "DataFiles/UnknownStageData/3. GeneratedAssets/AllUnknownStageLabels";

    [SerializeField] private YJ_UnknownStageContents unknownStageContents;
    [SerializeField] private YJ_ChoiceButtonBox choiceButtonBox;

    private readonly List<Button> choiceButtons = new();
    private MirrorTestNetworkManager networkManager;
    private bool choiceRequested;
    private bool clientReadyInteractivityApplied;

    public string SelectedStageId { get; private set; } = string.Empty;
    public int ChoiceCount => choiceButtons.Count;
    public bool IsBound => networkManager != null && !string.IsNullOrEmpty(SelectedStageId);
    public bool ChoiceRequested => choiceRequested;

    private void Start()
    {
        networkManager = NetworkManager.singleton as MirrorTestNetworkManager;
        if (!TryResolveStage(
                out YJ_UnknownStageDefinitionSO stage,
                out YJ_UnknownStageLabel label,
                out string error))
        {
            Debug.LogError($"[MirrorUnknownStageAdapter_MirrorTest] {error}", this);
            return;
        }

        SelectedStageId = stage.StageId;
        int choiceCount = Mathf.Clamp(stage.ChoiceNumber, 1, 3);
        BuildChoiceTexts(
            label,
            choiceCount,
            out List<string> titles,
            out List<string> descriptions);

        unknownStageContents.StageBackgroundSet(stage.BackgroundImage);
        choiceButtonBox.ButtonCreate(choiceCount);
        choiceButtonBox.ButtonTextSet(titles, descriptions);
        BindChoiceButtons();
        choiceButtonBox.HideButtons();
        unknownStageContents.PlayTextReveal(
            label.stageName,
            label.stageDescription,
            choiceButtonBox.PlayReveal);

        networkManager.ClientSessionLeaderChanged += HandleLeadershipChanged;
        RefreshInteractivity();
    }

    private void OnDestroy()
    {
        if (networkManager != null)
            networkManager.ClientSessionLeaderChanged -= HandleLeadershipChanged;
    }

    private void Update()
    {
        if (clientReadyInteractivityApplied ||
            !NetworkClient.active ||
            !NetworkClient.ready ||
            NetworkClient.localPlayer == null)
        {
            return;
        }

        clientReadyInteractivityApplied = true;
        RefreshInteractivity();
    }

    private bool TryResolveStage(
        out YJ_UnknownStageDefinitionSO stage,
        out YJ_UnknownStageLabel label,
        out string error)
    {
        stage = null;
        label = null;
        if (networkManager == null ||
            unknownStageContents == null ||
            choiceButtonBox == null)
        {
            error = "NetworkManager 또는 production 미지 UI 참조가 없습니다.";
            return false;
        }

        if (!networkManager.TryGetRunSnapshot(out StageMapSaveData snapshot) ||
            string.IsNullOrEmpty(snapshot.pendingNodeId))
        {
            error = "pending 미지 노드가 없습니다.";
            return false;
        }

        StageNodeSaveData pendingNode = snapshot.nodes.Find(
            node => node != null && node.id == snapshot.pendingNodeId);
        if (pendingNode == null ||
            pendingNode.type != StageNodeType.Event ||
            string.IsNullOrWhiteSpace(pendingNode.unknownStageId))
        {
            error = "pending 노드의 미지 이벤트 ID가 유효하지 않습니다.";
            return false;
        }

        YJ_UnknownStageDatabaseSO stageDatabase =
            Resources.Load<YJ_UnknownStageDatabaseSO>(
                StageDatabaseResourcePath);
        YJ_UnknownStageLabelDatabaseSO labelDatabase =
            Resources.Load<YJ_UnknownStageLabelDatabaseSO>(
                LabelDatabaseResourcePath);
        stage = stageDatabase != null
            ? stageDatabase.GetById(pendingNode.unknownStageId)
            : null;
        label = stage != null && labelDatabase != null
            ? labelDatabase.GetLabel(stage.StageId)
            : null;
        if (stage == null || label == null)
        {
            error = $"미지 이벤트 데이터 또는 표시 문구를 찾지 못했습니다: {pendingNode.unknownStageId}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void BindChoiceButtons()
    {
        choiceButtons.Clear();
        YJ_ChoiceButton[] productionButtons =
            choiceButtonBox.GetComponentsInChildren<YJ_ChoiceButton>(true);
        for (int i = 0; i < productionButtons.Length; i++)
        {
            YJ_ChoiceButton productionButton = productionButtons[i];
            Button button = productionButton.GetComponent<Button>();
            if (button == null)
                continue;

            int choiceIndex = i;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(
                () => HandleChoiceClicked(productionButton, choiceIndex));
            choiceButtons.Add(button);
        }
    }

    private void HandleChoiceClicked(
        YJ_ChoiceButton selectedButton,
        int choiceIndex)
    {
        if (choiceRequested ||
            networkManager == null ||
            !networkManager.CanLocalClientControlSession)
        {
            return;
        }

        choiceRequested = true;
        RefreshInteractivity();
        if (!choiceButtonBox.PlayExit(
                selectedButton,
                () =>
                {
                    if (networkManager.RequestUnknownStageChoice(choiceIndex))
                        return;

                    choiceRequested = false;
                    RefreshInteractivity();
                }))
        {
            choiceRequested = false;
            RefreshInteractivity();
        }
    }

    private void HandleLeadershipChanged(bool _)
    {
        RefreshInteractivity();
    }

    private void RefreshInteractivity()
    {
        bool interactable =
            !choiceRequested &&
            networkManager != null &&
            networkManager.CanLocalClientControlSession;
        foreach (Button button in choiceButtons)
        {
            if (button != null)
                button.interactable = interactable;
        }
    }

    private static void BuildChoiceTexts(
        YJ_UnknownStageLabel label,
        int choiceCount,
        out List<string> titles,
        out List<string> descriptions)
    {
        string[] allTitles =
        {
            label.choice1Name,
            label.choice2Name,
            label.choice3Name,
        };
        string[] allDescriptions =
        {
            label.choice1Description,
            label.choice2Description,
            label.choice3Description,
        };
        titles = new List<string>(choiceCount);
        descriptions = new List<string>(choiceCount);
        for (int i = 0; i < choiceCount; i++)
        {
            titles.Add(allTitles[i]);
            descriptions.Add(allDescriptions[i]);
        }
    }
}
