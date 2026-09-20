using System;
using System.Collections.Generic;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEditor;
using UnityEngine;

/// <summary>인페르노 P2-B의 실제 Fighter 근접 추가타와 제외 경계를 SW 프리팹으로 검증한다.</summary>
public static class InfernoExtraHitValidation_MirrorTest
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("SW/Mirror Test/Validate Inferno Extra Hit P2-B")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 끝난 Edit Mode에서 실행하세요.");

        // 열린 맵의 적을 공격하지 않도록 시험 전용 빈 위치를 먼저 확인합니다.
        Vector3 testOrigin = new Vector3(40000f, 0f, 40000f);
        if (Physics.OverlapSphere(testOrigin, 100f).Length != 0)
            throw new InvalidOperationException("인페르노 시험 위치에 기존 Collider가 있습니다. 검사를 중단합니다.");

        int checks = 0;
        void Check(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException("[InfernoExtraHitValidation] FAIL: " + label);
            checks++;
        }

        bool wasServerActive = NetworkServer.active;
        typeof(NetworkServer).GetProperty("active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(null, true);

        var created = new List<GameObject>();
        try
        {
            const string itemPath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/" +
                                    "item.weapon.axe.inferno_인페르노.asset";
            ItemDefinitionSO definition = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(itemPath);
            Check(definition != null && definition.itemId == "item.weapon.axe.inferno", "인페르노 SO 존재");
            Check(definition.characterClass == CharacterClass.Fighter && definition.weaponType == WeaponType.Axe,
                "Fighter Axe 장착 분류");
            Check(definition.uniqueEffectId == "UE_InfernoExtraHit", "인페르노 효과 ID 연결");
            Check(definition.uniqueEffect is InfernoExtraHitUniqueEffectSO, "화염 추가타 SO 타입 연결");

            var effect = (InfernoExtraHitUniqueEffectSO)definition.uniqueEffect;
            Check(Mathf.Approximately(effect.damageMultiplier, 0.2f), "공격력 20% 실행 계수");
            Check(effect.coefficients is { Length: 1 } && Mathf.Approximately(effect.coefficients[0], 20f) &&
                  effect.effectDescription.Contains("공격력의 {0}%"), "한국어 설명과 표시 계수 일치");

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab");
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorCombat/Prefabs/Normal_Melee_MirrorTest.prefab");
            Check(playerPrefab != null && enemyPrefab != null, "SW Fighter·적 프리팹 존재");

            GameObject player = UnityEngine.Object.Instantiate(playerPrefab, testOrigin, Quaternion.identity);
            player.name = "InfernoValidation_Player";
            created.Add(player);
            PlayerContext context = InitializePlayer(player, definition);
            SetPlayerStats(context, 100f, 100f, 50f);
            float fighterRange = player.GetComponent<WBH_PlayerStatus>().FighterAttackRange;
            Vector3 targetPosition = testOrigin + Vector3.forward * Mathf.Max(0.6f, fighterRange * 0.8f);

            (GameObject snapshotTarget, WBH_EnemyStatus snapshotStatus, NetworkEnemyAuthority_MirrorTest snapshotAuthority) =
                CreateEnemy(enemyPrefab, "Snapshot", targetPosition, 1000f);
            created.Add(snapshotTarget);
            snapshotTarget.AddComponent<BoxCollider>().size = Vector3.one * 0.25f;
            Physics.SyncTransforms();
            Check(snapshotTarget.GetComponentsInChildren<Collider>().Length >= 2,
                "다중 Collider 시험 전제 확인");
            var snapshotResults = new List<WBH_DamageResult>();
            Action<WBH_DamageResult> captureSnapshot = result =>
            {
                snapshotResults.Add(result);
                if (result.DamageCause == DamageCause.Direct)
                    SetPlayerStats(context, 1000f);
            };
            snapshotStatus.OnDamaged += captureSnapshot;
            uint triggerBefore = context.ItemTriggers.InfernoTriggerCount;
            uint resolvedBefore = context.ItemTriggers.InfernoResolvedHitCount;
            InvokeFighterAttack(context.CombatAuthority, 9400u);
            snapshotStatus.OnDamaged -= captureSnapshot;

            Check(context.CombatAuthority.LastResult == MirrorCombatRequestResult.Hit, "실제 Fighter 권한 공격 적중");
            Check(snapshotResults.Count == 2, "다중 Collider가 있어도 Direct와 Effect 각 1회");
            Check(snapshotResults[0].DamageCause == DamageCause.Direct && snapshotResults[0].IsCritical &&
                  Mathf.Approximately(snapshotResults[0].FinalDamage, 150f), "치명 직접타 150 처리");
            Check(snapshotResults[1].DamageCause == DamageCause.Effect && !snapshotResults[1].IsCritical &&
                  snapshotResults[1].ElementType == ElementType.Fire && snapshotResults[1].AttackId == 9400u &&
                  Mathf.Approximately(snapshotResults[1].FinalDamage, 20f), "같은 공격 번호의 비치명 화염 20 추가타");
            Check(Mathf.Approximately(snapshotStatus.CurrentHp, 830f), "직접 150 뒤 화염 20 순서와 합계");
            Check(Mathf.Approximately(context.Stats.Stat.attackPower, 1000f) &&
                  Mathf.Approximately(snapshotResults[1].FinalDamage, 20f), "추가타가 직접 피해 진입 스냅샷 사용");
            Check(context.ItemTriggers.InfernoTriggerCount == triggerBefore + 1 &&
                  context.ItemTriggers.InfernoResolvedHitCount == resolvedBefore + 1,
                "인페르노 발동·해결 진단 각 1회");
            Check(snapshotAuthority.KillRewardCount == 0, "비처치 추가타는 보상 없음");

            DestroyTracked(created, snapshotTarget);
            SetPlayerStats(context, 100f);

            (GameObject directKillTarget, WBH_EnemyStatus directKillStatus,
                NetworkEnemyAuthority_MirrorTest directKillAuthority) =
                CreateEnemy(enemyPrefab, "DirectKill", targetPosition, 50f);
            created.Add(directKillTarget);
            var directKillResults = new List<WBH_DamageResult>();
            directKillStatus.OnDamaged += directKillResults.Add;
            triggerBefore = context.ItemTriggers.InfernoTriggerCount;
            resolvedBefore = context.ItemTriggers.InfernoResolvedHitCount;
            InvokeFighterAttack(context.CombatAuthority, 9401u);
            Check(directKillStatus.IsDead && directKillResults.Count == 1 &&
                  directKillResults[0].DamageCause == DamageCause.Direct, "직접타 처치 시 추가타 미실행");
            Check(context.ItemTriggers.InfernoTriggerCount == triggerBefore &&
                  context.ItemTriggers.InfernoResolvedHitCount == resolvedBefore, "직접타 처치는 발동 진단 미소비");
            Check(directKillAuthority.KillRewardCount == 1, "직접타 처치 보상 1회");

            DestroyTracked(created, directKillTarget);

            (GameObject effectKillTarget, WBH_EnemyStatus effectKillStatus,
                NetworkEnemyAuthority_MirrorTest effectKillAuthority) =
                CreateEnemy(enemyPrefab, "EffectKill", targetPosition, 110f);
            created.Add(effectKillTarget);
            var effectKillResults = new List<WBH_DamageResult>();
            effectKillStatus.OnDamaged += effectKillResults.Add;
            triggerBefore = context.ItemTriggers.InfernoTriggerCount;
            resolvedBefore = context.ItemTriggers.InfernoResolvedHitCount;
            InvokeFighterAttack(context.CombatAuthority, 9402u);
            Check(effectKillStatus.IsDead && effectKillResults.Count == 2 &&
                  effectKillResults[1].DamageCause == DamageCause.Effect, "화염 추가타 처치 경로");
            Check(context.ItemTriggers.InfernoTriggerCount == triggerBefore + 1 &&
                  context.ItemTriggers.InfernoResolvedHitCount == resolvedBefore + 1, "처치 추가타도 정확히 1회");
            Check(effectKillAuthority.KillRewardCount == 1, "화염 추가타 처치 보상 1회");

            DestroyTracked(created, effectKillTarget);

            foreach (DamageCause excludedCause in new[] { DamageCause.Skill, DamageCause.Effect, DamageCause.DoT })
            {
                (GameObject excludedTarget, WBH_EnemyStatus excludedStatus, _) =
                    CreateEnemy(enemyPrefab, "Excluded_" + excludedCause, targetPosition, 1000f);
                created.Add(excludedTarget);
                var excludedResults = new List<WBH_DamageResult>();
                excludedStatus.OnDamaged += excludedResults.Add;
                triggerBefore = context.ItemTriggers.InfernoTriggerCount;
                Check(WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                        context, excludedTarget.GetComponent<WBH_EnemyController>(), ElementType.Fire, 1f, null,
                        out _, excludedCause, (uint)(9500 + (int)excludedCause)), excludedCause + " 피해 처리");
                Check(excludedResults.Count == 1 && context.ItemTriggers.InfernoTriggerCount == triggerBefore,
                    excludedCause + "에서는 화염 추가타 미발동");
                DestroyTracked(created, excludedTarget);
            }

            (GameObject unclassifiedTarget, WBH_EnemyStatus unclassifiedStatus, _) =
                CreateEnemy(enemyPrefab, "UnclassifiedDirect", targetPosition, 1000f);
            created.Add(unclassifiedTarget);
            var unclassifiedResults = new List<WBH_DamageResult>();
            unclassifiedStatus.OnDamaged += unclassifiedResults.Add;
            triggerBefore = context.ItemTriggers.InfernoTriggerCount;
            Check(WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    context, unclassifiedTarget.GetComponent<WBH_EnemyController>(), ElementType.Fire, 1f, null,
                    out _, DamageCause.Direct, 9600u), "권한 외 Direct 피해 처리");
            Check(unclassifiedResults.Count == 1 && context.ItemTriggers.InfernoTriggerCount == triggerBefore,
                "Fighter 권한 근접 대상으로 등록되지 않은 Direct는 미발동");

            Debug.Log($"[InfernoExtraHitValidation] PASS {checks} checks. " +
                      "실제 Fighter 근접·동일 대상 20%·비치명·스냅샷·직접/효과 처치·제외 원인 확인.");
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

    private static PlayerContext InitializePlayer(GameObject player, ItemDefinitionSO definition)
    {
        PlayerContext context = player.GetComponent<PlayerContext>();
        T_PlayerController controller = context.Controller;
        WBH_PlayerStatus status = player.GetComponent<WBH_PlayerStatus>();
        PlayerHealthManager health = context.Health;
        NetworkIdentity identity = player.GetComponent<NetworkIdentity>();

        foreach (NetworkBehaviour behaviour in player.GetComponentsInChildren<NetworkBehaviour>(true))
            SetNetworkServerState(identity, behaviour);
        typeof(T_PlayerController).GetField("status", PrivateInstance)?.SetValue(controller, status);
        status.Initialize(controller);
        SetPlayerStats(context, 100f);
        typeof(PlayerHealthManager).GetField("statManager", PrivateInstance)?.SetValue(health, context.Stats);
        health.RefreshMaxHealth();
        health.FillHealth();
        GetEquippedItems(context)[EquipSlotType.Weapon] = new InventoryItem(new ItemInstance
        {
            instanceId = "inferno_p2b_validation",
            definition = definition,
        });
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

    private static Dictionary<EquipSlotType, InventoryItem> GetEquippedItems(PlayerContext context)
    {
        return (Dictionary<EquipSlotType, InventoryItem>)typeof(EquipmentSystem)
            .GetField("equippedItems", PrivateInstance)?.GetValue(context.Equipment);
    }

    private static (GameObject go, WBH_EnemyStatus status, NetworkEnemyAuthority_MirrorTest authority) CreateEnemy(
        GameObject prefab, string suffix, Vector3 position, float maxHealth)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        instance.name = "InfernoValidation_" + suffix;
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
        MethodInfo handleDead = typeof(NetworkEnemyAuthority_MirrorTest).GetMethod("HandleDead", PrivateInstance);
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
        status.OnDead += () =>
        {
            try
            {
                handleDead?.Invoke(authority, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        };
        Physics.SyncTransforms();
        return (instance, status, authority);
    }

    private static void InvokeFighterAttack(PlayerCombatAuthority_MirrorTest authority, uint attackId)
    {
        MethodInfo resolve = typeof(PlayerCombatAuthority_MirrorTest).GetMethod("ResolveServerAttack", PrivateInstance);
        try
        {
            resolve?.Invoke(authority, new object[] { attackId });
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
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
