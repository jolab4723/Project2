using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ItemSystem;
using Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Fighter 예약 경계와 Host Gunner의 실제 기본 공격을 검사하고 임시 런타임 상태를 정리한다.</summary>
public static class MirrorCombatBoundaryValidation_MirrorTest
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool gunnerValidationRunning;

    [MenuItem("SW/Mirror Test/Validate Gunner Attack Cancellation")]
    public static async void ValidateGunnerAttackCancellation()
    {
        Require(!gunnerValidationRunning && Application.isPlaying && NetworkServer.active &&
            NetworkClient.localPlayer != null && NetworkServer.connections.Count == 1, "Solo Host가 필요합니다.");
        var context = NetworkClient.localPlayer.GetComponent<PlayerContext>();
        var attack = context.CombatAuthority;
        var controller = context.GetComponent<T_PlayerController>();
        var agent = context.GetComponent<NavMeshAgent>();
        Require(attack.IsGunner && !context.RuntimeState.IsDead && controller.IsControlEnabled &&
            agent.enabled && agent.isOnNavMesh, "살아 있고 이동 가능한 전투 씬의 거너가 필요합니다.");
        Require(context.GetComponent<PlayerWeaponVisualPresenter>().enabled &&
            context.GetComponentInChildren<GunnerWeaponVfxBinding>() != null, "장착 무기 VFX 로드를 기다리세요.");
        var input = context.GetComponent<WBH_PlayerInputHandler_MirrorTest>();
        bool wasEnabled = input.enabled;
        Vector3 position = context.transform.position;
        Quaternion rotation = context.transform.rotation;
        uint shots = attack.GunnerShotCount, unconfirmed = attack.UnconfirmedAttackCount;
        gunnerValidationRunning = true;
        input.enabled = false;
        try
        {
            for (int i = 0; i < 8; i++)
            {
                await WaitUntil(() => attack.TryBeginLocalAttack(context.transform.position + Vector3.forward * 5), "취소 검사 공격");
                double nextAttack = Get<double>(attack, "localNextAttackAt");
                uint canceled = attack.CanceledRequestCount;
                Require(attack.TryCancelLocalAttackForMove(), "타격 전 취소");
                controller.MoveCommand(position + Vector3.right);
                Require(Get<double>(attack, "localNextAttackAt") == nextAttack &&
                    !attack.TryBeginLocalAttack(position + Vector3.forward * 5), "취소 직후 재공격 차단");
                await WaitUntil(() => attack.CanceledRequestCount == canceled + 1, "서버 취소 확인");
                Require(Get<double>(attack, "nextAttackAt") >= nextAttack - 0.05d, "서버 공격 간격 보존");
            }
            Require(attack.GunnerShotCount == shots, "취소된 공격은 발사하지 않음");
            await WaitUntil(() => attack.TryBeginLocalAttack(context.transform.position + Vector3.forward * 5), "취소 후 정상 공격");
            await WaitUntil(() => attack.GunnerShotCount == shots + 1, "실제 AnimationEvent 발사");
            double recovery = Get<double>(attack, "localNextAttackAt");
            attack.TryCancelLocalAttackForMove();
            controller.MoveCommand(position + Vector3.right);
            Require(Get<double>(attack, "localNextAttackAt") == recovery &&
                attack.UnconfirmedAttackCount == unconfirmed, "발사 후 이동은 대기시간 유지·이벤트 누락 없음");
            Debug.Log("[MirrorGunnerCancelValidation] PASS: 타격 전 취소8회 발사0·즉시 재공격 차단·서버 간격 보존·정상 발사1·발사 후 이동 확인.");
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally
        {
            if (agent != null && agent.isOnNavMesh) { agent.ResetPath(); agent.Warp(position); }
            if (context != null) context.transform.rotation = rotation;
            if (input != null) input.enabled = wasEnabled;
            gunnerValidationRunning = false;
        }
    }

    [MenuItem("SW/Mirror Test/Validate Gunner Live Attacks")]
    public static async void ValidateGunnerLiveAttacks()
    {
        if (gunnerValidationRunning) return;
        gunnerValidationRunning = true;
        PlayerContext context = null;
        NetworkEnemyAuthority_MirrorTest target = null;
        GameObject wall = null;
        ItemDefinitionSO fixtureDefinition = null;
        InventoryItem fixtureItem = null;
        InventoryPlacementSnapshot fixturePlacement = default;
        Quaternion originalRotation = Quaternion.identity;
        WBH_PlayerInputHandler_MirrorTest movementInput = null;
        PlayerActionInputHandler_MirrorTest actionInput = null;
        bool movementEnabled = false, actionEnabled = false;
        bool restorePlayerState = false;
        int checks = 0;
        try
        {
            Require(Application.isPlaying && NetworkServer.active && NetworkClient.active && Time.timeScale > 0f &&
                NetworkServer.connections.Count == 1, "다른 참가자가 없는 solo Host Play Mode에서 실행하세요.");
            context = UnityEngine.Object.FindObjectsByType<PlayerContext>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.CombatAuthority != null && p.CombatAuthority.isLocalPlayer && p.CombatAuthority.IsGunner);
            Require(context != null && context.RuntimeState?.HasSnapshot == true &&
                context.StateMachine.Is(PlayerState.Idle) && context.CombatAuthority.CanContinueGunnerProjectile,
                "살아 있는 Idle 상태의 Host Gunner가 필요합니다.");
            Require(!context.Equipment.TryGetEquippedItem(EquipSlotType.Weapon, out _) &&
                context.GetComponent<PlayerInventorySync_MirrorTest>().PendingRequestCount == 0,
                "Weapon 슬롯을 비우고 인벤토리 요청이 끝난 뒤 실행하세요.");
            PlayerCombatAuthority_MirrorTest attack = context.CombatAuthority;
            uint ownerNetId = attack.netId;
            Require(!UnityEngine.Object.FindObjectsByType<NetworkEnemyProjectile_MirrorTest>(FindObjectsSortMode.None)
                .Any(p => p.IsPlayerShot && p.PlayerOwnerNetId == ownerNetId), "기존 Gunner 탄이 사라진 뒤 실행하세요.");
            WBH_PlayerStatus status = context.GetComponent<WBH_PlayerStatus>();
            Require(status.GunnerAttackRange >= 6.5f, "벽 바깥 폭발 검사를 위해 Gunner 사거리 6.5 이상이 필요합니다.");
            Require(!UnityEngine.Object.FindObjectsByType<NetworkEnemyAuthority_MirrorTest>(FindObjectsSortMode.None)
                .Any(e => Vector3.Distance(e.transform.position, context.transform.position) <= status.GunnerAttackRange + 3f),
                "주변에 실제 적이 없는 Camp 등에서 실행하세요. 기존 적에게 검증 피해를 주지 않습니다.");

            int obstacleMask = LayerMask.GetMask("Wall", "Prop", "Ground");
            Transform firePoint = Get<Transform>(attack, "gunnerFirePoint");
            Require(firePoint != null, "Gunner FirePoint가 연결되지 않았습니다.");
            Vector3 targetPosition = default;
            bool found = false;
            for (int i = 0; i < 8 && !found; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 45f, 0f) * context.transform.forward;
                if (!NavMesh.SamplePosition(context.transform.position + direction * 6f, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)) continue;
                if (Physics.Linecast(firePoint.position, hit.position + Vector3.up, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
                targetPosition = hit.position;
                found = true;
            }
            Require(found, "6m 앞의 장애물 없는 NavMesh 공간이 필요합니다.");
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SW/TEST/MirrorCombat/Prefabs/Normal_Melee_MirrorTest.prefab");
            Require(enemyPrefab != null, "검증 대상 SW 적 프리팹이 없습니다.");
            originalRotation = context.transform.rotation;
            movementInput = context.GetComponent<WBH_PlayerInputHandler_MirrorTest>();
            actionInput = context.GetComponent<PlayerActionInputHandler_MirrorTest>();
            movementEnabled = movementInput != null && movementInput.enabled;
            actionEnabled = actionInput != null && actionInput.enabled;
            restorePlayerState = true;
            if (movementInput != null) movementInput.enabled = false;
            if (actionInput != null) actionInput.enabled = false;
            string[] originalItems = context.Inventory.PlayerGrid.GetAllItems().Select(i => i.itemData.instanceId).OrderBy(s => s).ToArray();
            int originalGold = context.Wallet.Gold;

            target = UnityEngine.Object.Instantiate(enemyPrefab, targetPosition, Quaternion.identity).GetComponent<NetworkEnemyAuthority_MirrorTest>();
            WBH_EnemyInfo info = target.EnemyInfo.Clone();
            info.maxHP = 100000f;
            info.attack = 0f;
            info.defense = 0f;
            info.moveSpeed = 0f;
            info.exp = 0;
            info.credit = 0;
            target.ServerSetEnemyInfo(info);
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<WBH_EnemyPattern_MirrorTest>().StopServer();
            // 같은 WBH_ICombat의 두 Collider가 겹쳐도 피해가 한 번인지 실제 Physics 경로로 검사한다.
            GameObject duplicateCollider = new GameObject("GunnerValidation_SecondCollider");
            duplicateCollider.layer = 10;
            duplicateCollider.transform.SetParent(target.transform, false);
            duplicateCollider.transform.localPosition = Vector3.up;
            duplicateCollider.AddComponent<BoxCollider>().size = new Vector3(0.8f, 1.5f, 0.8f);
            Physics.SyncTransforms();

            ItemDefinitionSO[] definitions = AssetDatabase.FindAssets("t:ItemDefinitionSO",
                new[] { "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null && d.characterClass == CharacterClass.Gunner && d.category == ItemCategory.Weapon).ToArray();
            foreach (WeaponType weaponType in new[] { WeaponType.Rifle, WeaponType.Shotgun, WeaponType.GrenadeLauncher })
            {
                ItemDefinitionSO source = definitions.FirstOrDefault(d => d.weaponType == weaponType);
                Require(source != null, weaponType + " 아이템 정의가 없습니다.");
                fixtureDefinition = UnityEngine.Object.Instantiate(source);
                fixtureDefinition.mainOptions = Array.Empty<FixedStatValue>();
                fixtureDefinition.uniqueEffect = null;
                fixtureDefinition.weaponEnchantElement = ElementType.None;
                ItemInstance item = new ItemInstance { instanceId = Guid.NewGuid().ToString(), definition = fixtureDefinition };
                Require(context.Inventory.TryAddItemData(item).Result == InventoryAddResult.Success, "검증 무기를 넣을 인벤토리 공간이 없습니다.");
                fixtureItem = context.Inventory.PlayerGrid.GetAllItems().First(i => i.itemData == item);
                fixturePlacement = InventoryPlacementSnapshot.Capture(context.Inventory.PlayerGrid, fixtureItem);
                Require(new EquipmentTransaction(context.Equipment).TryEquip(context.Inventory.PlayerGrid, fixtureItem,
                    fixturePlacement, EquipSlotType.Weapon).IsSuccess, "공개 장비 트랜잭션 장착 실패");
                await Task.Delay(150);
                GunnerWeaponType expected = weaponType == WeaponType.Rifle ? GunnerWeaponType.Rifle :
                    weaponType == WeaponType.Shotgun ? GunnerWeaponType.Shotgun : GunnerWeaponType.GrenadeLauncher;
                await FireAndCheck(expected, blocked: false);

                Vector3 fireDirection = Vector3.ProjectOnPlane(targetPosition - context.transform.position, Vector3.up).normalized;
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "GunnerValidation_Wall";
                int wallLayer = LayerMask.NameToLayer("Wall");
                Require(wallLayer >= 0, "Wall 레이어가 없습니다.");
                wall.layer = wallLayer;
                wall.transform.SetPositionAndRotation(firePoint.position + fireDirection * 0.8f, Quaternion.LookRotation(fireDirection));
                wall.transform.localScale = new Vector3(3f, 4f, 0.3f);
                Physics.SyncTransforms();
                await FireAndCheck(expected, blocked: true);
                UnityEngine.Object.DestroyImmediate(wall);
                wall = null;
                RemoveFixtureItem();
            }
            Require(originalItems.SequenceEqual(context.Inventory.PlayerGrid.GetAllItems().Select(i => i.itemData.instanceId).OrderBy(s => s)) &&
                context.Wallet.Gold == originalGold, "검증 후 기존 인벤토리/골드 불일치");
            Debug.Log($"[MirrorGunnerLiveValidation] PASS {checks} checks. Host 실제 AnimationEvent·3종 장착·피해·중복 Collider·벽 차단 확인. 원격 클라이언트/최신 스킬/전체 VFX 검증과 구분합니다.");

            async Task FireAndCheck(GunnerWeaponType expected, bool blocked)
            {
                float health = target.CurrentHealth;
                uint hits = target.ReceivedDamagePresentationCount;
                uint shots = attack.GunnerShotCount;
                uint projectiles = NetworkEnemyProjectile_MirrorTest.ServerPlayerSpawnCount;
                uint observed = NetworkEnemyProjectile_MirrorTest.ClientPlayerObservedCount;
                await WaitUntil(() => attack.TryBeginLocalAttack(targetPosition), "공격 입력 승인");
                await WaitUntil(() => attack.GunnerShotCount == shots + 1, "실제 AnimationEvent 발사 확정");
                Check(attack.LastGunnerWeapon == expected, expected + " 서버 장비 분기");
                Check(!attack.TryConfirmLocalAttackImpactFromAnimation(), "이미 확정된 AnimationEvent 중복 거부");
                if (!blocked) await WaitUntil(() => target.CurrentHealth < health, expected + " 실제 HP 감소");
                await Task.Delay(1600);
                Check(attack.GunnerShotCount == shots + 1 && (blocked ? Mathf.Approximately(target.CurrentHealth, health) :
                    target.CurrentHealth < health && target.ReceivedDamagePresentationCount == hits + 1),
                    expected + (blocked ? " 근접 벽 차단" : " 다중 Collider 피해 1회"));
                uint expectedProjectiles = expected == GunnerWeaponType.Shotgun ? 0u : 1u;
                Check(NetworkEnemyProjectile_MirrorTest.ServerPlayerSpawnCount == projectiles + expectedProjectiles &&
                    NetworkEnemyProjectile_MirrorTest.ClientPlayerObservedCount == observed + expectedProjectiles, expected + " 서버 spawn/Host 관찰 횟수");
            }
            void Check(bool condition, string label) { Require(condition, label); checks++; }
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally
        {
            try { RemoveFixtureItem(); }
            catch (Exception cleanupFailure) { Debug.LogException(cleanupFailure); }
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            if (target != null) NetworkServer.Destroy(target.gameObject);
            if (context != null && restorePlayerState)
            {
                foreach (NetworkEnemyProjectile_MirrorTest projectile in UnityEngine.Object.FindObjectsByType<NetworkEnemyProjectile_MirrorTest>(FindObjectsSortMode.None))
                    if (projectile.IsPlayerShot && projectile.PlayerOwnerNetId == context.CombatAuthority.netId) NetworkServer.Destroy(projectile.gameObject);
                context.transform.rotation = originalRotation;
                if (movementInput != null) movementInput.enabled = movementEnabled;
                if (actionInput != null) actionInput.enabled = actionEnabled;
            }
            gunnerValidationRunning = false;
        }

        void RemoveFixtureItem()
        {
            if (fixtureItem != null && context != null)
            {
                if (context.Equipment.TryGetEquippedItem(EquipSlotType.Weapon, out InventoryItem equipped) && equipped == fixtureItem)
                    Require(new EquipmentTransaction(context.Equipment).TryUnequip(EquipSlotType.Weapon,
                        context.Inventory.PlayerGrid, fixturePlacement).IsSuccess, "검증 무기 해제 실패: 검증 아이템을 보존합니다.");
                Require(context.Inventory.TryRemoveInventoryItem(fixtureItem) == InventoryRemoveResult.Success, "검증 무기 제거 실패: 검증 아이템을 보존합니다.");
                fixtureItem = null;
            }
            if (fixtureDefinition != null && fixtureItem == null) UnityEngine.Object.DestroyImmediate(fixtureDefinition);
            fixtureDefinition = null;
        }
    }

    private static async Task WaitUntil(Func<bool> predicate, string description)
    {
        double deadline = EditorApplication.timeSinceStartup + 8d;
        while (Application.isPlaying && NetworkServer.active && EditorApplication.timeSinceStartup < deadline)
        {
            if (predicate()) return;
            await Task.Delay(50);
        }
        throw new InvalidOperationException("[MirrorGunnerLiveValidation] timeout: " + description);
    }

    [MenuItem("SW/Mirror Test/Validate Combat Boundaries")]
    public static void Validate()
    {
        Require(Application.isPlaying && NetworkServer.active, "Host 또는 서버 Play Mode에서 실행하세요.");
        PlayerContext context = UnityEngine.Object.FindObjectsByType<PlayerContext>(FindObjectsSortMode.None)
            .FirstOrDefault(p => p.CombatAuthority != null && p.CombatAuthority.isServer &&
                p.CombatAuthority.SupportsCharacter && !p.CombatAuthority.IsGunner && p.Mana != null && p.Mana.MaxMana >= 6f &&
                p.StateMachine != null && p.StateMachine.Is(PlayerState.Idle) &&
                p.GetComponent<WBH_PlayerStatus>()?.IsDead == false &&
                p.GetComponent<MirrorSpawnedPlayerBinder>()?.IsTemporarilyAbsent != true);
        Require(context != null, "대기 중인 살아 있는 서버 Fighter(최대 MP 6 이상)가 필요합니다.");
        FighterSkillAuthority_MirrorTest skill = context.GetComponent<FighterSkillAuthority_MirrorTest>();
        Require(skill != null && Get<uint>(skill, "pendingServerRequestId") == 0 &&
            Get<uint>(skill, "activeLocalRequestId") == 0, "스킬 예약이 없는 Fighter가 필요합니다.");

        // 테스트는 SW 어댑터의 private 예약만 주입한다. 팀원 원본의 private 실행/API를 우회하지 않는다.
        string[] fields = { "skills", "activeEvolutions", "pendingServerRequestId", "pendingServerSlot",
            "pendingServerAim", "serverAnimationExpiresAt" };
        object[] before = fields.Select(f => Field(skill, f).GetValue(skill)).ToArray();
        float originalMana = context.Mana.CurrentMana;
        CharacterClass originalClass = context.Equipment.CurrentCharacterClass.Value;
        SkillDefinitionSO definition = ScriptableObject.CreateInstance<SkillDefinitionSO>();
        definition.manaCost = 5.9f; // 원본 UseMana의 버림 규칙까지 재사용하는지 확인한다.
        int checks = 0;
        try
        {
            Set(skill, "skills", new[] { definition, definition, definition });
            Set(skill, "activeEvolutions", new SkillEvolutionId[3]);
            float angle = (float)typeof(PlayerCombatAuthority_MirrorTest)
                .GetProperty("AttackAngle", PrivateInstance).GetValue(context.CombatAuthority);
            Check(Mathf.Approximately(angle, context.GetComponent<WBH_PlayerStatus>().FighterAttackAngle), "상태의 Fighter 각도 재사용");

            context.Equipment.SetActiveCharacterClass(CharacterClass.Gunner);
            Check(context.CombatAuthority.SupportsCharacter && context.CombatAuthority.IsGunner &&
                !(bool)typeof(FighterSkillAuthority_MirrorTest).GetProperty("SupportsCharacter", PrivateInstance).GetValue(skill), "Gunner 기본 공격 분리/Fighter 스킬 거부");
            context.Mana.SetCurrentMana(6f);
            Reserve(1);
            Check(!Commit(1) && Mathf.Approximately(context.Mana.CurrentMana, 6f), "Gunner 확정 거부/마나 보존");
            context.Equipment.SetActiveCharacterClass(CharacterClass.Fighter);

            Reserve(2);
            Check(!Commit(99) && Mathf.Approximately(context.Mana.CurrentMana, 6f), "다른 요청의 확정 거부");
            Check(Commit(2) && Mathf.Approximately(context.Mana.CurrentMana, 1f), "충분한 마나를 확정 때 원본 규칙으로 1회 소비");
            Check(!Commit(2) && Mathf.Approximately(context.Mana.CurrentMana, 1f), "중복 확정 소비 방지");

            Reserve(3);
            Check(!Commit(3) && Mathf.Approximately(context.Mana.CurrentMana, 1f) &&
                Get<uint>(skill, "pendingServerRequestId") == 0, "마나 부족 시 예약 종료/소비 없음");
            context.Mana.SetCurrentMana(6f);
            Check(!Commit(3) && Mathf.Approximately(context.Mana.CurrentMana, 6f), "마나 회복 후 거절 요청 재사용 방지");

            Reserve(4);
            typeof(FighterSkillAuthority_MirrorTest).GetMethod("ClearServerPendingSkill", PrivateInstance).Invoke(skill, null);
            Check(!Commit(4) && Mathf.Approximately(context.Mana.CurrentMana, 6f), "취소된 예약 소비 방지");
            Reserve(5);
            Set(skill, "serverAnimationExpiresAt", NetworkTime.time - 1d);
            Check(!Commit(5) && Mathf.Approximately(context.Mana.CurrentMana, 6f), "만료된 예약 소비 방지");
        }
        finally
        {
            for (int i = 0; i < fields.Length; i++) Field(skill, fields[i]).SetValue(skill, before[i]);
            context.Equipment.SetActiveCharacterClass(originalClass);
            context.Mana.SetCurrentMana(originalMana);
            UnityEngine.Object.DestroyImmediate(definition);
        }
        Debug.Log($"[MirrorCombatBoundaryValidation] PASS {checks} checks. 클래스/각도/서버 마나 예약 경계 검사이며 실제 공격·대시·원격 플레이 검증을 대신하지 않습니다.");

        void Check(bool condition, string label) { Require(condition, label); checks++; }
        void Reserve(uint id)
        {
            Set(skill, "pendingServerRequestId", id);
            Set(skill, "pendingServerSlot", (byte)0);
            Set(skill, "pendingServerAim", Vector3.forward);
            Set(skill, "serverAnimationExpiresAt", NetworkTime.time + 10d);
        }
        bool Commit(uint id)
        {
            object[] args = { id, (byte)0, Vector3.zero, null };
            return (bool)typeof(FighterSkillAuthority_MirrorTest).GetMethod("TryCommitPendingSkill", PrivateInstance).Invoke(skill, args);
        }
    }

#if false // Retired: direct ScenarioRunner reflection was unreliable; use the configured MPPM profile.
    [MenuItem("SW/Mirror Test/Validate MPPM 2-Player Combat")]
    public static void ValidateMppm2PlayerCombat()
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "UnityEditor.MultiplayerModule");
        if (asm == null)
        {
            Debug.LogError("[MirrorCombatBoundaryValidation] UnityEditor.MultiplayerModule을 찾을 수 없습니다.");
            return;
        }

        var vpw = asm.GetType("Unity.Multiplayer.PlayMode.Editor.VirtualProjectWorkflow");
        vpw?.GetMethod("InitializeMPPMContexts", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);

        var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/PlayMode/2 Player.asset");
        if (config == null)
        {
            Debug.LogError("[MirrorCombatBoundaryValidation] Assets/Settings/PlayMode/2 Player.asset을 찾을 수 없습니다.");
            return;
        }

        var runnerType = asm.GetType("Unity.Multiplayer.PlayMode.Editor.ScenarioRunner");
        var createScenario = config.GetType().GetMethod("CreateScenario", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var loadScenario = runnerType?.GetMethod("LoadScenario", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var startScenario = runnerType?.GetMethod("StartScenario", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        object scenario = createScenario?.Invoke(config, null);
        if (scenario == null || loadScenario == null || startScenario == null)
        {
            Debug.LogError("[MirrorCombatBoundaryValidation] MPPM 시나리오 생성 또는 실행 API를 준비하지 못했습니다.");
            return;
        }

        // 생성 결과를 직접 로드하고, MPPM이 클론 컨텍스트를 반영할 다음 Editor tick에 실행합니다.
        loadScenario.Invoke(null, new[] { scenario });
        SessionState.SetBool("SW.RunMppmValidation", true);
        EditorApplication.delayCall += () =>
        {
            startScenario.Invoke(null, null);
            Debug.Log("[MirrorCombatBoundaryValidation] MPPM 2-Player 시나리오 실행 시작. Main Editor(Host) 및 Virtual Player(Client) 실행 중...");
        };
    }

#endif

    [MenuItem("SW/Mirror Test/Validate Unique Effect P1 Foundation")]
    public static void ValidateUniqueEffectP1Foundation()
    {
        int checks = 0;
        void Check(bool condition, string label) { Require(condition, label); checks++; }

        bool wasServerActive = NetworkServer.active;
        typeof(NetworkServer).GetProperty("active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(null, true);

        try
        {
            // 1. WBH_DamageRequest / WBH_DamageResult metadata and backwards compatibility
            var reqDirect = new WBH_DamageRequest(null, null, WBH_AttackType.Normal, ElementType.Fire, 1f);
            Check(reqDirect.DamageCause == DamageCause.Direct, "DamageRequest default Direct");
            Check(reqDirect.AttackId == 0, "DamageRequest default AttackId 0");

            var reqSkill = new WBH_DamageRequest(null, null, WBH_AttackType.Skill, ElementType.Ice, 1.5f);
            Check(reqSkill.DamageCause == DamageCause.Skill, "DamageRequest AttackType.Skill maps to DamageCause.Skill");
            Check(reqSkill.AttackId == 0, "DamageRequest Skill AttackId 0");

            var reqEffect = new WBH_DamageRequest(null, null, WBH_AttackType.Normal, ElementType.Electric, 1f, null, null, null, null, DamageCause.Effect, 77u);
            Check(reqEffect.DamageCause == DamageCause.Effect, "DamageRequest explicit Effect");
            Check(reqEffect.AttackId == 77u, "DamageRequest explicit AttackId 77");

            var reqDoT = new WBH_DamageRequest(null, null, WBH_AttackType.Normal, ElementType.Fire, 1f, null, null, null, null, DamageCause.DoT, 88u);
            Check(reqDoT.DamageCause == DamageCause.DoT, "DamageRequest explicit DoT");
            Check(reqDoT.AttackId == 88u, "DamageRequest explicit AttackId 88");

            var resDirect = new WBH_DamageResult(null, 50f, true, ElementType.Fire);
            Check(resDirect.DamageCause == DamageCause.Direct, "DamageResult default Direct");
            Check(resDirect.AttackId == 0, "DamageResult default AttackId 0");

            var resExplicit = new WBH_DamageResult(null, 50f, true, ElementType.Fire, null, null, null, null, DamageCause.Effect, 77u);
            Check(resExplicit.DamageCause == DamageCause.Effect, "DamageResult explicit Effect");
            Check(resExplicit.AttackId == 77u, "DamageResult explicit AttackId 77");

            // 2. WBH_CombatManager: alive check, lethal hit, and metadata passing
            var goAttacker = new GameObject("P1Validation_Attacker");
            var goEnemy = new GameObject("P1Validation_Enemy");
            try
            {
                var attackerDummy = goAttacker.AddComponent<TrainingDummyStatus>();
                var targetStatus = goEnemy.AddComponent<WBH_EnemyStatus>();
                var targetController = goEnemy.AddComponent<WBH_EnemyController>();
                typeof(WBH_EnemyController).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(targetController, targetStatus);
                var hpField = typeof(WBH_EnemyStatus).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic);

                // 2A: Target already dead before hit -> ignored
                hpField.SetValue(targetStatus, 0f);
                Check(targetStatus.IsDead, "EnemyStatus initially dead");
                WBH_CombatManager.ProcessDamage(new WBH_DamageRequest(attackerDummy, targetController, WBH_AttackType.Normal, ElementType.Fire, 1f, null, null, null, null, DamageCause.Direct, 10u));
                Check((float)hpField.GetValue(targetStatus) == 0f, "Dead target ignored without error");

                // 2B: Lethal hit on alive target -> executes TakeDamage, reaches dead state
                hpField.SetValue(targetStatus, 1f);
                Check(!targetStatus.IsDead, "EnemyStatus alive before hit");
                WBH_CombatManager.ProcessDamage(new WBH_DamageRequest(attackerDummy, targetController, WBH_AttackType.Normal, ElementType.Fire, 1f, null, null, null, null, DamageCause.Effect, 11u));
                Check(targetStatus.IsDead, "Lethal hit successfully transitions enemy to dead state");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goAttacker);
                UnityEngine.Object.DestroyImmediate(goEnemy);
            }

            // 3. NetworkEnemyAuthority_MirrorTest.IsServerDamageHandlingActive safety
            var enemyObj = new GameObject("P1Validation_EnemyObj");
            try
            {
                var auth = enemyObj.AddComponent<NetworkEnemyAuthority_MirrorTest>();
                Check(!auth.IsServerDamageHandlingActive, "IsServerDamageHandlingActive false when not server/subscribed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObj);
            }

            // =========================================================================
            // Section A: ItemTriggerManager_MirrorTest Live Buff Application & Filter Guard
            // =========================================================================
            var goSectionA = new GameObject("P1Validation_PlayerA");
            try
            {
                var netIdA = goSectionA.AddComponent<NetworkIdentity>();
                typeof(NetworkIdentity).GetProperty("isServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(netIdA, true);
                var buffsA = goSectionA.AddComponent<PlayerBuffManager>();
                var invA = goSectionA.AddComponent<InventoryController>();
                var equipA = goSectionA.AddComponent<EquipmentSystem>();
                typeof(InventoryController).GetField("equipmentSystem", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(invA, equipA);
                var triggerA = goSectionA.AddComponent<ItemTriggerManager_MirrorTest>();
                typeof(NetworkBehaviour).GetProperty("netIdentity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(triggerA, netIdA);
                typeof(ItemTriggerManager_MirrorTest).GetField("inventory", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(triggerA, invA);
                typeof(ItemTriggerManager_MirrorTest).GetField("buffs", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(triggerA, buffsA);

                var effectHit = ScriptableObject.CreateInstance<TriggeredBuffUniqueEffectSO>();
                effectHit.name = "Effect_Hit";
                effectHit.triggerCondition = TriggerCondition.OnDamageDealt;
                effectHit.cooldownSeconds = 0f;
                effectHit.buffSpec = new BuffSpec { duration = 10f, stackBehavior = BuffStackBehavior.Stack, maxStack = 10 };

                var effectCrit = ScriptableObject.CreateInstance<TriggeredBuffUniqueEffectSO>();
                effectCrit.name = "Effect_Crit";
                effectCrit.triggerCondition = TriggerCondition.OnCrit;
                effectCrit.cooldownSeconds = 0f;
                effectCrit.buffSpec = new BuffSpec { duration = 10f, stackBehavior = BuffStackBehavior.Stack, maxStack = 10 };

                var defHit = ScriptableObject.CreateInstance<ItemDefinitionSO>();
                defHit.uniqueEffect = effectHit;
                defHit.mainOptions = Array.Empty<FixedStatValue>();
                var defCrit = ScriptableObject.CreateInstance<ItemDefinitionSO>();
                defCrit.uniqueEffect = effectCrit;
                defCrit.mainOptions = Array.Empty<FixedStatValue>();

                var itemHit = new ItemInstance { instanceId = "item_hit", definition = defHit };
                var itemCrit = new ItemInstance { instanceId = "item_crit", definition = defCrit };

                var equippedDict = (System.Collections.Generic.Dictionary<EquipSlotType, InventoryItem>)
                    typeof(EquipmentSystem).GetField("equippedItems", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(equipA);
                equippedDict[EquipSlotType.Weapon] = new InventoryItem(itemHit);
                equippedDict[EquipSlotType.Helmet] = new InventoryItem(itemCrit);

                // A1: Direct non-crit -> OnDamageDealt triggers, OnCrit does not
                triggerA.FireDamageDealt(new WBH_DamageResult(null, 10f, false, ElementType.Fire, null, null, null, null, DamageCause.Direct, 101u));
                Check(buffsA.ActiveBuffs.Count == 1, "A1: ActiveBuffs count 1");
                Check(buffsA.ActiveBuffs[0].source == effectHit && buffsA.ActiveBuffs[0].stackCount == 1, "A1: Effect_Hit stack 1");

                // A2: Direct crit -> OnDamageDealt and OnCrit both trigger
                triggerA.FireDamageDealt(new WBH_DamageResult(null, 10f, true, ElementType.Fire, null, null, null, null, DamageCause.Direct, 102u));
                Check(buffsA.ActiveBuffs.Count == 2, "A2: ActiveBuffs count 2");
                var buffHit = buffsA.ActiveBuffs.FirstOrDefault(b => b.source == effectHit);
                var buffCrit = buffsA.ActiveBuffs.FirstOrDefault(b => b.source == effectCrit);
                Check(buffHit != null && buffHit.stackCount == 2, "A2: Effect_Hit stack 2");
                Check(buffCrit != null && buffCrit.stackCount == 1, "A2: Effect_Crit stack 1");

                // A3: Skill, Effect & DoT -> zero triggers, stacks unchanged
                triggerA.FireDamageDealt(new WBH_DamageResult(null, 10f, true, ElementType.Fire, null, null, null, null, DamageCause.Skill, 103u));
                Check(buffHit.stackCount == 2 && buffCrit.stackCount == 1, "A3: Buff stacks unchanged after Skill");
                triggerA.FireDamageDealt(new WBH_DamageResult(null, 10f, true, ElementType.Fire, null, null, null, null, DamageCause.Effect, 104u));
                triggerA.FireDamageDealt(new WBH_DamageResult(null, 10f, true, ElementType.Fire, null, null, null, null, DamageCause.DoT, 105u));
                Check(buffHit.stackCount == 2, "A3: Effect_Hit stack unchanged after Effect/DoT");
                Check(buffCrit.stackCount == 1, "A3: Effect_Crit stack unchanged after Effect/DoT");

                // A4: Cooldown & PerItem isolation
                var effectCd = ScriptableObject.CreateInstance<TriggeredBuffUniqueEffectSO>();
                effectCd.name = "Effect_Cd";
                effectCd.triggerCondition = TriggerCondition.OnDamageDealt;
                effectCd.cooldownSeconds = 50f;
                effectCd.duplicatePolicy = DuplicateTriggerPolicy.PerItem;
                effectCd.buffSpec = new BuffSpec { duration = 10f, stackBehavior = BuffStackBehavior.Stack, maxStack = 10 };

                var defCd = ScriptableObject.CreateInstance<ItemDefinitionSO>();
                defCd.uniqueEffect = effectCd;
                defCd.mainOptions = Array.Empty<FixedStatValue>();
                var itemCd1 = new ItemInstance { instanceId = "cd_1", definition = defCd };
                var itemCd2 = new ItemInstance { instanceId = "cd_2", definition = defCd };

                equippedDict[EquipSlotType.Chest] = new InventoryItem(itemCd1);
                equippedDict[EquipSlotType.Boots] = new InventoryItem(itemCd2);

                triggerA.FireDamageDealt(new WBH_DamageResult(null, 10f, false, ElementType.Fire, null, null, null, null, DamageCause.Direct, 105u));
                var buffCd = buffsA.ActiveBuffs.FirstOrDefault(b => b.source == effectCd);
                Check(buffCd != null && buffCd.stackCount == 2, "A4: Both items triggered initial hit (stack=2)");

                // Immediate 2nd hit: both items are on cooldown, stack must remain 2
                triggerA.FireDamageDealt(new WBH_DamageResult(null, 10f, false, ElementType.Fire, null, null, null, null, DamageCause.Direct, 106u));
                Check(buffCd.stackCount == 2, "A4: Re-hit during cooldown blocked");

                UnityEngine.Object.DestroyImmediate(effectHit);
                UnityEngine.Object.DestroyImmediate(effectCrit);
                UnityEngine.Object.DestroyImmediate(effectCd);
                UnityEngine.Object.DestroyImmediate(defHit);
                UnityEngine.Object.DestroyImmediate(defCrit);
                UnityEngine.Object.DestroyImmediate(defCd);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goSectionA);
            }

            // =========================================================================
            // Section B & C: Out-of-bounds, Re-entrancy, FIFO Queue, Dead Skip, Exception
            // =========================================================================
            var pPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab");
            var ePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SW/TEST/MirrorCombat/Prefabs/Normal_Melee_MirrorTest.prefab");
            Require(pPrefab != null && ePrefab != null, "필수 프리팹이 존재해야 합니다.");

            var pInst = UnityEngine.Object.Instantiate(pPrefab);
            var pInstB = UnityEngine.Object.Instantiate(pPrefab);
            var eInst = UnityEngine.Object.Instantiate(ePrefab);
            try
            {
                var pContext = pInst.GetComponent<PlayerContext>();
                var ctrl = pContext.Controller;
                var pStatus = pInst.GetComponent<WBH_PlayerStatus>();
                var statMgr = pInst.GetComponent<PlayerStatManager>();
                var healthMgr = pInst.GetComponent<PlayerHealthManager>();
                typeof(T_PlayerController).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ctrl, pStatus);
                pStatus.Initialize(ctrl);
                var stat = statMgr.EnsureInitialized();
                stat.Recalculate(new StatSet { maxHealthFlat = 1000f, attackPowerFlat = 30f }, StatSet.Zero, StatSet.Zero, StatSet.Zero);
                typeof(PlayerHealthManager).GetField("statManager", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(healthMgr, statMgr);
                healthMgr.RefreshMaxHealth();
                healthMgr.FillHealth();

                var pContextB = pInstB.GetComponent<PlayerContext>();
                var ctrlB = pContextB.Controller;
                var pStatusB = pInstB.GetComponent<WBH_PlayerStatus>();
                var statMgrB = pInstB.GetComponent<PlayerStatManager>();
                var healthMgrB = pInstB.GetComponent<PlayerHealthManager>();
                typeof(T_PlayerController).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ctrlB, pStatusB);
                pStatusB.Initialize(ctrlB);
                statMgrB.EnsureInitialized().Recalculate(new StatSet { maxHealthFlat = 1000f, attackPowerFlat = 30f }, StatSet.Zero, StatSet.Zero, StatSet.Zero);
                typeof(PlayerHealthManager).GetField("statManager", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(healthMgrB, statMgrB);
                healthMgrB.RefreshMaxHealth();
                healthMgrB.FillHealth();

                var eCombat = eInst.GetComponent<WBH_EnemyController>();
                var eStatus = eInst.GetComponent<WBH_EnemyStatus>();
                var eAuth = eInst.GetComponent<NetworkEnemyAuthority_MirrorTest>();
                typeof(WBH_EnemyController).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(eCombat, eStatus);

                WBH_EnemyInfo info = eAuth.EnemyInfo.Clone();
                info.maxHP = 100f;
                info.defense = 0f;
                eStatus.Initialize(info);

                // --- Section B: Out-of-bounds registration & Re-entrancy rejection ---
                // B1: Out-of-bounds registration
                float hpBeforeOOB = eStatus.CurrentHp;
                bool enqueuedOOB = WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(
                    pContext, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 201u);
                Check(!enqueuedOOB, "B1: EnqueueFollowUpDamage outside resolution rejected");
                Check(Mathf.Approximately(eStatus.CurrentHp, hpBeforeOOB), "B1: Target HP preserved on OOB rejection");

                // B2: Same-attacker re-entrancy rejection through the real damage event
                bool reentrantResult = true;
                Action<WBH_DamageResult> reentrantAttempt = (res) =>
                {
                    if (res.AttackId == 202u)
                        reentrantResult = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                            pContext, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Direct, 203u);
                };
                eStatus.OnDamaged += reentrantAttempt;
                float hpBeforeReentrant = eStatus.CurrentHp;
                bool primaryResult = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    pContext, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Direct, 202u);
                eStatus.OnDamaged -= reentrantAttempt;
                Check(primaryResult, "B2: Primary damage succeeded");
                Check(!reentrantResult, "B2: Same-attacker re-entrant damage rejected");
                Check(Mathf.Approximately(eStatus.CurrentHp, hpBeforeReentrant - 30f), "B2: Re-entrant damage did not change HP");

                // B3: Same attacker, AttackId, and target is accepted only once
                eStatus.Initialize(info);
                bool firstDuplicateProbe = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    pContext, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Direct, 210u);
                bool secondDuplicateProbe = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    pContext, eCombat, ElementType.Fire, 1f, null, out WBH_DamageResult duplicateResult, DamageCause.Direct, 210u);
                Check(firstDuplicateProbe, "B3: First attack-target pair accepted");
                Check(!secondDuplicateProbe && duplicateResult.AttackId == 0, "B3: Duplicate attack-target pair rejected");
                Check(Mathf.Approximately(eStatus.CurrentHp, 70f), "B3: Duplicate pair dealt damage only once");

                var resolutionStates = (System.Collections.IDictionary)typeof(WBH_CombatResolver_MirrorTest)
                    .GetField("resolutionStates", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Check(resolutionStates.Count == 0, "B3-1: Duplicate rejection leaves no resolver state");

                // B4: A different player may resolve damage while player A is resolving
                eStatus.Initialize(info);
                bool nestedOtherPlayerResult = false;
                bool nestedOtherPlayerStarted = false;
                Action<WBH_DamageResult> nestedOtherPlayer = (res) =>
                {
                    if (res.AttackId == 220u && !nestedOtherPlayerStarted)
                    {
                        nestedOtherPlayerStarted = true;
                        nestedOtherPlayerResult = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                            pContextB, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Direct, 220u);
                    }
                };
                eStatus.OnDamaged += nestedOtherPlayer;
                bool outerPlayerResult = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    pContext, eCombat, ElementType.Fire, 1f, null, out _, DamageCause.Direct, 220u);
                eStatus.OnDamaged -= nestedOtherPlayer;
                Check(outerPlayerResult && nestedOtherPlayerResult, "B4: Different players resolve independently");
                Check(Mathf.Approximately(eStatus.CurrentHp, 40f), "B4: Both players' damage applied");

                eStatus.Initialize(info);

                // --- Section C: Real Queue FIFO, Lethal Skip, Exception Recovery ---
                // C1: Hook follow-ups during resolution
                var receivedHits = new System.Collections.Generic.List<WBH_DamageResult>();
                eStatus.OnDamaged += (res) =>
                {
                    receivedHits.Add(res);
                    if (res.AttackId == 300u)
                    {
                        WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(pContext, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 301u);
                        WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(pContext, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 302u);
                    }
                };

                // Target has 100 HP, hits do 30 each. 3 hits = 90 damage, target survives (HP 10)
                bool hit300 = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    pContext, eCombat, ElementType.Fire, 1f, null, out WBH_DamageResult _, DamageCause.Direct, 300u);
                Check(hit300, "C1: Primary attack succeeded");
                Check(receivedHits.Count == 3, "C1: Direct hit + 2 enqueued follow-ups executed (3 hits total)");
                Check(receivedHits[0].AttackId == 300u && receivedHits[0].DamageCause == DamageCause.Direct, "C1: Hit 0 is 300 Direct");
                Check(receivedHits[1].AttackId == 301u && receivedHits[1].DamageCause == DamageCause.Effect, "C1: Hit 1 is 301 Effect (FIFO)");
                Check(receivedHits[2].AttackId == 302u && receivedHits[2].DamageCause == DamageCause.Effect, "C1: Hit 2 is 302 Effect (FIFO)");
                Check(Mathf.Approximately(eStatus.CurrentHp, 10f), "C1: Target HP reduced to 10");

                // C2: Lethal direct hit skips enqueued follow-up
                receivedHits.Clear();
                bool enqueued304 = false;
                bool enqueued305 = false;
                int skippedCallbacks = 0;
                Action<WBH_DamageResult> enqueueAfterLethal = (res) =>
                {
                    if (res.AttackId != 303u) return;
                    enqueued304 = WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(
                        pContext, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 304u, _ => skippedCallbacks++);
                    enqueued305 = WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(
                        pContext, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 305u, _ => skippedCallbacks++);
                };
                eStatus.OnDamaged += enqueueAfterLethal;
                bool hit303 = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    pContext, eCombat, ElementType.Fire, 1f, null, out WBH_DamageResult _, DamageCause.Direct, 303u);
                eStatus.OnDamaged -= enqueueAfterLethal;
                Check(hit303, "C2: Lethal hit processed");
                Check(eStatus.IsDead, "C2: Target died on direct hit");
                Check(enqueued304 && enqueued305, "C2: Two follow-ups were accepted inside the active boundary");
                Check(receivedHits.Count == 1, "C2: Follow-ups were skipped after lethal direct damage");
                Check(skippedCallbacks == 0, "C2: Skipped follow-up callbacks were not invoked");

                // C3: Surviving direct hit killed by follow-up skips 2nd follow-up
                info.maxHP = 50f;
                eStatus.Initialize(info);
                receivedHits.Clear();
                eStatus.OnDamaged += (res) =>
                {
                    if (res.AttackId == 310u)
                    {
                        WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(pContext, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 311u);
                        WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(pContext, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 312u);
                    }
                };

                bool hit310 = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    pContext, eCombat, ElementType.Fire, 1f, null, out WBH_DamageResult _, DamageCause.Direct, 310u);
                Check(hit310, "C3: Hit 310 succeeded");
                Check(eStatus.IsDead, "C3: Target died from follow-up 311");
                Check(receivedHits.Count == 2, "C3: Only 310 and 311 executed, 312 skipped due to death");
                Check(receivedHits[0].AttackId == 310u && receivedHits[1].AttackId == 311u, "C3: Hits were 310 then 311");

                // C4: Controlled exception recovery
                info.maxHP = 100f;
                eStatus.Initialize(info);
                bool shouldThrow = true;
                Action<WBH_DamageResult> thrower = (res) =>
                {
                    if (shouldThrow && res.AttackId == 399u)
                    {
                        WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(pContext, eCombat, ElementType.Fire, 1f, null, DamageCause.Effect, 400u);
                        throw new InvalidOperationException("Simulated exception in damage processing");
                    }
                };
                eStatus.OnDamaged += thrower;

                bool exceptionCaught = false;
                try
                {
                    WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                        pContext, eCombat, ElementType.Fire, 1f, null, out WBH_DamageResult _, DamageCause.Direct, 399u);
                }
                catch (InvalidOperationException)
                {
                    exceptionCaught = true;
                }
                Check(exceptionCaught, "C4: Controlled exception was caught");

                // Followed by clean normal attack
                shouldThrow = false;
                float hpBeforeCleanAttack = eStatus.CurrentHp;
                bool cleanAttackSuccess = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    pContext, eCombat, ElementType.Fire, 1f, null, out WBH_DamageResult cleanResult, DamageCause.Direct, 401u);
                Check(cleanAttackSuccess, "C4: Clean attack succeeds after exception recovery");
                Check(cleanResult.AttackId == 401u, "C4: Clean attack has AttackId 401");
                Check(Mathf.Approximately(eStatus.CurrentHp, hpBeforeCleanAttack - 30f), "C4: Queued damage was purged after exception");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pInst);
                UnityEngine.Object.DestroyImmediate(pInstB);
                UnityEngine.Object.DestroyImmediate(eInst);

            }

            // =========================================================================
            // Section D: Multi-player Kill Attribution (A damages -> B kills with Effect/DoT)
            // =========================================================================
            var pA = UnityEngine.Object.Instantiate(pPrefab);
            var pB = UnityEngine.Object.Instantiate(pPrefab);
            var eInstD = UnityEngine.Object.Instantiate(ePrefab);
            try
            {
                void InitPlayer(GameObject go, string name)
                {
                    go.name = name;
                    var pContext = go.GetComponent<PlayerContext>();
                    var ctrlD = pContext.Controller;
                    var pStatusD = go.GetComponent<WBH_PlayerStatus>();
                    var statMgrD = go.GetComponent<PlayerStatManager>();
                    var healthMgrD = go.GetComponent<PlayerHealthManager>();
                    var lvlD = go.GetComponent<PlayerLevelManager>();
                    var shopStateD = go.GetComponent<NetworkShopPlayerState_MirrorTest>();
                    var netId = go.GetComponent<NetworkIdentity>();
                    typeof(NetworkIdentity).GetProperty("isServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(netId, true);
                    typeof(NetworkBehaviour).GetProperty("netIdentity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(go.GetComponent<ItemTriggerManager_MirrorTest>(), netId);

                    typeof(T_PlayerController).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ctrlD, pStatusD);
                    pStatusD.Initialize(ctrlD);
                    lvlD?.Load();
                    var statD = statMgrD.EnsureInitialized();
                    statD.Recalculate(new StatSet { maxHealthFlat = 1000f, attackPowerFlat = 50f }, StatSet.Zero, StatSet.Zero, StatSet.Zero);
                    typeof(PlayerHealthManager).GetField("statManager", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(healthMgrD, statMgrD);
                    healthMgrD.RefreshMaxHealth();
                    healthMgrD.FillHealth();
                    typeof(NetworkShopPlayerState_MirrorTest).GetField("context", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(shopStateD, pContext);
                }

                InitPlayer(pA, "PlayerA");
                InitPlayer(pB, "PlayerB");

                var ctxA = pA.GetComponent<PlayerContext>();
                var ctxB = pB.GetComponent<PlayerContext>();

                var eCombatD = eInstD.GetComponent<WBH_EnemyController>();
                var eStatusD = eInstD.GetComponent<WBH_EnemyStatus>();
                var eAuthD = eInstD.GetComponent<NetworkEnemyAuthority_MirrorTest>();
                var eNetIdD = eInstD.GetComponent<NetworkIdentity>();
                typeof(NetworkIdentity).GetProperty("isServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(eNetIdD, true);

                typeof(WBH_EnemyController).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(eCombatD, eStatusD);
                typeof(NetworkEnemyAuthority_MirrorTest).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(eAuthD, eStatusD);
                typeof(NetworkEnemyAuthority_MirrorTest).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(eAuthD, eCombatD);
                typeof(NetworkEnemyAuthority_MirrorTest).GetField("networkAnimator", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(eAuthD, null);

                WBH_EnemyInfo infoD = eAuthD.EnemyInfo.Clone();
                infoD.maxHP = 100f;
                infoD.defense = 0f;
                infoD.exp = 150;
                infoD.credit = 80;
                eStatusD.Initialize(infoD);
                typeof(NetworkEnemyAuthority_MirrorTest).GetField("enemyInfo", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(eAuthD, infoD);

                eStatusD.OnDamaged += (res) => typeof(NetworkEnemyAuthority_MirrorTest).GetMethod("HandleDamaged", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(eAuthD, new object[] { res });

                // Equip OnKill effect item on both players
                var effectKill = ScriptableObject.CreateInstance<TriggeredBuffUniqueEffectSO>();
                effectKill.name = "Effect_OnKill";
                effectKill.triggerCondition = TriggerCondition.OnKill;
                effectKill.buffSpec = new BuffSpec { duration = 10f, stackBehavior = BuffStackBehavior.Stack, maxStack = 10 };

                var defKill = ScriptableObject.CreateInstance<ItemDefinitionSO>();
                defKill.uniqueEffect = effectKill;
                defKill.mainOptions = Array.Empty<FixedStatValue>();
                var itemKillA = new ItemInstance { instanceId = "kill_A", definition = defKill };
                var itemKillB = new ItemInstance { instanceId = "kill_B", definition = defKill };

                var eqDictA = (System.Collections.Generic.Dictionary<EquipSlotType, InventoryItem>)
                    typeof(EquipmentSystem).GetField("equippedItems", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ctxA.Equipment);
                var eqDictB = (System.Collections.Generic.Dictionary<EquipSlotType, InventoryItem>)
                    typeof(EquipmentSystem).GetField("equippedItems", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ctxB.Equipment);

                eqDictA[EquipSlotType.Weapon] = new InventoryItem(itemKillA);
                eqDictB[EquipSlotType.Weapon] = new InventoryItem(itemKillB);

                // Initial baseline
                Check(ctxA.Stats.CurrentExp == 0 && ctxA.Wallet.Gold == 0, "D0: Attacker A initial zero exp and gold");
                Check(ctxB.Stats.CurrentExp == 0 && ctxB.Wallet.Gold == 0, "D0: Attacker B initial zero exp and gold");

                // D1: Attacker A deals 50 Direct damage first
                bool hitA = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    ctxA, eCombatD, ElementType.Fire, 1f, null, out WBH_DamageResult resA, DamageCause.Direct, 501u);
                Check(hitA, "D1: Attacker A hit processed");
                Check(resA.DamageCause == DamageCause.Direct && resA.AttackId == 501u, "D1: DamageResult has Direct and AttackId 501");
                Check(Mathf.Approximately(eStatusD.CurrentHp, 50f) && !eStatusD.IsDead, "D1: Target HP 50 and alive");
                var lastAttackerA = typeof(NetworkEnemyAuthority_MirrorTest).GetField("lastAttackerContext", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(eAuthD);
                Check(lastAttackerA == ctxA, "D1: Last attacker context is Player A");

                // D2: Attacker B deals 50 Effect damage (lethal hit)
                bool hitB = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
                    ctxB, eCombatD, ElementType.Fire, 1f, null, out WBH_DamageResult resB, DamageCause.Effect, 502u);
                Check(hitB, "D2: Attacker B hit processed");
                Check(resB.DamageCause == DamageCause.Effect && resB.AttackId == 502u, "D2: DamageResult has Effect and AttackId 502");

                Check(eStatusD.IsDead, "D2: Target is dead after Attacker B lethal hit");
                var lastAttackerB = typeof(NetworkEnemyAuthority_MirrorTest).GetField("lastAttackerContext", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(eAuthD);
                Check(lastAttackerB == ctxB, "D2: Last attacker context updated to Player B");

                // D3: GrantKillRewardOnce called
                typeof(NetworkEnemyAuthority_MirrorTest).GetMethod("GrantKillRewardOnce", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(eAuthD, null);

                // Attacker B alone receives Gold, EXP, and OnKill buff:
                Check(ctxB.Wallet.Gold == 80, "D3: Attacker B received 80 Gold");
                Check(ctxB.Stats.CurrentLevel > 1 || ctxB.Stats.CurrentExp > 0, "D3: Attacker B received EXP");
                Check(ctxB.Buffs.ActiveBuffs.Count == 1 && ctxB.Buffs.ActiveBuffs[0].source == effectKill, "D3: Attacker B received OnKill buff");

                // Attacker A received ZERO rewards:
                Check(ctxA.Wallet.Gold == 0, "D3: Attacker A received ZERO Gold");
                Check(ctxA.Stats.CurrentExp == 0 && ctxA.Stats.CurrentLevel == 1, "D3: Attacker A received ZERO EXP");
                Check(ctxA.Buffs.ActiveBuffs.Count == 0, "D3: Attacker A received ZERO OnKill buffs");

                int goldAfterReward = ctxB.Wallet.Gold;
                float expAfterReward = ctxB.Stats.CurrentExp;
                int buffsAfterReward = ctxB.Buffs.ActiveBuffs.Count;
                typeof(NetworkEnemyAuthority_MirrorTest).GetMethod("GrantKillRewardOnce", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(eAuthD, null);
                typeof(NetworkEnemyAuthority_MirrorTest).GetMethod("GrantKillRewardOnce", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(eAuthD, null);
                Check(ctxB.Wallet.Gold == goldAfterReward, "D4: Repeated reward calls do not add Gold");
                Check(Mathf.Approximately(ctxB.Stats.CurrentExp, expAfterReward), "D4: Repeated reward calls do not add EXP");
                Check(ctxB.Buffs.ActiveBuffs.Count == buffsAfterReward, "D4: Repeated reward calls do not add buffs");

                UnityEngine.Object.DestroyImmediate(effectKill);
                UnityEngine.Object.DestroyImmediate(defKill);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pA);
                UnityEngine.Object.DestroyImmediate(pB);
                UnityEngine.Object.DestroyImmediate(eInstD);
            }

            Debug.Log($"[MirrorUniqueEffectP1Validation] PASS {checks} checks. A(버프/효과필터)·B(경계/재진입 거절 및 HP보존)·C(큐 FIFO/사망스킵/예외복구)·D(메타데이터/처치독점 귀속) 전체 검증 완료.");
        }
        finally
        {
            typeof(NetworkServer).GetProperty("active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(null, wasServerActive);
        }
    }

    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, PrivateInstance);
    private static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[MirrorCombatBoundaryValidation] " + message);
    }
}
