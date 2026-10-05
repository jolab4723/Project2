using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ItemSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>SW 수정: Assets 밖 run_script에서 실제 Gunner TryAttack 한 발과 OnDamaged로 C1/C2/C3를 검증한다.</summary>
public static class WeekendSpatialEffectsValidation
{
    private const string ItemsFolder = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items";
    private const string EnemyId = "enemy.normal.melee.working_machine";

    /// <summary>SW 수정: 게임 프레임마다 실제 공격을 시도하고 실행 요청 번호와 실제 적중을 따로 계측한다.</summary>
    public static Task<string> RunRifleBaseline()
        => RunBaseline("item.weapon.rifle.novalance", WeaponType.Rifle, GunnerWeaponType.Rifle);

    public static Task<string> RunShotgunBaseline()
        => RunBaseline("item.weapon.shotgun.embercoil", WeaponType.Shotgun, GunnerWeaponType.Shotgun);

    public static Task<string> RunGrenadeBaseline()
        => RunBaseline("item.weapon.grenadelauncher.smartfuse", WeaponType.GrenadeLauncher, GunnerWeaponType.GrenadeLauncher);

    private const int BaselineShots = 15;
    private const double BaselineTimeoutSeconds = 29d;

    private static async Task<string> RunBaseline(string itemId, WeaponType weaponType, GunnerWeaponType gunnerType)
    {
        const int requestedShots = BaselineShots;
        ItemDefinitionSO definition = Definition(itemId);
        Require(definition.characterClass == CharacterClass.Gunner && definition.weaponType == weaponType &&
            definition.uniqueEffect == null && definition.weaponEnchantElement == ElementType.None,
            $"{gunnerType} baseline item must be an actual unenchanted Gunner {weaponType} without a unique effect");
        using (var scope = await PrepareGunner())
        {
            string[] excludedItems = scope.PreserveBaselineItems();
            ItemInstance weapon = scope.Equip(definition);
            await scope.WaitVisual(gunnerType);
            PlayerContext context = scope.Context;
            var combat = context.GetComponent<T_PlayerCombat>();
            var status = context.GetComponent<WBH_PlayerStatus>();
            var state = context.GetComponent<WBH_PlayerStateMachine>();
            Require(status.CurrentElement == ElementType.None && status.GunnerBulletSpeed > 0f && status.GunnerAttackRange > 1f &&
                status.AttackPower > 0f && status.AttackSpeed > 0f,
                $"{gunnerType} baseline must have no enchantment and valid actual attack power/speed, bullet speed and range");
            object statsBefore = BaselineStats(status);
            Vector3 forward = ClearForward(context, status.GunnerAttackRange, gunnerType == GunnerWeaponType.Shotgun ? 90f : 0f);
            context.transform.forward = forward;
            Vector3 origin = FirePoint(context).position;
            float targetDistance = Mathf.Min(4f, status.GunnerAttackRange * 0.4f);
            Vector3 targetPoint = origin + forward * targetDistance;
            float? explosionRadius = null;
            if (gunnerType == GunnerWeaponType.GrenadeLauncher)
            {
                // SW 수정: 유탄은 실제 Ground 위 적의 발밑을 조준한다. 기존 적이 폭발 범위에 있으면 피해를 주기 전에 실패한다.
                targetPoint.y = context.transform.position.y;
                Require(Physics.Raycast(targetPoint + Vector3.up * 4f, Vector3.down, out RaycastHit ground, 12f,
                    LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore), "Grenade baseline landing point has no actual Ground");
                targetPoint = ground.point;
                explosionRadius = Read<float>(combat, "explosionRadius");
                Require(explosionRadius.Value > 0f && Physics.OverlapSphere(targetPoint, explosionRadius.Value, 1 << 10,
                    QueryTriggerInteraction.Collide).Length == 0, "Grenade baseline landing area contains existing enemy bodies; isolate firing space");
                Require(Vector3.Distance(origin, targetPoint) < status.GunnerAttackRange,
                    "Grenade baseline ground aim is beyond actual projectile range");
            }
            Vector3 aimPoint = gunnerType == GunnerWeaponType.GrenadeLauncher ? targetPoint : origin + forward * status.GunnerAttackRange;
            var enemy = scope.Spawn(targetPoint, centerOnPoint: gunnerType != GunnerWeaponType.GrenadeLauncher);
            Physics.SyncTransforms();
            var hits = new List<HitEvidence>();
            var attackPowerAtDamage = new Dictionary<uint, float>();
            scope.Observe(enemy, 0, hits, result =>
            {
                attackPowerAtDamage[result.AttackId] = status.AttackPower;
                return ExpectedDamage(context, enemy, result.ElementType,
                    Read<float>(combat, "basicAttackMult") * T_PlayerCombat.GunnerBasicDamageMultiplier(gunnerType));
            });
            var shots = new List<GunnerShotEvidence>();
            uint counterStart = Read<uint>(combat, "nextAttackId"), lastCounter = counterStart;
            int attempts = 0, acceptedRequests = 0, attemptIntervalCount = 0;
            double gameStart = Time.timeAsDouble, firstAttemptAt = 0d, lastAttemptAt = 0d;
            DateTime deadline = DateTime.UtcNow.AddSeconds(BaselineTimeoutSeconds);
            double attemptIntervalSum = 0d, minimumAttemptInterval = double.PositiveInfinity, maximumAttemptInterval = 0d;
            float hpBefore = enemy.Status.CurrentHp;
            int previousFrame = Time.frameCount - 1;
            double previousTime = gameStart - 0.001d;
            string Timeout() => $"{gunnerType} baseline exceeded {BaselineTimeoutSeconds} seconds: attempts={attempts}, acceptedRequests={acceptedRequests}, actualShotRequests={shots.Count}/{requestedShots}, damageEvents={hits.Count}";

            // SW 수정: 세 총기는 애니메이션의 GunnerAttack에서 초기 요청 번호를 한 번 만든다. Shotgun SectorAttack과 대상 요청은 그 번호를 재사용한다.
            void ObserveShots()
            {
                uint current = Read<uint>(combat, "nextAttackId");
                Require(current >= lastCounter && current - lastCounter <= 20u, $"{gunnerType} baseline attack counter wrapped or unexpected requests occurred");
                for (uint id = lastCounter + 1; id <= current && id != 0; id++)
                    shots.Add(new GunnerShotEvidence { AttackId = id, ObservedAt = Time.timeAsDouble, ObservedFrame = Time.frameCount, FrameSeconds = Time.deltaTime });
                lastCounter = current;
            }

            while (shots.Count < requestedShots)
            {
                await NextBaselineFrame(previousFrame, previousTime, gameStart, deadline, Timeout);
                previousFrame = Time.frameCount; previousTime = Time.timeAsDouble;
                Require(context.Effects.CanExecute && context.Health.CurrentHealth > 0f && !enemy.Status.IsDead, $"{gunnerType} baseline player/target lifetime ended during measurement");
                ObserveShots();
                if (shots.Count >= requestedShots) break;
                if (attempts == 0) firstAttemptAt = previousTime;
                else
                {
                    double interval = previousTime - lastAttemptAt;
                    minimumAttemptInterval = Math.Min(minimumAttemptInterval, interval);
                    maximumAttemptInterval = Math.Max(maximumAttemptInterval, interval);
                    attemptIntervalSum += interval; attemptIntervalCount++;
                }
                lastAttemptAt = previousTime;
                bool wasAttacking = state.Is(PlayerState.Attack);
                attempts++;
                combat.TryAttack(aimPoint);
                if (!wasAttacking && state.Is(PlayerState.Attack)) acceptedRequests++;
                ObserveShots();
            }

            // SW 수정: 마지막 실행 후 실제 비행과 물리 적중을 기다린다. 미적중도 측정 결과로 남기며 타이머나 스탯을 고치지 않는다.
            double arrivalMargin = Math.Max(0.5d, targetDistance / status.GunnerBulletSpeed + Time.deltaTime * 3d);
            if (gunnerType == GunnerWeaponType.GrenadeLauncher)
                arrivalMargin = Math.Max(1d, Vector3.Distance(origin, targetPoint) / status.GunnerBulletSpeed) + Math.Max(0.1d, Time.deltaTime * 3d);
            while (Time.timeAsDouble < shots[shots.Count - 1].ObservedAt + arrivalMargin || state.Is(PlayerState.Attack))
            {
                await NextBaselineFrame(previousFrame, previousTime, gameStart, deadline, Timeout);
                previousFrame = Time.frameCount; previousTime = Time.timeAsDouble;
                ObserveShots();
            }
            Require(shots.Count == requestedShots, $"{gunnerType} baseline observed {shots.Count} requests; expected exactly {requestedShots}");
            var shotIds = new HashSet<uint>(shots.Select(s => s.AttackId));
            Require(hits.All(h => shotIds.Contains(h.AttackId)), $"{gunnerType} baseline damage contained an unobserved attack ID");
            var directHits = hits.Where(h => h.Cause == DamageCause.Direct.ToString()).ToArray();
            var noncriticalHits = directHits.Where(h => !h.Critical).ToArray();
            Require(directHits.All(h => attackPowerAtDamage[h.AttackId] > 0f), "Baseline damage normalization requires positive actual attack power");
            int distinctHits = directHits.Select(h => h.AttackId).Distinct().Count();
            double[] fireIntervals = shots.Skip(1).Select((s, i) => s.ObservedAt - shots[i].ObservedAt).ToArray();
            double windowEnd = directHits.Length > 0 ? directHits.Max(h => h.At) : Time.timeAsDouble;
            double duration = Math.Max(0d, windowEnd - firstAttemptAt);
            float totalDamage = directHits.Sum(h => h.Damage);
            double normalizedTotalDamage = directHits.Sum(h => (double)h.Damage / attackPowerAtDamage[h.AttackId]);
            float? meanNoncriticalDamage = noncriticalHits.Length > 0 ? noncriticalHits.Average(h => h.Damage) : (float?)null;
            double? normalizedMeanNoncriticalDamage = noncriticalHits.Length > 0
                ? noncriticalHits.Average(h => (double)h.Damage / attackPowerAtDamage[h.AttackId]) : (double?)null;
            return JsonConvert.SerializeObject(new
            {
                definition.itemId, definition.itemName, weaponType = gunnerType.ToString(), rarity = definition.rarity.ToString(),
                rarityValue = (int)definition.rarity, weapon.upgradeLevel, playerLevel = status.CurrentLevel,
                itemMainOptions = definition.mainOptions.Select(o => new { stat = o.statType.ToString(), o.value }),
                itemRolledOptions = weapon.rolledSubStats.Select(o => new { stat = o.statType.ToString(), o.value }),
                itemLevelEvidence = "ItemDefinitionSO has no item level or tier field; rarity, actual ItemInstance.upgradeLevel and player CurrentLevel are recorded.",
                requestedShots, timeoutSeconds = BaselineTimeoutSeconds, attempts, acceptedRequests, actualShots = shots.Count,
                counterStart, counterEnd = lastCounter,
                hitEvents = directHits.Length, hitShots = distinctHits, missedShots = shots.Count - distinctHits,
                duplicateHitEvents = directHits.Length - distinctHits, unexpectedEffectEvents = hits.Count - directHits.Length,
                baselineClean = hits.Count == directHits.Length, shots, hits, fireIntervals,
                meanFireInterval = fireIntervals.Average(), minimumFireInterval = fireIntervals.Min(), maximumFireInterval = fireIntervals.Max(),
                sameObservationShotPairs = fireIntervals.Count(i => i == 0d),
                attemptIntervals = new { mean = attemptIntervalCount > 0 ? attemptIntervalSum / attemptIntervalCount : 0d,
                    minimum = attemptIntervalCount > 0 ? minimumAttemptInterval : 0d, maximum = maximumAttemptInterval },
                firstAttemptAt, lastDamageAt = directHits.Length > 0 ? (double?)windowEnd : null, duration, totalDamage,
                meanDamagePerHit = directHits.Length > 0 ? totalDamage / directHits.Length : 0f,
                noncriticalHits = noncriticalHits.Length, meanNoncriticalDamage, normalizedMeanNoncriticalDamage,
                normalizedTotalDamage, normalizedDurationDps = duration > 0d ? normalizedTotalDamage / duration : 0d,
                normalizedHits = directHits.Select(h => new { h.AttackId, h.Damage, h.Critical, attackPower = attackPowerAtDamage[h.AttackId],
                    normalizedDamage = (double)h.Damage / attackPowerAtDamage[h.AttackId] }),
                normalization = "Each actual Direct damage event divided by actual AttackPower read at that OnDamaged event; normalized DPS uses the same first-attempt to last-hit window and includes crits/misses. Noncritical means exclude crit events.",
                durationDps = duration > 0d ? totalDamage / duration : 0d, hpLoss = hpBefore - enemy.Status.CurrentHp,
                criticalHits = directHits.Count(h => h.Critical), targetDistance, targetDefense = enemy.Status.DefensePower, explosionRadius,
                aimPoint = aimPoint.ToString(), actualTargetPoint = targetPoint.ToString(), statsBefore, statsAfter = BaselineStats(status), excludedItems,
                shotEvidence = "Read-only T_PlayerCombat.nextAttackId increments once in actual animation GunnerAttack/CreateDamageRequest for all three weapons, correlated with same-ID OnDamaged. Shotgun passes that nonzero ID into SectorAttack and each target request, generating no extra ID; it uses immediate sector damage and no projectile. Rifle/Grenade retain the original request ID at impact. Acceptance is the public state transition into Attack.",
                dpsWindow = "First TryAttack attempt through last actual Direct damage arrival; misses contribute zero damage. With no hits the window ends after projectile arrival wait.",
                limits = "Shot times are frame observations, not a projectile-spawn callback; several increments in one sample share a timestamp. Crit rolls, item rolls, character level and learned passives are retained. TimeScale and production state/timers/stats are never assigned."
            });
        }
    }

