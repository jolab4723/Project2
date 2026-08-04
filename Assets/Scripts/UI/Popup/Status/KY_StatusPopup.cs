using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 스탯 팝업용 코드입니다.
// 데이터를 받아 캐릭터의 스탯을 팝업에 표시. PlayerStatManager.Instance와 연결됨.
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

    [Header("스탯 이름 라벨")]
    [Tooltip("비워두면 각 행의 이름 텍스트를 건드리지 않는다(기존 하드코딩된 텍스트 유지).")]
    public StatLabelDatabaseSO statLabels;

    private KY_StatData currentData;
    private bool isDetailed = false;

    private KY_SlideAnimator slideAnimator;
    private KY_CurtainEffect curtainEffect;

    private PlayerStatManager statManager;

    void Awake()
    {
        slideAnimator = GetComponent<KY_SlideAnimator>();
        curtainEffect = GetComponentInChildren<KY_CurtainEffect>();
        detailToggle.onValueChanged.AddListener(OnDetailToggleChanged);

        ApplyLabels();
    }

    /// <summary>
    /// 각 행의 이름 텍스트를 StatLabelDatabase 값으로 채운다.
    /// 표시 언어가 바뀌면 문구도 바뀌므로, 생성 시 한 번이 아니라
    /// 팝업을 열 때와 언어 변경 이벤트가 올 때마다 다시 채운다.
    /// </summary>
    void ApplyLabels()
    {
        if (statLabels == null)
            return;

        hpRow.SetLabel(statLabels.GetLabel("maxHealth"));
        attackRow.SetLabel(statLabels.GetLabel("attackPower"));
        defenseRow.SetLabel(statLabels.GetLabel("defensePower"));
        moveSpeedRow.SetLabel(statLabels.GetLabel("moveSpeed"));
        attackSpeedRow.SetLabel(statLabels.GetLabel("attackSpeed"));
        critChanceRow.SetLabel(statLabels.GetLabel("critRate"));
        critMultiplierRow.SetLabel(statLabels.GetLabel("critMult"));
        cooldownReductionRow.SetLabel(statLabels.GetLabel("cdr"));
        mpRegenRow.SetLabel(statLabels.GetLabel("mpRegen"));
        penetrationRow.SetLabel(statLabels.GetLabel("pen"));
    }

    void OnEnable()
    {
        statManager = PlayerStatManager.Instance;
        if (statManager != null)
            statManager.Stat.OnStatChanged += HandleStatChanged;

        // 팝업이 떠 있는 동안 언어가 바뀌면 즉시 반영되도록 구독한다.
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;
    }

    void OnDisable()
    {
        if (statManager != null)
            statManager.Stat.OnStatChanged -= HandleStatChanged;

        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= HandleLanguageChanged;
    }

    void HandleLanguageChanged(GameLanguage _)
    {
        ApplyLabels();
    }

    void HandleStatChanged()
    {
        RequestData();
    }

    public override void Open()
    {
        gameObject.SetActive(true);

        // 닫혀 있는 동안 언어가 바뀌었을 수 있으므로 열 때마다 다시 채운다.
        ApplyLabels();
        RequestData();

        Sequence seq = DOTween.Sequence();
        seq.Append(slideAnimator.SlideIn());
        seq.AppendCallback(() => curtainEffect.Open());
    }

    public override void Close()
    {
        Sequence seq = DOTween.Sequence();
        seq.Append(curtainEffect.Close());
        seq.AppendCallback(() => slideAnimator.SlideOut(() => gameObject.SetActive(false)));
    }

    void RequestData()
    {
        if (statManager == null)
            statManager = PlayerStatManager.Instance;

        if (statManager == null || statManager.Stat == null)
        {
            Debug.LogWarning("[KY_StatusPopup] PlayerStatManager.Instance가 없어 스탯을 표시할 수 없습니다.");
            return;
        }

        SetData(BuildDataFromPlayerStat(statManager));
    }

    /// <summary>
    /// PlayerStatManager의 캐릭터/장비/버프/패시브 레이어를 KY_StatData(캐릭터/장비/버프 3단)로 변환한다.
    /// 패시브 스킬트리는 아직 UI가 구분하는 3단에 없어서 버프 몫에 합쳐 넣는다(현재는 패시브가 스텁이라 실질적으로 0).
    /// </summary>
    private static KY_StatData BuildDataFromPlayerStat(PlayerStatManager statManager)
    {
        statManager.GetLayerStatSets(out StatSet c, out StatSet eq, out StatSet bu, out StatSet pa);

        return new KY_StatData
        {
            hp = Build(c.maxHealthFlat, eq.maxHealthFlat, eq.maxHealthPercent, bu.maxHealthPercent, bu.maxHealthFlat, pa.maxHealthPercent, pa.maxHealthFlat),
            attack = Build(c.attackPowerFlat, eq.attackPowerFlat, eq.attackPowerPercent, bu.attackPowerPercent, bu.attackPowerFlat, pa.attackPowerPercent, pa.attackPowerFlat),
            defense = Build(c.defensePowerFlat, eq.defensePowerFlat, eq.defensePowerPercent, bu.defensePowerPercent, bu.defensePowerFlat, pa.defensePowerPercent, pa.defensePowerFlat),
            moveSpeed = Build(c.moveSpeedFlat, eq.moveSpeedFlat, eq.moveSpeedPercent, bu.moveSpeedPercent, bu.moveSpeedFlat, pa.moveSpeedPercent, pa.moveSpeedFlat),
            attackSpeed = Build(c.attackSpeedFlat, eq.attackSpeedFlat, eq.attackSpeedPercent, bu.attackSpeedPercent, bu.attackSpeedFlat, pa.attackSpeedPercent, pa.attackSpeedFlat),
            critChance = BuildClamped(c.critRateFlat, eq.critRateFlat, bu.critRateFlat, pa.critRateFlat, 0f, 100f),
            critMultiplier = Build(c.critMultFlat, eq.critMultFlat, eq.critMultPercent, bu.critMultPercent, bu.critMultFlat, pa.critMultPercent, pa.critMultFlat),
            cooldownReduction = BuildClamped(c.cdrFlat, eq.cdrFlat, bu.cdrFlat, pa.cdrFlat, 0f, 70f),
            mpRegen = Build(c.mpRegenFlat, eq.mpRegenFlat, eq.mpRegenPercent, bu.mpRegenPercent, bu.mpRegenFlat, pa.mpRegenPercent, pa.mpRegenFlat),
            penetration = Build(c.penFlat, eq.penFlat, eq.penPercent, bu.penPercent, bu.penFlat, pa.penPercent, pa.penFlat),
            fireDamage = Build(c.fireBonusFlat, eq.fireBonusFlat, eq.fireBonusPercent, bu.fireBonusPercent, bu.fireBonusFlat, pa.fireBonusPercent, pa.fireBonusFlat),
            iceDamage = Build(c.iceBonusFlat, eq.iceBonusFlat, eq.iceBonusPercent, bu.iceBonusPercent, bu.iceBonusFlat, pa.iceBonusPercent, pa.iceBonusFlat),
            lightningDamage = Build(c.electricBonusFlat, eq.electricBonusFlat, eq.electricBonusPercent, bu.electricBonusPercent, bu.electricBonusFlat, pa.electricBonusPercent, pa.electricBonusFlat),
        };
    }

    private static KY_StatTypeData Build(float characterFlat, float equipFlat, float equipPercent, float buffPercent, float buffFlat, float passivePercent, float passiveFlat)
    {
        PlayerStat.CalcBreakdown(characterFlat, equipFlat, equipPercent, buffPercent, buffFlat, passivePercent, passiveFlat,
            out float baseValue, out float equipValue, out float buffValue);
        return new KY_StatTypeData { baseValue = baseValue, equipValue = equipValue, buffValue = buffValue };
    }

    private static KY_StatTypeData BuildClamped(float characterFlat, float equipFlat, float buffFlat, float passiveFlat, float min, float max)
    {
        PlayerStat.CalcBreakdownClampedFlat(characterFlat, equipFlat, buffFlat, passiveFlat, min, max,
            out float baseValue, out float equipValue, out float buffValue);
        return new KY_StatTypeData { baseValue = baseValue, equipValue = equipValue, buffValue = buffValue };
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