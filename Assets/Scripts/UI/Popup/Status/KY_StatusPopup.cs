using DG.Tweening;
using ItemSystem;
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
    public KY_StatRow mpRow;             
    public KY_StatRow attackRow;
    public KY_StatRow defenseRow;
    public KY_StatRow moveSpeedRow;
    public KY_StatRow attackSpeedRow;
    public KY_StatRow critChanceRow;
    public KY_StatRow critMultiplierRow;
    public KY_StatRow cooldownReductionRow;
    public KY_StatRow mpRegenRow;
    public KY_StatRow penetrationRow;
    public KY_StatRow skillRangeRow;
    public KY_StatRow normalDamageRow;
    public KY_StatRow skillDamageRow;

    [Header("속성 행")]
    public KY_ElementRow fireRow;
    public KY_ElementRow iceRow;
    public KY_ElementRow lightningRow;

    [Header("세부 보기 토글")]
    public Toggle detailToggle;

    [Header("스탯 이름 라벨")]
    [Tooltip("비워두면 각 행의 이름 텍스트를 건드리지 않는다(기존 하드코딩된 텍스트 유지).")]
    public StatLabelDatabaseSO statLabels;

    [Header("스탯 아이콘")]
    [Tooltip("각 행의 자식 오브젝트를 열지 않고 이곳에서 아이콘을 지정한다.")]
    public Sprite hpIcon;
    public Sprite mpIcon;
    public Sprite attackIcon;
    public Sprite defenseIcon;
    public Sprite moveSpeedIcon;
    public Sprite attackSpeedIcon;
    public Sprite critChanceIcon;
    public Sprite critMultiplierIcon;
    public Sprite cooldownReductionIcon;
    public Sprite mpRegenIcon;
    public Sprite penetrationIcon;
    public Sprite skillRangeIcon;
    [Tooltip("전용 아이콘이 없어 기존 것을 임시로 쓴다. 디자인 나오면 교체.")]
    public Sprite normalDamageIcon;
    public Sprite skillDamageIcon;

    private KY_StatData currentData;
    private bool isDetailed = false;

    private KY_SlideAnimator slideAnimator;
    private KY_CurtainEffect curtainEffect;
    private KY_UIAnimationManager animationManager;
    private Sequence transitionSequence;

    private PlayerStatManager statManager;
    private PlayerStat subscribedStat;
    private bool explicitOwner;
    public bool IsOpen => gameObject.activeSelf;
    public PlayerStatManager BoundStats => statManager;
    private KY_StatRow[] statRows;

    void Awake()
    {
        slideAnimator = GetComponent<KY_SlideAnimator>();
        curtainEffect = GetComponentInChildren<KY_CurtainEffect>();
        animationManager = GetComponent<KY_UIAnimationManager>();
        statRows = GetComponentsInChildren<KY_StatRow>(true);
        detailToggle.onValueChanged.AddListener(OnDetailToggleChanged);

        // 씬에서 직접 안 배선해도(다른 맵/스테이지 씬 등) Resources의 공용 DB를 자동으로 찾아 쓴다.
        if (statLabels == null)
            statLabels = Resources.Load<StatLabelDatabaseSO>("DataFiles/CharData/ClassData/3. GeneratedAssets/StatLabelDatabase");

        ApplyLabels();
        ApplyIcons();
        ApplyValueSuffixes();
    }

    /// <summary>
    /// 값 자체가 %p인 행(크리티컬 확률 0~100, 쿨타임 감소 0~70)은 총합 뒤에 %를 붙인다.
    /// 아이템 툴팁 등의 표기(ItemDisplayNames.StatUnit)와 맞춘다.
    /// </summary>
    void ApplyValueSuffixes()
    {
        critChanceRow?.SetValueSuffix("%");
        cooldownReductionRow?.SetValueSuffix("%");
    }

    void OnValidate()
    {
        ApplyIcons();
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
        mpRow.SetLabel(statLabels.GetLabel("maxMana"));          
        attackRow.SetLabel(statLabels.GetLabel("attackPower"));
        defenseRow.SetLabel(statLabels.GetLabel("defensePower"));
        moveSpeedRow.SetLabel(statLabels.GetLabel("moveSpeed"));
        attackSpeedRow.SetLabel(statLabels.GetLabel("attackSpeed"));
        critChanceRow.SetLabel(statLabels.GetLabel("critRate"));
        critMultiplierRow.SetLabel(statLabels.GetLabel("critMult"));
        cooldownReductionRow.SetLabel(statLabels.GetLabel("cdr"));
        mpRegenRow.SetLabel(statLabels.GetLabel("mpRegen"));
        penetrationRow.SetLabel(statLabels.GetLabel("pen"));
        skillRangeRow.SetLabel(statLabels.GetLabel("skillRange"));
        normalDamageRow?.SetLabel(statLabels.GetLabel("normalDamage"));
        skillDamageRow?.SetLabel(statLabels.GetLabel("skillDamage"));
    }

    /// <summary>루트 인스펙터에 지정한 아이콘을 각 스탯 행에 반영한다.</summary>
    void ApplyIcons()
    {
        hpRow?.SetIcon(hpIcon);
        mpRow?.SetIcon(mpIcon);
        attackRow?.SetIcon(attackIcon);
        defenseRow?.SetIcon(defenseIcon);
        moveSpeedRow?.SetIcon(moveSpeedIcon);
        attackSpeedRow?.SetIcon(attackSpeedIcon);
        critChanceRow?.SetIcon(critChanceIcon);
        critMultiplierRow?.SetIcon(critMultiplierIcon);
        cooldownReductionRow?.SetIcon(cooldownReductionIcon);
        mpRegenRow?.SetIcon(mpRegenIcon);
        penetrationRow?.SetIcon(penetrationIcon);
        skillRangeRow?.SetIcon(skillRangeIcon);
        normalDamageRow?.SetIcon(normalDamageIcon);
        skillDamageRow?.SetIcon(skillDamageIcon);
    }

    void OnEnable()
    {
        if (!explicitOwner && !MirrorNetworkManager.OwnsGameplay) statManager = PlayerStatManager.Instance;
        SubscribeStats();

        // 팝업이 떠 있는 동안 언어가 바뀌면 즉시 반영되도록 구독한다.
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;
    }

    void OnDisable()
    {
        UnsubscribeStats();

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
        transitionSequence?.Kill();
        // 슬라이드 중에 정상 크기 콘텐츠가 한 프레임 보였다가 다시 접히지 않도록,
        // 비활성 상태에서 먼저 접어 둔 뒤 슬라이드와 함께 펼친다.
        curtainEffect ??= GetComponentInChildren<KY_CurtainEffect>(true);
        curtainEffect?.PrepareOpen();
        gameObject.SetActive(true);

        // 닫혀 있는 동안 언어가 바뀌었을 수 있으므로 열 때마다 다시 채운다.
        ApplyLabels();
        RequestData();

        // SW 수정: 씬마다 저장된 스크롤 위치가 달라 상단 행(속성·HP·MP·공격력)이 가려지지 않도록 항상 맨 위부터 연다.
        var scrollRect = GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        animationManager?.PlayPanelOpen();

        // 팝업이 화면 안으로 들어온 뒤에 내용을 전개한다.
        transitionSequence = DOTween.Sequence();
        float slideDuration = slideAnimator != null ? slideAnimator.duration : 0f;
        transitionSequence.AppendInterval(slideDuration);
        transitionSequence.AppendCallback(() => curtainEffect?.Open());
    }

    public override void Close()
    {
        transitionSequence?.Kill();
        transitionSequence = DOTween.Sequence();
        if (curtainEffect != null)
            transitionSequence.Append(curtainEffect.Close());
        transitionSequence.AppendCallback(() =>
        {
            if (slideAnimator != null)
                slideAnimator.SlideOut(() => gameObject.SetActive(false));
            else
                gameObject.SetActive(false);
        });
    }

    void OnDestroy()
    {
        UnsubscribeStats();
        transitionSequence?.Kill();
    }

    /// <summary>SW 수정: 싱글 표시 기능을 유지하며 멀티에서는 지정한 로컬 플레이어만 구독합니다.</summary>
    public void Bind(PlayerStatManager owner)
    {
        UnsubscribeStats();
        explicitOwner = true;
        statManager = owner;
        if (isActiveAndEnabled) { SubscribeStats(); RequestData(); }
    }

    /// <summary>SW 수정: 씬 전환 때 기존 플레이어 구독과 참조를 해제합니다.</summary>
    public void Unbind() { UnsubscribeStats(); explicitOwner = true; statManager = null; }

    /// <summary>SW 수정: 씬 전환 중 남은 UI 연출을 종료합니다.</summary>
    public void CloseImmediate() { transitionSequence?.Kill(); gameObject.SetActive(false); }

    private void SubscribeStats()
    {
        UnsubscribeStats();
        subscribedStat = statManager != null ? statManager.Stat : null;
        if (subscribedStat != null) subscribedStat.OnStatChanged += HandleStatChanged;
    }

    private void UnsubscribeStats()
    {
        if (subscribedStat != null) subscribedStat.OnStatChanged -= HandleStatChanged;
        subscribedStat = null;
    }

    void RequestData()
    {
        if (statManager == null && !explicitOwner && !MirrorNetworkManager.OwnsGameplay)
            statManager = PlayerStatManager.Instance;

        if (statManager == null || statManager.Stat == null)
        {
            Debug.LogWarning("[KY_StatusPopup] PlayerStatManager.Instance가 없어 스탯을 표시할 수 없습니다.");
            return;
        }

        SetData(BuildDataFromPlayerStat(statManager));
    }

    /// <summary>
    /// PlayerStatManager의 캐릭터/장비/패시브/버프 레이어를 KY_StatData(4단)로 변환한다.
    /// 화면에서는 캐릭터(흰색) → 장비(노랑) → 패시브(파랑) → 버프(초록) 순으로 보여준다.
    /// </summary>
    private static KY_StatData BuildDataFromPlayerStat(PlayerStatManager statManager)
    {
        statManager.GetLayerStatSets(out StatSet c, out StatSet eq, out StatSet bu, out StatSet pa);

        return new KY_StatData
        {
            hp = Build(c.maxHealthFlat, eq.maxHealthFlat, eq.maxHealthPercent, bu.maxHealthPercent, bu.maxHealthFlat, pa.maxHealthPercent, pa.maxHealthFlat),
            mp = Build(c.maxManaFlat, eq.maxManaFlat, 0f, 0f, bu.maxManaFlat, 0f, pa.maxManaFlat),
            attack = Build(c.attackPowerFlat, eq.attackPowerFlat, eq.attackPowerPercent, bu.attackPowerPercent, bu.attackPowerFlat, pa.attackPowerPercent, pa.attackPowerFlat),
            defense = Build(c.defensePowerFlat, eq.defensePowerFlat, eq.defensePowerPercent, bu.defensePowerPercent, bu.defensePowerFlat, pa.defensePowerPercent, pa.defensePowerFlat),
            moveSpeed = Build(c.moveSpeedFlat, eq.moveSpeedFlat, eq.moveSpeedPercent, bu.moveSpeedPercent, bu.moveSpeedFlat, pa.moveSpeedPercent, pa.moveSpeedFlat),
            attackSpeed = Build(c.attackSpeedFlat, eq.attackSpeedFlat, eq.attackSpeedPercent, bu.attackSpeedPercent, bu.attackSpeedFlat, pa.attackSpeedPercent, pa.attackSpeedFlat),
            critChance = BuildClamped(c.critRateFlat, eq.critRateFlat, bu.critRateFlat, pa.critRateFlat, 0f, 100f),
            critMultiplier = Build(c.critMultFlat, eq.critMultFlat, eq.critMultPercent, bu.critMultPercent, bu.critMultFlat, pa.critMultPercent, pa.critMultFlat),
            cooldownReduction = BuildClamped(c.cdrFlat, eq.cdrFlat, bu.cdrFlat, pa.cdrFlat, 0f, 70f),
            mpRegen = Build(c.mpRegenFlat, eq.mpRegenFlat, eq.mpRegenPercent, bu.mpRegenPercent, bu.mpRegenFlat, pa.mpRegenPercent, pa.mpRegenFlat),
            penetration = Build(c.penFlat, eq.penFlat, eq.penPercent, bu.penPercent, bu.penFlat, pa.penPercent, pa.penFlat),
            skillRange = Build(c.skillRangeFlat, eq.skillRangeFlat, eq.skillRangePercent, bu.skillRangePercent, bu.skillRangeFlat, pa.skillRangePercent, pa.skillRangeFlat),
            // 피해 증감 %는 기준이 되는 캐릭터 기본값이 없다. mp와 같은 flat 합산 패턴으로 기본값 0에
            // 각 레이어의 %를 그대로 더한다(BuildClamped는 0 밑으로 잘려서 디버프를 표현 못 한다).
            normalDamage = Build(0f, eq.normalDamagePercent, 0f, 0f, bu.normalDamagePercent, 0f, pa.normalDamagePercent),
            skillDamage = Build(0f, eq.skillDamagePercent, 0f, 0f, bu.skillDamagePercent, 0f, pa.skillDamagePercent),
            fireDamage = Build(c.fireBonusFlat, eq.fireBonusFlat, eq.fireBonusPercent, bu.fireBonusPercent, bu.fireBonusFlat, pa.fireBonusPercent, pa.fireBonusFlat),
            iceDamage = Build(c.iceBonusFlat, eq.iceBonusFlat, eq.iceBonusPercent, bu.iceBonusPercent, bu.iceBonusFlat, pa.iceBonusPercent, pa.iceBonusFlat),
            lightningDamage = Build(c.electricBonusFlat, eq.electricBonusFlat, eq.electricBonusPercent, bu.electricBonusPercent, bu.electricBonusFlat, pa.electricBonusPercent, pa.electricBonusFlat),
        };
    }

    private static KY_StatTypeData Build(float characterFlat, float equipFlat, float equipPercent, float buffPercent, float buffFlat, float passivePercent, float passiveFlat)
    {
        PlayerStat.CalcBreakdown(characterFlat, equipFlat, equipPercent, buffPercent, buffFlat, passivePercent, passiveFlat,
            out float baseValue, out float equipValue, out float passiveValue, out float buffValue);
        return new KY_StatTypeData { baseValue = baseValue, equipValue = equipValue, passiveValue = passiveValue, buffValue = buffValue };
    }

    private static KY_StatTypeData BuildClamped(float characterFlat, float equipFlat, float buffFlat, float passiveFlat, float min, float max)
    {
        PlayerStat.CalcBreakdownClampedFlat(characterFlat, equipFlat, buffFlat, passiveFlat, min, max,
            out float baseValue, out float equipValue, out float passiveValue, out float buffValue);
        return new KY_StatTypeData { baseValue = baseValue, equipValue = equipValue, passiveValue = passiveValue, buffValue = buffValue };
    }

    void SetData(KY_StatData data)
    {
        currentData = data;

        hpRow.UpdateMode(data.hp, isDetailed);
        mpRow.UpdateMode(data.mp, isDetailed);                     
        attackRow.UpdateMode(data.attack, isDetailed);
        defenseRow.UpdateMode(data.defense, isDetailed);
        moveSpeedRow.UpdateMode(data.moveSpeed, isDetailed);
        attackSpeedRow.UpdateMode(data.attackSpeed, isDetailed);
        critChanceRow.UpdateMode(data.critChance, isDetailed);
        critMultiplierRow.UpdateMode(data.critMultiplier, isDetailed);
        cooldownReductionRow.UpdateMode(data.cooldownReduction, isDetailed);
        mpRegenRow.UpdateMode(data.mpRegen, isDetailed);
        penetrationRow.UpdateMode(data.penetration, isDetailed);
        skillRangeRow.UpdateMode(data.skillRange, isDetailed);
        normalDamageRow?.UpdateMode(data.normalDamage, isDetailed);
        skillDamageRow?.UpdateMode(data.skillDamage, isDetailed);
        fireRow.SetData(data.fireDamage);
        iceRow.SetData(data.iceDamage);
        lightningRow.SetData(data.lightningDamage);

        ApplyEnchantState();
    }

    /// <summary>
    /// 장착 무기의 속성(인챈트)에 해당하는 칸만 강조 표시한다.
    /// 무기가 없거나 무속성이면 세 칸 모두 해제된다.
    ///
    /// !! 인챈트는 "현재 장착 무기의 속성"이다. 속성별 피해 보너스 수치와는 별개라서
    ///    보너스가 0이어도 인챈트 표시는 켜질 수 있다.
    /// </summary>
    void ApplyEnchantState()
    {
        ElementType enchanted = ElementType.None;

        if (statManager != null && statManager.TryGetEquippedWeaponInfo(out EquippedWeaponInfo weapon))
            enchanted = weapon.elementType;

        fireRow.SetEnchanted(enchanted == ElementType.Fire);
        iceRow.SetEnchanted(enchanted == ElementType.Ice);
        lightningRow.SetEnchanted(enchanted == ElementType.Electric);
    }

    void OnDetailToggleChanged(bool isOn)
    {
        Debug.Log("[토글] 눌림: " + isOn + " / currentData null?: " + (currentData == null));
        if (currentData == null) return;

        isDetailed = isOn;
        SetData(currentData);

        if (statRows == null || statRows.Length == 0)
            statRows = GetComponentsInChildren<KY_StatRow>(true);

        for (int i = 0; i < statRows.Length; i++)
            statRows[i].PlayDetailTransition(isDetailed, i * 0.04f);

        StartCoroutine(RebuildLayout());
    }

    IEnumerator RebuildLayout()
    {
        yield return null; // 한 프레임 대기
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }
}
