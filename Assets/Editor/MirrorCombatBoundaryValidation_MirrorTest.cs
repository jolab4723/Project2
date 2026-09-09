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

    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, PrivateInstance);
    private static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[MirrorCombatBoundaryValidation] " + message);
    }
}
