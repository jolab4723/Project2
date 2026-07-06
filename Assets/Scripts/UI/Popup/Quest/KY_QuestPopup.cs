using UnityEngine;
using UnityEngine.Pool;

public class KY_QuestPopup : KY_PopupBase
{
    public KY_QuestSlot slotPrefab;
    public Transform content;

    private KY_SlideAnimator slideAnimator;
    private ObjectPool<KY_QuestSlot> slotPool;
    private System.Collections.Generic.List<KY_QuestSlot> activeSlots = new System.Collections.Generic.List<KY_QuestSlot>();

    void Awake()
    {
        slideAnimator = GetComponent<KY_SlideAnimator>();

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
        slideAnimator.SlideIn();
        RefreshList();
    }

    public override void Close()
    {
        slideAnimator.SlideOut(() => gameObject.SetActive(false));
    }

    void RefreshList()
    {
        // 기존 슬롯 전부 반납
        foreach (var slot in activeSlots)
            slotPool.Release(slot);
        activeSlots.Clear();

        // 더미 데이터로 테스트
        KY_QuestData[] dummyQuests = new KY_QuestData[]
        {
            new KY_QuestData
            {
                questName = "고장난 로봇의 부품",
                description = "공장 지대에 고장난 로봇들이 돌아다니고 있다. 그 로봇들에게서 면도기 모터를 가져다 달라.",
                conditions = new KY_QuestConditionData[]
                {
                    new KY_QuestConditionData { description = "고장난 로봇 처치", current = 0, required = 5 },
                    new KY_QuestConditionData { description = "면도기 모터 수집", current = 0, required = 3 }
                },
                reward = "골드 500, 경험치 300"
            }
        };

        foreach (var quest in dummyQuests)
        {
            KY_QuestSlot slot = slotPool.Get();
            slot.SetData(quest);
            activeSlots.Add(slot);
        }
    }
}