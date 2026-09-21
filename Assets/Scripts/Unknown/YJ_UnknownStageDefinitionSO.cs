using System;
using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

[CreateAssetMenu(
    fileName = "UnknownStageDefinition",
    menuName = "Unknown Stage/Definition")]
public class YJ_UnknownStageDefinitionSO : ScriptableObject
{
    [SerializeField] private string stageId;
    [SerializeField] private string stageName;
    [SerializeField, Range(0, 3)] private int choiceNumber = 1;
    [SerializeField] private Sprite backgroundImage;

    [Header("선택지 실행 데이터 (설명 문구와 별도)")]
    [Tooltip("Element 0/1/2는 화면의 선택지 1/2/3입니다. 빈 Effects는 미설정이며, 효과가 없으면 None을 명시하세요. 생성기 재실행 시 보존됩니다.")]
    [SerializeField] private List<YJ_UnknownStageChoice> choices = new();

    public string StageId => stageId;
    public string StageName => stageName;
    public int ChoiceNumber => choiceNumber;
    public Sprite BackgroundImage => backgroundImage;

    /// <summary>표시 중인 선택지만 조회한다. 아직 효과를 설정하지 않았다면 실패한다.</summary>
    public bool TryGetChoice(int index, out YJ_UnknownStageChoice choice, out string error)
    {
        choice = null;
        if (index < 0 || index >= choiceNumber || choices == null || index >= choices.Count)
        {
            error = "선택지 인덱스가 잘못되었거나 실행 데이터가 없습니다.";
            return false;
        }

        YJ_UnknownStageChoice candidate = choices[index];
        if (candidate == null)
        {
            error = "선택지 실행 데이터가 없습니다.";
            return false;
        }

        if (!candidate.TryValidate(out error))
            return false;

        choice = candidate;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        choiceNumber = Mathf.Clamp(choiceNumber, 0, 3);
        EnsureChoiceSlots();
    }

    private void EnsureChoiceSlots()
    {
        choices ??= new List<YJ_UnknownStageChoice>();
        while (choices.Count < choiceNumber)
            choices.Add(new YJ_UnknownStageChoice());
        // 선택지 수를 줄였다 늘려도 기존 설정을 잃지 않도록 초과 슬롯은 보존한다.
    }

    public void SetEditorData(
        string id,
        string displayName,
        int choices,
        Sprite background)
    {
        stageId = id;
        stageName = displayName;
        choiceNumber = Mathf.Clamp(choices, 0, 3);
        backgroundImage = background;
        // 표/배경만 갱신한다. 인스펙터에서 작성한 실행 데이터는 덮어쓰지 않는다.
        EnsureChoiceSlots();
    }
#endif
}

// 직렬화된 enum 값은 기존 에셋과 저장 데이터의 호환성을 위해 재배정하지 않는다.
public enum YJ_UnknownEffectType
{
    None = 0,
    ModifyStats = 1,
    HealMaxHealthPercent = 2,
    DamageMaxHealthPercent = 3,
    AddGold = 4,
    SpendGold = 5,
    LoseAllGold = 6,
    GrantItem = 7,
    GrantRandomEquipment = 8,
    DiscardSelectedItems = 9,
    DiscardRandomItems = 10,
    MoveToStage = 11
}

public enum YJ_UnknownEffectLifetime
{
    ThisRun = 0,
    NextBattle = 1
}

public enum YJ_UnknownStageDestination
{
    StageSelect = 0,
    Battle = 1,
    Elite = 2,
    Camp = 3
}

[Serializable]
public class YJ_UnknownStageChoice
{
    [Tooltip("위에서부터 순서대로 적용할 효과입니다. 실행부는 모든 비용과 조건을 먼저 검사해야 합니다.")]
    [SerializeField] private List<YJ_UnknownStageEffect> effects = new();

    public IReadOnlyList<YJ_UnknownStageEffect> Effects => effects;

    public bool TryValidate(out string error)
    {
        if (effects == null || effects.Count == 0)
        {
            error = "Effects가 미설정입니다. 효과 없는 선택지는 None을 하나 설정하세요.";
            return false;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] == null)
            {
                error = $"효과 {i + 1}의 데이터가 없습니다.";
                return false;
            }
            if (!effects[i].TryValidate(out error))
            {
                error = $"효과 {i + 1}: {error}";
                return false;
            }
            if (effects[i].Type == YJ_UnknownEffectType.None && effects.Count != 1)
            {
                error = "None은 다른 효과와 함께 사용할 수 없습니다.";
                return false;
            }
            if (effects[i].Type == YJ_UnknownEffectType.MoveToStage)
            {
                if (i != effects.Count - 1)
                {
                    error = "씬 이동은 마지막 효과에 한 번만 설정하세요.";
                    return false;
                }
            }
        }
        error = null;
        return true;
    }
}

