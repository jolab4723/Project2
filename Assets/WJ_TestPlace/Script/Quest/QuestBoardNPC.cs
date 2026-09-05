using UnityEngine;

/// <summary>
/// 의뢰 NPC. 상호작용하면(YJ_ClickNPC.onClicked에 이 컴포넌트의 Interact()를 연결) QuestManager에서
/// 무작위 퀘스트를 하나 뽑아 QuestOfferUI에 제시를 맡기고, 1회 리롤과 수락 로직을 담당한다.
/// 화면 자체는 QuestOfferUI(uGUI)가 그린다 - 이 클래스는 상태(currentOffer/hasRerolled)만 갖는다.
///
/// !! QuestOfferPopup은 김관영님의 다른 CampPopup 형제들(DialoguePopup 등)과 같은 관례를 따라
///    프리팹 기본값이 "비활성"이다 - 그래서 이 오브젝트를 활성화하는 책임을 이 NPC 쪽으로 가져왔다
///    (예전엔 씬에서 QuestOfferPopup을 직접 활성 상태로 저장해뒀는데, 프리팹 기본값이 비활성이라
///    씬이 다시 저장되거나 오버라이드가 초기화될 때마다 도로 비활성으로 돌아가는 문제가 반복됐다).
///    ShowOfferPopup()이 상호작용 시점에 직접 활성화하므로, 씬/프리팹의 저장된 활성 상태와
///    무관하게 항상 정상 동작한다.
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

    /// <summary>이번 캠프 진입 중 이 NPC로 이미 리롤을 썼는지. Accept와 마찬가지로 캠프 진입당 1회만
    /// 허용한다 - Interact()에서 초기화하지 않는다(초기화하면 팝업을 닫았다 다시 열어서 리롤을
    /// 무한히 쓸 수 있게 됨). 새 캠프 진입(= 이 컴포넌트의 새 인스턴스)마다 자연히 false로 초기화된다.</summary>
    public bool HasRerolled { get; private set; }

    /// <summary>이번 캠프 진입 중 이 NPC를 통해 이미 퀘스트를 수락했는지.</summary>
    public bool HasAcceptedThisVisit { get; private set; }

    /// <summary>
    /// NPC와 상호작용을 시작한다. 아직 답하지 않은 제시(CurrentOffer)가 남아있으면 새로 뽑지 않고
    /// 그 팝업을 그대로 다시 보여주기만 한다 - 팝업이 떠 있는 중에 NPC를 또 클릭해도(더블클릭,
    /// 콜라이더가 팝업 뒤에서도 눌리는 경우 등) 매번 새 퀘스트로 갱신되던 문제를 막는다. 팝업을
    /// 닫았다(거절/X) 다시 열어도 CurrentOffer는 그대로라 같은 제시가 다시 뜬다 - 리롤/수락 전까지는
    /// 캠프 한 번 진입 중 이 NPC가 제시하는 퀘스트가 계속 바뀌지 않고 고정된다.
    /// </summary>
    public void Interact()
    {
        // 캠프 한 번 진입 중 이 NPC로는 1회만 수락할 수 있다 - 이미 썼으면 새로 제시하지 않는다.
        if (HasAcceptedThisVisit)
        {
            Debug.LogWarning("[QuestBoardNPC] 이번 캠프 진입 중에는 이미 퀘스트를 수락했습니다. 다시 캠프에 들어오면 새로 수락할 수 있습니다.");
            NotifyAlreadyAccepted();
            return;
        }

        // 이미 제시해둔 퀘스트가 있으면(팝업이 열려있거나 아직 수락/거절하지 않은 상태) 새로 뽑지
        // 않고 같은 제시를 그대로 다시 보여준다.
        if (CurrentOffer != null)
        {
            ShowOfferPopup();
            return;
        }

        CurrentOffer = QuestManager.Instance.GetRandomAvailableQuest();

        if (CurrentOffer == null)
        {
            Debug.LogWarning("[QuestBoardNPC] 제시할 수 있는 퀘스트가 없습니다(전부 진행 중이거나 완료됨).");
            return;
        }

        ShowOfferPopup();
    }

    /// <summary>
    /// QuestOfferUI 팝업을 찾아서(비활성 포함) 필요하면 먼저 활성화한 뒤 보여준다. 팝업이 비활성
    /// 상태면 Awake()가 아직 실행되지 않아 QuestOfferUI.Instance가 null일 수 있으므로, Instance가
    /// 없으면 비활성 포함 검색으로 직접 찾는다 - 찾은 오브젝트를 활성화하면 그 시점에 Awake()가 돌면서
    /// Instance가 정상적으로 채워진다.
    /// </summary>
    private void ShowOfferPopup()
    {
        QuestOfferUI popup = QuestOfferUI.Instance;
        if (popup == null)
            popup = FindFirstObjectByType<QuestOfferUI>(FindObjectsInactive.Include);

        if (popup == null)
        {
            Debug.LogWarning("[QuestBoardNPC] QuestOfferUI를 씬에서 찾지 못했습니다.");
            return;
        }

        if (!popup.gameObject.activeSelf)
            popup.gameObject.SetActive(true);

        popup.Show(this);
    }

    /// <summary>캠프 진입당 1회만 가능한 리롤. 이미 리롤했거나 후보가 없으면 false.</summary>
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
