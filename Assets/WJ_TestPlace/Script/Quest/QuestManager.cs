using System;
using System.Collections.Generic;
using UnityEngine;
using ItemSystem;

/// <summary>
/// 퀘스트 시작/진행/완료/보상을 전담하는 매니저. 한 퀘스트가 여러 조건(QuestConditionType)을 동시에
/// 가질 수 있고, 그 조건 전부가 충족되면 자동으로 완료 처리하고 보상을 지급한다(별도 "수령" 조작 없음 -
/// NPC 대화/turn-in 흐름이 아직 없어서 우선 자동 완료로 구현). 목표 달성 이력은 유지하며,
/// 지급하지 못한 보상은 인벤토리가 준비되거나 공간이 생긴 뒤 자동으로 재시도한다.
///
/// !! 저장/불러오기는 DataManager가 전담한다(DataManager.SaveQuestData/LoadQuestData). 이 클래스는
///    GetSaveData()/ApplySaveData()로 데이터만 내어주고 받을 뿐, 파일 입출력은 하지 않는다
///    (SettingManager/PassiveSkillManager와 동일한 역할 분리).
///
/// !! 보상 크레딧/아이템은 InventoryController.Instance(로컬 플레이어)로 지급한다. InventoryController가
///    비활성 인벤토리 팝업 안에 있어서 팝업을 한 번도 안 열면 Instance가 계속 null일 수 있다는 게 이미
///    확인된 구조적 한계다(158번 작업 로그 참고) - 그 문제 자체는 이 클래스가 고치지 않으며,
///    그동안 보상을 미수령 상태로 보존한다.
/// </summary>
public class QuestManager : Singleton<QuestManager>
{
    [Tooltip("프로젝트에 존재하는 모든 퀘스트 정의. 인스펙터에서 QuestDatabaseSO 에셋을 연결해야 한다.")]
    [SerializeField] private QuestDatabaseSO database;

    [Header("다국어(비워두면 QuestDefinitionSO 원본 문구로 폴백)")]
    [SerializeField] private QuestLabelDatabaseSO questLabels;
    [SerializeField] private ItemLabelDatabaseSO itemLabels;

    /// <summary>QuestBoardNPC 등 다른 퀘스트 관련 컴포넌트가 같은 다국어 DB를 공유해서 쓸 때 참조.</summary>
    public QuestLabelDatabaseSO QuestLabels => questLabels;

    /// <summary>표시 컴포넌트가 현재 진행 항목의 디자인 정보를 같은 DB에서 읽는다.</summary>
    public QuestDefinitionSO GetDefinition(string questId) => database != null ? database.FindById(questId) : null;

    private readonly List<ActiveQuestData> activeQuests = new List<ActiveQuestData>();
    private InventoryController subscribedInventory;
    // SW 수정 : AddItem 이벤트로 다른 퀘스트가 완료되어도 현재 지급을 재진입하지 않는다.
    private bool grantingRewards;
    private float nextRewardRetry;

    /// <summary>진행 중/완료된 퀘스트 전체 목록(읽기 전용). UI 조회용.</summary>
    public IReadOnlyList<ActiveQuestData> ActiveQuests => activeQuests;

    /// <summary>퀘스트 목록 구성이 바뀔 때(시작/완료) 발행. UI가 전체 목록을 다시 그릴 때 사용.</summary>
    public event Action OnQuestListChanged;

    /// <summary>특정 퀘스트의 조건 진행도만 바뀔 때 발행(목록 구성은 그대로). UI가 진행도 숫자만 갱신할 때 사용.</summary>
    public event Action<ActiveQuestData> OnQuestProgressChanged;

    /// <summary>목표를 처음 달성했을 때 한 번 발행한다. 보상 지급 완료를 뜻하지 않는다.</summary>
    public event Action<ActiveQuestData> OnQuestCompleted;

    // SW 수정 : 첫 표시 전에 저장된 완료 이력을 복원한다. 복원은 완료 알림을 발행하지 않는다.
    private void Start()
    {
        if (!Mirror.NetworkClient.active && !Mirror.NetworkServer.active)
            Core.DataManager.Instance.LoadQuestData();
    }

    private void OnEnable()
    {
        // 씬에서 직접 안 배선해도(다른 맵/스테이지 씬 등) Resources의 공용 DB를 자동으로 찾아 쓴다.
        if (questLabels == null)
            questLabels = Resources.Load<QuestLabelDatabaseSO>("DataFiles/QuestData/3. GeneratedAssets/QuestLabelDatabase");
        if (itemLabels == null)
            itemLabels = Resources.Load<ItemLabelDatabaseSO>("DataFiles/ItemData/3. GeneratedAssets/LabelData/ItemLabelDatabase");

        WBH_EnemyController.OnEnemyDead += HandleEnemyDead;
    }

