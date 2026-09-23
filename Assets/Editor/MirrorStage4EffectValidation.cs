using System;
using ItemSystem;
using UnityEditor;
using UnityEngine;

/// <summary>실제 플레이어에서 공통 상태 소유권과 후속 피해의 직접 공격 재발동 금지를 검사한다.</summary>
public static class MirrorStage4EffectValidation
{
    [MenuItem("SW/Mirror Test/Validate Stage4 Effect Boundaries (Play Mode)")]
    public static void Validate()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("플레이 중 실제 플레이어가 필요합니다.");
        var players = UnityEngine.Object.FindObjectsByType<PlayerContext>(FindObjectsSortMode.None);
        if (players.Length == 0) throw new InvalidOperationException("실제 플레이어가 없습니다.");
        var states = new System.Collections.Generic.HashSet<PlayerItemEffectState>();
        foreach (var player in players)
        {
            Require(player.IsComplete && states.Add(player.Effects), "플레이어별 상태·인벤토리 소유");
            Require(player.GetComponent<PlayerArmorEffectRuntime>() != null, "공통 방어구 실행기 연결");
            Require(player.GetComponent<PlayerRelicEffectRuntime>() != null, "공통 유물 실행기 연결");
            var effects = player.Effects;
            var counts = (effects.ChainLightningTriggerCount, effects.InfernoTriggerCount,
                effects.GlassRailTriggerCount, effects.PreparedAttackConsumeCount, effects.Cooldowns.Count);
            foreach (var cause in new[] { DamageCause.Skill, DamageCause.Effect, DamageCause.DoT })
            {
                var result = new WBH_DamageResult(player.Controller, 10f, true, ElementType.Fire,
                    null, null, null, null, cause, 123u);
                effects.FireDamageDealt(result);
                Require(effects.ConsumePreparedAttackMultiplier(cause, 123u) == 1f, "Direct 이외 준비 공격 소비 금지");
            }
            Require(counts == (effects.ChainLightningTriggerCount, effects.InfernoTriggerCount,
                effects.GlassRailTriggerCount, effects.PreparedAttackConsumeCount, effects.Cooldowns.Count),
                "Skill·Effect·DoT 재발동 금지");
        }
        Debug.Log($"[MirrorStage4EffectValidation] PASS {players.Length} actual player boundaries");
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("[MirrorStage4EffectValidation] " + label);
    }
}
