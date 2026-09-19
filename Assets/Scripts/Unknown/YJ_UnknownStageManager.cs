using System.Collections.Generic;
using UnityEngine;

public class YJ_UnknownStageManager : MonoBehaviour
{
    private const string StageDatabaseResourcePath =
        "DataFiles/UnknownStageData/3. GeneratedAssets/AllUnknownStages";
    private const string LabelDatabaseResourcePath =
        "DataFiles/UnknownStageData/3. GeneratedAssets/AllUnknownStageLabels";

    [Header("UI")]
    [SerializeField] private YJ_UnknownStageContents unknownStageContents;
    [SerializeField] private YJ_ChoiceButtonBox choiceButtonBox;

    [Header("Data")]
    [SerializeField] private YJ_UnknownStageDatabaseSO stageDatabase;
    [SerializeField] private YJ_UnknownStageLabelDatabaseSO labelDatabase;
    [SerializeField] private YJ_StageSaveService stageSaveService;
    [SerializeField] private string stageSelectSceneName = "StageSelect";

    [Header("Runtime")]
    [SerializeField] private YJ_UnknownStageDefinitionSO selectedStage;

    private YJ_LanguageManager languageManager;
    private bool isProcessingChoice;
    private string requestedNodeKey;
    private string requestedNodeId;
    private YJ_UnknownDiscardPanel discardPanel;

    private void OnEnable()
    {
        BindLanguageManager();
    }

    private void OnDisable()
    {
        CancelDiscardSelection();
        UnbindLanguageManager();
    }

    private void Start()
    {
        if (!ResolveReferences())
            return;

        if (!TrySelectSavedStage() &&
            selectedStage == null &&
            !TrySelectRandomStage())
        {
            return;
        }

        ApplySelectedStage(true);
    }

    private void BindLanguageManager()
    {
        if (languageManager != null)
            return;

        languageManager = YJ_LanguageManager.Instance;
        if (languageManager == null)
        {
            Log.Error("YJ_LanguageManager could not be found.");
            return;
        }

        languageManager.LanguageChanged += HandleLanguageChanged;
    }

    private void UnbindLanguageManager()
    {
        if (languageManager == null)
            return;

        languageManager.LanguageChanged -= HandleLanguageChanged;
        languageManager = null;
    }

    private void HandleLanguageChanged(GameLanguage _)
    {
        CancelDiscardSelection();
        if (selectedStage != null && labelDatabase != null)
            ApplySelectedStage(false);
    }