    private void OnDisable()
    {
        WBH_EnemyController.OnEnemyDead -= HandleEnemyDead;

        if (subscribedInventory != null)
            subscribedInventory.OnItemAdded -= HandleItemAdded;

        subscribedInventory = null;
    }

    private void Update()
    {
        // 멀티에서는 Mirror 세션의 서버 판정과 참가자별 보상이 유일한 원본이다.
        if (Mirror.NetworkClient.active || Mirror.NetworkServer.active)
            return;
        // InventoryController가 비활성 인벤토리 팝업 안에 있어서 시작 시점엔 Instance가 null일 수 있다
        // (158번에서 확인한 구조적 한계) - SW 수정 : 준비되거나 다른 인벤토리로 바뀔 때 구독을 갱신한다.
        if (subscribedInventory != InventoryController.Instance)
        {
            if (subscribedInventory != null)
                subscribedInventory.OnItemAdded -= HandleItemAdded;
            subscribedInventory = InventoryController.Instance;
            if (subscribedInventory != null)
                subscribedInventory.OnItemAdded += HandleItemAdded;
        }

        if (Time.unscaledTime >= nextRewardRetry)
        {
            nextRewardRetry = Time.unscaledTime + 0.5f;
            TryGrantPendingRewards();
        }
    }

    private void HandleEnemyDead()
    {
        ReportKillEnemy();
    }

    private void HandleItemAdded(InventoryItem item)
    {
        string itemId = item?.itemData?.definition?.itemId;
        if (!string.IsNullOrEmpty(itemId))
            ReportItemCollected(itemId);
    }

