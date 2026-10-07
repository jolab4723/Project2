using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>진행 중인 퀘스트 목록을 슬롯으로 표시하는 사이드 팝업이다.</summary>
public class KY_QuestPopup : KY_PopupBase
{
    [Header("References")]
    [SerializeField] private KY_QuestSlot slotPrefab;
    [SerializeField] private Transform content;

    [Header("Quest List")]
    [SerializeField] private List<KY_QuestData> quests = new List<KY_QuestData>();

    private KY_SlideAnimator slideAnimator;
    private ObjectPool<KY_QuestSlot> slotPool;
    private readonly List<KY_QuestSlot> activeSlots = new List<KY_QuestSlot>();

    // SW 수정
    /// <summary>외부에서 확정된 표시 데이터를 받는다. 게임 상태와 보상은 이 팝업에서 변경하지 않는다.</summary>
    public void SetQuestData(IReadOnlyList<KY_QuestData> data)
    {
        quests.Clear();
        if (data != null)
            for (int i = 0; i < data.Count; i++) quests.Add(data[i]);
        if (gameObject.activeInHierarchy) RefreshList();
    }

    /// <summary>필수 참조와 퀘스트 슬롯 풀을 준비한다.</summary>
    private void Awake()
    {
        slideAnimator = GetComponent<KY_SlideAnimator>();

        if (slotPrefab == null || content == null)
        {
            Debug.LogError("[KY_QuestPopup] Slot Prefab 또는 Content가 연결되지 않았습니다.", this);
            return;
        }

        slotPool = new ObjectPool<KY_QuestSlot>(
            createFunc: () => Instantiate(slotPrefab, content),
            actionOnGet: slot => slot.gameObject.SetActive(true),
            actionOnRelease: slot => slot.gameObject.SetActive(false),
            actionOnDestroy: slot => Destroy(slot.gameObject),
            maxSize: 20
        );
    }

    /// <summary>팝업을 열고 최신 퀘스트 목록을 그린다.</summary>
    public override void Open()
    {
        gameObject.SetActive(true);

        if (slideAnimator != null)
            slideAnimator.SlideIn();

        RefreshList();
    }

    /// <summary>팝업을 닫고 슬라이드 애니메이션을 적용한다.</summary>
    public override void Close()
    {
        if (slideAnimator != null)
            slideAnimator.SlideOut(() => gameObject.SetActive(false));
        else
            gameObject.SetActive(false);
    }

    /// <summary>현재 퀘스트 데이터로 슬롯 목록을 다시 구성한다.</summary>
    private void RefreshList()
    {
        if (slotPool == null)
            return;

        // 기존 슬롯 전부 반납
        foreach (var slot in activeSlots)
            slotPool.Release(slot);
        activeSlots.Clear();

        if (quests == null)
            return;

        // WJ 이우진 수정(2026-10-06): 싱글(QuestPopupBridge)·멀티(MirrorQuestUIBinder) 공통으로 여기서 정렬한다.
        quests.Sort(CompareForDisplay);

        foreach (var quest in quests)
        {
            if (quest == null)
                continue;

            KY_QuestSlot slot = slotPool.Get();
            // WJ 이우진 수정(2026-10-06): 풀에서 꺼낸 슬롯은 이전 위치(형제 순서)를 유지하므로,
            // 꺼낸 순서대로 맨 뒤로 보내야 화면 순서가 정렬된 데이터 순서와 같아진다.
            slot.transform.SetAsLastSibling();
            slot.SetData(quest);
            activeSlots.Add(slot);
        }
    }

    /// <summary>
    /// WJ 이우진 추가(2026-10-06): 진행 중 → 목표 달성(보상 대기) → 보상 수령 완료 순, 같은 상태는 이름 가나다순.
    /// 이름 비교는 서수 비교라 한글은 가나다순이 되고, 실행 환경의 문화권 설정에 따라 순서가 바뀌지 않는다.
    /// </summary>
    private static int CompareForDisplay(KY_QuestData a, KY_QuestData b)
    {
        if (a == null || b == null)
            return a == null ? (b == null ? 0 : 1) : -1;

        int state = GetDisplayState(a).CompareTo(GetDisplayState(b));
        return state != 0 ? state : string.CompareOrdinal(a.questName ?? string.Empty, b.questName ?? string.Empty);
    }

    private static int GetDisplayState(KY_QuestData quest) =>
        !quest.isCompleted ? 0 : quest.rewardPending ? 1 : 2;
}