    private sealed class GunnerShotEvidence
    {
        public uint AttackId;
        public double ObservedAt;
        public int ObservedFrame;
        public float FrameSeconds;
    }

    private static async Task NextBaselineFrame(int previousFrame, double previousTime, double gameStart, DateTime deadline, Func<string> timeout)
    {
        while (true)
        {
            Require(Application.isPlaying && !EditorApplication.isPaused && Time.timeScale > 0f, "Baseline interrupted or game clock paused");
            Require(DateTime.UtcNow < deadline && Time.timeAsDouble - gameStart < BaselineTimeoutSeconds, timeout());
            if (Time.frameCount != previousFrame && Time.timeAsDouble > previousTime) return;
            // SW 수정: 게임 프레임·게임 시각의 진행만 공격 간격을 결정한다. 실시간 Task.Delay로 발사 간격을 만들지 않는다.
            await Task.Yield();
        }
    }

    private static object BaselineStats(WBH_PlayerStatus status) => new
    {
        status.AttackPower, status.AttackSpeed, status.CritRate, status.CritMult, status.NormalDamageModifier, status.Pen, status.CurrentLevel,
        element = status.CurrentElement.ToString(), range = status.GunnerAttackRange, bulletSpeed = status.GunnerBulletSpeed
    };

    public static async Task<string> RunAntimatterPiercing()
    {
        ItemDefinitionSO definition = Definition("item.weapon.rifle.antimatterlance");
        var effect = Effect<AntimatterPiercingShotUniqueEffectSO>(definition, "UE_AntimatterPiercingShot");
        Require(effect.maxTargets == 3, "C1 target limit must be three");
        Near(effect.secondDamageMultiplier, 0.7f, "C1 second coefficient");
        Near(effect.thirdDamageMultiplier, 0.5f, "C1 third coefficient");
        using (var scope = await PrepareGunner())
        {
            scope.Equip(definition);
            await scope.WaitVisual(GunnerWeaponType.Rifle);
            PlayerContext context = scope.Context;
            float range = context.GetComponent<WBH_PlayerStatus>().GunnerAttackRange;
            Require(range >= 6f, "C1 actual Rifle range is too short to separate four enemy bodies");
            Vector3 forward = ClearForward(context, range);
            context.transform.forward = forward;
            Vector3 origin = FirePoint(context).position;
            var records = new List<HitEvidence>();
            var enemies = new WBH_EnemyController[4];
            for (int i = 0; i < enemies.Length; i++)
            {
                int index = i;
                enemies[i] = scope.Spawn(origin + forward * range * (0.18f + 0.2f * i), centerOnPoint: true);
                scope.Observe(enemies[i], i, records, result => index is 1 or 2
                    ? ExpectedDamage(context, enemies[index], result.ElementType,
                        Read<float>(context.GetComponent<T_PlayerCombat>(), "basicAttackMult") *
                        T_PlayerCombat.GunnerBasicDamageMultiplier(GunnerWeaponType.Rifle) * effect.GetDamageMultiplier(index))
                    : (float?)null);
            }
            Physics.SyncTransforms();
            float fourthHp = enemies[3].Status.CurrentHp;
            context.GetComponent<T_PlayerCombat>().TryAttack(origin + forward * range);
            await WaitFor(() => records.Count >= 3, 5d, "C1 actual TryAttack did not hit three inline enemies");
            await WaitAttackEnd(context);
            Require(records.Count == 3 && records.Select(r => r.Target).SequenceEqual(new[] { 0, 1, 2 }), "C1 shot did not hit exactly the nearest three enemies once in order");
            Require(records[0].Cause == DamageCause.Direct.ToString() && records.Skip(1).All(r => r.Cause == DamageCause.Effect.ToString() && !r.Critical), "C1 causes/critical flags were not Direct, noncritical Effect, noncritical Effect");
            Require(records[0].AttackId != 0 && records.All(r => r.AttackId == records[0].AttackId), "C1 one actual shot did not preserve its AttackId");
            foreach (HitEvidence record in records.Skip(1)) Near(record.Damage, record.ExpectedDamage.Value, "C1 actual piercing coefficient");
            Near(enemies[3].Status.CurrentHp, fourthHp, "C1 fourth enemy took damage");
            return JsonConvert.SerializeObject(new { records, fourthHp, tests = "actual Rifle TryAttack once, distance order, three distinct bodies, 70/50 current-stat coefficients, noncritical followups, one AttackId, fourth untouched" });
        }
    }

