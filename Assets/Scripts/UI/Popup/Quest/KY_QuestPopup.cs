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

        foreach (var quest in quests)
        {
            if (quest == null)
                continue;

            KY_QuestSlot slot = slotPool.Get();
            slot.SetData(quest);
            activeSlots.Add(slot);
        }
    }
}
