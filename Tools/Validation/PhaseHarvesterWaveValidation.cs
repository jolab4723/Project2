using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ItemSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>SW 수정: Assets 밖 Editor run_script에서 실제 Fighter·적 프리팹으로 A1을 검사한다. 운영 런타임 진입점과 저장 데이터는 추가하지 않는다.</summary>
public static class PhaseHarvesterWaveValidation
{
    /// <summary>SW 수정: Editor에서 Dirty Scene·Prefab을 저장하지 않고 정식 A1 싱글 검증 맵을 연다. 검증 뒤 RestoreScene으로 Start 씬을 복원한다.</summary>
    public static string OpenSingleScene()
    {
        Require(!Application.isPlaying, "먼저 Play Mode를 종료해야 합니다.");
        Require(PrefabStageUtility.GetCurrentPrefabStage() == null, "Prefab Stage가 열려 있습니다.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Require(!SceneManager.GetSceneAt(i).isDirty, "Dirty Scene을 저장하거나 닫지 않습니다.");
        EditorSceneManager.OpenScene("Assets/Scenes/Maps/Act1_Maps/Act1_Stage1/Act1_Stage1.unity");
        return SceneManager.GetActiveScene().path;
    }

    /// <summary>SW 수정: 싱글 실제 스탯·피해 이벤트에서 치명타 100% 기본 공격과 치명타 없는 무속성 Effect 파동을 구분하며 임시 버프를 해제한다.</summary>
    public static string RunSingleNoCritical()
    {
        Require(Application.isPlaying && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active, "Single Play Mode가 필요합니다.");
        var context = Object.FindFirstObjectByType<T_PlayerController>().GetComponent<PlayerContext>();
        ItemInstance item = EquipWave(context);
        Require(context.Effects.GetRemainingCooldown(item) == 0f, "이전 파동 쿨다운이 남아 있습니다.");
        var owned = new List<GameObject>();
        Vector3 forward = context.transform.forward; forward.y = 0f; forward.Normalize();
        WBH_EnemyController first = SpawnEnemy(context.transform.position + forward * 1.5f, 1f, owned);
        WBH_EnemyController living = SpawnEnemy(context.transform.position + forward * 4f, 100000f, owned);
        WBH_DamageResult firstResult = default, waveResult = default;
        int waveHits = 0;
        Action<WBH_DamageResult> onFirst = result => firstResult = result;
        Action<WBH_DamageResult> onWave = result => { waveResult = result; waveHits++; };
        first.GetComponent<WBH_EnemyStatus>().OnDamaged += onFirst;
        living.GetComponent<WBH_EnemyStatus>().OnDamaged += onWave;
        var buff = ScriptableObject.CreateInstance<BuffDefinitionSO>();
        buff.name = "A1 critical validation";
        buff.duration = 0f;
        buff.statEffects = new[] { new FixedStatValue { statType = StatType.critRateFlat, value = 100f } };
        var buffs = context.GetComponent<PlayerBuffManager>();
        try
        {
            buffs.ApplyBuff(buff);
            Physics.SyncTransforms();
            context.Effects.SetDirectTargets(910030, new WBH_ICombat[] { first }, forward, true);
            Require(PlayerDamageResolver.TryProcessPlayerDamage(context, first, ElementType.Fire, 1000f, null,
                out _, DamageCause.Direct, 910030), "치명타 검사 피해 실패");
            Require(firstResult.IsCritical && waveHits == 1 && !waveResult.IsCritical &&
                waveResult.ElementType == ElementType.None && waveResult.DamageCause == DamageCause.Effect &&
                waveResult.AttackId == 910030 && waveResult.FinalDamage > 0f,
                "파동의 비치명타·무속성·Effect·AttackId 계약 불일치");
            return JsonConvert.SerializeObject(new { firstCritical = firstResult.IsCritical, waveCritical = waveResult.IsCritical,
                waveHits, damage = waveResult.FinalDamage, cause = waveResult.DamageCause.ToString(), element = waveResult.ElementType.ToString(), waveResult.AttackId });
        }
        finally
        {
            context.Effects.SetDirectTargets(0, null);
            buffs.RemoveBuff(buff); Object.Destroy(buff);
            first.GetComponent<WBH_EnemyStatus>().OnDamaged -= onFirst;
            living.GetComponent<WBH_EnemyStatus>().OnDamaged -= onWave;
            var pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
            foreach (GameObject obj in owned.Distinct())
                if (obj != null && obj.activeSelf) pool.Return(obj.GetComponent<WBH_EnemyController>());
        }
    }

    /// <summary>SW 수정: 실제 싱글 처치 뒤 잠시 느린 시간으로 HUD·파동 화면 검증 기회를 남긴다. 캡처 직후 Play를 종료하며 씬은 저장하지 않는다.</summary>
    public static async Task<string> PrepareSingleVisual()
    {
        Require(Application.isPlaying && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active, "Single Play Mode가 필요합니다.");
        var context = Object.FindFirstObjectByType<T_PlayerController>().GetComponent<PlayerContext>();
        ItemInstance item = EquipWave(context);
        Require(context.Effects.GetRemainingCooldown(item) == 0f, "이전 파동 쿨다운이 남아 있습니다.");
        Vector3 forward = context.transform.forward; forward.y = 0f; forward.Normalize();
        var owned = new List<GameObject>();
        WBH_EnemyController first = SpawnEnemy(context.transform.position + forward * 1.5f, 1f, owned);
        SpawnEnemy(context.transform.position + forward * 4f, 100000f, owned);
        Time.timeScale = 0.01f;
        Physics.SyncTransforms();
        context.Effects.SetDirectTargets(910020, new WBH_ICombat[] { first }, forward, true);
        Require(PlayerDamageResolver.TryProcessPlayerDamage(context, first, ElementType.None, 1000f, null,
            out _, DamageCause.Direct, 910020), "화면 검증 처치 실패");
        context.Effects.SetDirectTargets(0, null);
        await Task.Delay(100);
        var slots = Object.FindObjectsByType<CooldownIconSlot>(FindObjectsSortMode.None)
            .Where(slot => slot.gameObject.activeInHierarchy).ToArray();
        var lines = context.GetComponentsInChildren<LineRenderer>()
            .Where(line => line.name == "Phase Harvester Wave Presentation").ToArray();
        Require(slots.Length > 0 && lines.Length == 1 && lines[0].sharedMaterial != null,
            "실제 HUD 슬롯 또는 단일 파동 표시 누락");
        slots[0].OnPointerEnter(null);
        return JsonConvert.SerializeObject(new { slots = slots.Length, lines = lines.Length,
            icon = slots[0].GetComponentsInChildren<UnityEngine.UI.Image>().Any(image => image.sprite == item.definition.icon),
            texts = Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None)
                .Where(text => text.gameObject.activeInHierarchy && (text.text.Contains("처형") || text.text.Contains("재사용")))
                .Select(text => text.text).ToArray(), cooldown = context.Effects.GetRemainingCooldown(item) });
    }