    public static async Task<string> RunEchoReplay()
    {
        ItemDefinitionSO definition = Definition("item.weapon.shotgun.echovault");
        var effect = Effect<EchoVaultReplayUniqueEffectSO>(definition, "UE_EchoVaultReplay");
        Near(effect.delaySeconds, 0.45f, "C2 replay delay");
        Near(effect.damageMultiplier, 0.35f, "C2 replay coefficient");
        Near(effect.cooldownSeconds, 1.5f, "C2 reservation cooldown");
        using (var scope = await PrepareGunner())
        {
            ItemInstance weapon = scope.Equip(definition);
            await scope.WaitVisual(GunnerWeaponType.Shotgun);
            PlayerContext context = scope.Context;
            float range = context.GetComponent<WBH_PlayerStatus>().GunnerAttackRange;
            Vector3 forward = ClearForward(context, range, coneAngle: 90f);
            context.transform.forward = forward;
            Vector3 origin = FirePoint(context).position;
            var enemy = scope.Spawn(origin - forward * (range + 3f), centerOnPoint: true);
            var records = new List<HitEvidence>();
            scope.Observe(enemy, 0, records, result => ExpectedDamage(context, enemy, result.ElementType, effect.damageMultiplier));
            int replays = 0;
            Vector3 replayOrigin = default, replayForward = default;
            Action<Vector3, Vector3, float, float> presented = (p, d, _, _) => { replays++; replayOrigin = p; replayForward = d; };
            context.Effects.EchoReplayPresented += presented;
            scope.Cleanup(() => context.Effects.EchoReplayPresented -= presented);
            context.GetComponent<T_PlayerCombat>().TryAttack(origin + forward * range);
            string key = effect.name + ":echo";
            await WaitFor(() => context.Effects.Cooldowns.ContainsKey(key), 5d, "C2 actual missed TryAttack did not reserve a replay");
            double cooldownEnd = context.Effects.Cooldowns[key];
            var runner = Object.FindObjectsByType<PlayerGrenadeEffect>(FindObjectsSortMode.None)
                .FirstOrDefault(r => Read<PlayerContext>(r, "owner") == context && Read<EchoVaultReplayUniqueEffectSO>(r, "echo") == effect);
            Require(runner != null && records.Count == 0, "C2 initial shot was not a miss or replay observation was late");
            double replayAt = Read<double>(runner, "endsAt");
            double reservedAt = replayAt - effect.delaySeconds;
            Near(cooldownEnd - reservedAt, 1.5d, "C2 cooldown did not begin on missed execution");
            Require(Time.timeAsDouble < replayAt, "C2 frame delay prevented moving enemy before replay");
            Vector3 recordedOrigin = runner.transform.position;
            Vector3 recordedForward = Read<Vector3>(runner, "echoForward");
            Require(Vector3.Distance(recordedOrigin, origin) < 0.05f && Vector3.Angle(recordedForward, forward) < 0.1f, "C2 did not record actual original shooting space");
            Place(enemy, recordedOrigin + recordedForward * Mathf.Min(4f, range * 0.4f), centerOnPoint: true);
            context.transform.forward = Vector3.Cross(Vector3.up, forward);
            Physics.SyncTransforms();
            uint attackId = Read<uint>(runner, "attackId");
            await WaitFor(() => records.Count > 0 && replays > 0, 3d, "C2 moved target was not hit in original shooting space");
            Require(records.Count == 1 && records[0].Cause == DamageCause.Effect.ToString() && !records[0].Critical && records[0].AttackId == attackId,
                "C2 moved target damage was not one noncritical Effect from the original shot");
            Near(records[0].Damage, records[0].ExpectedDamage.Value, "C2 replay damage coefficient");
            Require(records[0].At >= replayAt - 0.01d && records[0].At <= replayAt + Math.Max(0.1d, records[0].FrameSeconds * 2d), "C2 replay did not occur at 0.45 seconds within frame tolerance");
            Require(replays == 1 && Vector3.Distance(replayOrigin, recordedOrigin) < 0.05f && Vector3.Angle(replayForward, recordedForward) < 0.1f,
                "C2 presentation did not retain original origin/direction after player turned");
            Require(context.Effects.GetRemainingCooldown(weapon) > 0f, "C2 1.5-second cooldown ended with the replay");
            await WaitFor(() => Time.timeAsDouble >= cooldownEnd + 0.05d, 3d, "C2 cooldown clock did not advance");
            Near(context.Effects.GetRemainingCooldown(weapon), 0f, "C2 cooldown did not expire at 1.5 seconds");
            return JsonConvert.SerializeObject(new { records, delay = records[0].At - reservedAt, cooldown = cooldownEnd - reservedAt,
                origin = recordedOrigin.ToString(), direction = recordedForward.ToString(), tests = "actual missed Shotgun execution, enemy moved into original space, player turned, delayed noncritical replay, reservation cooldown and expiry" });
        }
    }

