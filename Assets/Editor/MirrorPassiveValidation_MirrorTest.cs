using System;
using Core;
using UnityEditor;
using UnityEngine;

public static class MirrorPassiveValidation_MirrorTest
{
    [MenuItem("SW/Mirror Test/Validate Passive Profiles")]
    public static void Run()
    {
        var database = AssetDatabase.LoadAssetAtPath<PassiveSkillDatabaseSO>(
            "Assets/WJ_TestPlace/Data/Passive/PassiveSkillDatabase.asset");
        int checks = 0;
        void Check(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException("[MirrorPassiveValidation] " + name);
            checks++;
        }
        string Json(params PassiveSkillEntry[] entries)
        {
            var tree = new PassiveSkillTreeData();
            tree.learnedSkills.AddRange(entries);
            return JsonUtility.ToJson(tree);
        }
        PassiveSkillEntry Entry(PassiveSkillId id, int current, int unlocked) =>
            new PassiveSkillEntry { id = id, currentLevel = current, unlockedLevel = unlocked };
        bool Valid(string json) => MirrorPassiveProfile_MirrorTest.TryValidate(json, database, out _, out _);
        Check(database != null, "server database");
        Check(Valid(Json()), "fresh empty profile");
        Check(!Valid(null) && !Valid("") && !Valid("null") && !Valid("[broken"), "invalid JSON");
        Check(!Valid(new string(' ', 4097)), "payload limit");
        Check(!Valid("{\"learnedSkills\":null}"), "null entries");
        Check(!Valid(Json(Entry((PassiveSkillId)999, 0, 0))), "unknown ID");
        Check(!Valid(Json(Entry(PassiveSkillId.AttackPower, -1, 0))), "negative current");
        Check(!Valid(Json(Entry(PassiveSkillId.AttackPower, 0, -1))), "negative unlocked");
        Check(!Valid(Json(Entry(PassiveSkillId.AttackPower, 2, 1))), "current above unlocked");
        Check(!Valid(Json(Entry(PassiveSkillId.AttackPower, 1, 99))), "unlocked above max");
        Check(!Valid(Json(Entry(PassiveSkillId.AttackPower, 1, 1), Entry(PassiveSkillId.AttackPower, 1, 1))), "duplicate ID");
        Check(!MirrorPassiveProfile_MirrorTest.TryValidate(Json(), null, out _, out _), "missing server DB");
        foreach (PassiveSkillId id in Enum.GetValues(typeof(PassiveSkillId)))
        {
            var definition = database.Get(id);
            Check(definition != null, "definition " + id);
            Check(Valid(Json(Entry(id, 0, definition.maxLevel))), "unlocked but inactive " + id);
            Check(Valid(Json(Entry(id, definition.maxLevel, definition.maxLevel))), "maximum " + id);
            Check(!Valid(Json(Entry(id, definition.maxLevel + 1, definition.maxLevel + 1))), "rank overflow " + id);
        }
        MirrorPassiveProfile_MirrorTest.TryValidate(Json(Entry(PassiveSkillId.AttackPower, 1, 5)), database, out var first, out _);
        MirrorPassiveProfile_MirrorTest.TryValidate(Json(Entry(PassiveSkillId.AttackPower, 5, 5),
            Entry(PassiveSkillId.ShopEnhance, 1, 1)), database, out var second, out _);
        Check(first.Stats.attackPowerPercent == database.Get(PassiveSkillId.AttackPower).GetValue(1), "personal rank one");
        Check(second.Stats.attackPowerPercent == database.Get(PassiveSkillId.AttackPower).GetValue(5), "personal rank five");
        Check(first.ShopLevel == 0 && second.ShopLevel == 1, "separate shop investment");
        Check(Mathf.Approximately(second.DiscountFraction, database.Get(PassiveSkillId.ShopEnhance).GetValue(1) / 100f), "discount percent to fraction");
        Check(second.ExtraRerolls == database.Get(PassiveSkillId.ShopEnhance).extraRerollCount, "server reroll count");
        Check(first.ReviveHealthFraction == 0f && Mathf.Approximately(first.CampHealFraction, 0.2f), "untrained revive and base camp heal");
        Check(MirrorPassiveProfile_MirrorTest.TryValidate(Json(Entry(PassiveSkillId.Revive, 1, 1),
            Entry(PassiveSkillId.CampHealBonus, 1, 1)), database, out var recovery, out _), "recovery passive profile");
        Check(Mathf.Approximately(recovery.ReviveHealthFraction, 0.2f) &&
            Mathf.Approximately(recovery.CampHealFraction, 0.4f), "individual recovery percentages");
        Check(Valid(recovery.TreeJson), "validated profile round trip");
        var noPassive = new PlayerStat();
        var low = new PlayerStat();
        var high = new PlayerStat();
        var baseline = new StatSet { attackPowerFlat = 100f };
        noPassive.Recalculate(baseline, StatSet.Zero, StatSet.Zero, StatSet.Zero);
        low.Recalculate(baseline, StatSet.Zero, StatSet.Zero, first.Stats);
        high.Recalculate(baseline, StatSet.Zero, StatSet.Zero, second.Stats);
        Check(noPassive.attackPower < low.attackPower && low.attackPower < high.attackPower, "actual independent stat formula");
        Debug.Log("[MirrorPassiveValidation] PASS " + checks + " checks");
    }
}