    /// <summary>
    /// 아직 시작 안 했고 완료도 안 한 퀘스트 중 하나를 무작위로 뽑는다(의뢰 NPC의 제시/리롤용).
    /// excludeQuestId를 넘기면 그 퀘스트는 후보에서 제외한다(리롤 시 방금 보여준 것과 같은 게 다시
    /// 뽑히지 않게). 후보가 하나도 없으면 null.
    /// </summary>
    public QuestDefinitionSO GetRandomAvailableQuest(string excludeQuestId = null)
    {
        if (database == null || database.allQuests == null)
            return null;

        var candidates = new List<QuestDefinitionSO>();
        foreach (QuestDefinitionSO quest in database.allQuests)
        {
            if (quest == null || string.IsNullOrEmpty(quest.questId))
                continue;

            if (quest.questId == excludeQuestId)
                continue;

            if (FindActive(quest.questId) != null)
                continue;

            candidates.Add(quest);
        }

        if (candidates.Count == 0)
            return null;

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    /// <summary>퀘스트를 시작한다. questId가 없거나 이미 시작(진행 중/완료)된 퀘스트면 아무 것도 안 하고 false.</summary>
    public bool StartQuest(QuestDefinitionSO definition)
    {
        if (Mirror.NetworkClient.active || Mirror.NetworkServer.active)
            return false;
        if (definition == null || string.IsNullOrEmpty(definition.questId))
        {
            Debug.LogWarning("[QuestManager] questId가 없는 퀘스트는 시작할 수 없습니다.");
            return false;
        }

        if (FindActive(definition.questId) != null)
            return false;

        ActiveQuestData data = CreateProgress(definition);
        if (data == null)
            return false;

        activeQuests.Add(data);
        OnQuestListChanged?.Invoke();
        SaveQuestState();
        return true;
    }

    /// <summary>적 처치 조건 진행. targetId를 비워두면(현재 유일하게 지원되는 방식) targetId가 빈 모든
    /// KillEnemy 조건에 반영된다 - QuestConditionType 주석 참고.</summary>
    public void ReportKillEnemy(string targetId = null)
    {
        ReportProgress(QuestConditionType.KillEnemy, targetId, 1);
    }

    /// <summary>아이템 획득 조건 진행.</summary>
    public void ReportItemCollected(string itemId, int amount = 1)
    {
        ReportProgress(QuestConditionType.CollectItem, itemId, amount);
    }

    private void ReportProgress(QuestConditionType type, string targetId, int amount)
    {
        if (database == null || amount <= 0 || Mirror.NetworkClient.active || Mirror.NetworkServer.active ||
            Core.DataManager.Instance.IsRestoringGameplay)
            return;

        bool changed = false;
        for (int i = 0; i < activeQuests.Count; i++)
        {
            ActiveQuestData active = activeQuests[i];
            if (active.isCompleted)
                continue;

            QuestDefinitionSO def = database.FindById(active.questId);
            if (def == null)
                continue;

            if (!TryAdvanceProgress(def, active, type, targetId, amount))
                continue;

            changed = true;
            OnQuestProgressChanged?.Invoke(active);

            if (IsAllConditionsMet(def, active))
                CompleteQuest(active);
        }

        if (changed)
            SaveQuestState();
    }

    /// <summary>로컬 이벤트 구독·지갑 접근·파일 저장 없이 호출자가 소유할 진행 상태를 만든다.</summary>
    public static ActiveQuestData CreateProgress(QuestDefinitionSO definition)
    {
        // SW 수정
        if (definition == null || string.IsNullOrEmpty(definition.questId) || definition.conditions == null)
            return null;
        var progress = new ActiveQuestData { questId = definition.questId, rewardInitialized = true };
        for (int i = 0; i < definition.conditions.Length; i++)
            progress.conditionProgress.Add(0);
        return progress;
    }

    /// <summary>전달받은 상태의 진행도만 계산한다. 완료 확정과 보상 대상은 호출자가 결정한다.</summary>
    public static bool TryAdvanceProgress(QuestDefinitionSO definition, ActiveQuestData progress,
        QuestConditionType type, string targetId, int amount = 1)
    {
        if (!IsMatchingProgress(definition, progress) || progress.isCompleted || amount <= 0)
            return false;
        bool changed = false;
        for (int i = 0; i < definition.conditions.Length; i++)
        {
            QuestConditionDefinition condition = definition.conditions[i];
            if (condition == null || condition.conditionType != type ||
                (!string.IsNullOrEmpty(condition.targetId) && condition.targetId != targetId))
                continue;
            int current = progress.conditionProgress[i];
            if (current >= condition.requiredCount)
                continue;
            // 큰 획득 수를 더해도 정수 오버플로로 진행도가 음수가 되지 않게 한다.
            progress.conditionProgress[i] = (int)Math.Min((long)current + amount, condition.requiredCount);
            changed = true;
        }
        return changed;
    }

    private static bool IsMatchingProgress(QuestDefinitionSO definition, ActiveQuestData progress) =>
        definition != null && definition.conditions != null && progress != null &&
        progress.questId == definition.questId && progress.conditionProgress != null &&
        progress.conditionProgress.Count == definition.conditions.Length;

    public static bool IsAllConditionsMet(QuestDefinitionSO def, ActiveQuestData active)
    {
        if (!IsMatchingProgress(def, active) || def.conditions.Length == 0)
            return false;
        for (int c = 0; c < def.conditions.Length; c++)
        {
            if (def.conditions[c] == null || active.conditionProgress[c] < def.conditions[c].requiredCount)
                return false;
        }
        return true;
    }

    private void CompleteQuest(ActiveQuestData active)
    {
        if (active.isCompleted)
            return;
        active.isCompleted = true;
        active.rewardInitialized = true;
        TryGrantPendingRewards();
        OnQuestCompleted?.Invoke(active);
        OnQuestListChanged?.Invoke();
    }

    /// <summary>목표 완료와 별개로 아직 지급하지 못한 크레딧 또는 아이템이 있는지 확인한다.</summary>
    public static bool HasPendingReward(QuestDefinitionSO def, ActiveQuestData active) =>
        def != null && active != null && active.isCompleted && active.rewardInitialized &&
        ((def.rewardGold > 0 && !active.goldPaid) ||
         (def.rewardItem != null && active.itemsGranted < Mathf.Max(0, def.rewardItemCount)));

    /// <summary>
    /// 실제 보상 수령 완료를 김성우님이 만든 알림 경로(InventoryController.PrintLog + 싱글플레이 채팅)로
    /// 띄운다. QuestBoardNPC.NotifyAlreadyAccepted()와 같은 방식 - 획득 알림이므로 장비 거절/경고와
    /// 달리 ChatKind.Acquisition("[획득]" 접두사, 초록색)을 쓴다. 인벤토리 팝업을 한 번도 안 열어
    /// Instance가 아직 없으면 지급을 보류하므로 이 수령 완료 알림도 띄우지 않는다.
    /// </summary>
    private void NotifyRewardReceived(QuestDefinitionSO def)
    {
        if (InventoryController.Instance == null)
            return;

        string questName = questLabels != null ? questLabels.GetQuestName(def.questId) : def.questName;
        string messageFormat = Label("quest_ui.reward_received_message", "'{0}' 의뢰 보상을 수령했습니다: {1}");
        string message = string.Format(messageFormat, questName, BuildRewardText(def));

        InventoryController.Instance.PrintLog(message);
        InventoryController.Instance.ReportSinglePlayerMessage(ChatKind.Acquisition, message);
    }

    private string BuildRewardText(QuestDefinitionSO def)
    {
        string creditFormat = Label("quest_ui.reward_credit_plain", "크레딧 {0}");
        string reward = string.Format(creditFormat, def.rewardGold);

        if (def.rewardItem != null)
        {
            string itemName = itemLabels != null ? itemLabels.GetName(def.rewardItem.itemId) : def.rewardItem.itemName;
            string suffixFormat = Label("quest_ui.reward_item_suffix", ", {0} x{1}");
            reward += string.Format(suffixFormat, itemName, def.rewardItemCount);
        }

        return reward;
    }

    /// <summary>실제 지급 성공분만 기록하고 나머지는 0.5초 간격으로 다시 시도한다.</summary>
    private void TryGrantPendingRewards()
    {
        InventoryController inventory = InventoryController.Instance;
        if (grantingRewards || inventory == null || database == null || !Core.DataManager.Instance.IsGameplayReady ||
            Mirror.NetworkClient.active || Mirror.NetworkServer.active)
            return;

        grantingRewards = true;
        bool changed = false;
        try
        {
            // 지급 중 아이템 획득 이벤트가 새 퀘스트를 완료하더라도 다음 시도에서 안전하게 처리한다.
            foreach (ActiveQuestData active in activeQuests.ToArray())
            {
                QuestDefinitionSO def = database.FindById(active.questId);
                if (!HasPendingReward(def, active))
                    continue;

                if (!active.goldPaid && def.rewardGold > 0 && inventory.PlayerWallet != null)
                {
                    inventory.PlayerWallet.AddGold(def.rewardGold);
                    active.goldPaid = true;
                    changed = true;
                }
                if (def.rewardItem != null && inventory.PlayerGrid != null)
                {
                    while (active.itemsGranted < Mathf.Max(0, def.rewardItemCount))
                    {
                        if (!inventory.AddItem(ItemDataCreator.CreateItemData(def.rewardItem)))
                            break;
                        active.itemsGranted++;
                        changed = true;
                    }
                }
                if (!HasPendingReward(def, active))
                    NotifyRewardReceived(def);
            }
        }
        finally
        {
            grantingRewards = false;
        }

        if (changed)
        {
            // 지갑·인벤토리의 실제 변경을 기존 런 저장 경로에 먼저 요청한다.
            SaveQuestState();
            OnQuestListChanged?.Invoke();
        }
    }

    /// <summary>지급 중 발생한 진행 이벤트는 바깥 지급이 끝난 뒤 한 상태로 저장한다.</summary>
    private void SaveQuestState()
    {
        if (!grantingRewards)
            Core.DataManager.Instance?.SaveQuestData();
    }

    private string Label(string key, string fallback)
    {
        string value = questLabels != null ? questLabels.GetLabel(key) : null;
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }

    private ActiveQuestData FindActive(string questId)
    {
        foreach (ActiveQuestData q in activeQuests)
        {
            if (q.questId == questId)
                return q;
        }
        return null;
    }

    // ===================== 저장/불러오기 (DataManager 전담) =====================

    public QuestSaveData GetSaveData()
    {
        var data = new QuestSaveData();
        data.quests.AddRange(activeQuests);
        return data;
    }

    public void ApplySaveData(QuestSaveData data)
    {
        activeQuests.Clear();
        if (data?.quests != null)
        {
            foreach (ActiveQuestData active in data.quests)
            {
                if (active == null || string.IsNullOrEmpty(active.questId) || FindActive(active.questId) != null)
                    continue;
                // 구형 저장에는 지급 결과가 없다. 완료 기록은 이미 수령한 것으로 복원해 재지급을 막는다.
                QuestDefinitionSO def = database != null ? database.FindById(active.questId) : null;
                if (active.isCompleted && !active.rewardInitialized && def != null)
                {
                    active.rewardInitialized = true;
                    active.goldPaid = true;
                    active.itemsGranted = def.rewardItem != null ? Mathf.Max(0, def.rewardItemCount) : 0;
                }
                active.itemsGranted = Mathf.Max(0, active.itemsGranted);
                activeQuests.Add(active);
            }
        }

        OnQuestListChanged?.Invoke();
    }
}