    public static async Task<string> RunWorldEnderBlast()
    {
        ItemDefinitionSO definition = Definition("item.weapon.grenadelauncher.worldender");
        var effect = Effect<WorldEnderChargedBlastUniqueEffectSO>(definition, "UE_WorldEnderChargedBlast");
        Near(effect.rechargeSeconds, 8f, "C3 recharge duration");
        Near(effect.radius, 5f, "C3 extra blast radius");
        Near(effect.damageMultiplier, 1.5f, "C3 extra blast coefficient");
        using (var scope = await PrepareGunner())
        {
            PlayerContext context = scope.Context;
            double readyObservedAt = 0d, consumedAt = 0d;
            bool hasBeenReady = false;
            Action<bool> readiness = ready =>
            {
                if (ready) { readyObservedAt = Time.timeAsDouble; hasBeenReady = true; }
                else if (hasBeenReady) consumedAt = Time.timeAsDouble;
            };
            context.Effects.WorldEnderReadyChanged += readiness;
            scope.Cleanup(() => context.Effects.WorldEnderReadyChanged -= readiness);
            double equippedAt = Time.timeAsDouble;
            ItemInstance weapon = scope.Equip(definition);
            Require(!context.Effects.WorldEnderReady, "C3 initial equip was immediately charged");
            string key = effect.name + ":world-ender";
            // SW 수정: 장착 직후의 실제 PlayerContext Update가 충전 시각을 만들 때까지 기다린다.
            await WaitFor(() => context.Effects.Cooldowns.ContainsKey(key), 2d, "C3 initial equip did not begin charging in actual Update");
            double readyAt = context.Effects.Cooldowns[key];
            float initialCooldown = context.Effects.GetRemainingCooldown(weapon);
            Require(!context.Effects.WorldEnderReady && initialCooldown > 7.8f && initialCooldown <= 8.01f &&
                readyAt - equippedAt >= 7.99d && readyAt - equippedAt <= 8d + Math.Max(0.1d, Time.deltaTime * 2d), "C3 initial charge was not eight seconds from the first equipped Update");
            await scope.WaitVisual(GunnerWeaponType.GrenadeLauncher);
            await WaitFor(() => context.Effects.WorldEnderReady, 10d, "C3 actual initial eight-second charge never became ready");
            Require(readyObservedAt >= readyAt - 0.01d, "C3 readiness occurred before eight seconds");
            float range = context.GetComponent<WBH_PlayerStatus>().GunnerAttackRange;
            Vector3 forward = ClearForward(context, range);
            context.transform.forward = forward;
            Vector3 origin = FirePoint(context).position;
            Vector3 landing = origin + forward * Mathf.Min(6f, range * 0.6f);
            landing.y = context.transform.position.y;
            var enemy = scope.Spawn(landing, centerOnPoint: false);
            var records = new List<HitEvidence>();
            WBH_CombatManager.DamageSourceSnapshot? impactSnapshot = null;
            scope.Observe(enemy, 0, records, result =>
            {
                if (result.DamageCause == DamageCause.Direct) impactSnapshot = new WBH_CombatManager.DamageSourceSnapshot(context.Controller.Status);
                return result.DamageCause == DamageCause.Effect
                    ? ExpectedDamage(context, enemy, result.ElementType, effect.damageMultiplier, impactSnapshot)
                    : (float?)null;
            });
            int blasts = 0;
            float shownRadius = 0f;
            Action<Vector3, float> blast = (_, r) => { blasts++; shownRadius = r; };
            context.Effects.WorldEnderBlastPresented += blast;
            scope.Cleanup(() => context.Effects.WorldEnderBlastPresented -= blast);
            Physics.SyncTransforms();
            context.GetComponent<T_PlayerCombat>().TryAttack(landing);
            await WaitFor(() => consumedAt > 0d, 5d, "C3 actual TryAttack did not consume readiness at launch");
            Require(!context.Effects.WorldEnderReady, "C3 readiness remained after actual launch");
            Near(context.Effects.Cooldowns[key] - consumedAt, 8d, "C3 actual launch did not start eight-second recharge");
            await WaitFor(() => records.Count >= 2 && blasts > 0, 5d, "C3 actual grenade did not produce ordinary and extra explosion damage");
            await WaitAttackEnd(context);
            Require(records.Count == 2 && records[0].Cause == DamageCause.Direct.ToString() && records[1].Cause == DamageCause.Effect.ToString() && !records[1].Critical,
                "C3 ordinary/extra explosion was duplicated, reordered, or extra damage was critical");
            Require(records[0].AttackId != 0 && records[1].AttackId == records[0].AttackId, "C3 explosions did not share actual shot AttackId");
            Near(records[1].Damage, records[1].ExpectedDamage.Value, "C3 150% noncritical extra explosion");
            Require(blasts == 1, "C3 one actual shot presented extra blast more than once");
            Near(shownRadius, 5f, "C3 actual extra blast radius");
            return JsonConvert.SerializeObject(new { records, initialCooldown, equipToReady = readyObservedAt - equippedAt, readyObservedAt, consumedAt, blasts, shownRadius,
                tests = "first equip eight-second charge, actual GrenadeLauncher TryAttack consumes at launch, recharge begins, ordinary plus 150% noncritical extra explosion, same AttackId" });
        }
    }

