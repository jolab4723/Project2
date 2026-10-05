using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>항상 활성인 HUD에서 본인의 목표 완료 전이만 3초간 알린다.</summary>
public sealed class QuestCompletionFeedback : MonoBehaviour
{
    // SW 수정 : 상태 원본의 이벤트를 구독하고 UI는 보상이나 진행도를 변경하지 않는다.
    [Tooltip("팝업 바깥의 공용 ScreenNotice. 표시 시간을 3초로 설정한다.")]
    [SerializeField] private KY_ScreenNotice screenNotice;

    private QuestManager quests;
    private MirrorNetworkManager session;
    private QuestLabelDatabaseSO questLabels;
    private bool hasNetworkBaseline;
    private readonly Dictionary<string, bool> completedByQuestId = new();
    private readonly Queue<string> messages = new();
    private float nextNoticeTime;

    private void Awake()
    {
        questLabels = Resources.Load<QuestLabelDatabaseSO>("DataFiles/QuestData/3. GeneratedAssets/QuestLabelDatabase");
    }

    private void OnEnable() => BindSource();

    private void Update()
    {
        // 초기화 순서와 싱글/멀티 진입에 맞춰 한 원본만 구독한다.
        BindSource();
        if (screenNotice == null || messages.Count == 0 || Time.unscaledTime < nextNoticeTime)
            return;
        screenNotice.Show(messages.Dequeue());
        nextNoticeTime = Time.unscaledTime + 3f;
    }

    private void OnDisable()
    {
        UnbindSource();
    }

    private void BindSource()
    {
        bool multiplayer = NetworkClient.active || NetworkServer.active;
        MirrorNetworkManager nextSession = NetworkClient.active ? NetworkManager.singleton as MirrorNetworkManager : null;
        QuestManager nextQuests = multiplayer ? null : QuestManager.Instance;
        if (session == nextSession && quests == nextQuests)
            return;

        UnbindSource();
        session = nextSession;
        quests = nextQuests;
        if (session != null)
        {
            session.QuestStateChanged += HandleNetworkStateChanged;
            HandleNetworkStateChanged();
        }
        else if (quests != null)
        {
            // 저장 복원/씬 재진입은 완료 이벤트를 발행하지 않으므로 과거 알림을 재생하지 않는다.
            quests.OnQuestCompleted += HandleLocalCompleted;
        }
    }

    private void UnbindSource()
    {
        if (session != null)
            session.QuestStateChanged -= HandleNetworkStateChanged;
        if (quests != null)
            quests.OnQuestCompleted -= HandleLocalCompleted;

        session = null;
        quests = null;
        hasNetworkBaseline = false;
        completedByQuestId.Clear();
        messages.Clear();
        nextNoticeTime = 0f;
    }

    private void HandleLocalCompleted(ActiveQuestData active)
    {
        if (active == null || !active.isCompleted || string.IsNullOrEmpty(active.questId))
            return;
        // 로컬 이벤트 자체가 최초 목표 완료 전이다. 저장 복원은 이 이벤트를 발행하지 않는다.
        QuestDefinitionSO definition = quests.GetDefinition(active.questId);
        if (definition != null)
            EnqueueCompletionNotice(definition);
    }

    private void HandleNetworkStateChanged()
    {
        // ClientQuests는 서버가 이 참가자에게 보낸 진행·보상 상태다. 다른 지갑을 조회하지 않는다.
        MirrorQuestEntry[] entries = session.ClientQuests.Quests;
        if (entries == null)
        {
            // 세션 종료/재접속 후 첫 스냅샷도 새 기준 상태로 처리한다.
            completedByQuestId.Clear();
            messages.Clear();
            hasNetworkBaseline = false;
            nextNoticeTime = 0f;
            return;
        }
        foreach (MirrorQuestEntry entry in entries)
        {
            if (string.IsNullOrEmpty(entry.QuestId))
                continue;
            bool transitioned = hasNetworkBaseline && completedByQuestId.TryGetValue(entry.QuestId, out bool previous) &&
                                !previous && entry.Completed;
            completedByQuestId[entry.QuestId] = entry.Completed;
            if (transitioned)
            {
                QuestDefinitionSO definition = session.QuestDatabase != null ? session.QuestDatabase.FindById(entry.QuestId) : null;
                if (definition != null)
                    EnqueueCompletionNotice(definition);
            }
        }
        hasNetworkBaseline = true;
    }

    /// <summary>목표 달성을 알린다. 변할 수 있는 보상 지급 상태는 실제 원본을 따르는 목록에서 확인한다.</summary>
    private void EnqueueCompletionNotice(QuestDefinitionSO definition)
    {
        string name = questLabels != null ? questLabels.GetQuestName(definition.questId) : null;
        if (string.IsNullOrEmpty(name) || name == definition.questId + ".name")
            name = definition.questName;
        messages.Enqueue(name + "\n" + Label("quest_ui.objective_completed", "목표 달성"));
    }

    private string Label(string key, string fallback)
    {
        string value = questLabels != null ? questLabels.GetLabel(key) : null;
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }
}
