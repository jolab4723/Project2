using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEditor;
using UnityEngine;

/// <summary>P3-B 첫 대표(H3)인 저마나 Threshold와 방어구 장착 어댑터의 수명을 검증한다.</summary>
public static class ArmorRelicP3BValidation_MirrorTest
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("SW/Mirror Test/Validate Armor Relic P3-B H3")]
    public static void ValidateH3()
    {
        int checks = 0;
        void Check(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException("[ArmorRelicP3BValidation] FAIL: " + label);
            checks++;
        }

        bool wasServerActive = NetworkServer.active;
        typeof(NetworkServer).GetProperty("active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(null, true);

        var created = new List<GameObject>();
        StatThresholdBuffUniqueEffectSO effect = null;
        try
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab");
            Check(playerPrefab != null, "SW Fighter PlayerContext 프리팹 존재");

            effect = CreateH3Effect();
            ItemDefinitionSO definition = CreateH3Definition(effect);
            GameObject player = UnityEngine.Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            player.name = "ArmorRelicP3BValidation_Player";
            created.Add(player);

            PlayerContext context = InitializePlayer(player);
            PlayerArmorEffectProvider_MirrorTest provider =
                player.GetComponent<PlayerArmorEffectProvider_MirrorTest>() ??
                player.AddComponent<PlayerArmorEffectProvider_MirrorTest>();
            Check(provider != null, "방어구 활성 어댑터 생성");

            context.Mana.SetCurrentMana(100f);
            InventoryItem helmet = EquipArmor(context, definition, "p3b_h3_helmet");
            Check(helmet != null, "H3 투구 장착 상태 생성");
            Check(!HasBuff(context, effect), "마나 100%에서는 Threshold 비활성");
            Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 10f), "비활성 시 기본 마나 재생 유지");

            context.Mana.SetCurrentMana(25f);
            Check(HasBuff(context, effect), "마나 25% 경계에서 Threshold 활성");
            Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 13f), "활성 시 마나 재생 +30% 적용");

            context.Mana.SetCurrentMana(26f);
            Check(!HasBuff(context, effect), "마나 25% 초과에서 Threshold 비활성");
            Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 10f), "경계 이탈 시 기본 마나 재생 복귀");

            context.Mana.SetCurrentMana(0f);
            Check(HasBuff(context, effect), "마나 0에서 Threshold 활성");

            GetEquippedItems(context).Remove(EquipSlotType.Helmet);
            PublishEquipmentChanged(context.Equipment);
            Check(!HasBuff(context, effect), "투구 해제 직후 Threshold 제거");
            Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 10f), "투구 해제 후 기본 마나 재생 복귀");

            Debug.Log($"[ArmorRelicP3BValidation] PASS {checks} checks. H3 CurrentManaPercent 25% 경계·0·해제 수명 확인.");
        }
        finally
        {
            foreach (GameObject gameObject in created)
            {
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);
            }

            if (effect != null)
                UnityEngine.Object.DestroyImmediate(effect);

            typeof(NetworkServer).GetProperty("active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(null, wasServerActive);
        }
    }

    private static PlayerContext InitializePlayer(GameObject player)
    {
        PlayerContext context = player.GetComponent<PlayerContext>();
        NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
        foreach (NetworkBehaviour behaviour in player.GetComponentsInChildren<NetworkBehaviour>(true))
            SetNetworkServerState(identity, behaviour);

        context.Stats.EnsureInitialized().Recalculate(
            new StatSet { maxManaFlat = 100f, mpRegenFlat = 10f },
            StatSet.Zero,
            StatSet.Zero,
            StatSet.Zero);
        context.Mana.RefreshMaxMana();
        return context;
    }

    private static StatThresholdBuffUniqueEffectSO CreateH3Effect()
    {
        var effect = ScriptableObject.CreateInstance<StatThresholdBuffUniqueEffectSO>();
        effect.name = "P3B_H3_ManaRecovery";
        effect.effectName = "절전 모드 헤드셋";
        effect.effectDescription = "현재 마나가 최대 마나의 {0}% 이하일 때 마나 재생이 {1}% 증가합니다.";
        effect.coefficients = new[] { 25f, 30f };
        effect.referenceStat = StatReference.CurrentManaPercent;
        effect.comparisonOperator = ComparisonOperator.LessOrEqual;
        effect.thresholdValue = 25f;
        effect.buffSpec = new BuffSpec
        {
            statEffects = new[]
            {
                new FixedStatValue { statType = StatType.mpRegenPercent, value = 30f },
            },
            duration = 0f,
            stackBehavior = BuffStackBehavior.Ignore,
            maxStack = 1,
        };
        return effect;
    }

    private static ItemDefinitionSO CreateH3Definition(StatThresholdBuffUniqueEffectSO effect)
    {
        var definition = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        definition.name = "P3B_H3_ArmorDefinition";
        definition.itemId = "P3B_H3_HELMET";
        definition.itemName = "절전 모드 헤드셋";
        definition.category = ItemCategory.Armor;
        definition.armorType = ArmorType.Helmet;
        definition.itemWidth = 2;
        definition.itemHeight = 2;
        definition.uniqueEffect = effect;
        definition.uniqueEffectId = effect.name;
        return definition;
    }

    private static InventoryItem EquipArmor(PlayerContext context, ItemDefinitionSO definition, string instanceId)
    {
        var item = new InventoryItem(new ItemInstance
        {
            instanceId = instanceId,
            definition = definition,
        });
        GetEquippedItems(context)[EquipSlotType.Helmet] = item;
        PublishEquipmentChanged(context.Equipment);
        return item;
    }

    private static bool HasBuff(PlayerContext context, IBuffSource source)
    {
        return context.Buffs.ActiveBuffs.Any(buff => ReferenceEquals(buff.source, source));
    }

    private static void PublishEquipmentChanged(EquipmentSystem equipment)
    {
        typeof(EquipmentSystem).GetMethod("PublishChanged", PrivateInstance)?.Invoke(equipment, null);
    }

    private static Dictionary<EquipSlotType, InventoryItem> GetEquippedItems(PlayerContext context)
    {
        return (Dictionary<EquipSlotType, InventoryItem>)typeof(EquipmentSystem)
            .GetField("equippedItems", PrivateInstance)?.GetValue(context.Equipment);
    }

    private static void SetNetworkServerState(NetworkIdentity identity, NetworkBehaviour behaviour)
    {
        typeof(NetworkIdentity).GetProperty("isServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(identity, true);
        typeof(NetworkBehaviour).GetProperty("netIdentity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(behaviour, identity);
    }
}