    /// <summary>SW 수정: 실제 싱글 기본 공격으로 같은 공격의 두 처치·중복 Collider·벽·Skill 출처를 검사하고 풀 객체를 반환한다.</summary>
    public static async Task<string> RunSingleBoundaries()
    {
        Require(Application.isPlaying && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active, "Single Play Mode가 필요합니다.");
        var player = Object.FindFirstObjectByType<T_PlayerController>();
        PlayerContext context = player.GetComponent<PlayerContext>();
        ItemInstance item = EquipWave(context);
        var owned = new List<GameObject>();
        GameObject extraCollider = null, wall = null;
        int waves = 0;
        Action<Vector3, Vector3, float> onWave = (_, _, _) => waves++;
        context.Effects.PhaseHarvesterPresented += onWave;
        Vector3 origin = player.transform.position;
        Vector3 forward = player.transform.forward; forward.y = 0f; forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        try
        {
            WBH_EnemyController first = SpawnEnemy(origin + forward * 1.5f - right * 0.6f, 1f, owned);
            WBH_EnemyController second = SpawnEnemy(origin + forward * 1.5f + right * 0.6f, 1f, owned);
            Physics.SyncTransforms();
            player.GetComponent<T_PlayerCombat>().TryAttack(origin + forward * 5f);
            for (int frame = 0; frame < 20 && (!first.Status.IsDead || !second.Status.IsDead); frame++)
                await Task.Delay(100);
            Require(first.Status.IsDead && second.Status.IsDead && waves == 1,
                $"실제 기본 공격의 두 처치 불일치: first={first.Status.CurrentHp}, second={second.Status.CurrentHp}, waves={waves}");
            await Task.Delay(1100);
            first = SpawnEnemy(origin + forward * 1.5f, 1f, owned);
            WBH_EnemyController living = SpawnEnemy(origin + forward * 4f, 100000f, owned);
            extraCollider = new GameObject("A1 duplicate body collider");
            extraCollider.layer = 10;
            extraCollider.transform.SetParent(living.transform, false);
            extraCollider.AddComponent<SphereCollider>().radius = 0.5f;
            float before = living.Status.CurrentHp;
            Physics.SyncTransforms();
            context.Effects.SetDirectTargets(910010, new WBH_ICombat[] { first }, forward, true);
            Require(PlayerDamageResolver.TryProcessPlayerDamage(context, first, ElementType.None, 1000f, null,
                out _, DamageCause.Direct, 910010), "중복 Collider 검사 피해 실패");
            context.Effects.SetDirectTargets(0, null);
            float withDuplicate = before - living.Status.CurrentHp;
            Require(withDuplicate > 0f && waves == 2, "중복 Collider 대상 피해 누락");
            Object.Destroy(extraCollider); extraCollider = null;
            await Task.Delay(1100);
            first = SpawnEnemy(origin + forward * 1.5f, 1f, owned);
            before = living.Status.CurrentHp;
            context.Effects.SetDirectTargets(910011, new WBH_ICombat[] { first }, forward, true);
            Require(PlayerDamageResolver.TryProcessPlayerDamage(context, first, ElementType.None, 1000f, null,
                out _, DamageCause.Direct, 910011), "단일 Collider 검사 피해 실패");
            context.Effects.SetDirectTargets(0, null);
            Require(Mathf.Abs((before - living.Status.CurrentHp) - withDuplicate) < 0.01f, "Collider 수에 따른 중복 피해");
            await Task.Delay(1100);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "A1 wall validation"; wall.layer = LayerMask.NameToLayer("Wall");
            wall.transform.SetPositionAndRotation(origin + forward * 2.5f + Vector3.up, Quaternion.LookRotation(forward));
            wall.transform.localScale = new Vector3(3f, 2f, 0.2f);
            first = SpawnEnemy(origin + forward * 1.5f, 1f, owned);
            before = living.Status.CurrentHp;
            Physics.SyncTransforms();
            context.Effects.SetDirectTargets(910012, new WBH_ICombat[] { first }, forward, true);
            Require(PlayerDamageResolver.TryProcessPlayerDamage(context, first, ElementType.None, 1000f, null,
                out _, DamageCause.Direct, 910012), "벽 검사 피해 실패");
            context.Effects.SetDirectTargets(0, null);
            Require(living.Status.CurrentHp == before && waves == 4 && context.Effects.GetRemainingCooldown(item) > 0f,
                "벽 뒤 피해 또는 대상 0체일 때 쿨다운 누락");
            Object.Destroy(wall); wall = null;
            await Task.Delay(1100);
            first = SpawnEnemy(origin + forward * 1.5f, 1f, owned);
            context.Effects.SetDirectTargets(910013, new WBH_ICombat[] { first }, forward, true);
            Require(PlayerDamageResolver.TryProcessPlayerDamage(context, first, ElementType.None, 1000f, null,
                out _, DamageCause.Skill, 910013), "Skill 처치 실패");
            context.Effects.SetDirectTargets(0, null);
            Require(waves == 4 && context.Effects.GetRemainingCooldown(item) == 0f, "Skill 처치 재발동");
            return JsonConvert.SerializeObject(new { waves, withDuplicate, tests = "actual basic multi-kill once, collider dedupe, wall, zero target CD, Skill no trigger" });
        }
        finally
        {
            context.Effects.SetDirectTargets(0, null);
            context.Effects.PhaseHarvesterPresented -= onWave;
            if (wall != null) Object.Destroy(wall);
            if (extraCollider != null) Object.Destroy(extraCollider);
            var pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
            foreach (GameObject obj in owned.Distinct())
                if (obj != null && obj.activeSelf) pool.Return(obj.GetComponent<WBH_EnemyController>());
        }
    }

