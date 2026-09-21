using System;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEditor;
using UnityEngine;

public static class SolarGraceShieldValidation_MirrorTest
{
    [MenuItem("SW/Equipment/Validate Solar Grace Shield")]
    public static void Validate()
    {
        if (!EditorApplication.isPlaying || !NetworkServer.active)
            throw new InvalidOperationException("서버 Play Mode에서 실행해야 합니다.");

        foreach (PlayerArmorEffectProvider_MirrorTest provider in
                 UnityEngine.Object.FindObjectsByType<PlayerArmorEffectProvider_MirrorTest>(FindObjectsSortMode.None))
        {
            PlayerContext player = provider.GetComponent<PlayerContext>();
            if (!provider.isServer || player?.Health == null || player.Health.CurrentHealth <= 0f ||
                player.Equipment == null ||
                !player.Equipment.TryGetEquippedItemInstance(EquipSlotType.Chest, out ItemInstance chest) ||
                chest.definition.uniqueEffect is not SolarGraceShieldUniqueEffectSO)
                continue;

            PlayerHealthManager health = player.Health;
            health.FillHealth();
            FieldInfo nextCharge = typeof(PlayerArmorEffectProvider_MirrorTest).GetField(
                "nextShieldChargeAt", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo update = typeof(PlayerArmorEffectProvider_MirrorTest).GetMethod(
                "Update", BindingFlags.Instance | BindingFlags.NonPublic);
            nextCharge.SetValue(provider, 0f);
            update.Invoke(provider, null);

            float initialShield = provider.ShieldAmount;
            float initialHealth = health.CurrentHealth;
            if (initialShield <= 10f)
                throw new InvalidOperationException("보호막이 충전되지 않았습니다.");

            float firstHit = Mathf.Floor(initialShield / 2f);
            health.TakeDamage(firstHit);
            if (!Mathf.Approximately(health.CurrentHealth, initialHealth) ||
                !Mathf.Approximately(provider.ShieldAmount, initialShield - firstHit))
                throw new InvalidOperationException("보호막 전용 흡수가 잘못됐습니다.");

            float remainingShield = provider.ShieldAmount;
            float secondHit = Mathf.Ceil(remainingShield) + 10f;
            health.TakeDamage(secondHit);
            float expectedHealth = initialHealth - (secondHit - remainingShield);
            if (!Mathf.Approximately(provider.ShieldAmount, 0f) ||
                !Mathf.Approximately(health.CurrentHealth, expectedHealth))
                throw new InvalidOperationException("초과 피해의 HP 전달이 잘못됐습니다.");

            Debug.Log($"[SolarGraceShieldValidation] PASS: {player.name}, shield={initialShield:0.##}, " +
                      $"shield-only={firstHit:0.##}, overflow HP={initialHealth - expectedHealth:0.##}");
            return;
        }

        throw new InvalidOperationException("태양의 은혜를 장착한 살아 있는 서버 플레이어가 없습니다.");
    }
}
