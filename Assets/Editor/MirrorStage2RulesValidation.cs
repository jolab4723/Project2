using System;
using System.Reflection;
using ItemSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>2단계 공통 규칙의 실패·소유 분리·쿨타임을 검사합니다. 실제 네트워크 검증은 별도입니다.</summary>
public static class MirrorStage2RulesValidation
{
    [MenuItem("SW/Mirror Test/Validate Stage2 Rules")]
    public static void ValidateRules()
    {
        Require(!Application.isPlaying, "Edit Mode에서 실행하세요.");
        var preview = EditorSceneManager.NewPreviewScene();
        var potion = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        var buff = ScriptableObject.CreateInstance<BuffDefinitionSO>();
        try
        {
            var owner = new GameObject("Stage2 rules owner");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner, preview);
            var health = owner.AddComponent<PlayerHealthManager>();
            typeof(PlayerHealthManager).GetField("<MaxHealth>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, 100f);
            health.SetCurrentHealth(10f);
            var wallet = owner.AddComponent<PlayerWallet>();
            potion.category = ItemCategory.Potion;
            potion.potionEffectType = PotionEffectType.Heal;
            potion.potionEffectValue = 10f;

            var state = new PotionUseState();
            var other = new PotionUseState();
            state.Recharge(3);
            other.Recharge(3);
            Require(state.TryUse(potion, health, null, 1f) && state.CurrentCharges == 2 && health.CurrentHealth == 20f,
                "회복·충전 차감");
            Require(!state.TryUse(potion, health, null, 1f) && state.CurrentCharges == 2 && health.CurrentHealth == 20f,
                "1초 내 중복 사용 거절");
            Require(other.CurrentCharges == 3 && other.IsCooldownReady, "플레이어별 충전·쿨타임 분리");
            state.Recharge(3);
            Require(!state.IsCooldownReady && !state.TryUse(potion, health, null, 1f), "충전으로 쿨타임 우회 불가");
            typeof(PotionUseState).GetField("nextUsableTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state, Time.time - 1f);
            Require(state.TryUse(potion, health, null, 1f), "쿨타임 만료 후 사용");
            state.ApplyCharges(-1, 3);
            Require(state.CurrentCharges == 0 && !state.TryUse(potion, health, null, 0f), "충전 하한·빈 충전 거절");
            state.ApplyCharges(20, 3);
            Require(state.CurrentCharges == 3, "서버 충전 상한");
            health.SetCurrentHealth(0f);
            Require(!other.TryUse(potion, health, null, 0f) && other.CurrentCharges == 3, "사망 중 사용 거절");
            health.SetCurrentHealth(10f);
            potion.potionEffectType = PotionEffectType.StatBoost;
            Require(!other.TryUse(potion, health, null, 0f) && other.CurrentCharges == 3, "없는 효과는 충전 소비 없음");
            var buffs = owner.AddComponent<PlayerBuffManager>();
            potion.potionBuff = buff;
            Require(other.TryUse(potion, health, buffs, 0f) && other.CurrentCharges == 2 &&
                buffs.ActiveBuffs.Count == 1 && buffs.ActiveBuffs[0].source == buff, "버프 포션은 소유자의 버프 목록에 적용");
            Require(ShopPricing.GetBuyPrice(101, 0.1f) == 91 && ShopPricing.GetBuyPrice(1, 0.95f) == 1 &&
                ShopPricing.GetBuyPrice(100, -1f) == 100 && ShopPricing.GetBuyPrice(100, 2f) == 6 &&
                ShopPricing.GetBuyPrice(-1, 0f) == 0 && ShopPricing.GetBuyPrice(100, float.NaN) == 100,
                "공통 가격·올림·할인 범위");

            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(
                "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/item.armor.helmet.alienskullcrown_우주 괴물 두개골.asset");
            Require(definition != null, "실제 강화 아이템 정의");
            var item = ItemDataCreator.CreateItemData(definition);
            var upgrade = new UpgradeService(wallet);
            Require(UpgradeService.TryGetUpgradeCost(item, out int cost), "공통 강화 비용");
            wallet.SetGold(cost - 1);
            Require(upgrade.TryUpgrade(item) == UpgradeResult.NotEnoughGold && item.upgradeLevel == 0 && wallet.Gold == cost - 1,
                "결제 실패 시 강화·골드 보존");
            wallet.SetGold(cost);
            Require(upgrade.TryUpgrade(item) == UpgradeResult.Success && item.upgradeLevel == 1 && wallet.Gold == 0,
                "공통 강화 실행");
            Debug.Log("[Stage2 Rules] PASS: 포션 회복/충전/쿨타임/사망/실패/소유 분리, 공통 할인 가격, 강화 결제 성공·실패. 네트워크 검증은 별도.");
        }
        finally
        {
            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(buff);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Stage2 Rules] " + message);
    }
}
