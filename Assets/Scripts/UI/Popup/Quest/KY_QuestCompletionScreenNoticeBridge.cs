using UnityEngine;

/// <summary>
/// 퀘스트 완료 이벤트를 공용 ScreenNotice 표시로 변환한다.
/// Canvas_Popup 루트에 붙어 퀘스트 팝업의 열림 여부와 무관하게 구독한다.
/// </summary>
public class KY_QuestCompletionScreenNoticeBridge : MonoBehaviour
{
    [SerializeField] private QuestDatabaseSO database;
    [SerializeField] private KY_ScreenNotice screenNotice;

    private QuestManager subscribedManager;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Update()
    {
        if (subscribedManager == null)
            TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        QuestManager manager = QuestManager.Instance;
        if (manager == null || manager == subscribedManager)
            return;

        Unsubscribe();
        subscribedManager = manager;
        subscribedManager.OnQuestCompleted += HandleQuestCompleted;
    }

    private void Unsubscribe()
    {
        if (subscribedManager != null)
            subscribedManager.OnQuestCompleted -= HandleQuestCompleted;

        subscribedManager = null;
    }

    private void HandleQuestCompleted(ActiveQuestData active)
    {
        QuestDefinitionSO definition = database != null ? database.FindById(active.questId) : null;
        if (definition == null || screenNotice == null)
            return;

        QuestLabelDatabaseSO labels = subscribedManager != null ? subscribedManager.QuestLabels : null;
        string questName = labels != null ? labels.GetQuestName(definition.questId) : definition.questName;
        screenNotice.Show($"퀘스트 완료: {questName}");
    }
}
