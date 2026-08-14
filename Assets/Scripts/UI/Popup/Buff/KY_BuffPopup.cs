using UnityEngine;
using System.Collections.Generic;
using ItemSystem;

// 버프 팝업 전체를 조율하는 Presenter입니다. PlayerBuffManager의 데이터를 그대로 읽어서 표시만 합니다.
public class KY_BuffPopup : KY_PopupBase
{
    [Header("슬롯 생성용")]
    public Transform slotContainer;
    public KY_BuffSlot slotPrefab;

    [Header("View")]
    public KY_BuffDescriptionView descriptionView;
    public GameObject emptyStateText;

    private List<KY_BuffSlot> spawnedSlots = new List<KY_BuffSlot>();
    private List<BuffInstance> currentBuffs = new List<BuffInstance>();

    void OnEnable()
    {
        if (PlayerBuffManager.Instance != null)
            PlayerBuffManager.Instance.OnBuffsChanged += RefreshList;
    }

    void OnDisable()
    {
        if (PlayerBuffManager.Instance != null)
            PlayerBuffManager.Instance.OnBuffsChanged -= RefreshList;
    }

    void Update()
    {
        // 팝업이 열려있는 동안, 목록 재구성 없이 남은 시간만 매 프레임 갱신
        for (int i = 0; i < currentBuffs.Count && i < spawnedSlots.Count; i++)
            spawnedSlots[i].RefreshTimeOnly();
    }

    public override void Open()
    {
        base.Open();
        RefreshList();
    }

    // 목록 구성(추가/제거)이 바뀌었을 때만 호출됨
    void RefreshList()
    {
        if (PlayerBuffManager.Instance == null)
        {
            Debug.LogWarning("[KY_BuffPopup] PlayerBuffManager.Instance가 없습니다.");
            return;
        }

        currentBuffs = KY_BuffSortRule.Sort(PlayerBuffManager.Instance.ActiveBuffs);

        // 슬롯이 부족하면 그만큼만 새로 생성 (Destroy는 안 함)
        while (spawnedSlots.Count < currentBuffs.Count)
        {
            KY_BuffSlot newSlot = Instantiate(slotPrefab, slotContainer);
            newSlot.OnSlotHoverEnter += OnSlotHoverEnter;
            newSlot.OnSlotHoverExit += OnSlotHoverExit;
            spawnedSlots.Add(newSlot);
        }

        // 슬롯 전체를 순회하며 필요한 만큼만 켜고 데이터 채움, 나머지는 꺼둠
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            bool hasData = i < currentBuffs.Count;
            spawnedSlots[i].gameObject.SetActive(hasData);

            if (hasData)
                spawnedSlots[i].Render(currentBuffs[i]);
        }

        emptyStateText.SetActive(currentBuffs.Count == 0);
        descriptionView.Clear();
    }

    void OnSlotHoverEnter(BuffInstance data)
    {
        descriptionView.Render(data);
    }

    void OnSlotHoverExit()
    {
        descriptionView.Clear();
    }
}