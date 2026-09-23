using System;
using System.Collections.Generic;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 밤의 칼날(Night Sword) P5-B 고유효과(회피 준비 및 다음 기본 공격 40% 증폭)의
/// 데이터 무결성, 서버 판정, 소비 규칙, 다중 타격, 예외 처리, 클라이언트 표시를 전수 검증합니다.
/// </summary>
public static class NightSwordPreparedAttackValidation_MirrorTest
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("SW/Mirror Test/Validate Night Sword Prepared Attack P5-B")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 완료된 Edit Mode에서 실행하세요.");

        Vector3 testOrigin = new Vector3(45000f, 0f, 45000f);
        if (Physics.OverlapSphere(testOrigin, 100f).Length != 0)
            throw new InvalidOperationException("시험 위치에 기존 Collider가 있습니다. 검사를 중단합니다.");

        int checks = 0;
        void Check(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException("[NightSwordPreparedAttackValidation] FAIL: " + label);
            checks++;
        }

        bool wasServerActive = NetworkServer.active;
        typeof(NetworkServer).GetProperty("active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(null, true);

        var created = new List<GameObject>();
        try
        {
            // 1. 데이터 무결성 검증
            const string itemPath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/" +
                                    "item.weapon.greatsword.nightsword_밤의 칼날.asset";
            ItemDefinitionSO nightSwordDef = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(itemPath);
            Check(nightSwordDef != null && nightSwordDef.itemId == "item.weapon.greatsword.nightsword",
                "밤의 칼날 ItemDefinitionSO 존재 및 itemId 확인");
            Check(nightSwordDef.characterClass == CharacterClass.Fighter && nightSwordDef.weaponType == WeaponType.Greatsword,
                "밤의 칼날 Fighter / Greatsword 분류 확인");
            Check(nightSwordDef.uniqueEffectId == "UE_NightSwordDodgeStrike",
                "고유효과 ID UE_NightSwordDodgeStrike 연결 확인");
            Check(nightSwordDef.uniqueEffect is DodgePreparedAttackUniqueEffectSO,
                "고유효과 SO 타입 DodgePreparedAttackUniqueEffectSO 확인");

            var effect = (DodgePreparedAttackUniqueEffectSO)nightSwordDef.uniqueEffect;
            Check(Mathf.Approximately(effect.damageMultiplier, 1.4f),
                "기본 공격 40% 증폭 계수(damageMultiplier = 1.4f) 확인");

            const string poolPath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/UniqueEffectPool/UE_NightSwordDodgeStrike.asset";
            DodgePreparedAttackUniqueEffectSO poolEffect = AssetDatabase.LoadAssetAtPath<DodgePreparedAttackUniqueEffectSO>(poolPath);
            Check(poolEffect != null && Mathf.Approximately(poolEffect.damageMultiplier, 1.4f),
                "고유효과 풀 에셋 존재 및 배율 1.4f 확인");

            // 2. 프리팹 로드 및 플레이어 초기화
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab");
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorCombat/Prefabs/Normal_Melee_MirrorTest.prefab");
            Check(playerPrefab != null && enemyPrefab != null, "SW Fighter 및 적 프리팹 로드 성공");

            GameObject player = UnityEngine.Object.Instantiate(playerPrefab, testOrigin, Quaternion.identity);
            player.name = "NightSwordValidation_Player";
            created.Add(player);

            PlayerContext context = InitializePlayer(player, nightSwordDef);

            float fighterRange = player.GetComponent<WBH_PlayerStatus>().FighterAttackRange;
            Vector3 enemyPos1 = testOrigin + Vector3.forward * Mathf.Max(0.6f, fighterRange * 0.8f);

            (GameObject enemy1, WBH_EnemyStatus enemyStatus1, NetworkEnemyAuthority_MirrorTest enemyAuth1) =
                CreateEnemy(enemyPrefab, "Target1", enemyPos1, 1000f);
            created.Add(enemy1);
            Physics.SyncTransforms();

            ItemTriggerManager_MirrorTest triggers = context.ItemTriggers;
            Check(triggers != null, "ItemTriggerManager_MirrorTest 컴포넌트 확인");
            Check(!triggers.PreparedAttackReady, "초기 상태에서 PreparedAttackReady == false");
            Check(triggers.PreparedAttackConsumeCount == 0, "초기 상태에서 PreparedAttackConsumeCount == 0");

            // 3. 회피 전 일반 기본 공격 (베이스라인: 1.0배 피해 100)
            uint currentAttackId = 1001u;
            WBH_DamageResult baseResult = default;
            Action<WBH_DamageResult> captureBase = r => { if (r.AttackId == currentAttackId) baseResult = r; };
            enemyStatus1.OnDamaged += captureBase;
            InvokeFighterAttack(context.CombatAuthority, currentAttackId);
            enemyStatus1.OnDamaged -= captureBase;

            Check(context.CombatAuthority.LastResult == MirrorCombatRequestResult.Hit, "회피 전 일반 공격 적중 확인");
            Check(baseResult.DamageCause == DamageCause.Direct && Mathf.Approximately(baseResult.FinalDamage, 100f),
                $"회피 전 기본 공격 피해 100 확인 (actual={baseResult.FinalDamage})");
            Check(!triggers.PreparedAttackReady, "일반 공격 후 PreparedAttackReady 유지 false");
            Check(triggers.PreparedAttackConsumeCount == 0, "일반 공격 후 소비 카운트 0 유지");

            // 4. 회피 진입 -> 준비 상태 활성화 및 중복 회피 시 미중첩 검증
            TriggerDodge(context);
            Check(triggers.PreparedAttackReady, "회피 후 PreparedAttackReady == true 활성화");

            // 연속 회피 진입 시도 (미중첩 및 상태 유지)
            TriggerDodge(context);
            Check(triggers.PreparedAttackReady, "연속 회피 후에도 PreparedAttackReady == true 유지");
            Check(triggers.PreparedAttackConsumeCount == 0, "회피 시점 소비 카운트 0 유지");

            // 5. 빗나감(NoTarget) 시 준비 상태 보존 검증
            currentAttackId = 1002u;
            // 적을 공격 범위 밖으로 임시 이동
            enemy1.transform.position = testOrigin + Vector3.forward * 50f;
            Physics.SyncTransforms();

            InvokeFighterAttack(context.CombatAuthority, currentAttackId);
            Check(context.CombatAuthority.LastResult == MirrorCombatRequestResult.NoTarget, "공격 범위 밖 빗나감(NoTarget) 확인");
            Check(triggers.PreparedAttackReady, "공격이 빗나갔을 때 준비 상태(PreparedAttackReady) 보존 확인");
            Check(triggers.PreparedAttackConsumeCount == 0, "빗나감 시 소비 카운트 0 유지");

            // 적 원래 위치 복귀
            enemy1.transform.position = enemyPos1;
            Physics.SyncTransforms();

            // 6. 스킬/Non-Direct 공격의 준비 상태 미소비 검증
            currentAttackId = 1003u;
            float nonDirectMultiplier = triggers.ConsumePreparedAttackMultiplier(DamageCause.Skill, currentAttackId);
            Check(Mathf.Approximately(nonDirectMultiplier, 1.0f), "Skill 피해는 준비 증폭 미적용(배율 1.0f)");
            Check(triggers.PreparedAttackReady, "Skill 피해 후 준비 상태 보존 확인");
            Check(triggers.PreparedAttackConsumeCount == 0, "Skill 피해 후 소비 카운트 0 유지");

            float effectMultiplier = triggers.ConsumePreparedAttackMultiplier(DamageCause.Effect, currentAttackId);
            Check(Mathf.Approximately(effectMultiplier, 1.0f), "Effect 피해는 준비 증폭 미적용(배율 1.0f)");
            Check(triggers.PreparedAttackReady, "Effect 피해 후 준비 상태 보존 확인");
            Check(triggers.PreparedAttackConsumeCount == 0, "Effect 피해 후 소비 카운트 0 유지");

            // 7. 유효 기본 공격(Direct) 적중 시 40% 증폭 및 1회 소비 검증
            currentAttackId = 1004u;
            WBH_DamageResult empoweredResult = default;
            Action<WBH_DamageResult> captureEmpowered = r => { if (r.AttackId == currentAttackId) empoweredResult = r; };
            enemyStatus1.OnDamaged += captureEmpowered;
            float hpBeforeEmpowered = enemyStatus1.CurrentHp;
            InvokeFighterAttack(context.CombatAuthority, currentAttackId);
            enemyStatus1.OnDamaged -= captureEmpowered;

            Check(context.CombatAuthority.LastResult == MirrorCombatRequestResult.Hit, "강화 공격 적중 확인");
            Check(empoweredResult.DamageCause == DamageCause.Direct && Mathf.Approximately(empoweredResult.FinalDamage, 140f),
                $"준비된 기본 공격 40% 증폭(140 피해) 확인 (actual={empoweredResult.FinalDamage})");
            Check(Mathf.Approximately(enemyStatus1.CurrentHp, hpBeforeEmpowered - 140f), "적 체력 140 정확히 감소");
            Check(!triggers.PreparedAttackReady, "공격 적중 후 PreparedAttackReady == false 정상 소비 확인");
            Check(triggers.PreparedAttackConsumeCount == 1, "소비 카운트 1 증가 확인 (count=1)");

            // 8. 소비 후 다음 공격(AttackId: 1005) 시 일반 1.0배 복귀 검증
            currentAttackId = 1005u;
            WBH_DamageResult nextResult = default;
            Action<WBH_DamageResult> captureNext = r => { if (r.AttackId == currentAttackId) nextResult = r; };
            enemyStatus1.OnDamaged += captureNext;
            InvokeFighterAttack(context.CombatAuthority, currentAttackId);
            enemyStatus1.OnDamaged -= captureNext;

            Check(nextResult.DamageCause == DamageCause.Direct && Mathf.Approximately(nextResult.FinalDamage, 100f),
                $"소비 후 다음 기본 공격 일반 피해 100 복귀 확인 (actual={nextResult.FinalDamage})");
            Check(triggers.PreparedAttackConsumeCount == 1, "다음 공격 후에도 소비 카운트 1 유지");

            // 9. 동일 AttackId 다중 피격(여러 적) 검증
            Vector3 enemyPos2 = testOrigin + Vector3.forward * Mathf.Max(0.6f, fighterRange * 0.8f) + Vector3.right * 0.5f;
            (GameObject enemy2, WBH_EnemyStatus enemyStatus2, NetworkEnemyAuthority_MirrorTest enemyAuth2) =
                CreateEnemy(enemyPrefab, "Target2", enemyPos2, 1000f);
            created.Add(enemy2);
            Physics.SyncTransforms();

            TriggerDodge(context);
            Check(triggers.PreparedAttackReady, "다중 타격 시험용 회피 후 준비 상태 확인");

            currentAttackId = 2001u;
            // 동일 attackId로 두 적에게 연속 TryProcessPlayerDamage 전달 시뮬레이션
            float multiMult1 = triggers.ConsumePreparedAttackMultiplier(DamageCause.Direct, currentAttackId);
            Check(Mathf.Approximately(multiMult1, 1.4f), "첫 번째 적 타격 시 1.4배율 반환");
            Check(!triggers.PreparedAttackReady, "첫 번째 적 타격 즉시 준비 상태 소비됨");
            Check(triggers.PreparedAttackConsumeCount == 2, "첫 번째 적 타격으로 소비 카운트 2로 증가");

            float multiMult2 = triggers.ConsumePreparedAttackMultiplier(DamageCause.Direct, currentAttackId);
            Check(Mathf.Approximately(multiMult2, 1.4f), "동일 attackId 두 번째 적 타격에도 1.4배율 적용");
            Check(triggers.PreparedAttackConsumeCount == 2, "동일 attackId 두 번째 적 타격 시 소비 카운트 중복 증가 방지(2 유지)");

            // 다음 attackId는 미준비 상태이므로 1.0배
            float multiMultNext = triggers.ConsumePreparedAttackMultiplier(DamageCause.Direct, 2002u);
            Check(Mathf.Approximately(multiMultNext, 1.0f), "다음 attackId는 1.0배율 정상 복귀");

            // 10. 무기 해제(Unequip) 시 준비 상태 소멸 검증
            TriggerDodge(context);
            Check(triggers.PreparedAttackReady, "무기 해제 시험용 회피 후 준비 상태 확인");
            UnequipWeapon(context);
            Check(!triggers.PreparedAttackReady, "무기 해제 시 준비 상태(PreparedAttackReady) 즉시 클리어 확인");
            float unequipMult = triggers.ConsumePreparedAttackMultiplier(DamageCause.Direct, 3001u);
            Check(Mathf.Approximately(unequipMult, 1.0f), "무기 해제 후 배율 1.0f 확인");

            // 밤의 칼날 재장착
            EquipWeapon(context, nightSwordDef);

            // 11. 무기 교체(타 무기로 변경) 시 준비 상태 소멸 검증
            TriggerDodge(context);
            Check(triggers.PreparedAttackReady, "무기 교체 시험용 회피 후 준비 상태 확인");

            const string arcBladePath = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/" +
                                        "item.weapon.greatsword.arcblade_아크 블레이드.asset";
            ItemDefinitionSO arcBladeDef = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(arcBladePath);
            Check(arcBladeDef != null, "아크 블레이드 SO 로드 확인");

            EquipWeapon(context, arcBladeDef);
            Check(!triggers.PreparedAttackReady, "타 무기(아크 블레이드)로 교체 시 준비 상태 즉시 클리어 확인");
            float swapMult = triggers.ConsumePreparedAttackMultiplier(DamageCause.Direct, 3002u);
            Check(Mathf.Approximately(swapMult, 1.0f), "무기 교체 후 배율 1.0f 확인");

            // 12. 밤의 칼날이 아닌 타 무기에서 회피 발동 시 준비 상태 미발동 검증
            TriggerDodge(context);
            Check(!triggers.PreparedAttackReady, "밤의 칼날이 아닌 무기 장착 시 회피해도 준비 상태 미발동 확인");

            // 밤의 칼날 다시 장착
            EquipWeapon(context, nightSwordDef);

            // 13. 플레이어 사망 시 준비 상태 소멸 검증
            TriggerDodge(context);
            Check(triggers.PreparedAttackReady, "사망 시험용 회피 후 준비 상태 확인");

            MethodInfo handleDeath = typeof(ItemTriggerManager_MirrorTest).GetMethod("HandleDeath", PrivateInstance);
            handleDeath?.Invoke(triggers, null);
            Check(!triggers.PreparedAttackReady, "사망(HandleDeath) 시 준비 상태 즉시 클리어 확인");

            TriggerDodge(context);
            Check(triggers.PreparedAttackReady, "사망 상태 진입 시험용 회피 후 준비 상태 확인");
            MethodInfo handleStateEntered = typeof(ItemTriggerManager_MirrorTest).GetMethod("HandleStateEntered", PrivateInstance);
            handleStateEntered?.Invoke(triggers, new object[] { PlayerState.Dead });
            Check(!triggers.PreparedAttackReady, "PlayerState.Dead 진입 시 준비 상태 즉시 클리어 확인");

            // 14. 클라이언트 프리젠테이션(UniqueEffectPresentation_MirrorTest) 시각 효과 검증
            UniqueEffectPresentation_MirrorTest presentation = player.GetComponent<UniqueEffectPresentation_MirrorTest>();
            if (presentation == null)
                presentation = player.AddComponent<UniqueEffectPresentation_MirrorTest>();

            presentation.SetPreparedAttack(true);
            Transform ringTransform = player.transform.Find("NightSwordPreparedAttack");
            Check(ringTransform != null && ringTransform.gameObject.activeSelf, "준비 시각 링(NightSwordPreparedAttack) 생성 및 활성화 확인");

            LineRenderer line = ringTransform.GetComponent<LineRenderer>();
            Check(line != null && line.positionCount == 32 && line.loop, "LineRenderer 32포인트 원형 링 설정 확인");
            Check(Mathf.Abs(line.startColor.r - 0.48f) < 0.05f && Mathf.Abs(line.startColor.b - 0.85f) < 0.05f,
                "보라색 계열 고유 효과 링 색상 확인");

            presentation.SetPreparedAttack(false);
            Check(ringTransform != null && !ringTransform.gameObject.activeSelf, "준비 해제 시 시각 링 비활성화 확인");

            // 리소스 릴리즈 검증
            MethodInfo releaseResources = typeof(UniqueEffectPresentation_MirrorTest).GetMethod("ReleaseOwnedResources", PrivateInstance);
            releaseResources?.Invoke(presentation, null);
            Check(player.transform.Find("NightSwordPreparedAttack") == null, "ReleaseOwnedResources 호출 시 링 파괴 정리 확인");

            Debug.Log($"[NightSwordPreparedAttackValidation] PASS: 총 {checks}개 검사 전원 통과!");
        }
        finally
        {
            typeof(NetworkServer).GetProperty("active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(null, wasServerActive);

            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                    UnityEngine.Object.DestroyImmediate(created[i]);
            }
            Physics.SyncTransforms();
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
        typeof(T_PlayerController).GetField("canControl", PrivateInstance)?.SetValue(controller, true);
        status.Initialize(controller);
        SetPlayerStats(context, 100f);
        typeof(PlayerHealthManager).GetField("statManager", PrivateInstance)?.SetValue(health, context.Stats);
        health.RefreshMaxHealth();
        health.FillHealth();

        EquipWeapon(context, definition);
        context.ItemTriggers.ResetAttackLifetime();

        return context;
    }

    private static void EquipWeapon(PlayerContext context, ItemDefinitionSO definition)
    {
        var equippedItems = GetEquippedItems(context);
        if (definition != null)
        {
            equippedItems[EquipSlotType.Weapon] = new InventoryItem(new ItemInstance
            {
                instanceId = "nightsword_p5b_val_" + definition.itemId,
                definition = definition,
            });
        }
        else
        {
            equippedItems.Remove(EquipSlotType.Weapon);
        }

        // EquipmentChanged 이벤트 발행
        MethodInfo onEquipChanged = typeof(EquipmentSystem).GetMethod("NotifyEquipmentChanged", PrivateInstance);
        if (onEquipChanged != null)
        {
            onEquipChanged.Invoke(context.Equipment, null);
        }
        else
        {
            MethodInfo handleEquipChanged = typeof(ItemTriggerManager_MirrorTest).GetMethod("HandleEquipmentChanged", PrivateInstance);
            handleEquipChanged?.Invoke(context.ItemTriggers, new object[] { null });
        }
    }

    private static void UnequipWeapon(PlayerContext context)
    {
        EquipWeapon(context, null);
    }

    private static Dictionary<EquipSlotType, InventoryItem> GetEquippedItems(PlayerContext context)
    {
        return (Dictionary<EquipSlotType, InventoryItem>)typeof(EquipmentSystem)
            .GetField("equippedItems", PrivateInstance)?.GetValue(context.Equipment);
    }

    private static void SetPlayerStats(PlayerContext context, float attackPower)
    {
        context.Stats.EnsureInitialized().Recalculate(
            new StatSet
            {
                maxHealthFlat = 1000f,
                attackPowerFlat = attackPower,
                critRateFlat = 0f,
                critMultFlat = 0f,
            },
            StatSet.Zero, StatSet.Zero, StatSet.Zero);
    }

    private static (GameObject go, WBH_EnemyStatus status, NetworkEnemyAuthority_MirrorTest authority) CreateEnemy(
        GameObject prefab, string suffix, Vector3 position, float maxHealth)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        instance.name = "NightSwordValidation_" + suffix;
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
            try { handleDamaged?.Invoke(authority, new object[] { result }); }
            catch (TargetInvocationException ex) when (ex.InnerException != null) { throw ex.InnerException; }
        };
        status.OnDead += () =>
        {
            try { handleDead?.Invoke(authority, null); }
            catch (TargetInvocationException ex) when (ex.InnerException != null) { throw ex.InnerException; }
        };

        Physics.SyncTransforms();
        return (instance, status, authority);
    }

    private static void TriggerDodge(PlayerContext context)
    {
        typeof(ItemTriggerManager_MirrorTest).GetField("nextDodgeTriggerAt", PrivateInstance)
            ?.SetValue(context.ItemTriggers, 0.0);
        MethodInfo confirmDodge = typeof(ItemTriggerManager_MirrorTest).GetMethod("ConfirmDodgeTrigger", PrivateInstance);
        if (confirmDodge != null)
        {
            confirmDodge.Invoke(context.ItemTriggers, null);
        }
        else
        {
            MethodInfo prepareDodge = typeof(ItemTriggerManager_MirrorTest).GetMethod("PrepareDodgeAttack", PrivateInstance);
            prepareDodge?.Invoke(context.ItemTriggers, null);
        }
    }

    private static void InvokeFighterAttack(PlayerCombatAuthority_MirrorTest authority, uint attackId)
    {
        MethodInfo resolve = typeof(PlayerCombatAuthority_MirrorTest).GetMethod("ResolveServerAttack", PrivateInstance);
        try
        {
            resolve?.Invoke(authority, new object[] { attackId });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static void SetNetworkServerState(NetworkIdentity identity, NetworkBehaviour behaviour)
    {
        typeof(NetworkIdentity).GetProperty("isServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(identity, true);
        if (behaviour != null)
        {
            typeof(NetworkBehaviour).GetProperty("netIdentity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(behaviour, identity);
        }
    }
}
