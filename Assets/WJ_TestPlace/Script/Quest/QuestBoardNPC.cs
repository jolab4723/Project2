using UnityEngine;

/// <summary>
/// 의뢰 NPC. 상호작용하면(YJ_ClickNPC.onClicked에 이 컴포넌트의 Interact()를 연결) QuestManager에서
/// 무작위 퀘스트를 하나 뽑아 QuestOfferUI에 제시를 맡기고, 1회 리롤과 수락 로직을 담당한다.
/// 화면 자체는 QuestOfferUI(uGUI)가 그린다 - 이 클래스는 상태(currentOffer/hasRerolled)만 갖는다.
///
/// !! 퀘스트 자체는 여러 개를 동시에 진행할 수 있다(QuestManager는 진행 개수를 제한하지 않음).
///    제한하는 건 "이 NPC를 통해 캠프 한 번 진입 중에 수락할 수 있는 횟수"다 - 1회로 제한한다.
///    이 컴포넌트는 씬(캠프)이 로드될 때마다 새로 생성되는 일반 씬 오브젝트라(DontDestroyOnLoad 아님),
///    hasAcceptedThisVisit은 캠프에 다시 들어올 때마다 새 인스턴스와 함께 자연스럽게 false로 초기화된다
///    - 별도 리셋 로직이 필요 없다.
///
///    (멀티 - 아직 미구현) 파티장(호스트)만 대표로 수락 가능해야 한다는 요구가 있다. 이 클래스에는
///    아직 Mirror NetworkBehaviour나 호스트 판별 수단이 없어서, 지금은 싱글플레이 기준(캠프당 1회)만
///    구현했고 호스트 전용 수락은 실제 멀티 연동 시점에 추가해야 한다.
/// </summary>
public class QuestBoardNPC : MonoBehaviour
{
    private const string AlreadyAcceptedMessage = "이번 캠프에서는 의뢰를 이미 수락했습니다. 다시 캠프에 들어오면 새로 수락할 수 있습니다.";

    public QuestDefinitionSO CurrentOffer { get; private set; }
    public bool HasRerolled { get; private set; }

    /// <summary>이번 캠프 진입 중 이 NPC를 통해 이미 퀘스트를 수락했는지.</summary>
    public bool HasAcceptedThisVisit { get; private set; }

    /// <summary>NPC와 상호작용을 시작한다. 매번 새로 뽑고 리롤 기회를 초기화한다.</summary>
    public void Interact()
    {
        // 캠프 한 번 진입 중 이 NPC로는 1회만 수락할 수 있다 - 이미 썼으면 새로 제시하지 않는다.
        if (HasAcceptedThisVisit)
        {
            Debug.LogWarning("[QuestBoardNPC] 이번 캠프 진입 중에는 이미 퀘스트를 수락했습니다. 다시 캠프에 들어오면 새로 수락할 수 있습니다.");
            NotifyAlreadyAccepted();
            return;
        }

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
        HasAcceptedThisVisit = true;
        return true;
    }

    /// <summary>
    /// 추가 수락 시도를 김성우님이 만든 알림 경로(InventoryController.PrintLog + 싱글플레이 채팅 경고)로
    /// 띄운다 - EquipmentSystem이 장비 거절을 알릴 때(InventoryController.ReportEquipmentRejection)와
    /// 같은 방식을 그대로 재사용한다. 인벤토리 팝업을 한 번도 안 열어 Instance가 아직 없으면 조용히 건너뛴다.
    /// </summary>
    private static void NotifyAlreadyAccepted()
    {
        if (InventoryController.Instance == null)
            return;

        InventoryController.Instance.PrintLog(AlreadyAcceptedMessage);
        InventoryController.Instance.ReportSinglePlayerMessage(ChatKind.Warning, AlreadyAcceptedMessage);
    }
}
