using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

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

    void Awake()
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

    public override void Open()
    {
        gameObject.SetActive(true);

        if (slideAnimator != null)
            slideAnimator.SlideIn();

        RefreshList();
    }

    public override void Close()
    {
        if (slideAnimator != null)
            slideAnimator.SlideOut(() => gameObject.SetActive(false));
        else
            gameObject.SetActive(false);
    }

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