/// <summary>정의 데이터만 보유한다. 확률 판정, 지급, 차감, 저장은 실행 단계의 책임이다.</summary>
[Serializable]
public class YJ_UnknownStageEffect
{
    [SerializeField] private YJ_UnknownEffectType type;
    [Tooltip("효과별 성공 확률. 1 = 100%, 0.5 = 50%. 여러 효과의 확률은 독립입니다.")]
    [SerializeField, Range(0f, 1f)] private float probability = 1f;

    [Header("ModifyStats 전용")]
    [SerializeField] private YJ_UnknownEffectLifetime lifetime;
    [Tooltip("기존 스탯 단위 사용. attackPowerPercent 5 = 공격력 +5%. 디버프는 음수.")]
    [SerializeField] private FixedStatValue[] statEffects = Array.Empty<FixedStatValue>();

    [Header("체력 회복 / 피해 전용")]
    [Tooltip("최대 체력 대비 퍼센트. 30 = 최대 체력의 30%. 음수 대신 효과 타입으로 구분합니다.")]
    [SerializeField, Range(0f, 100f)] private float healthPercent;

    [Header("재화 / 아이템 수량 전용")]
    [SerializeField, Min(1)] private int amount = 1;
    [Tooltip("GrantItem 전용. 표시 이름 대신 이 SO의 itemId로 저장합니다.")]
    [SerializeField] private ItemDefinitionSO item;
    [Tooltip("GrantRandomEquipment 전용. 무기/방어구 후보를 이 등급으로 제한합니다.")]
    [SerializeField] private ItemRarity rarity;

    [Header("MoveToStage 전용")]
    [SerializeField] private YJ_UnknownStageDestination destination;

    public YJ_UnknownEffectType Type => type;
    public float Probability => probability;
    public YJ_UnknownEffectLifetime Lifetime => lifetime;
    public IReadOnlyList<FixedStatValue> StatEffects => statEffects;
    public float HealthPercent => healthPercent;
    public int Amount => amount;
    public ItemDefinitionSO Item => item;
    public ItemRarity Rarity => rarity;
    public YJ_UnknownStageDestination Destination => destination;

    public bool TryValidate(out string error)
    {
        error = null;
        if (!Enum.IsDefined(typeof(YJ_UnknownEffectType), type))
            error = "알 수 없는 효과 타입입니다.";
        else if (!IsFinite(probability) || probability < 0f || probability > 1f)
            error = "Probability는 0~1이어야 합니다.";
        else if (type == YJ_UnknownEffectType.ModifyStats)
        {
            if (!Enum.IsDefined(typeof(YJ_UnknownEffectLifetime), lifetime))
                error = "지속 범위가 잘못되었습니다.";
            else if (statEffects == null || statEffects.Length == 0)
                error = "변경할 스탯이 없습니다.";
            else
                foreach (FixedStatValue stat in statEffects)
                    if (stat == null || !Enum.IsDefined(typeof(StatType), stat.statType) || !IsFinite(stat.value))
                    {
                        error = "스탯 종류 또는 값이 잘못되었습니다.";
                        break;
                    }
        }
        else if (type == YJ_UnknownEffectType.HealMaxHealthPercent || type == YJ_UnknownEffectType.DamageMaxHealthPercent)
        {
            if (!IsFinite(healthPercent) || healthPercent <= 0f || healthPercent > 100f)
                error = "Health Percent는 0보다 크고 100 이하여야 합니다.";
        }
        else if (type == YJ_UnknownEffectType.MoveToStage)
        {
            if (!Enum.IsDefined(typeof(YJ_UnknownStageDestination), destination))
                error = "이동 목적지가 잘못되었습니다.";
        }
        else if (type != YJ_UnknownEffectType.None && type != YJ_UnknownEffectType.LoseAllGold)
        {
            if (amount <= 0)
                error = "재화/아이템 수량은 1 이상이어야 합니다.";
            else if (type == YJ_UnknownEffectType.GrantItem && (item == null || string.IsNullOrWhiteSpace(item.itemId)))
                error = "지급할 아이템 SO와 itemId가 필요합니다.";
            else if (type == YJ_UnknownEffectType.GrantRandomEquipment && !Enum.IsDefined(typeof(ItemRarity), rarity))
                error = "장비 등급이 잘못되었습니다.";
        }
        return error == null;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