    private sealed class HitEvidence
    {
        public int Target;
        public uint AttackId;
        public string Cause;
        public bool Critical;
        public float Damage;
        public float? ExpectedDamage;
        public double At;
        public float FrameSeconds;
    }

    private static async Task<GunnerScope> PrepareGunner()
    {
        Require(Application.isPlaying && !EditorApplication.isPaused && Time.timeScale > 0f && !Mirror.NetworkClient.active && !Mirror.NetworkServer.active, "Unpaused single Play required");
        var inventory = InventoryController.Instance;
        Require(inventory?.BoundPlayer != null && inventory.BoundPlayer.IsComplete, "Actual bound player/inventory missing");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Character/Player/Gunner.prefab");
        Require(prefab != null && prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), "Actual Gunner original missing or has missing scripts");
        var scope = new GunnerScope(inventory);
        try
        {
            scope.Prepare(prefab);
            await Task.Delay(100);
            Require(scope.Context.IsComplete && scope.Context.Effects.CanExecute, "Actual Gunner context incomplete or lacks damage authority");
            scope.Context.Equipment.SetActiveCharacterClass(CharacterClass.Gunner);
            scope.PreserveWeapon();
            return scope;
        }
        catch { scope.Dispose(); throw; }
    }

    // SW 수정: 새 실제 Gunner의 효과 상태만 사용하고 원 장비·플레이어·기존 적 AI는 finally에서 복원한다.
    private sealed class GunnerScope : IDisposable
    {
        private readonly InventoryController inventory;
        private readonly PlayerContext previous;
        private readonly CharacterClass previousClass;
        private readonly List<(WBH_EnemyController enemy, bool enabled, WBH_EnemyMovement movement, bool canControl)> paused = new();
        private readonly List<WBH_EnemyController> enemies = new();
        private readonly List<Action> cleanup = new();
        private readonly List<KeyValuePair<EquipSlotType, InventoryItem>> baselineEquipment = new();
        private readonly List<(InventoryItem item, InventoryPlacementSnapshot placement)> baselineRelics = new();
        private InventoryItem previousWeapon, temporaryWeapon;
        private GameObject gunner;
        public PlayerContext Context { get; private set; }

        public GunnerScope(InventoryController inventory)
        {
            this.inventory = inventory;
            previous = inventory.BoundPlayer;
            Require(inventory.EquipmentSystem.CurrentCharacterClass.HasValue, "Original character class unavailable for restoration");
            previousClass = inventory.EquipmentSystem.CurrentCharacterClass.Value;
        }

        public void Prepare(GameObject prefab)
        {
            foreach (var enemy in Object.FindObjectsByType<WBH_EnemyController>(FindObjectsSortMode.None))
            {
                var movement = enemy.GetComponent<WBH_EnemyMovement>();
                paused.Add((enemy, enemy.enabled, movement, movement != null && Read<bool>(movement, "canControl")));
                movement?.SetControlEnable(false);
                enemy.enabled = false;
            }
            Vector3 position = previous.transform.position;
            Quaternion rotation = previous.transform.rotation;
            previous.gameObject.SetActive(false);
            gunner = Object.Instantiate(prefab, position, rotation);
            Context = gunner.GetComponent<PlayerContext>() ?? gunner.AddComponent<PlayerContext>();
            Context.Controller.SetControlEnable(false);
            var input = gunner.GetComponent<PlayerActionInputHandler>();
            if (input != null) input.enabled = false;
            var legacy = gunner.GetComponent<WBH_PlayerInputHandler>();
            if (legacy != null) legacy.enabled = false;
            Require(Context.BindSinglePlayerInventory(inventory), "Actual Gunner inventory bind failed");
            gunner.GetComponent<T_PlayerCombat>().Initialize(Object.FindFirstObjectByType<WBH_ProjectileSpawner>());
        }

        public void PreserveWeapon()
        {
            if (!inventory.EquipmentSystem.TryGetEquippedItem(EquipSlotType.Weapon, out InventoryItem weapon)) return;
            UnequipWeapon();
            previousWeapon = weapon;
        }

        // SW 수정: baseline에서만 다른 장비와 가방 유물을 정식 API로 보존해 고유효과·장비 보너스를 분리한다.
        public string[] PreserveBaselineItems()
        {
            var excluded = new List<string>();
            foreach (var pair in inventory.EquipmentSystem.GetEquippedItems().Where(p => p.Key != EquipSlotType.Weapon).ToArray())
            {
                Require(inventory.PlayerGrid.TryFindEmptySpaceForItem(pair.Value, pair.Value.isRotated, out InventoryPlacementSnapshot placement), $"Inventory space unavailable to preserve baseline {pair.Key}");
                Require(new EquipmentTransaction(inventory.EquipmentSystem).TryUnequip(pair.Key, inventory.PlayerGrid, placement).IsSuccess, $"Baseline equipment preservation failed: {pair.Key}");
                baselineEquipment.Add(pair);
                excluded.Add(pair.Value.itemData.definition.itemId);
            }
            foreach (var item in inventory.GetAllInventoryItems().Where(i => i?.itemData?.definition?.itemId?.StartsWith("item.relic.", StringComparison.Ordinal) == true).ToArray())
            {
                InventoryPlacementSnapshot placement = InventoryPlacementSnapshot.Capture(inventory.PlayerGrid, item);
                Require(inventory.TryRemoveInventoryItem(item) == InventoryRemoveResult.Success, "Baseline relic preservation failed: " + item.itemData.definition.itemId);
                baselineRelics.Add((item, placement));
                excluded.Add(item.itemData.definition.itemId);
            }
            return excluded.ToArray();
        }

        public ItemInstance Equip(ItemDefinitionSO definition)
        {
            var item = ItemDataCreator.CreateItemData(definition);
            var added = inventory.TryAddItemData(item);
            Require(added.Result == InventoryAddResult.Success, $"Actual item add failed: {definition.itemId}, {added.Result}");
            temporaryWeapon = inventory.PlayerGrid.GetItemAt(added.X, added.Y);
            Require(temporaryWeapon != null && ReferenceEquals(temporaryWeapon.itemData, item), "Added test item identity mismatch");
            Require(new EquipmentTransaction(inventory.EquipmentSystem).TryEquip(inventory.PlayerGrid, temporaryWeapon,
                InventoryPlacementSnapshot.Capture(inventory.PlayerGrid, temporaryWeapon), EquipSlotType.Weapon).IsSuccess, $"Actual weapon equip failed: {definition.itemId}");
            return item;
        }

        private void UnequipWeapon()
        {
            Require(inventory.EquipmentSystem.TryGetEquippedItem(EquipSlotType.Weapon, out InventoryItem weapon), "Equipped weapon missing");
            Require(inventory.PlayerGrid.TryFindEmptySpaceForItem(weapon, weapon.isRotated, out InventoryPlacementSnapshot placement), "Inventory space unavailable to preserve weapon");
            Require(new EquipmentTransaction(inventory.EquipmentSystem).TryUnequip(EquipSlotType.Weapon, inventory.PlayerGrid, placement).IsSuccess, "Actual weapon unequip failed");
        }

        public async Task WaitVisual(GunnerWeaponType type)
        {
            var presenter = Context.GetComponent<PlayerWeaponVisualPresenter>();
            Require(presenter != null && presenter.isActiveAndEnabled, "Actual weapon presenter missing");
            await WaitFor(() =>
            {
                Require(string.IsNullOrEmpty(presenter.VisualLoadError), "Actual weapon visual load failed: " + presenter.VisualLoadError);
                return presenter.IsVisualReady && presenter.CurrentVisualItemId == temporaryWeapon.itemData.definition.itemId;
            }, 10d, "Actual Addressables weapon visual was not ready");
            var binding = Context.GetComponentInChildren<GunnerWeaponVfxBinding>();
            Require(binding != null && binding.WeaponType == type, $"Actual weapon visual must bind {type}");
        }

        public WBH_EnemyController Spawn(Vector3 position, bool centerOnPoint)
        {
            var provider = Object.FindFirstObjectByType<WBH_EnemyDataProvider>();
            var pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
            Require(provider != null && pool != null, "Actual EnemyProvider/Pool missing");
            Require(provider.TryCreateEnemyInfo(EnemyId, new WBH_EnemyStatContext(1, "normal", 1), out WBH_EnemyInfo info), "Actual normal enemy data missing");
            info = info.Clone(); info.maxHP = 100000f;
            var enemy = pool.Get(EnemyId);
            Require(enemy != null, "Actual enemy pool returned no body");
            enemies.Add(enemy);
            enemy.transform.SetPositionAndRotation(position, Quaternion.identity);
            enemy.GetComponent<EnemyKillReward>()?.Initialize(Context.Wallet);
            enemy.GetComponent<WBH_EnemyView>()?.Initialize(Object.FindFirstObjectByType<WBH_FloatTextPoolManager>(), Object.FindFirstObjectByType<WBH_HighEnemyHpbarView>());
            enemy.Initialize(info, pool, Object.FindFirstObjectByType<WBH_EffectSpawner>(), Object.FindFirstObjectByType<WBH_ProjectileSpawner>(), YJ_SfxPlayer.Instance);
            enemy.gameObject.SetActive(true);
            enemy.GetComponent<WBH_EnemyMovement>().SetControlEnable(false);
            enemy.enabled = false;
            Place(enemy, position, centerOnPoint);
            return enemy;
        }

        public void Observe(WBH_EnemyController enemy, int index, List<HitEvidence> records, Func<WBH_DamageResult, float?> expected)
        {
            var status = enemy.GetComponent<WBH_EnemyStatus>();
            Action<WBH_DamageResult> handler = result =>
            {
                if (!ReferenceEquals(result.Attacker, Context.Controller) || result.DamageCause is not (DamageCause.Direct or DamageCause.Effect)) return;
                records.Add(new HitEvidence { Target = index, AttackId = result.AttackId, Cause = result.DamageCause.ToString(), Critical = result.IsCritical,
                    Damage = result.FinalDamage, ExpectedDamage = expected(result), At = Time.timeAsDouble, FrameSeconds = Time.deltaTime });
            };
            status.OnDamaged += handler;
            Cleanup(() => { if (status != null) status.OnDamaged -= handler; });
        }

        public void Cleanup(Action action) => cleanup.Add(action);

        public void Dispose()
        {
            var errors = new List<string>();
            foreach (Action action in cleanup) TryCleanup(action, errors);
            if (gunner != null) gunner.SetActive(false);
            var pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
            foreach (var enemy in enemies.Distinct())
            {
                if (enemy == null) continue;
                TryCleanup(() =>
                {
                    enemy.GetComponent<WBH_StatusEffectController>()?.ClearAllStatusEffects();
                    enemy.enabled = true;
                    if (pool != null && enemy.gameObject.activeSelf) pool.Return(enemy);
                    else if (pool == null) Object.Destroy(enemy.gameObject);
                }, errors);
            }
            if (temporaryWeapon != null) TryCleanup(() =>
            {
                if (inventory.EquipmentSystem.TryGetEquippedItem(EquipSlotType.Weapon, out InventoryItem current) && ReferenceEquals(current, temporaryWeapon)) UnequipWeapon();
                if (inventory.PlayerGrid.ContainsItem(temporaryWeapon))
                    Require(inventory.TryRemoveInventoryItem(temporaryWeapon) == InventoryRemoveResult.Success, "Temporary weapon removal failed");
            }, errors);
            inventory.EquipmentSystem.SetActiveCharacterClass(previousClass);
            if (previousWeapon != null) TryCleanup(() =>
            {
                Require(inventory.PlayerGrid.ContainsItem(previousWeapon), "Original weapon missing from preserved inventory");
                Require(new EquipmentTransaction(inventory.EquipmentSystem).TryEquip(inventory.PlayerGrid, previousWeapon,
                    InventoryPlacementSnapshot.Capture(inventory.PlayerGrid, previousWeapon), EquipSlotType.Weapon).IsSuccess, "Original weapon restoration failed");
            }, errors);
            foreach (var pair in baselineEquipment) TryCleanup(() =>
            {
                Require(inventory.PlayerGrid.ContainsItem(pair.Value), "Preserved baseline equipment missing: " + pair.Key);
                Require(new EquipmentTransaction(inventory.EquipmentSystem).TryEquip(inventory.PlayerGrid, pair.Value,
                    InventoryPlacementSnapshot.Capture(inventory.PlayerGrid, pair.Value), pair.Key).IsSuccess, "Baseline equipment restoration failed: " + pair.Key);
            }, errors);
            foreach (var entry in baselineRelics) TryCleanup(() =>
            {
                Require(entry.item.isRotated == entry.placement.IsRotated, "Preserved baseline relic rotation changed");
                Require(inventory.TryAddItemAt(entry.item, entry.placement.Rect.X, entry.placement.Rect.Y).Result == InventoryAddResult.Success,
                    "Baseline relic restoration failed: " + entry.item.itemData.definition.itemId);
            }, errors);
            if (gunner != null) Object.Destroy(gunner);
            if (previous != null) TryCleanup(() =>
            {
                previous.gameObject.SetActive(true);
                Require(previous.BindSinglePlayerInventory(inventory), "Original player inventory rebind failed");
            }, errors);
            foreach (var state in paused)
                if (state.enemy != null) TryCleanup(() => { state.movement?.SetControlEnable(state.canControl); state.enemy.enabled = state.enabled; }, errors);
            Require(errors.Count == 0, "Spatial validation cleanup failed: " + string.Join("; ", errors));
        }
    }

    private static Vector3 ClearForward(PlayerContext context, float range, float coneAngle = 0f)
    {
        int obstacles = LayerMask.GetMask("Wall", "Prop", "Ground");
        for (int i = 0; i < 16; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
            context.transform.forward = direction;
            Vector3 origin = FirePoint(context).position;
            if (Physics.SphereCast(origin, 0.15f, direction, out _, range, obstacles | (1 << 10), QueryTriggerInteraction.Collide)) continue;
            if (coneAngle > 0f && Physics.OverlapSphere(origin, range, 1 << 10, QueryTriggerInteraction.Collide).Any(c =>
                Vector3.Angle(direction, Vector3.ProjectOnPlane(c.ClosestPoint(origin) - origin, Vector3.up)) <= coneAngle * 0.5f)) continue;
            return direction;
        }
        throw new InvalidOperationException("No clear actual firing space; stop wave spawning and isolate existing enemies before running this test");
    }

    private static void Place(WBH_EnemyController enemy, Vector3 position, bool centerOnPoint)
    {
        enemy.transform.position = position;
        Physics.SyncTransforms();
        if (!centerOnPoint) return;
        var colliders = enemy.GetComponentsInChildren<Collider>().Where(c => c.enabled && c.gameObject.activeInHierarchy && c.gameObject.layer == 10).ToArray();
        Require(colliders.Length > 0, "Actual enemy body has no enabled enemy-layer colliders");
        Bounds bounds = colliders[0].bounds;
        foreach (Collider collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds);
        enemy.transform.position += position - bounds.center;
        Physics.SyncTransforms();
    }

    private static Transform FirePoint(PlayerContext context)
    {
        Transform point = Read<Transform>(context.GetComponent<T_PlayerCombat>(), "firePoint");
        Require(point != null, "Actual Gunner FirePoint missing");
        return point;
    }

    private static float ExpectedDamage(PlayerContext context, WBH_EnemyController target, ElementType element, float multiplier,
        WBH_CombatManager.DamageSourceSnapshot? snapshot = null)
        => WBH_CombatManager.CalculateDamage(new WBH_DamageRequest(context.Controller, target, WBH_AttackType.Normal, element, multiplier),
            snapshot ?? new WBH_CombatManager.DamageSourceSnapshot(context.Controller.Status), canCrit: false).FinalDamage;

    private static async Task WaitAttackEnd(PlayerContext context)
    {
        await WaitFor(() => !context.GetComponent<WBH_PlayerStateMachine>().Is(PlayerState.Attack), 5d, "Actual attack animation/state did not complete");
        await Task.Delay(50);
    }

    private static async Task WaitFor(Func<bool> condition, double seconds, string error)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition())
        {
            Require(Application.isPlaying && !EditorApplication.isPaused && Time.timeScale > 0f, "Spatial validation interrupted or game clock paused");
            Require(DateTime.UtcNow < deadline, error);
            await Task.Delay(5);
        }
    }

    private static ItemDefinitionSO Definition(string itemId)
    {
        var matches = AssetDatabase.FindAssets("t:ItemDefinitionSO", new[] { ItemsFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null && d.itemId == itemId).ToArray();
        Require(matches.Length == 1, $"Actual item definition must be unique: {itemId}. Run ItemTable Run All first.");
        return matches[0];
    }

    private static T Effect<T>(ItemDefinitionSO definition, string effectId) where T : UniqueEffectSO
    {
        var effect = definition.uniqueEffect as T;
        Require(effect != null && effect.name == effectId, $"Actual {definition.itemId} must use {typeof(T).Name}/{effectId}");
        return effect;
    }

    // SW 수정: 검증용 상태만 읽고 내부 피해·예약 함수 호출이나 필드·시계 변경은 하지 않는다.
    private static T Read<T>(object source, string name)
    {
        Require(source != null, "State source missing: " + name);
        for (Type type = source.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return (T)field.GetValue(source);
        }
        throw new InvalidOperationException($"State field missing: {source.GetType().Name}.{name}");
    }

    private static void TryCleanup(Action action, List<string> errors)
    {
        try { action(); } catch (Exception ex) { errors.Add(ex.Message); }
    }

    private static void Near(double actual, double expected, string error)
        => Require(!double.IsNaN(actual) && !double.IsInfinity(actual) && Math.Abs(actual - expected) <= 0.01d, $"{error}: expected {expected}, actual {actual}");

    private static void Require(bool condition, string error)
    {
        if (!condition) throw new InvalidOperationException(error);
    }
}
