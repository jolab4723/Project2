using System;
using System.Collections.Generic;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEditor;
using UnityEngine;

/// <summary>유리빛 궤도 P3-A의 실제 Gunner 탄 출처와 장착 세대 수명을 SW 프리팹으로 검증한다.</summary>
public static class GlassRailExtraHitValidation_MirrorTest
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("SW/Mirror Test/Validate Glass Rail Extra Hit P3-A")]
    public static void Validate()
    {
        int checks = 0;
        void Check(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException("[GlassRailExtraHitValidation] FAIL: " + label);
            checks++;
        }

        bool wasServerActive = NetworkServer.active;
        typeof(NetworkServer).GetProperty("active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(null, true);

        var created = new List<GameObject>();
        try
        {
            ItemDefinitionSO glassRail = FindDefinition("item.weapon.rifle.glassrail");
            ItemDefinitionSO alternate = FindDefinition("item.weapon.rifle.massproduced");
            Check(glassRail != null && alternate != null, "유리빛 궤도·교체용 라이플 SO 존재");
            Check(glassRail.characterClass == CharacterClass.Gunner && glassRail.weaponType == WeaponType.Rifle &&
                  glassRail.weaponEnchantElement == ElementType.Ice, "Gunner Rifle Ice 분류");
            Check(glassRail.uniqueEffectId == "UE_GlassRailExtraHit" &&
                  glassRail.uniqueEffect is GlassRailExtraHitUniqueEffectSO, "P3-A 효과 연결");

            var effect = (GlassRailExtraHitUniqueEffectSO)glassRail.uniqueEffect;
            Check(Mathf.Approximately(effect.damageMultiplier, 0.15f) &&
                  effect.coefficients is { Length: 1 } && Mathf.Approximately(effect.coefficients[0], 15f),
                "공격력 15% 실행·표시 계수");

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorPlayerContext/Prefabs/GunnerNetworkPlayer_MirrorTest.prefab");
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorCombat/Prefabs/Normal_Melee_MirrorTest.prefab");
            GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorCombat/Prefabs/GunnerProjectile_MirrorTest.prefab");
            Check(playerPrefab != null && enemyPrefab != null && projectilePrefab != null,
                "SW Gunner·적·투사체 프리팹 존재");

            GameObject player = UnityEngine.Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            player.name = "GlassRailValidation_Player";
            created.Add(player);
            PlayerContext context = InitializePlayer(player, glassRail, "glassrail_source_a");
            PlayerCombatAuthority_MirrorTest authority = context.CombatAuthority;
            Check(authority.IsGunnerShotCurrent(glassRail.itemId, GunnerWeaponType.Rifle),
                "실제 장착 라이플 식별");

            SetPlayerStats(context, 100f, 100f, 50f);
            (GameObject firstTarget, WBH_EnemyStatus firstStatus, Collider firstCollider, Collider duplicateCollider) =
                CreateEnemy(enemyPrefab, "Snapshot", 1000f);
            created.Add(firstTarget);
            var firstResults = new List<WBH_DamageResult>();
            Action<WBH_DamageResult> captureFirst = result =>
            {
                firstResults.Add(result);
                if (result.DamageCause == DamageCause.Direct)
                    SetPlayerStats(context, 1000f);
            };
            firstStatus.OnDamaged += captureFirst;
            uint triggerBefore = context.ItemTriggers.GlassRailTriggerCount;
            uint resolvedBefore = context.ItemTriggers.GlassRailResolvedHitCount;
            NetworkEnemyProjectile_MirrorTest firstProjectile = CreateProjectile(
                projectilePrefab, context, glassRail, "glassrail_source_a", authority.WeaponEquipGeneration,
                effect, 9700u, created);
            ApplyProjectileDamage(firstProjectile, firstCollider);
            ApplyProjectileDamage(firstProjectile, duplicateCollider);
            firstStatus.OnDamaged -= captureFirst;

            Check(firstResults.Count == 2, "명시적 다중 Collider에서도 Direct·Effect 각 1회");
            Check(firstResults[0].DamageCause == DamageCause.Direct && firstResults[0].IsCritical &&
                  Mathf.Approximately(firstResults[0].FinalDamage, 150f), "치명 직접타 150 처리");
            Check(firstResults[1].DamageCause == DamageCause.Effect && !firstResults[1].IsCritical &&
                  firstResults[1].ElementType == ElementType.Ice && firstResults[1].AttackId == 9700u &&
                  Mathf.Approximately(firstResults[1].FinalDamage, 15f), "같은 공격 번호의 비치명 냉기 15 추가타");
            Check(Mathf.Approximately(firstStatus.CurrentHp, 835f) &&
                  Mathf.Approximately(context.Stats.Stat.attackPower, 1000f), "적중 시점 스냅샷과 피해 순서");
            Check(context.ItemTriggers.GlassRailTriggerCount == triggerBefore + 1 &&
                  context.ItemTriggers.GlassRailResolvedHitCount == resolvedBefore + 1,
                "유리빛 궤도 발동·해결 진단 각 1회");
            Check(!authority.TryGetGunnerHitSource(9700u, out _, out _, out _), "탄 피해 처리 뒤 출처 범위 정리");

            SetPlayerStats(context, 100f);
            DestroyTracked(created, firstTarget);

            (GameObject swappedTarget, WBH_EnemyStatus swappedStatus, Collider swappedCollider, _) =
                CreateEnemy(enemyPrefab, "WeaponSwap", 1000f);
            created.Add(swappedTarget);
            var swappedResults = new List<WBH_DamageResult>();
            swappedStatus.OnDamaged += swappedResults.Add;
            triggerBefore = context.ItemTriggers.GlassRailTriggerCount;
            NetworkEnemyProjectile_MirrorTest swappedProjectile = CreateProjectile(
                projectilePrefab, context, glassRail, "glassrail_source_a", authority.WeaponEquipGeneration,
                effect, 9701u, created);
            EquipWeapon(context, alternate, "alternate_rifle");
            ApplyProjectileDamage(swappedProjectile, swappedCollider);
            Check(swappedResults.Count == 1 && swappedResults[0].DamageCause == DamageCause.Direct,
                "발사 후 다른 무기 교체에도 기존 탄 직접 피해 유지");
            Check(context.ItemTriggers.GlassRailTriggerCount == triggerBefore,
                "해제된 유리빛 궤도의 옛 탄은 고유효과 미발동");
            DestroyTracked(created, swappedTarget);

            EquipWeapon(context, glassRail, "glassrail_source_a");
            uint sameIdSourceGeneration = authority.WeaponEquipGeneration;
            (GameObject sameIdTarget, WBH_EnemyStatus sameIdStatus, Collider sameIdCollider, _) =
                CreateEnemy(enemyPrefab, "SameDefinitionSwap", 1000f);
            created.Add(sameIdTarget);
            var sameIdResults = new List<WBH_DamageResult>();
            sameIdStatus.OnDamaged += sameIdResults.Add;
            triggerBefore = context.ItemTriggers.GlassRailTriggerCount;
            NetworkEnemyProjectile_MirrorTest sameIdProjectile = CreateProjectile(
                projectilePrefab, context, glassRail, "glassrail_source_a", sameIdSourceGeneration,
                effect, 9702u, created);
            EquipWeapon(context, glassRail, "glassrail_source_b");
            ApplyProjectileDamage(sameIdProjectile, sameIdCollider);
            Check(sameIdResults.Count == 1 && context.ItemTriggers.GlassRailTriggerCount == triggerBefore,
                "같은 itemId의 다른 인스턴스로 교체해도 옛 효과 미발동");
            DestroyTracked(created, sameIdTarget);

            EquipWeapon(context, glassRail, "glassrail_source_a");
            uint reequippedSourceGeneration = authority.WeaponEquipGeneration;
            (GameObject reequippedTarget, WBH_EnemyStatus reequippedStatus, Collider reequippedCollider, _) =
                CreateEnemy(enemyPrefab, "ReequipSameInstance", 1000f);
            created.Add(reequippedTarget);
            var reequippedResults = new List<WBH_DamageResult>();
            reequippedStatus.OnDamaged += reequippedResults.Add;
            triggerBefore = context.ItemTriggers.GlassRailTriggerCount;
            NetworkEnemyProjectile_MirrorTest reequippedProjectile = CreateProjectile(
                projectilePrefab, context, glassRail, "glassrail_source_a", reequippedSourceGeneration,
                effect, 9703u, created);
            UnequipWeapon(context);
            EquipWeapon(context, glassRail, "glassrail_source_a");
            ApplyProjectileDamage(reequippedProjectile, reequippedCollider);
            Check(reequippedResults.Count == 1 && context.ItemTriggers.GlassRailTriggerCount == triggerBefore,
                "같은 인스턴스 해제·재장착도 장착 세대로 옛 효과 차단");
            DestroyTracked(created, reequippedTarget);

            (GameObject orderTarget, WBH_EnemyStatus orderStatus, Collider orderCollider, _) =
                CreateEnemy(enemyPrefab, "ArrivalOrder", 1000f);
            created.Add(orderTarget);
            var orderResults = new List<WBH_DamageResult>();
            orderStatus.OnDamaged += orderResults.Add;
            uint currentGeneration = authority.WeaponEquipGeneration;
            triggerBefore = context.ItemTriggers.GlassRailTriggerCount;
            NetworkEnemyProjectile_MirrorTest earlierProjectile = CreateProjectile(
                projectilePrefab, context, glassRail, "glassrail_source_a", currentGeneration,
                effect, 9704u, created);
            NetworkEnemyProjectile_MirrorTest laterProjectile = CreateProjectile(
                projectilePrefab, context, glassRail, "glassrail_source_a", currentGeneration,
                effect, 9705u, created);
            ApplyProjectileDamage(laterProjectile, orderCollider);
            ApplyProjectileDamage(earlierProjectile, orderCollider);
            Check(orderResults.Count == 4 && orderResults[0].AttackId == 9705u &&
                  orderResults[1].AttackId == 9705u && orderResults[2].AttackId == 9704u &&
                  orderResults[3].AttackId == 9704u, "서로 다른 탄의 역순 도착도 각각 Direct·Effect 처리");
            Check(context.ItemTriggers.GlassRailTriggerCount == triggerBefore + 2 &&
                  Mathf.Approximately(orderStatus.CurrentHp, 770f), "연속 탄 누락 없이 공격별 15% 추가타");

            Debug.Log($"[GlassRailExtraHitValidation] PASS {checks} checks. " +
                      "실제 Gunner 탄 출처·명시적 다중 Collider·스냅샷·교체·동일 정의·재장착·역순 도착 확인.");
        }
        finally
        {
            foreach (GameObject gameObject in created)
            {
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);
            }

            typeof(NetworkServer).GetProperty("active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(null, wasServerActive);
        }
    }

    private static PlayerContext InitializePlayer(GameObject player, ItemDefinitionSO definition, string instanceId)
    {
        PlayerContext context = player.GetComponent<PlayerContext>();
        T_PlayerController controller = context.Controller;
        WBH_PlayerStatus status = player.GetComponent<WBH_PlayerStatus>();
        PlayerHealthManager health = context.Health;
        NetworkIdentity identity = player.GetComponent<NetworkIdentity>();

        foreach (NetworkBehaviour behaviour in player.GetComponentsInChildren<NetworkBehaviour>(true))
            SetNetworkServerState(identity, behaviour);
        context.Equipment.SetActiveCharacterClass(CharacterClass.Gunner);
        typeof(T_PlayerController).GetField("status", PrivateInstance)?.SetValue(controller, status);
        status.Initialize(controller);
        SetPlayerStats(context, 100f);
        typeof(PlayerHealthManager).GetField("statManager", PrivateInstance)?.SetValue(health, context.Stats);
        health.RefreshMaxHealth();
        health.FillHealth();
        EquipWeapon(context, definition, instanceId);
        return context;
    }

    private static void SetPlayerStats(PlayerContext context, float attackPower,
        float criticalRate = 0f, float criticalMultiplier = 0f)
    {
        context.Stats.EnsureInitialized().Recalculate(
            new StatSet
            {
                maxHealthFlat = 1000f,
                attackPowerFlat = attackPower,
                critRateFlat = criticalRate,
                critMultFlat = criticalMultiplier,
            },
            StatSet.Zero, StatSet.Zero, StatSet.Zero);
    }

    private static void EquipWeapon(PlayerContext context, ItemDefinitionSO definition, string instanceId)
    {
        GetEquippedItems(context)[EquipSlotType.Weapon] = new InventoryItem(new ItemInstance
        {
            instanceId = instanceId,
            definition = definition,
        });
        PublishEquipmentChanged(context.Equipment);
    }

    private static void UnequipWeapon(PlayerContext context)
    {
        GetEquippedItems(context).Remove(EquipSlotType.Weapon);
        PublishEquipmentChanged(context.Equipment);
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

    private static ItemDefinitionSO FindDefinition(string itemId)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinitionSO",
                     new[] { "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items" }))
        {
            ItemDefinitionSO definition = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null && definition.itemId == itemId)
                return definition;
        }
        return null;
    }

    private static (GameObject go, WBH_EnemyStatus status, Collider first, Collider duplicate) CreateEnemy(
        GameObject prefab, string suffix, float maxHealth)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab, Vector3.forward * 3f, Quaternion.identity);
        instance.name = "GlassRailValidation_" + suffix;
        WBH_EnemyController combat = instance.GetComponent<WBH_EnemyController>();
        WBH_EnemyStatus status = instance.GetComponent<WBH_EnemyStatus>();
        NetworkEnemyAuthority_MirrorTest authority = instance.GetComponent<NetworkEnemyAuthority_MirrorTest>();
        NetworkIdentity identity = instance.GetComponent<NetworkIdentity>();
        SetNetworkServerState(identity, authority);
        typeof(WBH_EnemyController).GetField("status", PrivateInstance)?.SetValue(combat, status);
        typeof(NetworkEnemyAuthority_MirrorTest).GetField("status", PrivateInstance)?.SetValue(authority, status);
        typeof(NetworkEnemyAuthority_MirrorTest).GetField("controller", PrivateInstance)?.SetValue(authority, combat);
        typeof(NetworkEnemyAuthority_MirrorTest).GetField("networkAnimator", PrivateInstance)?.SetValue(authority, null);

        WBH_EnemyInfo info = authority.EnemyInfo.Clone();
        info.maxHP = maxHealth;
        info.defense = 0f;
        info.moveSpeed = 0f;
        info.exp = 0;
        info.credit = 0;
        status.Initialize(info);
        typeof(NetworkEnemyAuthority_MirrorTest).GetField("enemyInfo", PrivateInstance)?.SetValue(authority, info);
        MethodInfo handleDamaged = typeof(NetworkEnemyAuthority_MirrorTest).GetMethod("HandleDamaged", PrivateInstance);
        status.OnDamaged += result =>
        {
            try
            {
                handleDamaged?.Invoke(authority, new object[] { result });
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        };

        Collider first = instance.GetComponentInChildren<Collider>();
        GameObject duplicateObject = new GameObject("GlassRailValidation_SecondCollider");
        duplicateObject.layer = 10;
        duplicateObject.transform.SetParent(instance.transform, false);
        duplicateObject.transform.localPosition = Vector3.up;
        Collider duplicate = duplicateObject.AddComponent<BoxCollider>();
        Physics.SyncTransforms();
        return (instance, status, first, duplicate);
    }

    private static NetworkEnemyProjectile_MirrorTest CreateProjectile(GameObject prefab, PlayerContext context,
        ItemDefinitionSO definition, string instanceId, uint equipGeneration,
        GlassRailExtraHitUniqueEffectSO effect, uint attackId, List<GameObject> created)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        instance.name = "GlassRailValidation_Projectile_" + attackId;
        created.Add(instance);
        NetworkEnemyProjectile_MirrorTest projectile = instance.GetComponent<NetworkEnemyProjectile_MirrorTest>();
        SetNetworkServerState(instance.GetComponent<NetworkIdentity>(), projectile);
        projectile.InitializePlayerServer(
            context,
            GunnerWeaponType.Rifle,
            definition.itemId,
            ElementType.Ice,
            Vector3.forward,
            10f,
            20f,
            Vector3.forward * 10f,
            1f,
            attackId,
            instanceId,
            equipGeneration,
            effect);
        return projectile;
    }

    private static void ApplyProjectileDamage(NetworkEnemyProjectile_MirrorTest projectile, Collider collider)
    {
        MethodInfo apply = typeof(NetworkEnemyProjectile_MirrorTest).GetMethod("ApplyPlayerDamage", PrivateInstance);
        try
        {
            apply?.Invoke(projectile, new object[] { collider });
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw exception.InnerException;
        }
    }

    private static void DestroyTracked(List<GameObject> created, GameObject target)
    {
        created.Remove(target);
        if (target != null)
            UnityEngine.Object.DestroyImmediate(target);
        Physics.SyncTransforms();
    }

    private static void SetNetworkServerState(NetworkIdentity identity, NetworkBehaviour behaviour)
    {
        typeof(NetworkIdentity).GetProperty("isServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(identity, true);
        typeof(NetworkBehaviour).GetProperty("netIdentity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(behaviour, identity);
    }
}
