using System;
using System.Collections.Generic;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEditor;
using UnityEngine;

/// <summary>아크 블레이드 P2-A와 P2-B 전제 계약을 실제 SW 프리팹으로 검증한다.</summary>
public static class ArcBladeChainLightningValidation_MirrorTest
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("SW/Mirror Test/Validate Arc Blade Chain Lightning P2")]
    public static void Validate()
    {
        int checks = 0;
        void Check(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException("[ArcBladeChainLightningValidation] FAIL: " + label);
            checks++;
        }

        bool wasServerActive = NetworkServer.active;
        typeof(NetworkServer).GetProperty("active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(null, true);

        var created = new List<GameObject>();
        var createdAssets = new List<UnityEngine.Object>();
        try
        {
            const string itemPath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/" +
                                    "item.weapon.greatsword.arcblade_아크 블레이드.asset";
            ItemDefinitionSO definition = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(itemPath);
            Check(definition != null && definition.itemId == "item.weapon.greatsword.arcblade", "아크 블레이드 SO 존재");
            Check(definition.uniqueEffectId == "UE_ArcBladeChainLightning", "아크 블레이드 효과 ID 연결");
            Check(definition.uniqueEffect is ChainLightningUniqueEffectSO, "연쇄 번개 SO 타입 연결");

            var effect = (ChainLightningUniqueEffectSO)definition.uniqueEffect;
            Check(effect.maxAdditionalTargets == 3 && Mathf.Approximately(effect.jumpRadius, 4f),
                "최대 3명·반경 4m 설정");
            Check(Mathf.Approximately(effect.firstDamageMultiplier, 0.25f) &&
                  Mathf.Approximately(effect.subsequentDamageMultiplier, 0.8f), "25%·후속 80% 피해 설정");
            Check(Mathf.Approximately(effect.cooldownSeconds, 0.8f), "0.8초 쿨다운 설정");
            Check(effect.coefficients is { Length: 5 } &&
                  Mathf.Approximately(effect.coefficients[0], effect.maxAdditionalTargets) &&
                  Mathf.Approximately(effect.coefficients[1], effect.jumpRadius) &&
                  Mathf.Approximately(effect.coefficients[2], effect.firstDamageMultiplier * 100f) &&
                  Mathf.Approximately(effect.coefficients[3], effect.subsequentDamageMultiplier * 100f) &&
                  Mathf.Approximately(effect.coefficients[4], effect.cooldownSeconds), "표시 계수와 실행 값 일치");
            Check(effect.effectDescription.Contains("이전 피해 계수의 {3}%"), "감쇠 설명이 이전 피해 계수임을 명시");
            Check(Type.GetType("ChainLightningExecutor_MirrorTest, Assembly-CSharp") != null &&
                  typeof(UniqueEffectPresentation_MirrorTest) != null, "실행기·로컬 표현 책임 분리");

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab");
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorCombat/Prefabs/Normal_Melee_MirrorTest.prefab");
            Check(playerPrefab != null && enemyPrefab != null, "SW Fighter·적 프리팹 존재");

            GameObject player = UnityEngine.Object.Instantiate(playerPrefab, new Vector3(-3f, 0f, 0f), Quaternion.identity);
            player.name = "ArcBladeValidation_Player";
            created.Add(player);
            PlayerContext context = InitializePlayer(player, definition);
            SetPlayerStats(context, 100f, 100f, 1.5f);

            (GameObject first, WBH_EnemyController firstCombat, WBH_EnemyStatus firstStatus) =
                CreateEnemy(enemyPrefab, "First", Vector3.zero);
            (GameObject chain1, _, WBH_EnemyStatus chain1Status) =
                CreateEnemy(enemyPrefab, "Chain1", new Vector3(2f, 0f, 0f));
            (GameObject chain2, _, WBH_EnemyStatus chain2Status) =
                CreateEnemy(enemyPrefab, "Chain2", new Vector3(4f, 0f, 0f));
            (GameObject chain3, _, WBH_EnemyStatus chain3Status) =
                CreateEnemy(enemyPrefab, "Chain3", new Vector3(6f, 0f, 0f));
            (GameObject chain4, _, WBH_EnemyStatus chain4Status) =
                CreateEnemy(enemyPrefab, "Chain4", new Vector3(8f, 0f, 0f));
            created.AddRange(new[] { first, chain1, chain2, chain3, chain4 });
            Physics.SyncTransforms();

            WBH_DamageResult firstChainResult = default;
            Action<WBH_DamageResult> captureFirstChainResult = result =>
            {
                if (result.DamageCause == DamageCause.Effect && result.AttackId == 9001u)
                    firstChainResult = result;
            };
            chain1Status.OnDamaged += captureFirstChainResult;

            Action<WBH_DamageResult> inflateAttackDuringResolution = result =>
            {
                if (result.DamageCause == DamageCause.Direct && result.AttackId == 9001u)
                    SetPlayerStats(context, 1000f);
            };
            firstStatus.OnDamaged += inflateAttackDuringResolution;

            bool resolved = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                context, firstCombat, ElementType.Electric, 1f, null, out WBH_DamageResult directResult,
                DamageCause.Direct, 9001u);
            firstStatus.OnDamaged -= inflateAttackDuringResolution;
            chain1Status.OnDamaged -= captureFirstChainResult;
            Check(resolved && directResult.DamageCause == DamageCause.Direct && directResult.IsCritical,
                "최초 직접 피해는 치명타 처리");
            Check(context.ItemTriggers.ChainLightningTriggerCount == 1,
                $"공격당 연쇄 1회 시작 (actual={context.ItemTriggers.ChainLightningTriggerCount}, " +
                $"resolved={context.ItemTriggers.ChainLightningResolvedHitCount})");
            Check(context.ItemTriggers.ChainLightningResolvedHitCount == 3, "추가 대상 최대 3명 피해");
            Check(Mathf.Approximately(chain1Status.CurrentHp, 975f), "첫 전이 공격력 25% 피해");
            Check(Mathf.Approximately(chain2Status.CurrentHp, 980f), "두 번째 전이 80% 감쇠 피해");
            Check(Mathf.Approximately(chain3Status.CurrentHp, 984f), "세 번째 전이 누적 감쇠 피해");
            Check(Mathf.Approximately(chain4Status.CurrentHp, 1000f), "연결 가능한 네 번째 적은 상한으로 제외");
            Check(firstChainResult.DamageCause == DamageCause.Effect && !firstChainResult.IsCritical &&
                  Mathf.Approximately(firstChainResult.FinalDamage, 25f),
                "치명타 직접타와 달리 연쇄 후속타는 비치명 25% 피해");
            Check(Mathf.Approximately(context.Stats.Stat.attackPower, 1000f) &&
                  Mathf.Approximately(chain1Status.CurrentHp, 975f), "후속 피해가 공격 시작 스냅샷 사용");

            SetPlayerStats(context, 100f);
            float chain1Health = chain1Status.CurrentHp;
            Check(WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                context, firstCombat, ElementType.Electric, 1f, null, out _, DamageCause.Direct, 9002u),
                "쿨다운 중 직접 피해 처리");
            Check(context.ItemTriggers.ChainLightningTriggerCount == 1 &&
                  context.ItemTriggers.ChainLightningResolvedHitCount == 3, "0.8초 쿨다운 재발동 차단");
            Check(Mathf.Approximately(chain1Status.CurrentHp, chain1Health), "쿨다운 중 추가 피해 없음");

            DestroyTracked(created, chain1, chain2, chain3, chain4);
            ClearChainRuntimeState(context.ItemTriggers);
            ResetEnemy(firstStatus, first.GetComponent<NetworkEnemyAuthority_MirrorTest>());

            (GameObject blocked, _, WBH_EnemyStatus blockedStatus) =
                CreateEnemy(enemyPrefab, "BlockedOnly", new Vector3(0f, 0f, 3f));
            created.Add(blocked);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "ArcBladeValidation_Wall";
            wall.layer = LayerMask.NameToLayer("Wall");
            wall.transform.SetPositionAndRotation(new Vector3(0f, 1f, 1.5f), Quaternion.identity);
            wall.transform.localScale = new Vector3(1.2f, 3f, 0.25f);
            created.Add(wall);
            Physics.SyncTransforms();

            uint wallTriggerBefore = context.ItemTriggers.ChainLightningTriggerCount;
            uint wallHitBefore = context.ItemTriggers.ChainLightningResolvedHitCount;
            Check(WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                context, firstCombat, ElementType.Electric, 1f, null, out _, DamageCause.Direct, 9100u),
                "벽 단독 후보 직접 피해 처리");
            Check(context.ItemTriggers.ChainLightningTriggerCount == wallTriggerBefore &&
                  context.ItemTriggers.ChainLightningResolvedHitCount == wallHitBefore &&
                  Mathf.Approximately(blockedStatus.CurrentHp, 1000f), "벽이 유일 후보를 실제 차단");

            DestroyTracked(created, wall);
            ClearChainRuntimeState(context.ItemTriggers);
            ResetEnemy(firstStatus, first.GetComponent<NetworkEnemyAuthority_MirrorTest>());
            Check(WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                context, firstCombat, ElementType.Electric, 1f, null, out _, DamageCause.Direct, 9101u),
                "벽 제거 뒤 직접 피해 처리");
            Check(context.ItemTriggers.ChainLightningTriggerCount == wallTriggerBefore + 1 &&
                  context.ItemTriggers.ChainLightningResolvedHitCount == wallHitBefore + 1 &&
                  Mathf.Approximately(blockedStatus.CurrentHp, 975f), "벽 제거 시 같은 후보 1회 피해");

            DestroyTracked(created, blocked);
            ClearChainRuntimeState(context.ItemTriggers);
            ResetEnemy(firstStatus, first.GetComponent<NetworkEnemyAuthority_MirrorTest>());
            uint singleTriggerBefore = context.ItemTriggers.ChainLightningTriggerCount;
            Check(WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                context, firstCombat, ElementType.Electric, 1f, null, out _, DamageCause.Direct, 9102u),
                "단일 적 직접 피해 처리");
            Check(context.ItemTriggers.ChainLightningTriggerCount == singleTriggerBefore,
                "추가 후보가 없으면 발동 횟수 미소비");
            Check(context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance equippedWeapon) &&
                  context.ItemTriggers.GetRemainingCooldown(equippedWeapon) <= 0f,
                "추가 후보가 없으면 쿨다운 미소비");

            UnequipWeapon(context);
            ClearChainRuntimeState(context.ItemTriggers);
            ResetEnemy(firstStatus, first.GetComponent<NetworkEnemyAuthority_MirrorTest>());
            bool firstFollowUpQueued = false;
            bool duplicateFollowUpQueued = true;
            Action<WBH_DamageResult> enqueueSameTarget = result =>
            {
                if (result.DamageCause != DamageCause.Direct || result.AttackId != 9200u)
                    return;

                firstFollowUpQueued = WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(
                    context, firstCombat, ElementType.Electric, 0.25f, null, DamageCause.Effect, result.AttackId,
                    canCrit: false);
                duplicateFollowUpQueued = WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(
                    context, firstCombat, ElementType.Electric, 0.25f, null, DamageCause.Effect, result.AttackId,
                    canCrit: false);
            };
            firstStatus.OnDamaged += enqueueSameTarget;
            Check(WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                context, firstCombat, ElementType.Electric, 1f, null, out _, DamageCause.Direct, 9200u),
                "직격과 같은 대상의 후속 효과 처리");
            firstStatus.OnDamaged -= enqueueSameTarget;
            Check(firstFollowUpQueued && !duplicateFollowUpQueued, "같은 효과 후속 피해는 대상당 1회 등록");
            Check(Mathf.Approximately(firstStatus.CurrentHp, 875f), "같은 AttackId의 Direct 100 + Effect 25 허용");
            EquipWeapon(context, definition);

            (GameObject reuseTarget, _, WBH_EnemyStatus reuseStatus) =
                CreateEnemy(enemyPrefab, "ReuseTarget", new Vector3(2f, 0f, 0f));
            created.Add(reuseTarget);
            ClearChainRuntimeState(context.ItemTriggers);
            ResetEnemy(firstStatus, first.GetComponent<NetworkEnemyAuthority_MirrorTest>());
            Check(WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                context, firstCombat, ElementType.Electric, 1f, null, out _, DamageCause.Direct, 1u),
                "공격 번호 1 최초 처리");
            uint reuseTriggerBefore = context.ItemTriggers.ChainLightningTriggerCount;
            ClearCooldownState(context.ItemTriggers);
            context.CombatAuthority.ServerCancelForDisconnect();
            ResetEnemy(firstStatus, first.GetComponent<NetworkEnemyAuthority_MirrorTest>());
            ResetEnemy(reuseStatus, reuseTarget.GetComponent<NetworkEnemyAuthority_MirrorTest>());
            Check(WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                context, firstCombat, ElementType.Electric, 1f, null, out _, DamageCause.Direct, 1u),
                "서버 수명 재시작 뒤 공격 번호 1 재사용");
            Check(context.ItemTriggers.ChainLightningTriggerCount == reuseTriggerBefore + 1 &&
                  Mathf.Approximately(reuseStatus.CurrentHp, 975f), "재접속 초기화가 연쇄 중복 기록도 정리");

            DestroyTracked(created, first, reuseTarget);
            ClearChainRuntimeState(context.ItemTriggers);
            ItemDefinitionSO zeroCooldownDefinition = UnityEngine.Object.Instantiate(definition);
            var zeroCooldownEffect = UnityEngine.Object.Instantiate(effect);
            zeroCooldownEffect.cooldownSeconds = 0f;
            zeroCooldownDefinition.uniqueEffect = zeroCooldownEffect;
            createdAssets.Add(zeroCooldownDefinition);
            createdAssets.Add(zeroCooldownEffect);
            EquipWeapon(context, zeroCooldownDefinition);
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            float fighterRange = player.GetComponent<WBH_PlayerStatus>().FighterAttackRange;
            float directZ = Mathf.Max(0.6f, fighterRange * 0.8f);
            (GameObject directA, _, WBH_EnemyStatus directAStatus) =
                CreateEnemy(enemyPrefab, "AuthorityDirectA", new Vector3(0f, 0f, directZ));
            (GameObject directB, _, WBH_EnemyStatus directBStatus) =
                CreateEnemy(enemyPrefab, "AuthorityDirectB", new Vector3(0.65f, 0f, directZ + 0.15f));
            (GameObject chainOnly, _, WBH_EnemyStatus chainOnlyStatus) =
                CreateEnemy(enemyPrefab, "AuthorityChainOnly", new Vector3(0f, 0f, directZ + 3.4f));
            created.AddRange(new[] { directA, directB, chainOnly });
            Physics.SyncTransforms();

            uint authorityTriggerBefore = context.ItemTriggers.ChainLightningTriggerCount;
            uint authorityHitBefore = context.ItemTriggers.ChainLightningResolvedHitCount;
            MethodInfo resolveServerAttack = typeof(PlayerCombatAuthority_MirrorTest)
                .GetMethod("ResolveServerAttack", PrivateInstance);
            resolveServerAttack?.Invoke(context.CombatAuthority, new object[] { 9300u });
            Check(context.CombatAuthority.LastResult == MirrorCombatRequestResult.Hit, "실제 Fighter 권한 공격 경로 적중");
            Check(Mathf.Approximately(directAStatus.CurrentHp, 900f) &&
                  Mathf.Approximately(directBStatus.CurrentHp, 900f), "다중 직접 대상은 각각 정확히 1회 직격");
            Check(context.ItemTriggers.ChainLightningTriggerCount == authorityTriggerBefore + 1 &&
                  context.ItemTriggers.ChainLightningResolvedHitCount == authorityHitBefore + 1,
                $"쿨다운 0에서도 다중 직접 적중 연쇄는 공격당 1회 (trigger " +
                $"{authorityTriggerBefore}->{context.ItemTriggers.ChainLightningTriggerCount}, hit " +
                $"{authorityHitBefore}->{context.ItemTriggers.ChainLightningResolvedHitCount})");
            Check(Mathf.Approximately(chainOnlyStatus.CurrentHp, 975f), "직접 대상 제외 후 추가 대상만 연쇄 피해");

            Debug.Log($"[ArcBladeChainLightningValidation] PASS {checks} checks. " +
                      "책임 분리·공격 스냅샷·Direct/Effect 경계·상한·벽·쿨다운·수명 초기화·실제 Fighter 다중 적중 확인.");

            (GameObject go, WBH_EnemyController combat, WBH_EnemyStatus status) CreateEnemy(
                GameObject prefab, string suffix, Vector3 position)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
                instance.name = "ArcBladeValidation_" + suffix;
                WBH_EnemyController combat = instance.GetComponent<WBH_EnemyController>();
                WBH_EnemyStatus status = instance.GetComponent<WBH_EnemyStatus>();
                NetworkEnemyAuthority_MirrorTest authority = instance.GetComponent<NetworkEnemyAuthority_MirrorTest>();
                NetworkIdentity identity = instance.GetComponent<NetworkIdentity>();
                SetNetworkServerState(identity, authority);
                typeof(WBH_EnemyController).GetField("status", PrivateInstance)?.SetValue(combat, status);
                typeof(NetworkEnemyAuthority_MirrorTest).GetField("status", PrivateInstance)?.SetValue(authority, status);
                typeof(NetworkEnemyAuthority_MirrorTest).GetField("controller", PrivateInstance)?.SetValue(authority, combat);
                typeof(NetworkEnemyAuthority_MirrorTest).GetField("networkAnimator", PrivateInstance)?.SetValue(authority, null);
                ResetEnemy(status, authority);
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
                return (instance, combat, status);
            }
        }
        finally
        {
            foreach (GameObject gameObject in created)
            {
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);
            }

            foreach (UnityEngine.Object asset in createdAssets)
            {
                if (asset != null)
                    UnityEngine.Object.DestroyImmediate(asset);
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

        foreach (NetworkBehaviour nb in player.GetComponentsInChildren<NetworkBehaviour>(true))
            SetNetworkServerState(identity, nb);
        typeof(T_PlayerController).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(controller, status);
        status.Initialize(controller);
        SetPlayerStats(context, 100f);
        typeof(PlayerHealthManager).GetField("statManager", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(health, context.Stats);
        health.RefreshMaxHealth();
        health.FillHealth();
        EquipWeapon(context, definition);
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

    private static void EquipWeapon(PlayerContext context, ItemDefinitionSO definition)
    {
        GetEquippedItems(context)[EquipSlotType.Weapon] = new InventoryItem(new ItemInstance
        {
            instanceId = "arc_blade_p2_validation",
            definition = definition,
        });
    }

    private static void UnequipWeapon(PlayerContext context)
    {
        GetEquippedItems(context).Remove(EquipSlotType.Weapon);
    }

    private static Dictionary<EquipSlotType, InventoryItem> GetEquippedItems(PlayerContext context)
    {
        return (Dictionary<EquipSlotType, InventoryItem>)typeof(EquipmentSystem)
            .GetField("equippedItems", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(context.Equipment);
    }

    private static void ResetEnemy(WBH_EnemyStatus status, NetworkEnemyAuthority_MirrorTest authority)
    {
        WBH_EnemyInfo info = authority.EnemyInfo.Clone();
        info.maxHP = 1000f;
        info.defense = 0f;
        info.moveSpeed = 0f;
        info.exp = 0;
        info.credit = 0;
        status.Initialize(info);
        typeof(NetworkEnemyAuthority_MirrorTest).GetField("enemyInfo", PrivateInstance)?.SetValue(authority, info);
    }

    private static void ClearChainRuntimeState(ItemTriggerManager_MirrorTest triggers)
    {
        ClearCooldownState(triggers);
        triggers.ResetAttackLifetime();
    }

    private static void ClearCooldownState(ItemTriggerManager_MirrorTest triggers)
    {
        object cooldowns = typeof(ItemTriggerManager_MirrorTest).GetField("cooldownEndTimes", PrivateInstance)
            ?.GetValue(triggers);
        cooldowns?.GetType().GetMethod("Clear")?.Invoke(cooldowns, null);
    }

    private static void DestroyTracked(List<GameObject> created, params GameObject[] targets)
    {
        foreach (GameObject target in targets)
        {
            created.Remove(target);
            if (target != null)
                UnityEngine.Object.DestroyImmediate(target);
        }
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
