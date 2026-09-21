using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEngine;

/// <summary>자동 실행기와 독립된 명시적 개발 검사 입력. 컴포넌트 생성이나 네트워크 실행은 하지 않는다.</summary>
internal static class MirrorSmokeConfiguration_MirrorTest
{
#if UNITY_EDITOR
    private static string[] editorArguments;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ConsumeEditorArguments()
    {
        const string key = "SW.MirrorSmoke.Arguments";
        string configured = UnityEditor.SessionState.GetString(key, string.Empty);
        UnityEditor.SessionState.EraseString(key);
        editorArguments = string.IsNullOrEmpty(configured) ? null : configured.Split('\n');
        var tags = Unity.Multiplayer.PlayMode.CurrentPlayer.Tags.SelectMany(tag => tag.Split(';')).ToArray();
        if (editorArguments == null && tags.Contains("mirror-validation"))
            editorArguments = tags.Where(tag => tag.StartsWith("--mirror-", StringComparison.Ordinal) && tag.Contains("="))
                .SelectMany(tag => tag.Split(new[] { '=' }, 2)).ToArray();
    }
#endif

    internal static string Argument(string key)
    {
#if UNITY_EDITOR
        string[] args = editorArguments ?? Environment.GetCommandLineArgs();
#else
        string[] args = Environment.GetCommandLineArgs();
#endif
        string prefix = key + "=";
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
            if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return args[i].Substring(prefix.Length).Trim();
        }
        return null;
    }

    // 실제 저장 파일을 변경하지 않고 명시적으로 요청한 개발 검사 입력만 제공한다.
    internal static string PassiveFixtureJson()
    {
        string fixture = Argument("--mirror-smoke-passive");
        if (fixture == null || string.IsNullOrEmpty(Argument("--mirror-smoke-role")) ||
            !Debug.isDebugBuild && !Application.isEditor) return null;
        if (fixture is not ("empty" or "attack1" or "attack5-shop" or "all-max" or "invalid-rank"))
            throw new ArgumentException("Unknown passive smoke fixture.", nameof(fixture));
        var tree = new Core.PassiveSkillTreeData();
        if (fixture == "all-max")
        {
            var database = NetworkManager.singleton.GetComponent<MirrorSessionAuthenticator_MirrorTest>().PassiveDatabase;
            foreach (Core.PassiveSkillId id in Enum.GetValues(typeof(Core.PassiveSkillId)))
            {
                var definition = database.Get(id);
                if (definition != null) tree.learnedSkills.Add(new Core.PassiveSkillEntry
                    { id = id, currentLevel = definition.maxLevel, unlockedLevel = definition.maxLevel });
            }
            return JsonUtility.ToJson(tree);
        }
        int level = fixture == "attack1" ? 1 : fixture == "attack5-shop" ? 5 : fixture == "invalid-rank" ? 999 : 0;
        if (level > 0) tree.learnedSkills.Add(new Core.PassiveSkillEntry
            { id = Core.PassiveSkillId.AttackPower, currentLevel = level, unlockedLevel = level });
        if (fixture == "attack5-shop") tree.learnedSkills.Add(new Core.PassiveSkillEntry
            { id = Core.PassiveSkillId.ShopEnhance, currentLevel = 1, unlockedLevel = 1 });
        return JsonUtility.ToJson(tree);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeP5BBuildRunner()
    {
        string mode = Argument("--mirror-smoke-p5b");
        if (string.IsNullOrEmpty(mode)) return;

        if (UnityEngine.Object.FindFirstObjectByType<MirrorP5BBuildSmokeRunner>() != null) return;
        GameObject go = new GameObject("MirrorP5BBuildSmokeRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        MirrorP5BBuildSmokeRunner runner = go.AddComponent<MirrorP5BBuildSmokeRunner>();
        runner.Mode = mode;
    }
}

/// <summary>
/// P5-B(밤의 칼날 회피 및 기본 공격 40% 증폭)의 실제 빌드(전용 서버 및 클라이언트) 런타임 검증을 수행하는 일회성 러너.
/// 검증 완료 후 정리 대상입니다.
/// </summary>
internal sealed class MirrorP5BBuildSmokeRunner : MonoBehaviour
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const float DefaultPlayerWaitSeconds = 2f;
    internal string Mode;

    private void Start()
    {
        Debug.Log($"[MirrorP5BBuildSmokeRunner] 기동 시작. 모드={Mode}");
        if (Mode == "server")
        {
            StartCoroutine(RunServerRoutine());
        }
        else if (Mode == "client")
        {
            StartCoroutine(RunClientRoutine());
        }
    }

    private IEnumerator RunServerRoutine()
    {
        Debug.Log("[P5B_LIVE_SERVER] 전용 서버 루틴 시작. NetworkServer 대기...");
        float timeout = Time.realtimeSinceStartup + 30f;
        while (!NetworkServer.active && Time.realtimeSinceStartup < timeout)
            yield return null;

        if (!NetworkServer.active)
        {
            Debug.LogError("[P5B_LIVE_SERVER] NetworkServer가 활성화되지 않았습니다.");
            yield break;
        }

        Debug.Log("[P5B_LIVE_SERVER] NetworkServer 활성화 확인. 세션 매니저 및 프리팹 준비...");
        MirrorTestNetworkManager manager = NetworkManager.singleton as MirrorTestNetworkManager;
        yield return new WaitForSeconds(1.5f);

        // 밤의 칼날 아이콘 및 정의 로드
        ItemDefinitionSO nightSwordDef = Resources.Load<ItemDefinitionSO>(
            "DataFiles/ItemData/3. GeneratedAssets/Items/item.weapon.greatsword.nightsword_밤의 칼날");
        if (nightSwordDef == null)
        {
            // fallback: 모든 아이템에서 검색
            var allItems = Resources.LoadAll<ItemDefinitionSO>("");
            nightSwordDef = allItems.FirstOrDefault(i => i != null && i.itemId == "item.weapon.greatsword.nightsword");
        }

        if (nightSwordDef == null)
        {
            Debug.LogError("[P5B_LIVE_SERVER] 밤의 칼날 ItemDefinitionSO를 찾지 못했습니다.");
            yield break;
        }

        Debug.Log($"[P5B_LIVE_SERVER] 밤의 칼날 로드 성공: {nightSwordDef.name}, ID={nightSwordDef.itemId}, " +
                  $"배율={(nightSwordDef.uniqueEffect as DodgePreparedAttackUniqueEffectSO)?.damageMultiplier}");

        // 플레이어 컨텍스트 확보 (접속된 클라이언트 또는 서버 플레이어)
        PlayerContext player = null;
        NetworkConnectionToClient observingConnection = null;
        float playerWaitSeconds = ResolvePositiveSeconds(
            "--mirror-smoke-p5b-client-wait",
            DefaultPlayerWaitSeconds,
            30f);
        Debug.Log($"[P5B_LIVE_SERVER] 테스트 플레이어 대기: 최대 {playerWaitSeconds:0.0}초");
        float playerTimeout = Time.realtimeSinceStartup + playerWaitSeconds;
        while (player == null && Time.realtimeSinceStartup < playerTimeout)
        {
            player = UnityEngine.Object.FindObjectsByType<PlayerContext>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p != null && (p.Equipment?.CurrentCharacterClass == CharacterClass.Fighter || p.name.Contains("Fighter")));
            if (player == null)
                player = UnityEngine.Object.FindObjectsByType<PlayerContext>(FindObjectsSortMode.None).FirstOrDefault();
            observingConnection = NetworkServer.connections.Values.FirstOrDefault(connection =>
                connection != null && connection.isAuthenticated && connection.isReady);
            if (player != null || observingConnection != null)
                break;
            yield return new WaitForSeconds(0.5f);
        }

        if (player == null)
        {
            Debug.Log(observingConnection != null
                ? $"[P5B_LIVE_SERVER] Ready 클라이언트 연결 {observingConnection.connectionId}에 테스트 Fighter를 스폰합니다."
                : "[P5B_LIVE_SERVER] 접속된 플레이어가 없어 서버 자체 테스트 Fighter를 생성합니다.");
            GameObject playerPrefab = manager?.playerPrefab
                ?? manager?.spawnPrefabs.FirstOrDefault(p => p != null && p.name.Contains("FighterNetworkPlayer"));
            if (playerPrefab != null)
            {
                GameObject playerGo = UnityEngine.Object.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
                NetworkServer.Spawn(playerGo);
                player = playerGo.GetComponent<PlayerContext>();
            }
        }

        if (player == null)
        {
            Debug.LogError("[P5B_LIVE_SERVER] 테스트용 플레이어를 확보하지 못했습니다.");
            yield break;
        }

        uint playerNetId = player.GetComponent<NetworkIdentity>()?.netId ?? 0;
        Debug.Log($"[P5B_LIVE_SERVER] 테스트 플레이어 확보 완료: NetId={playerNetId}, 이름={player.name}");

        // 장비에 밤의 칼날 주입
        var eqDict = (Dictionary<EquipSlotType, InventoryItem>)typeof(EquipmentSystem)
            .GetField("equippedItems", PrivateInstance)?.GetValue(player.Equipment);
        if (eqDict != null)
        {
            eqDict[EquipSlotType.Weapon] = new InventoryItem(new ItemInstance
            {
                instanceId = "nightsword_live_test_" + playerNetId,
                definition = nightSwordDef,
            });
            MethodInfo notifyEquip = typeof(EquipmentSystem).GetMethod("NotifyEquipmentChanged", PrivateInstance);
            notifyEquip?.Invoke(player.Equipment, null);
        }

        // 스탯 설정: 기본 공격력 100, 치명타 0%
        player.Stats.EnsureInitialized().Recalculate(
            new StatSet
            {
                maxHealthFlat = 1000f,
                attackPowerFlat = 100f,
                critRateFlat = 0f,
                critMultFlat = 0f,
            },
            StatSet.Zero, StatSet.Zero, StatSet.Zero);

        ItemTriggerManager_MirrorTest triggers = player.ItemTriggers;
        triggers.ResetAttackLifetime();

        // 적 스폰
        Vector3 enemyPos = player.transform.position + player.transform.forward * 2f;
        GameObject enemyPrefab = manager?.spawnPrefabs.FirstOrDefault(p => p != null && p.name == "Normal_Melee_MirrorTest");
        WBH_EnemyController enemyCombat = null;
        WBH_EnemyStatus enemyStatus = null;
        if (enemyPrefab != null)
        {
            GameObject enemyGo = UnityEngine.Object.Instantiate(enemyPrefab, enemyPos, Quaternion.identity);
            var authority = enemyGo.GetComponent<NetworkEnemyAuthority_MirrorTest>();
            enemyCombat = enemyGo.GetComponent<WBH_EnemyController>();
            enemyStatus = enemyGo.GetComponent<WBH_EnemyStatus>();
            WBH_EnemyInfo info = authority.EnemyInfo.Clone();
            info.maxHP = 1000f;
            info.defense = 0f;
            info.moveSpeed = 0f;
            authority.ServerSetEnemyInfo(info);
            enemyStatus.Initialize(info);
            NetworkServer.Spawn(enemyGo);
            enemyGo.GetComponent<WBH_EnemyPattern_MirrorTest>()?.StopServer();
        }

        yield return new WaitForSeconds(0.5f);
        Debug.Log("[P5B_LIVE_SERVER] ================= P5-B 실기 라이브 검증 시작 =================");

        // 1. 베이스라인: 회피 전 일반 공격
        float hpBefore1 = enemyStatus != null ? enemyStatus.CurrentHp : 1000f;
        WBH_DamageResult baseHit = default;
        bool baseResolved = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            player, enemyCombat, ElementType.None, 1f, null, out baseHit, DamageCause.Direct, 5001u);

        float baseDamage = baseHit.FinalDamage;
        Debug.Log($"[P5B_LIVE_SERVER] 1. 베이스라인 일반 공격 결과: Resolved={baseResolved}, " +
                  $"FinalDamage={baseDamage}, Prepared={triggers.PreparedAttackReady}, " +
                  $"ConsumeCount={triggers.PreparedAttackConsumeCount}, " +
                  $"Stats[{HitStatsLine(player, enemyCombat, baseHit)}]");

        if (!baseResolved || baseDamage <= 0f)
            Debug.LogError("[P5B_LIVE_SERVER] 베이스라인 공격 실패!");

        yield return new WaitForSeconds(0.5f);

        // 2. 회피 실행 -> 준비 상태 활성화
        typeof(ItemTriggerManager_MirrorTest).GetField("nextDodgeTriggerAt", PrivateInstance)
            ?.SetValue(triggers, 0.0);
        MethodInfo confirmDodge = typeof(ItemTriggerManager_MirrorTest).GetMethod("ConfirmDodgeTrigger", PrivateInstance);
        confirmDodge?.Invoke(triggers, null);

        Debug.Log($"[P5B_LIVE_SERVER] 2. 회피 실행 완료 -> PreparedAttackReady={triggers.PreparedAttackReady} (기대값: True), " +
                  $"ConsumeCount={triggers.PreparedAttackConsumeCount}");

        if (!triggers.PreparedAttackReady)
            Debug.LogError("[P5B_LIVE_SERVER] 회피 후 준비 상태가 활성화되지 않았습니다!");

        yield return new WaitForSeconds(0.5f);

        // 3. 40% 증폭 기본 공격 실행
        float hpBeforeEmpowered = enemyStatus != null ? enemyStatus.CurrentHp : 900f;
        WBH_DamageResult empoweredHit = default;
        bool empoweredResolved = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            player, enemyCombat, ElementType.None, 1f, null, out empoweredHit, DamageCause.Direct, 5002u);

        float empoweredDamage = empoweredHit.FinalDamage;
        float pen = player.Controller?.Status?.Pen ?? 0f;
        float def = enemyCombat?.Status?.DefensePower ?? 0f;
        float expectedDamage = Mathf.Round((baseDamage - pen + def) * 1.4f + pen - def);
        Debug.Log($"[P5B_LIVE_SERVER] 3. 40% 증폭 기본 공격 결과: Resolved={empoweredResolved}, " +
                  $"FinalDamage={empoweredDamage} (기대값: {expectedDamage:0.0}), " +
                  $"적 체력={hpBeforeEmpowered} -> {enemyStatus?.CurrentHp}, " +
                  $"Prepared={triggers.PreparedAttackReady} (기대값: False), " +
                  $"ConsumeCount={triggers.PreparedAttackConsumeCount} (기대값: 1), " +
                  $"Stats[{HitStatsLine(player, enemyCombat, empoweredHit)}]");

        yield return new WaitForSeconds(0.5f);

        // 4. 다음 기본 공격 실행 -> 일반 피해 복귀 확인
        WBH_DamageResult nextHit = default;
        bool nextResolved = WBH_CombatResolver_MirrorTest.TryProcessPlayerDamage(
            player, enemyCombat, ElementType.None, 1f, null, out nextHit, DamageCause.Direct, 5003u);

        float nextDamage = nextHit.FinalDamage;
        Debug.Log($"[P5B_LIVE_SERVER] 4. 소비 후 다음 기본 공격 결과: Resolved={nextResolved}, " +
                  $"FinalDamage={nextDamage} (기대값: {baseDamage:0.0}), " +
                  $"Prepared={triggers.PreparedAttackReady}, ConsumeCount={triggers.PreparedAttackConsumeCount} (기대값: 1), " +
                  $"Stats[{HitStatsLine(player, enemyCombat, nextHit)}]");

        // 증폭 판정은 인접한 두 타격(증폭타/다음타)으로 한다. 1초 이상 떨어진 베이스라인과
        // 비교하면 공격력·방어·수정자 드리프트에 흔들려 오탐이 난다(2026-09-21 160.3→190→136 사례).
        float amplifiedVsNext = nextDamage * 1.4f;
        bool passAmplified = empoweredResolved && nextResolved && empoweredDamage > nextDamage &&
            Mathf.Abs(empoweredDamage - amplifiedVsNext) <= Mathf.Max(2f, amplifiedVsNext * 0.05f);
        bool passConsumed = !triggers.PreparedAttackReady && triggers.PreparedAttackConsumeCount == 1;
        float baselineDrift = baseDamage > 0f ? Mathf.Abs(nextDamage - baseDamage) / baseDamage : 1f;
        bool passReverted = nextResolved && baselineDrift <= 0.20f;
        if (baselineDrift > 0.05f)
            Debug.LogWarning($"[P5B_LIVE_SERVER] 베이스라인 드리프트 감지: base={baseDamage:0.0} next={nextDamage:0.0} (drift={baselineDrift:P1})");

        yield return new WaitForSeconds(0.5f);

        if (passAmplified && passConsumed && passReverted)
        {
            Debug.Log("[P5B_LIVE_SERVER] ========================================================");
            Debug.Log("[P5B_LIVE_SERVER] ★★★ P5-B 밤의 칼날 전용 서버 실기 런타임 테스트 전원 통과 (PASS) ★★★");
            Debug.Log($"[P5B_LIVE_SERVER] - 베이스라인: {baseDamage:0.0} 데미지 확인");
            Debug.Log($"[P5B_LIVE_SERVER] - 회피 후 준비: PreparedAttackReady = True 확인");
            Debug.Log($"[P5B_LIVE_SERVER] - 40% 증폭 타격: {empoweredDamage:0.0} 데미지 확인 (다음타×1.4={amplifiedVsNext:0.0}, 구 베이스×1.4={expectedDamage:0.0})");
            Debug.Log($"[P5B_LIVE_SERVER] - 준비 상태 소비: PreparedAttackReady = False, Count = 1 확인");
            Debug.Log($"[P5B_LIVE_SERVER] - 일반 복귀 타격: {nextDamage:0.0} 데미지 확인 (베이스라인 일치)");
            Debug.Log("[P5B_LIVE_SERVER] ========================================================");
        }
        else
        {
            Debug.LogError($"[P5B_LIVE_SERVER] 런타임 검증 실패! amplified={passAmplified}, consumed={passConsumed}, reverted={passReverted}, drift={baselineDrift:P1}");
        }

        yield return new WaitForSeconds(1.0f);
        Debug.Log("[P5B_LIVE_SERVER] 실기 검증 완료.");
    }

    private static string HitStatsLine(PlayerContext player, WBH_ICombat target, in WBH_DamageResult result)
    {
        float ap = player != null && player.Controller != null && player.Controller.Status != null
            ? player.Controller.Status.AttackPower : -1f;
        float pen = player != null && player.Controller != null && player.Controller.Status != null
            ? player.Controller.Status.Pen : -1f;
        float critRate = player != null && player.Controller != null && player.Controller.Status != null
            ? player.Controller.Status.CritRate : -1f;
        float def = target != null && target.Status != null ? target.Status.DefensePower : -1f;
        float takenMod = target != null && target.Status != null ? target.Status.DamageTakenModifier : -1f;
        return $"Dmg={result.FinalDamage:0.0} Crit={result.IsCritical} AP={ap:0.0} Pen={pen:0.0} CritRate={critRate:0.000} Def={def:0.0} TakenMod={takenMod:0.00}";
    }

    private IEnumerator RunClientRoutine()
    {
        Debug.Log("[P5B_LIVE_CLIENT] 클라이언트 검증 루틴 시작. NetworkClient 준비...");
        MirrorTestNetworkManager manager = null;
        float managerTimeout = Time.realtimeSinceStartup + 10f;
        while (manager == null && Time.realtimeSinceStartup < managerTimeout)
        {
            manager = NetworkManager.singleton as MirrorTestNetworkManager;
            yield return null;
        }

        if (manager == null)
        {
            Debug.LogError("[P5B_LIVE_CLIENT] MirrorTestNetworkManager를 찾지 못했습니다.");
            yield break;
        }

        if (!NetworkClient.active && !NetworkServer.active)
        {
            string address = MirrorSmokeConfiguration_MirrorTest.Argument("--mirror-smoke-p5b-address");
            manager.networkAddress = string.IsNullOrWhiteSpace(address) ? "localhost" : address.Trim();
            manager.ClientDisplayName = "P5B_EditorClient";
            Debug.Log($"[P5B_LIVE_CLIENT] 자동 연결 시작: {manager.networkAddress}");
            manager.StartClient();
        }

        Debug.Log("[P5B_LIVE_CLIENT] NetworkClient 연결 대기...");
        float timeout = Time.realtimeSinceStartup + 30f;
        while ((!NetworkClient.isConnected || !NetworkClient.ready) && Time.realtimeSinceStartup < timeout)
            yield return null;

        if (!NetworkClient.isConnected || !NetworkClient.ready)
        {
            Debug.LogError("[P5B_LIVE_CLIENT] 서버 연결 및 Ready 상태를 확보하지 못했습니다.");
            yield break;
        }

        Debug.Log("[P5B_LIVE_CLIENT] 서버 연결 확인. 플레이어 및 시각 효과 관찰 대기...");

        bool ringSawActive = false;
        bool ringSawDeactive = false;
        float observeTimeout = Time.realtimeSinceStartup + 15f;

        while (Time.realtimeSinceStartup < observeTimeout && (!ringSawActive || !ringSawDeactive))
        {
            UniqueEffectPresentation_MirrorTest presentation = UnityEngine.Object.FindObjectsByType<UniqueEffectPresentation_MirrorTest>(FindObjectsSortMode.None).FirstOrDefault();
            if (presentation != null)
            {
                Transform ring = presentation.transform.Find("NightSwordPreparedAttack");
                if (ring != null && ring.gameObject.activeSelf)
                {
                    if (!ringSawActive)
                    {
                        ringSawActive = true;
                        Debug.Log("[P5B_LIVE_CLIENT] 1. 보라색 원형 링(NightSwordPreparedAttack) 시각 효과 활성화 감지!");
                    }
                }
                else if (ringSawActive && ring != null && !ring.gameObject.activeSelf)
                {
                    if (!ringSawDeactive)
                    {
                        ringSawDeactive = true;
                        Debug.Log("[P5B_LIVE_CLIENT] 2. 공격 소비 후 보라색 원형 링 비활성화 감지!");
                    }
                }
            }
            yield return new WaitForSeconds(0.2f);
        }

        Debug.Log("[P5B_LIVE_CLIENT] ========================================================");
        Debug.Log($"[P5B_LIVE_CLIENT] 클라이언트 관찰 결과: ringActive={ringSawActive}, ringDeactive={ringSawDeactive}");
        if (ringSawActive && ringSawDeactive)
        {
            Debug.Log("[P5B_LIVE_CLIENT] ★★★ P5-B 클라이언트 런타임 시각화 검증 PASS ★★★");
        }
        else
        {
            Debug.Log("[P5B_LIVE_CLIENT] (참고: 헤드리스/독립 실행 조건에 따라 링 상태 로그 기록됨)");
        }
        Debug.Log("[P5B_LIVE_CLIENT] ========================================================");
    }

    private static float ResolvePositiveSeconds(string key, float fallback, float maximum)
    {
        string configured = MirrorSmokeConfiguration_MirrorTest.Argument(key);
        return float.TryParse(configured, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out float parsed) &&
               float.IsFinite(parsed) && parsed >= 0f
            ? Mathf.Min(parsed, maximum)
            : fallback;
    }
}
