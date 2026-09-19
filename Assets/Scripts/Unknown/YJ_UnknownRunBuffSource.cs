using System;
using System.Collections.Generic;
using Core;
using ItemSystem;
using UnityEngine;

/// <summary>런 지속/다음 전투 Unknown 보상을 기존 버프 레이어에 전달한다. 전역 상태나 컴포넌트가 아니다.</summary>
public sealed class YJ_UnknownRunBuffSource : IBuffSource
{
    public string EffectKey { get; }
    public string BattleKey { get; }
    public string BuffDisplayName { get; }
    public string BuffDescription => string.Empty;
    public Sprite BuffIcon => null;
    public FixedStatValue[] StatEffects { get; }
    public float Duration => 0f;
    public BuffStackBehavior StackBehavior => BuffStackBehavior.Ignore;
    public int MaxStack => 1;
    public bool IsPermanent => true;

    private YJ_UnknownRunBuffSource(UnknownStageBuffRecord record)
    {
        EffectKey = record.effectKey;
        BattleKey = record.battleKey;
        BuffDisplayName = string.IsNullOrWhiteSpace(record.displayName) ? record.stageId : record.displayName;
        StatEffects = CopyStats(record.statEffects);
    }

    public static FixedStatValue[] CopyStats(IReadOnlyList<FixedStatValue> stats)
    {
        var copy = new FixedStatValue[stats.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = new FixedStatValue { statType = stats[i].statType, value = stats[i].value };
        return copy;
    }

    public static bool TryValidateRecords(IReadOnlyList<UnknownStageBuffRecord> records, out string error)
    {
        error = null;
        if (records == null) return true; // 기존 세이브에는 이 필드가 없다.
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (UnknownStageBuffRecord record in records)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.effectKey) ||
                string.IsNullOrWhiteSpace(record.stageId) || !keys.Add(record.effectKey) ||
                record.statEffects == null || record.statEffects.Length == 0)
            {
                error = "Unknown 지속 효과의 저장 정보가 누락되었거나 중복되었습니다.";
                return false;
            }
            foreach (FixedStatValue stat in record.statEffects)
            {
                if (stat == null || !Enum.IsDefined(typeof(StatType), stat.statType) ||
                    float.IsNaN(stat.value) || float.IsInfinity(stat.value))
                {
                    error = "Unknown 지속 효과의 저장 스탯이 유효하지 않습니다.";
                    return false;
                }
            }
            if (!Enum.IsDefined(typeof(YJ_UnknownEffectLifetime), record.lifetime) ||
                (record.lifetime == YJ_UnknownEffectLifetime.ThisRun && !string.IsNullOrEmpty(record.battleKey)))
            {
                error = "Unknown 효과의 지속 범위 또는 전투 기록이 유효하지 않습니다.";
                return false;
            }
        }
        return true;
    }

    /// <summary>이벤트 버프만 교체한다. 포션/장비 버프는 보존하고 반복 로드 시 중복을 막는다.</summary>
    public static bool TryRestore(PlayerBuffManager target, IReadOnlyList<UnknownStageBuffRecord> records, out string error,
        string battleKey = null)
    {
        if (!TryValidateRecords(records, out error)) return false;
        if (target == null)
        {
            if (records != null)
                foreach (var record in records)
                    if (IsActive(record, battleKey))
                    {
                        error = "PlayerBuffManager가 없어 Unknown 효과를 복원할 수 없습니다.";
                        return false;
                    }
            return true;
        }

        var restored = new List<YJ_UnknownRunBuffSource>();
        if (records != null)
            foreach (UnknownStageBuffRecord record in records)
                if (IsActive(record, battleKey)) restored.Add(new YJ_UnknownRunBuffSource(record));

        // ActiveBuffs는 RemoveBuff 중 변경되므로 제거할 참조를 먼저 수집한다.
        var previous = new List<IBuffSource>();
        foreach (BuffInstance buff in target.ActiveBuffs)
            if (buff.source is YJ_UnknownRunBuffSource) previous.Add(buff.source);
        foreach (IBuffSource source in previous) target.RemoveBuff(source);
        foreach (YJ_UnknownRunBuffSource source in restored) target.ApplyBuff(source);
        return true;
    }

    public static bool IsActive(UnknownStageBuffRecord record, string battleKey = null) =>
        record.lifetime == YJ_UnknownEffectLifetime.ThisRun ||
        (!string.IsNullOrEmpty(battleKey) && record.battleKey == battleKey);

    // 호출부에서 변경 데이터를 원자적으로 저장한 뒤 실제 플레이어에게 적용한다.
    public static bool TryBindBattle(GameSaveData data, string battleKey, out bool changed, out string error)
    {
        changed = false;
        if (!TryValidateRecords(data.unknownStageBuffs, out error)) return false;
        if (string.IsNullOrWhiteSpace(battleKey)) { error = "전투 노드 키가 없습니다."; return false; }
        if (data.lastCompletedUnknownBattleKey == battleKey) return true;
        if (data.unknownStageBuffs == null) return true;
        foreach (var record in data.unknownStageBuffs)
            if (record.lifetime == YJ_UnknownEffectLifetime.NextBattle &&
                !string.IsNullOrEmpty(record.battleKey) && record.battleKey != battleKey)
            {
                error = "다른 전투에 적용 중인 Unknown 효과가 남아 있습니다. 진행 저장을 확인하세요.";
                return false;
            }
        foreach (var record in data.unknownStageBuffs)
            if (record.lifetime == YJ_UnknownEffectLifetime.NextBattle && string.IsNullOrEmpty(record.battleKey))
            {
                record.battleKey = battleKey;
                changed = true;
            }
        return true;
    }

    public static void CompleteBattle(GameSaveData data, string battleKey)
    {
        if (string.IsNullOrWhiteSpace(battleKey)) throw new ArgumentException("전투 노드 키가 없습니다.");
        data.unknownStageBuffs?.RemoveAll(r => r.lifetime == YJ_UnknownEffectLifetime.NextBattle && r.battleKey == battleKey);
        data.lastCompletedUnknownBattleKey = battleKey;
    }

    /// <summary>클리어한 전투 효과만 제거한다. 지속 버프를 재적용하여 체력을 재클램프하지 않는다.</summary>
    public static void RemoveCompletedBattle(PlayerBuffManager target, string battleKey)
    {
        if (target == null || string.IsNullOrEmpty(battleKey)) return;
        var expired = new List<IBuffSource>();
        foreach (var buff in target.ActiveBuffs)
            if (buff.source is YJ_UnknownRunBuffSource source && source.BattleKey == battleKey)
                expired.Add(source);
        foreach (var source in expired) target.RemoveBuff(source);
    }
}
