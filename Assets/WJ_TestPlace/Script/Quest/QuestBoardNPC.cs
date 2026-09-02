using UnityEngine;

/// <summary>
/// 의뢰 NPC. 상호작용하면(YJ_ClickNPC.onClicked에 이 컴포넌트의 Interact()를 연결) QuestManager에서
/// 무작위 퀘스트를 하나 뽑아 QuestOfferUI에 제시를 맡기고, 1회 리롤과 수락 로직을 담당한다.
/// 화면 자체는 QuestOfferUI(uGUI)가 그린다 - 이 클래스는 상태(currentOffer/hasRerolled)만 갖는다.
/// </summary>
public class QuestBoardNPC : MonoBehaviour
{
    public QuestDefinitionSO CurrentOffer { get; private set; }
    public bool HasRerolled { get; private set; }

    /// <summary>NPC와 상호작용을 시작한다. 매번 새로 뽑고 리롤 기회를 초기화한다.</summary>
    public void Interact()
    {
        HasRerolled = false;
        CurrentOffer = QuestManager.Instance.GetRandomAvailableQuest();

        if (CurrentOffer == null)
        {
            Debug.LogWarning("[QuestBoardNPC] 제시할 수 있는 퀘스트가 없습니다(전부 진행 중이거나 완료됨).");
            return;
        }

        if (QuestOfferUI.Instance != null)
            QuestOfferUI.Instance.Show(this);
    }

    /// <summary>1회만 가능한 리롤. 이미 리롤했거나 후보가 없으면 false.</summary>
    public bool Reroll()
    {
        if (HasRerolled || CurrentOffer == null)
            return false;

        QuestDefinitionSO next = QuestManager.Instance.GetRandomAvailableQuest(CurrentOffer.questId);
        if (next == null)
            return false;

        CurrentOffer = next;
        HasRerolled = true;
        return true;
    }

    /// <summary>제시된 퀘스트를 수락하고 진행을 시작한다.</summary>
    public bool Accept()
    {
        if (CurrentOffer == null)
            return false;

        if (!QuestManager.Instance.StartQuest(CurrentOffer))
            return false;

        CurrentOffer = null;
        return true;
    }
}
