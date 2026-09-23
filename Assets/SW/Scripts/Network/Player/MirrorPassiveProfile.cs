using System;
using System.Collections.Generic;
using Core;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>로컬 저장의 ID·단계만 검증하고 서버 DB로 효과를 계산한다. 계정의 구매 이력을 인증하지는 않는다.</summary>
public sealed class MirrorPassiveProfile
{
    public StatSet Stats { get; private set; }
    public int ShopLevel { get; private set; }
    public int ExtraRerolls { get; private set; }
    public float DiscountFraction { get; private set; }
    public float ReviveHealthFraction { get; private set; }
    public float CampHealFraction { get; private set; } = 0.2f;
    public string TreeJson { get; private set; }

    /// <summary>현재 선택한 프로필에서 패시브 단계만 읽어 서버에 보낼 JSON을 만든다.</summary>
    public static string ReadLocalPayload()
    {
        PlayerProfileData profile = PassiveSkillManager.Instance?.CurrentProfile ??
            DataManager.Instance?.LoadSinglePlayerSlot()?.profile;
        return JsonUtility.ToJson(profile?.passiveSkillTree ?? new PassiveSkillTreeData());
    }

    /// <summary>알 수 없는 스킬과 잘못된 단계를 거절하고, 효과 수치는 서버 DB에서 계산한다.</summary>
    public static bool TryValidate(string json, PassiveSkillDatabaseSO database,
        out MirrorPassiveProfile profile, out string reason)
    {
        profile = null;
        reason = "패시브 프로필을 확인할 수 없습니다.";
        if (database == null || json == null || json.Length > 4096) return false;
        PassiveSkillTreeData tree;
        try
        {
            tree = JsonConvert.DeserializeObject<PassiveSkillTreeData>(json, new JsonSerializerSettings
            {
                MaxDepth = 8, CheckAdditionalContent = true, TypeNameHandling = TypeNameHandling.None
            });
        }
        catch (JsonException) { return false; }
        if (tree?.learnedSkills == null || tree.learnedSkills.Count > Enum.GetValues(typeof(PassiveSkillId)).Length)
            return false;

        var seen = new HashSet<PassiveSkillId>();
        var effects = new Dictionary<PassiveSkillId, float>();
        var result = new MirrorPassiveProfile();
        foreach (PassiveSkillEntry entry in tree.learnedSkills)
        {
            if (entry == null || !Enum.IsDefined(typeof(PassiveSkillId), entry.id) || !seen.Add(entry.id)) return false;
            PassiveSkillDefinition definition = database.Get(entry.id);
            if (definition == null || entry.currentLevel < 0 || entry.unlockedLevel < entry.currentLevel ||
                entry.unlockedLevel > definition.maxLevel || definition.valuesPerLevel == null ||
                entry.currentLevel > definition.valuesPerLevel.Length) return false;
            float value = definition.GetValue(entry.currentLevel);
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) return false;
            effects.Add(entry.id, value);
            if (entry.id == PassiveSkillId.ShopEnhance)
            {
                result.ShopLevel = entry.currentLevel;
                result.ExtraRerolls = entry.currentLevel > 0 ? definition.extraRerollCount : 0;
                result.DiscountFraction = value / 100f;
                if (result.ExtraRerolls < 0 || result.DiscountFraction > 0.95f) return false;
            }
        }
        float Value(PassiveSkillId id) => effects.TryGetValue(id, out float value) ? value : 0f;
        result.ReviveHealthFraction = Value(PassiveSkillId.Revive) / 100f;
        float campHeal = Value(PassiveSkillId.CampHealBonus);
        result.CampHealFraction = campHeal > 0f ? campHeal / 100f : 0.2f;
        if (result.ReviveHealthFraction > 1f || result.CampHealFraction > 1f) return false;
        result.TreeJson = JsonUtility.ToJson(tree);
        result.Stats = new StatSet
        {
            maxHealthPercent = Value(PassiveSkillId.MaxHealth),
            attackPowerPercent = Value(PassiveSkillId.AttackPower),
            defensePowerPercent = Value(PassiveSkillId.DefensePower),
            moveSpeedPercent = Value(PassiveSkillId.AllSpeed),
            attackSpeedPercent = Value(PassiveSkillId.AllSpeed),
            critRateFlat = Value(PassiveSkillId.CritRate),
            critMultFlat = Value(PassiveSkillId.CritDamage),
            cdrFlat = Value(PassiveSkillId.CooldownReduction),
            fireBonusFlat = Value(PassiveSkillId.AllElementalBonus),
            iceBonusFlat = Value(PassiveSkillId.AllElementalBonus),
            electricBonusFlat = Value(PassiveSkillId.AllElementalBonus),
        };
        profile = result;
        reason = null;
        return true;
    }
}
