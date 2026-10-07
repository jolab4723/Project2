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
    private PlayerBuffManager observedBuffManager;
    private KY_SlideAnimator slideAnimator;
    private KY_CurtainEffect descriptionCurtain;
    private int lastTimeRefreshSecond = int.MinValue;

    void Awake()
    {
        slideAnimator = GetComponent<KY_SlideAnimator>();
        descriptionCurtain = descriptionView != null
            ? descriptionView.GetComponent<KY_CurtainEffect>()
            : null;
    }

    void OnEnable()
    {
        BindBuffManager();
    }

    void OnDisable()
    {
        UnbindBuffManager();
    }

    void Update()
    {
        if (observedBuffManager != PlayerBuffManager.Instance)
            BindBuffManager();

        // 시간은 초 단위로 표시하므로, 같은 초에는 텍스트를 다시 대입하지 않는다.
        int currentTimeSecond = Mathf.FloorToInt(Time.time);
        if (lastTimeRefreshSecond == currentTimeSecond)
            return;

        lastTimeRefreshSecond = currentTimeSecond;
        for (int i = 0; i < currentBuffs.Count && i < spawnedSlots.Count; i++)
            spawnedSlots[i].RefreshTimeOnly();
    }

    public override void Open()
    {
        base.Open();
        BindBuffManager();
        RefreshList();
        slideAnimator?.SlideIn();
        HideDescription();
    }

    public override void Close()
    {
        if (slideAnimator != null)
            slideAnimator.SlideOut(() => gameObject.SetActive(false));
        else
            base.Close();
    }

    private void BindBuffManager()
    {
        PlayerBuffManager current = PlayerBuffManager.Instance;
        if (observedBuffManager == current)
            return;

        UnbindBuffManager();
        observedBuffManager = current;

        if (observedBuffManager != null)
            observedBuffManager.OnBuffsChanged += RefreshList;
    }

    private void UnbindBuffManager()
    {
        if (observedBuffManager != null)
            observedBuffManager.OnBuffsChanged -= RefreshList;

        observedBuffManager = null;
    }

    // 목록 구성(추가/제거)이 바뀌었을 때만 호출됨
    void RefreshList()
    {
        if (observedBuffManager == null)
        {
            currentBuffs.Clear();
            SetSlotVisibility(0);
            if (emptyStateText != null) emptyStateText.SetActive(true);
            HideDescription();
            return;
        }

        currentBuffs = KY_BuffSortRule.Sort(observedBuffManager.ActiveBuffs);

        // 슬롯이 부족하면 그만큼만 새로 생성 (Destroy는 안 함)
        while (spawnedSlots.Count < currentBuffs.Count)
        {
            KY_BuffSlot newSlot = Instantiate(slotPrefab, slotContainer);
            newSlot.OnSlotHoverEnter += OnSlotHoverEnter;
            newSlot.OnSlotHoverExit += OnSlotHoverExit;
            spawnedSlots.Add(newSlot);
        }

        // 슬롯 전체를 순회하며 필요한 만큼만 켜고 데이터 채움, 나머지는 꺼둠
        SetSlotVisibility(currentBuffs.Count);
        if (emptyStateText != null) emptyStateText.SetActive(currentBuffs.Count == 0);
        HideDescription();
    }

    private void SetSlotVisibility(int count)
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            bool hasData = i < count;
            spawnedSlots[i].gameObject.SetActive(hasData);
            if (hasData) spawnedSlots[i].Render(currentBuffs[i]);
        }
    }

    void OnSlotHoverEnter(BuffInstance data)
    {
        if (descriptionView == null)
            return;

        descriptionView.Render(data);
        descriptionCurtain?.Open();
    }

    void OnSlotHoverExit()
    {
        HideDescription();
    }

    private void HideDescription()
    {
        if (descriptionView != null)
            descriptionView.Clear();

        // 버프가 없거나 슬롯을 벗어난 동안에는 Description을 세로 축으로 접어 둔다.
        descriptionCurtain?.Close();
    }
}
