using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 김성우 MergeTest 씬의 <c>KY_StatusPopup</c> 디자인과 수치 계산을 Mirror 테스트에서
/// 그대로 사용하는 PlayerContext 전용 복제본이다.
/// <para>원본과의 차이는 <c>PlayerStatManager.Instance</c>를 찾지 않고
/// <see cref="Bind"/>로 받은 로컬 플레이어의 Stat만 구독한다는 점이다.</para>
/// <para>팀원 원본 스크립트와 원본 Scene은 수정하지 않으며, MergeTest 스탯창 복제 프리팹에서만 사용한다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class KY_StatusPopup_MirrorTest : KY_PopupBase
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

    [Header("속성 행")]
    public KY_ElementRow fireRow;
    public KY_ElementRow iceRow;
    public KY_ElementRow lightningRow;

    [Header("세부 표시")]
    public Toggle detailToggle;

    [Header("표시 이름")]
    public StatLabelDatabaseSO statLabels;

    private KY_StatData currentData;
    private PlayerStatManager stats;
    private KY_SlideAnimator slideAnimator;
    private KY_CurtainEffect curtainEffect;
    private bool isDetailed;
    private bool initialized;

    public bool IsOpen => gameObject.activeSelf;
    public PlayerStatManager BoundStats => stats;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (detailToggle != null)
            detailToggle.onValueChanged.RemoveListener(HandleDetailToggleChanged);

        Unbind();
    }

    public void Bind(PlayerStatManager target)
    {
        if (stats == target)
        {
            if (IsOpen)
                RefreshData();
            return;
        }

        Unsubscribe();
        stats = target;
        Subscribe();

        if (IsOpen)
            RefreshData();
    }

    public void Unbind()
    {
        Unsubscribe();
        stats = null;
        currentData = null;
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void CloseImmediate()
    {
        DOTween.Kill(GetComponent<RectTransform>());
        gameObject.SetActive(false);
    }

    public override void Open()
    {
        gameObject.SetActive(true);
        EnsureInitialized();
        ApplyLabels();
        RefreshData();

        Sequence sequence = DOTween.Sequence();
        if (slideAnimator != null)
            sequence.Append(slideAnimator.SlideIn());
        if (curtainEffect != null)
            sequence.AppendCallback(() => curtainEffect.Open());
    }

    public override void Close()
    {
        EnsureInitialized();
        Sequence sequence = DOTween.Sequence();
        if (curtainEffect != null)
            sequence.Append(curtainEffect.Close());

        if (slideAnimator != null)
            sequence.Append(slideAnimator.SlideOut(() => gameObject.SetActive(false)));
        else
            sequence.AppendCallback(() => gameObject.SetActive(false));
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;
        slideAnimator = GetComponent<KY_SlideAnimator>();
        curtainEffect = GetComponentInChildren<KY_CurtainEffect>(true);

        if (detailToggle != null)
            detailToggle.onValueChanged.AddListener(HandleDetailToggleChanged);

        ApplyLabels();
    }

    private void Subscribe()
    {
        if (stats?.Stat != null)
            stats.Stat.OnStatChanged += HandleStatChanged;
    }

    private void Unsubscribe()
    {
        if (stats?.Stat != null)
            stats.Stat.OnStatChanged -= HandleStatChanged;
    }

    private void HandleStatChanged()
    {
        if (IsOpen)
            RefreshData();
    }

    private void ApplyLabels()
    {
        if (statLabels == null)
            return;

        hpRow?.SetLabel(statLabels.GetLabel("maxHealth"));
        mpRow?.SetLabel(statLabels.GetLabel("maxMana"));
        attackRow?.SetLabel(statLabels.GetLabel("attackPower"));
        defenseRow?.SetLabel(statLabels.GetLabel("defensePower"));
        moveSpeedRow?.SetLabel(statLabels.GetLabel("moveSpeed"));
        attackSpeedRow?.SetLabel(statLabels.GetLabel("attackSpeed"));
        critChanceRow?.SetLabel(statLabels.GetLabel("critRate"));
        critMultiplierRow?.SetLabel(statLabels.GetLabel("critMult"));
        cooldownReductionRow?.SetLabel(statLabels.GetLabel("cdr"));
        mpRegenRow?.SetLabel(statLabels.GetLabel("mpRegen"));
        penetrationRow?.SetLabel(statLabels.GetLabel("pen"));
        skillRangeRow?.SetLabel(statLabels.GetLabel("skillRange"));
    }

    private void RefreshData()
    {
        if (stats?.Stat == null)
        {
            Debug.LogWarning("[KY_StatusPopup_MirrorTest] 로컬 PlayerContext의 Stat이 연결되지 않았습니다.", this);
            return;
        }

        currentData = BuildData(stats);
        ApplyData(currentData);
    }

    private static KY_StatData BuildData(PlayerStatManager manager)
    {
        manager.GetLayerStatSets(out StatSet character, out StatSet equipment, out StatSet buff, out StatSet passive);

        return new KY_StatData
        {
            hp = Build(character.maxHealthFlat, equipment.maxHealthFlat, equipment.maxHealthPercent, buff.maxHealthPercent, buff.maxHealthFlat, passive.maxHealthPercent, passive.maxHealthFlat),
            mp = Build(character.maxManaFlat, equipment.maxManaFlat, 0f, 0f, buff.maxManaFlat, 0f, passive.maxManaFlat),
            attack = Build(character.attackPowerFlat, equipment.attackPowerFlat, equipment.attackPowerPercent, buff.attackPowerPercent, buff.attackPowerFlat, passive.attackPowerPercent, passive.attackPowerFlat),
            defense = Build(character.defensePowerFlat, equipment.defensePowerFlat, equipment.defensePowerPercent, buff.defensePowerPercent, buff.defensePowerFlat, passive.defensePowerPercent, passive.defensePowerFlat),
            moveSpeed = Build(character.moveSpeedFlat, equipment.moveSpeedFlat, equipment.moveSpeedPercent, buff.moveSpeedPercent, buff.moveSpeedFlat, passive.moveSpeedPercent, passive.moveSpeedFlat),
            attackSpeed = Build(character.attackSpeedFlat, equipment.attackSpeedFlat, equipment.attackSpeedPercent, buff.attackSpeedPercent, buff.attackSpeedFlat, passive.attackSpeedPercent, passive.attackSpeedFlat),
            critChance = BuildClamped(character.critRateFlat, equipment.critRateFlat, buff.critRateFlat, passive.critRateFlat, 0f, 100f),
            critMultiplier = Build(character.critMultFlat, equipment.critMultFlat, equipment.critMultPercent, buff.critMultPercent, buff.critMultFlat, passive.critMultPercent, passive.critMultFlat),
            cooldownReduction = BuildClamped(character.cdrFlat, equipment.cdrFlat, buff.cdrFlat, passive.cdrFlat, 0f, 70f),
            mpRegen = Build(character.mpRegenFlat, equipment.mpRegenFlat, equipment.mpRegenPercent, buff.mpRegenPercent, buff.mpRegenFlat, passive.mpRegenPercent, passive.mpRegenFlat),
            penetration = Build(character.penFlat, equipment.penFlat, equipment.penPercent, buff.penPercent, buff.penFlat, passive.penPercent, passive.penFlat),
            skillRange = Build(character.skillRangeFlat, equipment.skillRangeFlat, equipment.skillRangePercent, buff.skillRangePercent, buff.skillRangeFlat, passive.skillRangePercent, passive.skillRangeFlat),
            fireDamage = Build(character.fireBonusFlat, equipment.fireBonusFlat, equipment.fireBonusPercent, buff.fireBonusPercent, buff.fireBonusFlat, passive.fireBonusPercent, passive.fireBonusFlat),
            iceDamage = Build(character.iceBonusFlat, equipment.iceBonusFlat, equipment.iceBonusPercent, buff.iceBonusPercent, buff.iceBonusFlat, passive.iceBonusPercent, passive.iceBonusFlat),
            lightningDamage = Build(character.electricBonusFlat, equipment.electricBonusFlat, equipment.electricBonusPercent, buff.electricBonusPercent, buff.electricBonusFlat, passive.electricBonusPercent, passive.electricBonusFlat),
        };
    }

    private static KY_StatTypeData Build(
        float characterFlat,
        float equipmentFlat,
        float equipmentPercent,
        float buffPercent,
        float buffFlat,
        float passivePercent,
        float passiveFlat)
    {
        PlayerStat.CalcBreakdown(
            characterFlat,
            equipmentFlat,
            equipmentPercent,
            buffPercent,
            buffFlat,
            passivePercent,
            passiveFlat,
            out float baseValue,
            out float equipmentValue,
            out float buffValue);

        return new KY_StatTypeData
        {
            baseValue = baseValue,
            equipValue = equipmentValue,
            buffValue = buffValue,
        };
    }

    private static KY_StatTypeData BuildClamped(
        float characterFlat,
        float equipmentFlat,
        float buffFlat,
        float passiveFlat,
        float min,
        float max)
    {
        PlayerStat.CalcBreakdownClampedFlat(
            characterFlat,
            equipmentFlat,
            buffFlat,
            passiveFlat,
            min,
            max,
            out float baseValue,
            out float equipmentValue,
            out float buffValue);

        return new KY_StatTypeData
        {
            baseValue = baseValue,
            equipValue = equipmentValue,
            buffValue = buffValue,
        };
    }

    private void ApplyData(KY_StatData data)
    {
        hpRow?.UpdateMode(data.hp, isDetailed);
        mpRow?.UpdateMode(data.mp, isDetailed);
        attackRow?.UpdateMode(data.attack, isDetailed);
        defenseRow?.UpdateMode(data.defense, isDetailed);
        moveSpeedRow?.UpdateMode(data.moveSpeed, isDetailed);
        attackSpeedRow?.UpdateMode(data.attackSpeed, isDetailed);
        critChanceRow?.UpdateMode(data.critChance, isDetailed);
        critMultiplierRow?.UpdateMode(data.critMultiplier, isDetailed);
        cooldownReductionRow?.UpdateMode(data.cooldownReduction, isDetailed);
        mpRegenRow?.UpdateMode(data.mpRegen, isDetailed);
        penetrationRow?.UpdateMode(data.penetration, isDetailed);
        skillRangeRow?.UpdateMode(data.skillRange, isDetailed);
        fireRow?.SetData(data.fireDamage);
        iceRow?.SetData(data.iceDamage);
        lightningRow?.SetData(data.lightningDamage);
    }

    private void HandleDetailToggleChanged(bool value)
    {
        isDetailed = value;
        if (currentData != null)
            ApplyData(currentData);

        StartCoroutine(RebuildLayoutNextFrame());
    }

    private IEnumerator RebuildLayoutNextFrame()
    {
        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }
}
