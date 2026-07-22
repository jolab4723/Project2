using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 스탯 팝업용 코드입니다.
// 데이터를 받아 캐릭터의 스탯을 팝업에 표시. ( 아직 연결은 미구현 )
// 토탈, 디테일 전환
public class KY_StatusPopup : KY_PopupBase
{
    [Header("스탯 행")]
    public KY_StatRow hpRow;
    public KY_StatRow attackRow;
    public KY_StatRow defenseRow;
    public KY_StatRow moveSpeedRow;
    public KY_StatRow attackSpeedRow;
    public KY_StatRow critChanceRow;
    public KY_StatRow critMultiplierRow;
    public KY_StatRow cooldownReductionRow;
    public KY_StatRow mpRegenRow;
    public KY_StatRow penetrationRow;

    [Header("속성 행")]
    public KY_ElementRow fireRow;
    public KY_ElementRow iceRow;
    public KY_ElementRow lightningRow;

    [Header("세부 보기 토글")]
    public Toggle detailToggle;

    private KY_StatData currentData;
    private bool isDetailed = false;

    private KY_SlideAnimator slideAnimator;

    void Awake()
    {
        slideAnimator = GetComponent<KY_SlideAnimator>();
        detailToggle.onValueChanged.AddListener(OnDetailToggleChanged);
    }

    public override void Open()
    {
        gameObject.SetActive(true);
        slideAnimator.SlideIn();
        RequestData();
    }

    public override void Close()
    {
        slideAnimator.SlideOut(() => gameObject.SetActive(false));
    }

    void RequestData()
    {
        // 테스트용 더미 데이터
        KY_StatData dummyData = new KY_StatData
        {
            hp = new KY_StatTypeData { baseValue = 300, equipValue = 150, buffValue = 50 },
            attack = new KY_StatTypeData { baseValue = 80, equipValue = 30, buffValue = 10 },
            defense = new KY_StatTypeData { baseValue = 50, equipValue = 20, buffValue = 5 },
            moveSpeed = new KY_StatTypeData { baseValue = 10, equipValue = 2, buffValue = 1 },
            attackSpeed = new KY_StatTypeData { baseValue = 1, equipValue = 0.5f, buffValue = 0.2f },
            critChance = new KY_StatTypeData { baseValue = 5, equipValue = 10, buffValue = 3 },
            critMultiplier = new KY_StatTypeData { baseValue = 150, equipValue = 50, buffValue = 20 },
            cooldownReduction = new KY_StatTypeData { baseValue = 0, equipValue = 10, buffValue = 5 },
            mpRegen = new KY_StatTypeData { baseValue = 5, equipValue = 3, buffValue = 2 },
            penetration = new KY_StatTypeData { baseValue = 0, equipValue = 15, buffValue = 0 },
            fireDamage = new KY_StatTypeData { baseValue = 0, equipValue = 10, buffValue = 5 },
            iceDamage = new KY_StatTypeData { baseValue = 0, equipValue = 0, buffValue = 0 },
            lightningDamage = new KY_StatTypeData { baseValue = 0, equipValue = 8, buffValue = 2 }
        };

        SetData(dummyData);
    }

    void SetData(KY_StatData data)
    {
        currentData = data;

        hpRow.UpdateMode(data.hp, isDetailed);
        attackRow.UpdateMode(data.attack, isDetailed);
        defenseRow.UpdateMode(data.defense, isDetailed);
        moveSpeedRow.UpdateMode(data.moveSpeed, isDetailed);
        attackSpeedRow.UpdateMode(data.attackSpeed, isDetailed);
        critChanceRow.UpdateMode(data.critChance, isDetailed);
        critMultiplierRow.UpdateMode(data.critMultiplier, isDetailed);
        cooldownReductionRow.UpdateMode(data.cooldownReduction, isDetailed);
        mpRegenRow.UpdateMode(data.mpRegen, isDetailed);
        penetrationRow.UpdateMode(data.penetration, isDetailed);

        fireRow.SetData(data.fireDamage);
        iceRow.SetData(data.iceDamage);
        lightningRow.SetData(data.lightningDamage);
    }

    void OnDetailToggleChanged(bool isOn)
    {
        if (currentData == null) return;

        isDetailed = isOn;
        SetData(currentData);

        StartCoroutine(RebuildLayout());
    }

    IEnumerator RebuildLayout()
    {
        yield return null; // 한 프레임 대기
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }
}