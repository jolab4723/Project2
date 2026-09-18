using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public static class MirrorOriginalIntegrationValidation_MirrorTest
{
    public static string LastReport { get; private set; }
    private static readonly List<Object> temporary = new();
    private static NavMeshDataInstance navigation;
    private static int checks;

    [MenuItem("SW/Mirror Test/Validate Original Integration In Empty Play Scene")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || Camera.main != null ||
            Object.FindObjectsByType<T_PlayerController>(FindObjectsSortMode.None).Length != 0)
            throw new InvalidOperationException("Use an empty, camera-free Play Mode scene.");
        checks = 0;
        LastReport = "running";
        try
        {
            var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),
                new List<NavMeshBuildSource>
                {
                    new() { shape = NavMeshBuildSourceShape.Box,
                        transform = Matrix4x4.TRS(new Vector3(0, -0.1f, 0), Quaternion.identity, Vector3.one),
                        size = new Vector3(100, 0.2f, 100), area = 0 }
                }, new Bounds(Vector3.zero, new Vector3(100, 10, 100)), Vector3.zero, Quaternion.identity);
            temporary.Add(data);
            navigation = NavMesh.AddNavMeshData(data);
            GameObject fighter = SpawnPlayer("Fighter", Vector3.zero);
            GameObject gunner = SpawnPlayer("Gunner", new Vector3(10, 0, 0));
            fighter.GetComponent<T_PlayerController>().StartCoroutine(Validate(fighter, gunner));
        }
        catch
        {
            LastReport = "FAILED during setup";
            Cleanup();
            throw;
        }
    }

    private static GameObject SpawnPlayer(string character, Vector3 position)
    {
        var parent = new GameObject("OriginalValidation_" + character);
        temporary.Add(parent);
        parent.SetActive(false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/Character/Player/" + character + ".prefab");
        GameObject player = Object.Instantiate(prefab, position, Quaternion.identity, parent.transform);
        player.AddComponent<Mirror.NetworkIdentity>();
        object skillController = (object)player.GetComponent<FighterSkillController>() ?? player.GetComponent<GunnerSkillController>();
        var definitions = (SkillDefinitionSO[])Get(skillController, "skills");
        var copies = new SkillDefinitionSO[definitions.Length];
        for (int i = 0; i < definitions.Length; i++)
        {
            copies[i] = Object.Instantiate(definitions[i]);
            temporary.Add(copies[i]);
            // 원본 데이터는 유지하고 검증 복제본에만 비용을 넣어 자원 차감을 확인한다.
            copies[i].manaCost = copies[i].evolution1ManaCost = copies[i].evolution2ManaCost = copies[i].evolution3ManaCost = 7f;
        }
        Set(skillController, "skills", copies);
        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            string name = behaviour.GetType().Name;
            if (name.Contains("InputHandler") || name == "WBH_PlayerAnimation" ||
                name.EndsWith("TestKeyTrigger") || name == "PlayerHudEventBridge")
                behaviour.enabled = false;
        }
        var agent = player.GetComponent<NavMeshAgent>();
        agent.enabled = true;
        parent.SetActive(true);
        Check(agent.isOnNavMesh, character + " real prefab navigation");
        return player;
    }

    private static IEnumerator Validate(GameObject fighterObject, GameObject gunnerObject)
    {
        yield return null;
        yield return null;
        try
        {
            Check(Camera.main == null, "no camera in execution process");
            var fighter = fighterObject.GetComponent<FighterSkillController>();
            var gunner = gunnerObject.GetComponent<GunnerSkillController>();
            var fighterStats = fighterObject.GetComponent<PlayerStatManager>();
            var gunnerStats = gunnerObject.GetComponent<PlayerStatManager>();
            var fighterMana = fighterObject.GetComponent<PlayerManaManager>();
            var gunnerMana = gunnerObject.GetComponent<PlayerManaManager>();
            var fighterState = fighterObject.GetComponent<WBH_PlayerStateMachine>();
            var gunnerState = gunnerObject.GetComponent<WBH_PlayerStateMachine>();
            fighterStats.SetPassiveStats(new StatSet { skillRangePercent = 25f });
            gunnerStats.SetPassiveStats(new StatSet { skillRangePercent = 75f });
            fighterMana.IsRegenPaused = gunnerMana.IsRegenPaused = true;
            fighterMana.RefreshMaxMana();
            gunnerMana.RefreshMaxMana();
            fighterMana.FillMana();
            gunnerMana.FillMana();

            float mana = fighterMana.CurrentMana;
            Check(!fighter.TryUseSkill(0) && fighterMana.CurrentMana == mana, "camera-free local call costs nothing");
            Check(!fighter.TryUseSkill(0, Vector3.zero, fighterStats) &&
                !fighter.TryUseSkill(0, new Vector3(float.NaN, 0, 1), fighterStats) &&
                !fighter.TryUseSkill(0, Vector3.left, null) && fighterMana.CurrentMana == mana,
                "invalid fighter aim or owner costs nothing");
            Check(fighter.TryUseSkill(0, Vector3.left, fighterStats), "fighter external aim accepted");
            Check(Vector3.Dot(fighter.transform.forward, Vector3.left) > 0.999f &&
                fighterMana.CurrentMana < mana, "fighter uses supplied aim and actual mana");
            Check(ReferenceEquals(Get(fighter, "skillOwnerStats"), fighterStats), "fighter owner stats");
            Check(!fighter.TryUseSkill(0, Vector3.right, fighterStats), "pending fighter skill rejects repeat");
            fighter.ExecutePendingSkill();
            fighter.EndPendingSkillAni();
            Check(fighterState.Is(PlayerState.Idle), "fighter animation completion restores idle");

            fighter.SetEvolution(0, SkillEvolutionId.Evolution3);
            Array.Clear((float[])Get(fighter, "cooldownRemaining"), 0, 3);
            fighterMana.FillMana();
            mana = fighterMana.CurrentMana;
            Check(fighter.TryStartCharge(0, Vector3.forward, fighterStats) && fighterMana.CurrentMana == mana,
                "charge begins without committing mana");
            Check(!fighter.TryReleaseCharge(1), "wrong charge slot rejected");
            Check(fighter.TryReleaseCharge(0) && fighterMana.CurrentMana < mana, "charge release commits once");
            Check(!fighter.TryReleaseCharge(0), "duplicate release rejected");
            fighter.EndPendingSkillAni();
            Check(!fighter.TryStartCharge(-1, Vector3.forward, fighterStats), "invalid charge slot rejected");

            mana = gunnerMana.CurrentMana;
            Check(!gunner.TryUseSkill(1) && gunnerMana.CurrentMana == mana, "gunner local camera guard");
            Check(!gunner.TryUseSkill(1, Vector3.right, new Vector3(float.PositiveInfinity, 0, 0), gunnerStats) &&
                gunnerMana.CurrentMana == mana, "invalid target costs nothing");
            Vector3 bombTarget = gunner.transform.position + Vector3.left * 2f;
            Check(gunner.TryUseSkill(1, Vector3.left, bombTarget, gunnerStats), "gunner external bomb accepted");
            Check(ReferenceEquals(Get(gunner, "skillOwnerStats"), gunnerStats), "gunner owner stats");
            gunner.transform.forward = Vector3.right;
            gunner.ExecutePendingSkill();
            var bomb = Object.FindFirstObjectByType<GunnerBomb>();
            Check(bomb != null && Vector3.Distance((Vector3)Get(bomb, "targetPosition"), bombTarget) < 0.01f,
                "bomb keeps requested target through delayed animation");
            if (bomb != null) temporary.Add(bomb.gameObject);
            gunner.EndPendingSkillAni();
            Check(gunnerState.Is(PlayerState.Idle), "gunner animation completion restores idle");

            float expectedRange = 10f;
            fighterStats.GetSkillRangeBonus(out float flat, out float percent);
            expectedRange = (expectedRange + flat) * (1 + percent / 100f);
            float actualRange = (float)Invoke(fighter, "ApplySkillRangeBonus", fighter.GetSkillDefinition(0), 0, 10f);
            Check(Mathf.Approximately(actualRange, expectedRange), "fighter range uses its owner after another player casts");

            fighter.SetEvolution(2, SkillEvolutionId.None);
            fighterMana.FillMana();
            Check(fighter.TryUseSkill(2, Vector3.forward, fighterStats), "fighter dash starts before cancellation");
            fighter.ExecutePendingSkill();
            yield return null;
            fighter.CancelActiveSkill();
            Vector3 cancelledPosition = fighter.transform.position;
            mana = fighterMana.CurrentMana;
            yield return new WaitForSeconds(0.15f);
            Check(Vector3.Distance(fighter.transform.position, cancelledPosition) < 0.01f &&
                fighterState.Is(PlayerState.Idle) && fighterMana.CurrentMana == mana &&
                fighter.GetRemainingCooldown(2) > 0f, "cancelled dash stays stopped without resource refund");

            gunner.SetEvolution(2, SkillEvolutionId.None);
            gunnerMana.FillMana();
            Check(gunner.TryUseSkill(2, Vector3.forward, gunner.transform.position + Vector3.forward, gunnerStats),
                "gunner backstep starts before cancellation");
            gunner.ExecutePendingBackstepMove();
            yield return null;
            gunner.CancelActiveSkill();
            cancelledPosition = gunner.transform.position;
            mana = gunnerMana.CurrentMana;
            yield return new WaitForSeconds(0.15f);
            Check(Vector3.Distance(gunner.transform.position, cancelledPosition) < 0.01f &&
                gunnerState.Is(PlayerState.Idle) && gunnerMana.CurrentMana == mana &&
                gunner.GetRemainingCooldown(2) > 0f, "cancelled backstep stays stopped without resource refund");

            ValidateQuestProgress();
            ValidateQuestBoard();
            ValidateUnchangedManagers();

            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorCombat/Prefabs/Normal_Melee_MirrorTest.prefab");
            var enemy = Object.Instantiate(enemyPrefab, new Vector3(25, 0, 0), Quaternion.identity);
            temporary.Add(enemy);
            var authority = enemy.GetComponent<NetworkEnemyAuthority_MirrorTest>();
            var info = (WBH_EnemyInfo)Get(authority, "enemyInfo");
            enemy.GetComponent<NavMeshAgent>().enabled = true;
            enemy.GetComponent<WBH_EnemyController>().Initialize(info, null, null, null, null);
            var effects = enemy.GetComponent<WBH_EnemyStatusEffectController>();
            var enemyStatus = enemy.GetComponent<WBH_EnemyStatus>();
            float speed = enemyStatus.MoveSpeed;
            var visual = ScriptableObject.CreateInstance<WBH_EffectData>();
            temporary.Add(visual);
            Set(effects, "slowEffect", visual);
            effects.AddStatusEffect(new WBH_StatusEffectData(WBH_StatusEffectType.Slow, 0.1f, 0.5f));
            Check(effects.HasStatusEffect(WBH_StatusEffectType.Slow) && enemyStatus.MoveSpeed < speed,
                "real robot slow applies with VFX data but no spawner");
            yield return new WaitForSeconds(0.2f);
            Check(!effects.HasStatusEffect(WBH_StatusEffectType.Slow) && Mathf.Approximately(enemyStatus.MoveSpeed, speed),
                "real robot slow expires and restores speed without VFX");
            int deaths = 0;
            enemyStatus.OnDead += () => deaths++;
            effects.ApplyDotDamage(enemyStatus.CurrentHp + 10f);
            effects.ApplyDotDamage(10f);
            Check(enemyStatus.CurrentHp == 0f && deaths == 1, "robot lethal DOT uses normal death once");
            LastReport = "PASS " + checks + " checks; actual Fighter/Gunner/robot prefabs; no user file writes";
            Debug.Log("[MirrorOriginalIntegrationValidation] " + LastReport);
        }
        finally
        {
            if (LastReport == "running") LastReport = "FAILED; inspect Console";
            Cleanup();
        }
    }

    private static void ValidateQuestProgress()
    {
        var definition = ScriptableObject.CreateInstance<QuestDefinitionSO>();
        temporary.Add(definition);
        definition.questId = "validation_shared";
        definition.conditions = new[]
        {
            new QuestConditionDefinition { conditionType = QuestConditionType.KillEnemy, requiredCount = 2 },
            new QuestConditionDefinition { conditionType = QuestConditionType.CollectItem, targetId = "scrap", requiredCount = 3 }
        };
        ActiveQuestData a = QuestManager.CreateProgress(definition);
        ActiveQuestData b = QuestManager.CreateProgress(definition);
        Check(QuestManager.TryAdvanceProgress(definition, a, QuestConditionType.KillEnemy, "robot") &&
            a.conditionProgress[0] == 1 && b.conditionProgress[0] == 0, "caller-owned quest state isolation");
        Check(!QuestManager.TryAdvanceProgress(definition, a, QuestConditionType.CollectItem, "wrong"), "quest target filter");
        QuestManager.TryAdvanceProgress(definition, a, QuestConditionType.KillEnemy, null, int.MaxValue);
        Check(a.conditionProgress[0] == 2 && !QuestManager.IsAllConditionsMet(definition, a), "quest saturation and multi-condition gate");
        QuestManager.TryAdvanceProgress(definition, a, QuestConditionType.CollectItem, "scrap", int.MaxValue);
        Check(a.conditionProgress[1] == 3 && QuestManager.IsAllConditionsMet(definition, a) && !a.isCompleted,
            "quest completion does not grant local rewards or mark caller state");
        a.isCompleted = true;
        Check(!QuestManager.TryAdvanceProgress(definition, a, QuestConditionType.KillEnemy, null), "completed quest ignores progress");
        b.conditionProgress.Clear();
        Check(!QuestManager.IsAllConditionsMet(definition, b) &&
            !QuestManager.TryAdvanceProgress(definition, b, QuestConditionType.KillEnemy, null), "malformed quest state fails closed");
    }

    private static void ValidateQuestBoard()
    {
        var root = new GameObject("QuestBoardValidation");
        temporary.Add(root);
        var board = root.AddComponent<QuestBoardNPC>();
        var offer = ScriptableObject.CreateInstance<QuestDefinitionSO>();
        temporary.Add(offer);
        offer.questId = "validation_offer";
        var requests = new List<QuestBoardNPC.RequestKind>();
        board.BindExternalRequests(requests.Add);
        board.Interact();
        Check(requests.Count == 1 && board.CurrentOffer == null, "board interaction only sends intent");
        board.ApplyExternalOffer(offer, false, false);
        Check(!board.Reroll() && !board.Accept() && board.CurrentOffer == offer && !board.HasAcceptedThisVisit &&
            requests.Count == 3, "board requests do not commit local quest state");
        board.ApplyExternalOffer(null, true, true);
        Check(board.CurrentOffer == null && board.HasRerolled && board.HasAcceptedThisVisit, "board applies confirmed visit state");
        board.BindExternalRequests(null);
        board.Interact();
        Check(requests.Count == 3, "disconnected board never falls back to local rewards");
    }

    private static void ValidateUnchangedManagers()
    {
        string file = "profile_singleplayer.json";
        string path = (string)typeof(Core.DataManager).GetMethod("GetSavePath", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { file });
        Check(path == System.IO.Path.Combine(Application.persistentDataPath, file), "unchanged DataManager fixed save path confirmed");
        var root = new GameObject("UninitializedSettingValidation");
        root.SetActive(false);
        temporary.Add(root);
        var settings = root.AddComponent<Core.SettingManager>();
        bool uninitializedSaveFails = false;
        try { Invoke(settings, "Save"); }
        catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException) { uninitializedSaveFails = true; }
        Check(uninitializedSaveFails, "unchanged settings requires Activate before saving; no file written");
        Set(settings, "currentData", new KY_SettingsData { resolutionIndex = int.MaxValue });
        bool invalidResolutionFails = false;
        try { Invoke(settings, "ApplyDisplay"); }
        catch (TargetInvocationException ex) when (ex.InnerException is IndexOutOfRangeException) { invalidResolutionFails = true; }
        Check(invalidResolutionFails, "unchanged settings invalid resolution fails before display mutation");
    }

    private static object Get(object value, string name) => value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    private static void Set(object value, string name, object fieldValue) => value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, fieldValue);
    private static object Invoke(object value, string name, params object[] args) => value.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args);
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("[MirrorOriginalIntegrationValidation] " + name);
        checks++;
    }
    private static void Cleanup()
    {
        for (int i = temporary.Count - 1; i >= 0; i--)
            if (temporary[i] != null && temporary[i] is not NavMeshData) Object.Destroy(temporary[i]);
        navigation.Remove();
        foreach (Object value in temporary)
            if (value is NavMeshData) Object.Destroy(value);
        temporary.Clear();
    }
}