    private bool ResolveReferences()
    {
        BindLanguageManager();

        if (languageManager == null)
            return false;

        if (stageDatabase == null)
        {
            stageDatabase =
                Resources.Load<YJ_UnknownStageDatabaseSO>(
                    StageDatabaseResourcePath);
        }

        if (labelDatabase == null)
        {
            labelDatabase =
                Resources.Load<YJ_UnknownStageLabelDatabaseSO>(
                    LabelDatabaseResourcePath);
        }

        if (unknownStageContents == null)
        {
            Log.Error("YJ_UnknownStageContents reference is missing.");
            return false;
        }

        if (choiceButtonBox == null)
        {
            Log.Error("YJ_ChoiceButtonBox reference is missing.");
            return false;
        }

        if (stageDatabase == null)
        {
            Log.Error("AllUnknownStages database could not be loaded.");
            return false;
        }

        if (labelDatabase == null)
        {
            Log.Error("AllUnknownStageLabels database could not be loaded.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Stage Select가 저장한 pending Event 노드의 고정 이벤트 ID를 불러옵니다.
    /// </summary>
    private bool TrySelectSavedStage()
    {
        FindSaveService();

        if (stageSaveService == null ||
            !stageSaveService.HasSaveFile ||
            !stageSaveService.TryGetPendingNode(
                out StageNodeSaveData pendingNode))
        {
            return false;
        }

        if (pendingNode.type != StageNodeType.Event ||
            string.IsNullOrWhiteSpace(pendingNode.unknownStageId))
        {
            Log.Warning(
                $"pending 노드에 Unknown 이벤트 ID가 없습니다: " +
                $"{pendingNode.id}");
            return false;
        }

        selectedStage =
            stageDatabase.GetById(pendingNode.unknownStageId);

        if (selectedStage != null)
            return true;

        Log.Warning(
            $"저장된 Unknown 이벤트를 데이터베이스에서 찾지 못했습니다: " +
            $"{pendingNode.unknownStageId}");
        return false;
    }

    /// <summary>
    /// Unknown 씬 단독 실행처럼 저장된 Event 노드가 없을 때 사용할 테스트용 임의 이벤트를 선택합니다.
    /// </summary>
    private bool TrySelectRandomStage()
    {
        IReadOnlyList<YJ_UnknownStageDefinitionSO> stages =
            stageDatabase.Stages;

        if (stages == null || stages.Count == 0)
        {
            Log.Error("Unknown Stage database is empty.");
            return false;
        }

        selectedStage = stages[Random.Range(0, stages.Count)];

        if (selectedStage != null)
            return true;

        Log.Error("The randomly selected Unknown Stage is null.");
        return false;
    }

    /// <summary>
    /// Inspector, 같은 오브젝트, 현재 씬 순서로 저장 서비스를 찾고 없으면 추가합니다.
    /// </summary>
    private void FindSaveService()
    {
        if (stageSaveService != null)
            return;

        stageSaveService = GetComponent<YJ_StageSaveService>();
        if (stageSaveService == null)
            stageSaveService =
                FindFirstObjectByType<YJ_StageSaveService>();

        if (stageSaveService == null)
            stageSaveService =
                gameObject.AddComponent<YJ_StageSaveService>();
    }

    private void ApplySelectedStage(bool createButtons)
    {
        YJ_UnknownStageLabel label =
            labelDatabase.GetLabel(selectedStage.StageId);

        if (label == null)
        {
            Log.Error($"Unknown Stage label was not found: {selectedStage.StageId}");
            return;
        }

        unknownStageContents.StageBackgroundSet(selectedStage.BackgroundImage);

        int choiceCount = Mathf.Clamp(selectedStage.ChoiceNumber, 1, 3);
        BuildChoiceTexts(label, choiceCount, out List<string> titles, out List<string> descriptions);

        if (createButtons)
        {
            choiceButtonBox.ButtonCreate(choiceCount);
            choiceButtonBox.BindChoices(selectedStage.StageId, HandleChoiceSelected);
        }

        choiceButtonBox.ButtonTextSet(titles, descriptions);
        choiceButtonBox.HideButtons();
        unknownStageContents.PlayTextReveal(
            label.stageName,
            label.stageDescription,
            choiceButtonBox.PlayReveal);
    }

    private void HandleChoiceSelected(string stageId, int choiceIndex)
        => HandleChoiceSelected(stageId, choiceIndex, null);

    private void HandleChoiceSelected(string stageId, int choiceIndex, List<string> discardedItemIds)
    {
        if (!isActiveAndEnabled || isProcessingChoice || selectedStage == null ||
            choiceButtonBox == null || !choiceButtonBox.CanSelect ||
            Core.SceneLoader.Instance != null && Core.SceneLoader.Instance.IsLoading)
            return;

        if (!string.Equals(stageId, selectedStage.StageId, System.StringComparison.Ordinal))
        {
            Log.Error("현재 Unknown 이벤트와 버튼의 이벤트 ID가 다릅니다.");
            return;
        }

        isProcessingChoice = true;
        choiceButtonBox.SetButtonsInteractable(false);
        bool keepLocked = false;
        try
        {
            Core.SceneLoader loader = Core.SceneLoader.Instance;
            if (loader == null || string.IsNullOrWhiteSpace(stageSelectSceneName) ||
                !Application.CanStreamedLevelBeLoaded(stageSelectSceneName))
            {
                Log.Error("SceneLoader 또는 StageSelect 빌드 씬 설정이 없습니다. 보상을 적용하지 않습니다.");
                return;
            }

            YJ_ChoiceButton selectedButton = choiceButtonBox.GetChoiceButton(choiceIndex);
            if (selectedButton == null)
                return;

            if (!selectedStage.TryGetChoice(choiceIndex, out var choice, out string error))
            {
                Log.Warning($"[Unknown] {stageId} / 선택지 {choiceIndex + 1}: {error}");
                return;
            }

            if (!TryResolveSelectionNode(out error))
            {
                Log.Warning($"[Unknown] {error}");
                return;
            }
            if (Core.DataManager.Instance == null)
            {
                Log.Error("DataManager가 없어 Unknown 선택을 저장할 수 없습니다.");
                return;
            }
            if (discardedItemIds == null)
            {
                foreach (var effect in choice.Effects)
                {
                    if (effect.Type != YJ_UnknownEffectType.DiscardSelectedItems) continue;
                    if (!Core.DataManager.Instance.TryGetUnknownDiscardOptions(requestedNodeKey, selectedStage, choiceIndex,
                        out var items, out int required, out bool completed, out error))
                    { Log.Warning($"[Unknown] {error}"); return; }
                    if (completed) break; // 노드 완료/이동 재시도는 선택창을 다시 열지 않는다.
                    keepLocked = OpenDiscardSelection(stageId, choiceIndex, items, required);
                    return;
                }
            }
            if (!Core.DataManager.Instance.TryApplyUnknownStageChoice(requestedNodeKey, selectedStage, choiceIndex, out error, discardedItemIds))
            {
                Log.Warning($"[Unknown] {error}");
                return;
            }

            // 지급은 이미 기록되었다. 완료 저장 실패 시 같은 선택을 재시도해도 재지급하지 않는다.
            if (!stageSaveService.CompletePendingNode())
            {
                Log.Warning("[Unknown] 보상은 저장되었지만 노드 완료에 실패했습니다. 같은 선택을 다시 눌러 재시도하세요.");
                return;
            }

            keepLocked = choiceButtonBox.PlayExit(selectedButton, () =>
            {
                if (loader != null && !loader.IsLoading)
                    loader.LoadScene(stageSelectSceneName);
                else if (loader == null)
                {
                    Log.Error("[Unknown] SceneLoader가 사라졌습니다. 보상 재지급 없이 이동을 재시도할 수 있습니다.");
                    isProcessingChoice = false;
                    choiceButtonBox.HideButtons();
                    choiceButtonBox.PlayReveal();
                }
            });
        }
        finally
        {
            isProcessingChoice = keepLocked;
            if (!keepLocked && choiceButtonBox != null)
                choiceButtonBox.SetButtonsInteractable(true);
        }
    }

    private bool OpenDiscardSelection(string stageId, int choiceIndex, List<Core.ItemSaveData> items, int required)
    {
        var canvas = choiceButtonBox.GetComponentInParent<Canvas>();
        var template = choiceButtonBox.GetComponentInChildren<TMPro.TMP_Text>(true);
        var database = Core.ItemManager.Instance != null ? Core.ItemManager.Instance.ItemDatabase : null;
        if (canvas == null || template == null || template.font == null || database == null)
        { Log.Error("[Unknown] 폐기 선택창의 Canvas, 폰트 또는 아이템 DB가 없습니다."); return false; }
        var entries = new List<YJ_UnknownDiscardPanel.Entry>();
        var statNames = ItemSystem.ItemDisplayNames.StatNames;
        foreach (var saved in items)
        {
            var definition = string.IsNullOrWhiteSpace(saved.itemId) ? null : database.GetById(saved.itemId);
            if (definition == null) { Log.Warning("[Unknown] 아이템 원본이 없어 폐기 목록을 표시할 수 없습니다."); return false; }
            string text = $"{definition.itemName} (+{saved.upgradeLevel}) · 가방 ({saved.gridX + 1}, {saved.gridY + 1})";
            if (saved.rolledSubStats != null)
                foreach (var stat in saved.rolledSubStats)
                    text += $"\n{(statNames.TryGetValue(stat.statType, out var name) ? name : stat.statType.ToString())}: {stat.value:0.##}";
            entries.Add(new YJ_UnknownDiscardPanel.Entry { id = saved.instanceId, text = text, icon = definition.icon });
        }
        discardPanel = YJ_UnknownDiscardPanel.Open(canvas, template.font, entries, required, ids =>
        {
            CancelDiscardSelection();
            HandleChoiceSelected(stageId, choiceIndex, ids);
        }, CancelDiscardSelection);
        return true;
    }

    private void CancelDiscardSelection()
    {
        if (discardPanel == null) return;
        discardPanel.Close(); discardPanel = null;
        isProcessingChoice = false;
        if (choiceButtonBox != null) choiceButtonBox.SetButtonsInteractable(true);
    }

    private bool TryResolveSelectionNode(out string error)
    {
        error = null;
        FindSaveService();
        if (stageSaveService == null || !stageSaveService.TryLoadSaveData(out StageMapSaveData map) || map?.nodes == null)
        {
            error = "진행 중인 스테이지 맵이 없습니다. Unknown 씬 단독 실행에서는 실제 보상을 지급하지 않습니다.";
            return false;
        }
        StageNodeSaveData node = map.nodes.Find(n => n != null && n.id == map.pendingNodeId);
        if (node != null && node.type == StageNodeType.Event && node.unknownStageId == selectedStage.StageId)
        {
            string key = $"{(int)map.act}:{map.mapSeed}:{node.id}";
            if (requestedNodeKey != null && requestedNodeKey != key)
            {
                error = "선택 처리 도중 진행 노드가 변경되었습니다.";
                return false;
            }
            requestedNodeId = node.id;
            requestedNodeKey = key;
            return true;
        }
        // 같은 씬에서 노드 완료 후 퇴장 연출만 실패한 경우의 재시도.
        if (string.IsNullOrEmpty(map.pendingNodeId) && requestedNodeKey != null &&
            requestedNodeKey == $"{(int)map.act}:{map.mapSeed}:{requestedNodeId}" &&
            map.lastClearedNodeId == requestedNodeId && map.clearedNodeIds != null && map.clearedNodeIds.Contains(requestedNodeId))
            return true;

        error = "현재 이벤트와 일치하는 pending 노드가 없습니다. 보상을 적용하지 않습니다.";
        return false;
    }

    private static void BuildChoiceTexts(
        YJ_UnknownStageLabel label,
        int choiceCount,
        out List<string> titles,
        out List<string> descriptions)
    {
        titles = new List<string>(choiceCount);
        descriptions = new List<string>(choiceCount);

        if (choiceCount >= 1)
        {
            titles.Add(label.choice1Name);
            descriptions.Add(label.choice1Description);
        }

        if (choiceCount >= 2)
        {
            titles.Add(label.choice2Name);
            descriptions.Add(label.choice2Description);
        }

        if (choiceCount >= 3)
        {
            titles.Add(label.choice3Name);
            descriptions.Add(label.choice3Description);
        }
    }
}