    /// <summary>SW 수정: Play Mode 실제 싱글 객체와 검증 선행 상태를 읽는다. 네트워크 권한이나 데이터는 바꾸지 않는다.</summary>
    public static string InspectSingle()
        => JsonConvert.SerializeObject(new
        {
            Application.isPlaying,
            players = Object.FindObjectsByType<T_PlayerController>(FindObjectsSortMode.None)
                .Select(p => new { p.name, position = p.transform.position.ToString(), components = p.GetComponents<MonoBehaviour>().Where(c => c != null).Select(c => c.GetType().Name).ToArray() }).ToArray(),
            provider = Object.FindFirstObjectByType<WBH_EnemyDataProvider>() != null,
            pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>() != null,
            inventory = InventoryController.Instance != null,
        });

    /// <summary>SW 수정: 실제 싱글의 장착 거래·처치·후속 피해·쿨다운·표시를 검사하며 검증용 적과 장애물은 항상 제거한다.</summary>
    public static async Task<string> RunSingle()
    {
        Require(Application.isPlaying && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active,
            "실제 Single Play Mode가 필요합니다.");
        var player = Object.FindFirstObjectByType<T_PlayerController>();
        Require(player != null && player.GetComponent<FighterSkillController>() != null, "실제 Fighter 플레이어가 필요합니다.");
        PlayerContext context = player.GetComponent<PlayerContext>();
        Require(context != null && context.BindSinglePlayerInventory(InventoryController.Instance), "실제 싱글 인벤토리 연결 실패");
        await Task.Delay(100);
        ItemInstance waveItem = EquipWave(context);
        Require(player.GetComponent<UniqueEffectPresentation>() != null, "실제 싱글 Presenter 없음");
        var owned = new List<GameObject>();
        int waves = 0;
        Vector3 presentedStart = default, presentedEnd = default;
        Action<Vector3, Vector3, float> onWave = (start, end, width) =>
        {
            waves++; presentedStart = start; presentedEnd = end;
            Require(Mathf.Approximately(width, 2f), "파동 전체 폭 불일치");
        };
        context.Effects.PhaseHarvesterPresented += onWave;
        try
        {
            Vector3 origin = player.transform.position;
            Vector3 forward = player.transform.forward; forward.y = 0f; forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            WBH_EnemyController first = SpawnEnemy(origin + forward * 1.5f, 1f, owned);
            var living = new List<WBH_EnemyController>();
            for (int i = 0; i < 7; i++)
                living.Add(SpawnEnemy(origin + forward * (3f + i * 0.5f), 100000f, owned));
            WBH_EnemyController outside = SpawnEnemy(origin + forward * 4f + right * 1.2f, 100000f, owned);
            WBH_EnemyController high = SpawnEnemy(origin + forward * 4f + Vector3.up * 0.6f, 100000f, owned);
            Physics.SyncTransforms();
            float[] before = living.Select(enemy => enemy.Status.CurrentHp).ToArray();
            float outsideBefore = outside.Status.CurrentHp, highBefore = high.Status.CurrentHp;
            // SW 수정: 실제 기본 공격의 확정 대상·정면 경계를 사용하고 실제 적 TakeDamage를 공통 Resolver로 실행한다.
            context.Effects.SetDirectTargets(910001, new WBH_ICombat[] { first }, forward, fighterAttack: true);
            var request = new WBH_DamageRequest(context.Controller, first, WBH_AttackType.Normal,
                ElementType.None, 1000f, null, null, null, -right, DamageCause.Direct, 910001);
            Require(PlayerDamageResolver.TryProcessPlayerDamage(context, request, out _), "첫 기본 피해 처리 실패");
            context.Effects.SetDirectTargets(0, null);
            int hitCount = living.Select((enemy, i) => enemy.Status.CurrentHp < before[i]).Count(hit => hit);
            Require(waves == 1 && hitCount == 6, $"파동/최대 대상 불일치: waves={waves}, hits={hitCount}");
            Require(Mathf.Abs(outside.Status.CurrentHp - outsideBefore) < 0.01f, "통로 바깥 적중");
            Require(Mathf.Abs(high.Status.CurrentHp - highBefore) < 0.01f, "0.5m 초과 단차 적중");
            Require(Vector3.Distance(presentedStart, origin + forward * 1.5f + Vector3.up) < 0.1f &&
                Vector3.Dot((presentedEnd - presentedStart).normalized, forward) > 0.99f,
                "사망 위치 또는 실제 공격 정면 불일치");
            Require(context.Effects.GetRemainingCooldown(waveItem) > 0f, "소유자 쿨다운 누락");
            WBH_EnemyController second = SpawnEnemy(origin + forward * 1.5f, 1f, owned);
            context.Effects.SetDirectTargets(910002, new WBH_ICombat[] { second }, forward, fighterAttack: true);
            Require(PlayerDamageResolver.TryProcessPlayerDamage(context, second, ElementType.None, 1000f, null,
                out _, DamageCause.Direct, 910002), "쿨다운 중 기본 피해 실패");
            context.Effects.SetDirectTargets(0, null);
            Require(waves == 1, "쿨다운 중 재발동");
            context.Effects.ResetAttackLifetime();
            Require(context.Effects.GetRemainingCooldown(waveItem) > 0f, "수명 초기화로 쿨다운 우회");
            await Task.Delay(1100);
            WBH_EnemyController effectKill = SpawnEnemy(origin + forward * 1.5f, 1f, owned);
            context.Effects.SetDirectTargets(910003, new WBH_ICombat[] { effectKill }, forward, fighterAttack: true);
            Require(PlayerDamageResolver.TryProcessPlayerDamage(context, effectKill, ElementType.None, 1000f, null,
                out _, DamageCause.Effect, 910003), "Effect 처치 처리 실패");
            context.Effects.SetDirectTargets(0, null);
            Require(waves == 1 && context.Effects.GetRemainingCooldown(waveItem) == 0f, "Effect 처치 재발동");
            return JsonConvert.SerializeObject(new { waves, hitCount, tests = "death origin/forward, max6, corridor, elevation, owner CD, lifetime reset, Effect no trigger, real equipment/prefabs" });
        }
        finally
        {
            context.Effects.SetDirectTargets(0, null);
            context.Effects.PhaseHarvesterPresented -= onWave;
            var pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
            foreach (GameObject obj in owned.Distinct())
                if (obj != null && obj.activeSelf) pool.Return(obj.GetComponent<WBH_EnemyController>());
        }
    }

    /// <summary>SW 수정: 싱글 검증의 실제 장착 거래를 사용하고 기존 무기는 가방에 보존한다.</summary>
    private static ItemInstance EquipWave(PlayerContext context)
    {
        var grid = context.Inventory.PlayerGrid;
        var transaction = new EquipmentTransaction(context.Equipment);
        if (context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance equipped) &&
            equipped?.definition?.uniqueEffect is PhaseHarvesterWaveUniqueEffectSO)
            return equipped;
        if (context.Equipment.TryGetEquippedItem(EquipSlotType.Weapon, out InventoryItem oldWeapon))
        {
            Require(grid.TryFindEmptySpaceForItem(oldWeapon, oldWeapon.isRotated, out InventoryPlacementSnapshot destination), "기존 무기 보관 공간 부족");
            Require(transaction.TryUnequip(EquipSlotType.Weapon, grid, destination).IsSuccess, "기존 무기 해제 실패");
        }
        var definition = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath("db8efb52ecce6db45bf450d075a4f5e2"));
        Require(definition?.uniqueEffect is PhaseHarvesterWaveUniqueEffectSO, "실제 A1 아이템 데이터 없음");
        ItemInstance instance = ItemDataCreator.CreateItemData(definition);
        InventoryAddResultData added = context.Inventory.TryAddItemData(instance);
        Require(added.Result == InventoryAddResult.Success, "실제 아이템 추가 실패");
        InventoryItem item = grid.GetItemAt(added.X, added.Y);
        Require(transaction.TryEquip(grid, item, InventoryPlacementSnapshot.Capture(grid, item), EquipSlotType.Weapon).IsSuccess, "장착 거래 실패");
        return instance;
    }

    /// <summary>SW 수정: 정식 Provider의 EnemyInfo 복제본에서 검증용 HP만 정하고 실제 싱글 적 프리팹을 초기화한다.</summary>
    private static WBH_EnemyController SpawnEnemy(Vector3 position, float hp, List<GameObject> owned)
    {
        var provider = Object.FindFirstObjectByType<WBH_EnemyDataProvider>();
        var pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
        var effects = Object.FindFirstObjectByType<WBH_EffectSpawner>();
        var projectiles = Object.FindFirstObjectByType<WBH_ProjectileSpawner>();
        Require(provider != null && pool != null && effects != null && projectiles != null, "정식 Provider/Pool/Spawner 없음");
        var floatText = Object.FindFirstObjectByType<WBH_FloatTextPoolManager>();
        var eliteView = Object.FindFirstObjectByType<WBH_HighEnemyHpbarView>();
        Require(floatText != null && eliteView != null, "정식 적 View 표시 참조 없음");
        Require(provider.TryCreateEnemyInfo("enemy.normal.melee.working_machine", new WBH_EnemyStatContext(1, "normal", 1), out WBH_EnemyInfo info), "정식 EnemyInfo 생성 실패");
        info = info.Clone(); info.maxHP = hp;
        var enemy = pool.Get("enemy.normal.melee.working_machine");
        Require(enemy != null, "정식 적 풀 대여 실패");
        enemy.transform.SetPositionAndRotation(position, Quaternion.identity);
        owned.Add(enemy.gameObject);
        enemy.GetComponent<EnemyKillReward>()?.Initialize(InventoryController.Instance.BoundPlayer.Wallet);
        enemy.GetComponent<WBH_EnemyView>()?.Initialize(floatText, eliteView);
        enemy.Initialize(info, pool, effects, projectiles, YJ_SfxPlayer.Instance);
        enemy.GetComponent<WBH_EnemyMovement>().SetControlEnable(false);
        enemy.gameObject.SetActive(true);
        return enemy;
    }

    /// <summary>SW 수정: 외부 검증에서 계약 위반을 명확히 실패시켜 미검증 결과를 완료로 기록하지 않는다.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>SW 수정: Play 종료 뒤 저장 없이 원래 열린 Start 씬으로 돌아간다.</summary>
    public static string RestoreScene()
    {
        Require(!Application.isPlaying, "Play Mode 종료가 필요합니다.");
        EditorSceneManager.OpenScene("Assets/Scenes/Maps/Basic/Start.unity");
        return SceneManager.GetActiveScene().path;
    }
}
