using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ItemSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// SW 수정: Assets 밖 Editor run_script에서 스킬-아이템 연계(A1~A4)·소총 연사·신규 유물 3종을
/// 정식 Fighter/Gunner 프리팹, 실제 장비 거래, 정식 적 Provider/Pool로 검증한다. 운영 저장과 런타임 시험 진입점을 추가하지 않는다.
/// </summary>
public static class SkillLinkBuildValidation
{
    private const string ItemsFolder = "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items";
    private const string EnemyId = "enemy.normal.melee.working_machine";
    private const string StageScene = "Assets/Scenes/Maps/Act1_Maps/Act1_Stage1/Act1_Stage1.unity";
    private static bool running;

    /// <summary>SW 수정: 열린 Dirty Scene을 닫거나 저장하지 않고 Play 시작 씬만 정식 Act1 맵으로 바꾼다. 종료 뒤 RestoreStartScene으로 원복한다.</summary>
    public static string SetStageStartScene()
    {
        Require(!Application.isPlaying, "Play Mode 종료가 필요합니다.");
        Require(EditorSceneManager.playModeStartScene == null, "다른 Play 시작 씬이 이미 지정돼 있습니다.");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(StageScene);
        Require(EditorSceneManager.playModeStartScene != null, "Act1_Stage1 씬 없음");
        return StageScene;
    }

    public static string RestoreStartScene()
    {
        Require(!Application.isPlaying, "Play Mode 종료가 필요합니다.");
        EditorSceneManager.playModeStartScene = null;
        return "restored";
    }

    /// <summary>SW 수정: 파이터의 OnSkillCast 슬롯(A2), 스킬 적중 출처(A1), 무기 속성 상태이상(A4), 대시 회피(A3)와 기본 적중 회귀를 검사한다.</summary>
    public static async Task<string> RunFighterSkillLinks()
    {
        var log = new List<string>();
        using (var scope = await PreparePlayer(CharacterClass.Fighter))
        {
            PlayerContext context = scope.Context;
            ISkillController skills = context.Skills;
            Require(skills is FighterSkillController, "실제 Fighter 스킬 컨트롤러가 PlayerContext.Skills로 노출되지 않음");
            int dash = DodgeSlot(skills);
            int other = Enumerable.Range(0, skills.SkillCount).First(i => i != 0 && i != dash);
            log.Add($"slots={skills.SkillCount}, dash={dash}, other={other}, shapes={string.Join(",", Enumerable.Range(0, skills.SkillCount).Select(i => skills.GetSkillDefinition(i)?.shapeType.ToString()))}");

            // A2: 자기 레일 블레이드 - 1번 슬롯 시전만 발동
            var rail = scope.Equip(Definition("item.weapon.greatsword.magneticrailblade"), EquipSlotType.Weapon);
            var railEffect = (TriggeredBuffUniqueEffectSO)rail.definition.uniqueEffect;
            var enemy = scope.Spawn(context.transform.position + context.transform.forward * 2f);
            await Cast(context, other, enemy);
            Require(!HasBuff(context, railEffect), "A2 다른 슬롯 시전이 1번 슬롯 효과를 발동함");
            float hpBefore = enemy.Status.CurrentHp;
            var casts = new List<int>();
            Action<SkillCastInfo> onCast = i => casts.Add(i.Slot);
            skills.SkillCast += onCast;
            await Cast(context, 0, enemy);
            skills.SkillCast -= onCast;
            Require(HasBuff(context, railEffect), $"A2 1번 슬롯 시전이 자기 레일 블레이드를 발동하지 않음 casts=[{string.Join(",", casts)}]");
            Require(enemy.Status.CurrentHp < hpBefore, "실제 1번 스킬이 적에게 피해를 주지 않음");
            log.Add("A2 slot filter: other slot no buff, slot1 buff");
            await scope.Reset(); context = scope.Context; skills = context.Skills;

            // A1: 유압 파일 드라이버 - 기본 적중은 무시, 실제 스킬 적중만 발동 / A4: 무기 속성 상태이상
            var pile = scope.Equip(Definition("item.weapon.blunt.hydraulicpiledriver"), EquipSlotType.Weapon, ElementType.Ice);
            var pileEffect = (TriggeredBuffUniqueEffectSO)pile.definition.uniqueEffect;
            enemy = scope.Spawn(context.transform.position + context.transform.forward * 2f);
            Hit(context, enemy, DamageCause.Direct);
            Require(!HasBuff(context, pileEffect), "A1 기본 적중이 Skill 전용 효과를 발동함");
            var results = Record(enemy);
            await Cast(context, 0, enemy);
            Require(results.Any(r => r.DamageCause == DamageCause.Skill), "실제 스킬 피해가 Skill 출처로 기록되지 않음");
            Require(HasBuff(context, pileEffect), "A1 실제 스킬 적중이 Skill 전용 효과를 발동하지 않음");
            var freeze = enemy.GetComponent<WBH_StatusEffectController>();
            Require(freeze.HasStatusEffect(WBH_StatusEffectType.Freeze), "A4 Ice 무기 스킬 적중이 빙결 상태이상을 걸지 않음");
            log.Add($"A1 skill-only buff stacks={StackOf(context, pileEffect)}, A4 Ice skill -> Freeze");
            // C2: 치명타를 끄고 같은 적에게 기본 공격(1.0)과 실제 반원 베기 스킬 피해를 비교한다(파일 드라이버 스킬 버프 6% 포함).
            scope.NoCritical();
            var probe = scope.Spawn(context.transform.position + context.transform.forward * 2f);
            var pr2 = Record(probe);
            Hit(context, probe, DamageCause.Direct);
            await Task.Delay(5200);
            await Cast(context, 0, probe);
            float basicDmg = pr2.First(r => r.DamageCause == DamageCause.Direct).FinalDamage;
            float skillDmg = pr2.First(r => r.DamageCause == DamageCause.Skill).FinalDamage;
            log.Add($"C2 skill/basic = {skillDmg:F1}/{basicDmg:F1} = {skillDmg / basicDmg:F2} (half-circle 2.7)");
            await scope.Reset(); context = scope.Context; skills = context.Skills;

            // A3: 대시 스킬 = 회피 (사이버네틱 코어 OnDodge, 아이스 크러셔 회피 준비 공격)
            var ice = scope.Equip(Definition("item.weapon.axe.icecrusher"), EquipSlotType.Weapon);
            var cyber = scope.AddRelic(Definition("item.relic.cybernetic_core"));
            var cyberEffect = (TriggeredBuffUniqueEffectSO)cyber.definition.uniqueEffect;
            Require(!context.Effects.PreparedAttackReady && !HasBuff(context, cyberEffect), "A3 사전 상태 오염");
            var playerStatus = context.GetComponent<WBH_PlayerStatus>();
            float moveBefore = playerStatus.MoveSpeed;
            await Cast(context, dash, null);
            Require(HasBuff(context, cyberEffect), "A3 대시 스킬이 OnDodge(사이버네틱 코어)를 발동하지 않음");
            Require(context.Effects.PreparedAttackReady, "A3 대시 스킬이 회피 준비 공격(아이스 러시)을 준비하지 않음");
            log.Add($"A3 dash -> OnDodge move {moveBefore:F1}->{playerStatus.MoveSpeed:F1}, prepared attack ready ({ice.definition.uniqueEffect.name})");
            await scope.Reset(); context = scope.Context; skills = context.Skills;

            // 회귀: 기본 적중 전용(빈 hitSource) 효과는 스킬 피해로 발동하지 않고 기본 적중으로는 계속 발동
            var basic = scope.AddRelic(Definition("item.relic.mass-produced_core"));
            var basicEffect = (TriggeredBuffUniqueEffectSO)basic.definition.uniqueEffect;
            enemy = scope.Spawn(context.transform.position + context.transform.forward * 2f);
            Hit(context, enemy, DamageCause.Skill);
            Require(!HasBuff(context, basicEffect), "회귀: 스킬 피해가 기본 적중 전용 효과를 발동함");
            Hit(context, enemy, DamageCause.Direct);
            Require(HasBuff(context, basicEffect), "회귀: 기본 적중 전용 효과가 기본 공격으로 발동하지 않음");
            log.Add("regression: Basic-only trigger ignores Skill, fires on Direct");
        }
        return JsonConvert.SerializeObject(log);
    }

    /// <summary>SW 수정: 처치 시 쿨타임 감소, 표식 추가 피해, 원소 공명을 실제 피해 경계로 검사한다.</summary>
    public static async Task<string> RunFighterNewRelics()
    {
        var log = new List<string>();
        using (var scope = await PreparePlayer(CharacterClass.Fighter))
        {
            PlayerContext context = scope.Context;
            ISkillController skills = context.Skills;

            // 충전 회로: 처치 시 1번 슬롯 쿨타임 2.5초 감소(효과 쿨 1초)
            scope.AddRelic(Definition("item.relic.chargecircuit"));
            var enemy = scope.Spawn(context.transform.position + context.transform.forward * 2f);
            Task casting = Cast(context, 0, enemy);
            await Task.Delay(50);
            float before = skills.GetRemainingCooldown(0);
            Require(before > 2.6f, "1번 슬롯 쿨타임이 충분히 길지 않음: " + before);
            var victim = scope.Spawn(context.transform.position + context.transform.forward * 2f + context.transform.right, 1f);
            Hit(context, victim, DamageCause.Direct, 1000f);
            Require(victim.Status.IsDead, "처치 대상이 죽지 않음");
            await Task.Delay(50);
            float after = skills.GetRemainingCooldown(0);
            Require(before - after > 2.3f && before - after < 2.8f, $"처치 시 쿨타임 감소량 불일치: {before:F2}->{after:F2}");
            var victim2 = scope.Spawn(context.transform.position + context.transform.forward * 2f - context.transform.right, 1f);
            Hit(context, victim2, DamageCause.Direct, 1000f);
            await Task.Delay(50);
            float after2 = skills.GetRemainingCooldown(0);
            Require(after - after2 < 0.5f, $"효과 쿨다운 1초 안의 두 번째 처치가 다시 감소시킴: {after:F2}->{after2:F2}");
            await casting;
            log.Add($"chargecircuit: cd {before:F2}->{after:F2} (-{before - after:F2}), 2nd kill in 1s -> {after2:F2}");
            await scope.Reset(); context = scope.Context; skills = context.Skills;

            // 표적 렌즈: 표식 대상 +25% 피해
            scope.AddRelic(Definition("item.relic.targetlens"));
            scope.NoCritical();
            var marked = scope.Spawn(context.transform.position + context.transform.forward * 2f);
            var plain = scope.Spawn(context.transform.position + context.transform.forward * 2f + context.transform.right * 2f);
            marked.GetComponent<WBH_StatusEffectController>().AddStatusEffect(new WBH_StatusEffectData(WBH_StatusEffectType.Marked, duration: 10f, value: 1f));
            Require(marked.GetComponent<WBH_StatusEffectController>().HasStatusEffect(WBH_StatusEffectType.Marked), "표식 상태이상 부여 실패");
            var mr = Record(marked); var pr = Record(plain);
            Hit(context, plain, DamageCause.Direct);
            Hit(context, marked, DamageCause.Direct);
            float ratio = mr.Last().FinalDamage / pr.Last().FinalDamage;
            Require(ratio > 1.2f && ratio < 1.3f, "표식 추가 피해 배율 불일치: " + ratio);
            log.Add($"targetlens: plain={pr.Last().FinalDamage:F1}, marked={mr.Last().FinalDamage:F1}, ratio={ratio:F3}");
            await scope.Reset(); context = scope.Context; skills = context.Skills;

            // 원소 공명기: 화상+감전 대상 적중 시 반경 3m 추가 피해, 대상별 3초 쿨
            scope.AddRelic(Definition("item.relic.elementresonator"));
            var core = scope.Spawn(context.transform.position + context.transform.forward * 2f);
            var near = scope.Spawn(core.transform.position + context.transform.right * 1.5f);
            var far = scope.Spawn(core.transform.position + context.transform.right * 6f);
            var cr = Record(core); var nr = Record(near); var fr = Record(far);
            var coreStatus = core.GetComponent<WBH_StatusEffectController>();
            coreStatus.AddStatusEffect(WBH_StatusEffectPresets.Burn1);
            Hit(context, core, DamageCause.Direct);
            Require(!nr.Any(r => r.DamageCause == DamageCause.Effect), "원소 1종(화상)만으로 공명 발동");
            coreStatus.AddStatusEffect(WBH_StatusEffectPresets.Freeze1);
            Hit(context, core, DamageCause.Direct);
            await Task.Delay(100);
            int nearEffects = nr.Count(r => r.DamageCause == DamageCause.Effect);
            Require(Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Any(ps => ps.transform.root.name.StartsWith("UEVFX_ElementResonatorVortex")),
                "원소 공명기 전용 연출(UEVFX_ElementResonatorVortex)이 재생되지 않음");
            Require(nearEffects == 1 && !fr.Any(), $"공명 범위 피해 불일치 near={nearEffects}, far={fr.Count}");
            Hit(context, core, DamageCause.Direct);
            await Task.Delay(100);
            Require(nr.Count(r => r.DamageCause == DamageCause.Effect) == 1, "대상별 3초 쿨다운 안에 공명이 반복됨");
            log.Add($"resonator(Burn+Freeze): near effect={nr.First(r => r.DamageCause == DamageCause.Effect).FinalDamage:F1}, core effect hits={cr.Count(r => r.DamageCause == DamageCause.Effect)}, far none, per-target cd ok");
        }
        return JsonConvert.SerializeObject(log);
    }

    /// <summary>SW 수정: 거너 소총의 연사 판정·발사 간격·탄속, 스킬 적중(A1)·무기 속성(A4)·백스탭 회피(A3)·OnSkillCast 전체 슬롯을 검사한다.</summary>
    public static async Task<string> RunGunner()
    {
        var log = new List<string>();
        using (var scope = await PreparePlayer(CharacterClass.Gunner))
        {
            PlayerContext context = scope.Context;
            ISkillController skills = context.Skills;
            Require(skills is GunnerSkillController, "실제 Gunner 스킬 컨트롤러가 PlayerContext.Skills로 노출되지 않음");
            var combat = context.GetComponent<T_PlayerCombat>();

            // 소총: 연사 판정, 발사 간격, 탄속, 발당 피해
            var rifle = scope.Equip(Definition("item.weapon.rifle.novalance"), EquipSlotType.Weapon);
            await scope.WaitVisual(rifle);
            Require(combat.IsAutoFireWeapon, "노바 랜스가 연사 무기로 판정되지 않음");
            var enemy = scope.Spawn(context.transform.position + context.transform.forward * 5f);
            var rr = Record(enemy);
            var playerStatus = context.GetComponent<WBH_PlayerStatus>();
            float attackSpeed = playerStatus.AttackSpeed;
            float statSpeed = context.GetComponent<PlayerStatManager>().Stat.attackSpeed;
            float started = Time.time; float bulletSpeed = 0f;
            while (Time.time - started < 3f)
            {
                combat.RequestAttack(enemy.transform.position);   // 누르고 있기 분기와 같은 호출
                if (bulletSpeed == 0f)
                    bulletSpeed = Object.FindObjectsByType<WBH_Projectile>(FindObjectsSortMode.None).Where(p => p.isActiveAndEnabled).Select(p => Read<float>(p, "speed")).FirstOrDefault();
                await Task.Yield();
            }
            await Task.Delay(500);
            int shots = rr.Count(r => r.DamageCause == DamageCause.Direct);
            float expectedSpeed = playerStatus.GunnerBulletSpeed * 1.5f;
            Require(Mathf.Abs(attackSpeed / statSpeed - 2.2f) < 0.01f, $"소총 공속 배율 불일치 {attackSpeed}/{statSpeed}");
            Require(Mathf.Abs(bulletSpeed - expectedSpeed) < 0.01f, $"소총 탄속 불일치 {bulletSpeed} vs {expectedSpeed}");
            Require(shots >= 3, $"누르고 있기 3초 동안 발사가 거의 없음: {shots}, bulletSpeed={bulletSpeed}, state={context.GetComponent<WBH_PlayerStateMachine>().CurrentState}, others={Object.FindObjectsByType<WBH_EnemyController>(FindObjectsSortMode.None).Count(e => e != enemy && Vector3.Distance(e.transform.position, context.transform.position) < 7f)}, hp={context.GetComponent<WBH_PlayerStatus>().CurrentHp}");
            log.Add($"rifle: autoFire=true, attackSpeed {statSpeed:F2}x2.2={attackSpeed:F2}, shots in 3s={shots} ({shots / 3f:F2}/s), bulletSpeed={bulletSpeed:F1}, dmg/shot={rr.First().FinalDamage:F1}");
            await scope.Reset(); context = scope.Context; skills = context.Skills;

            // 산탄총은 연사하지 않음 + 백스탭 회피(A3) + OnSkillCast 전체 슬롯(마나 변환기)
            combat = context.GetComponent<T_PlayerCombat>();
            var shotgun = scope.Equip(Definition("item.weapon.shotgun.whiterefrigerant"), EquipSlotType.Weapon);
            await scope.WaitVisual(shotgun);
            Require(!combat.IsAutoFireWeapon, "산탄총이 연사 무기로 판정됨");
            var recoil = (TriggeredBuffUniqueEffectSO)shotgun.definition.uniqueEffect;
            var mana = scope.AddRelic(Definition("item.relic.manaconverter"));
            var manaEffect = (TriggeredBuffUniqueEffectSO)mana.definition.uniqueEffect;
            int back = DodgeSlot(skills);
            await Cast(context, back, null);
            Require(HasBuff(context, recoil), "A3 백스탭 스킬이 OnDodge(반동 스텝)를 발동하지 않음");
            Require(HasBuff(context, manaEffect), "A2 OnSkillCast(슬롯 무관) 마나 변환기 미발동");
            log.Add($"shotgun: autoFire=false, backstep slot={back} -> OnDodge RecoilStep + OnSkillCast ManaConverter");
            await scope.Reset(); context = scope.Context; skills = context.Skills;

            // A1 + A4: 헤일로 박격포(Fire) 스킬 적중
            var halo = scope.Equip(Definition("item.weapon.grenadelauncher.halomortar"), EquipSlotType.Weapon, ElementType.Fire);
            await scope.WaitVisual(halo);
            var haloEffect = (TriggeredBuffUniqueEffectSO)halo.definition.uniqueEffect;
            enemy = scope.Spawn(context.transform.position + context.transform.forward * 4f);
            Hit(context, enemy, DamageCause.Direct);
            Require(!HasBuff(context, haloEffect), "A1 거너 기본 적중이 Skill 전용 효과를 발동함");
            var hr = Record(enemy);
            int slot = Enumerable.Range(0, skills.SkillCount).First(i => i != back);
            await Cast(context, slot, enemy, 2500);
            Require(hr.Any(r => r.DamageCause == DamageCause.Skill), $"거너 {slot}번 스킬 피해가 기록되지 않음");
            Require(HasBuff(context, haloEffect), "A1 거너 스킬 적중이 Skill 전용 효과를 발동하지 않음");
            Require(enemy.GetComponent<WBH_StatusEffectController>().HasStatusEffect(WBH_StatusEffectType.Burn), "A4 Fire 무기 스킬 적중이 화상을 걸지 않음");
            log.Add($"halomortar: basic no buff, skill slot{slot + 1} hit -> buff + Burn");
        }
        return JsonConvert.SerializeObject(log);
    }

    // ---------- 헬퍼 ----------

    private static int DodgeSlot(ISkillController skills)
    {
        int slot = Enumerable.Range(0, skills.SkillCount).Where(i => skills.GetSkillDefinition(i)?.CountsAsDodge == true).DefaultIfEmpty(-1).First();
        Require(slot >= 0, "대시/백스탭 슬롯 없음");
        return slot;
    }

    /// <summary>SW 수정: 외부 입력과 같은 정식 TryUseSkill로 시전하고 애니메이션 이벤트 실행·상태 종료까지 기다린다.</summary>
    private static async Task Cast(PlayerContext context, int index, WBH_EnemyController target, int timeoutMs = 2000)
    {
        Vector3 aim = target != null ? target.transform.position - context.transform.position : context.transform.forward;
        aim.y = 0f;
        var stats = context.GetComponent<PlayerStatManager>();
        context.GetComponent<PlayerManaManager>().FillMana();
        bool used = context.Skills switch
        {
            FighterSkillController f => f.TryUseSkill(index, aim, stats),
            GunnerSkillController g => g.TryUseSkill(index, aim, target != null ? target.transform.position : context.transform.position + aim * 5f, stats),
            _ => false,
        };
        Require(used, $"{index}번 스킬 시전 실패 (cd={context.Skills.GetRemainingCooldown(index):F2})");
        var machine = context.GetComponent<WBH_PlayerStateMachine>();
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        await Task.Delay(100);
        while (machine.Is(PlayerState.Skill) && DateTime.UtcNow < deadline)
            await Task.Delay(25);
        await Task.Delay(300);
    }

    private static bool HasBuff(PlayerContext context, IBuffSource source) =>
        context.GetComponent<PlayerBuffManager>().ActiveBuffs.Any(b => ReferenceEquals(b.source, source) && !b.IsExpired);

    private static int StackOf(PlayerContext context, IBuffSource source) =>
        context.GetComponent<PlayerBuffManager>().ActiveBuffs.Where(b => ReferenceEquals(b.source, source)).Select(b => b.stackCount).FirstOrDefault();

    private static List<WBH_DamageResult> Record(WBH_EnemyController enemy)
    {
        var list = new List<WBH_DamageResult>();
        enemy.GetComponent<WBH_EnemyStatus>().OnDamaged += list.Add;
        return list;
    }

    private static void Hit(PlayerContext context, WBH_EnemyController enemy, DamageCause cause, float multiplier = 1f)
    {
        var request = new WBH_DamageRequest(context.Controller, enemy, cause == DamageCause.Skill ? WBH_AttackType.Skill : WBH_AttackType.Normal,
            ElementType.None, multiplier, null, null, enemy.transform.position, -context.transform.forward, cause, 0);
        Require(PlayerDamageResolver.TryProcessPlayerDamage(context, request, out _), "실제 피해 경계 실패");
    }

    private static ItemDefinitionSO Definition(string itemId)
    {
        var found = AssetDatabase.FindAssets("t:ItemDefinitionSO", new[] { ItemsFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null && d.itemId == itemId).ToArray();
        Require(found.Length == 1, $"아이템 정의 1개가 필요함: {itemId}, {found.Length}개");
        return found[0];
    }

    private static T Read<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private static async Task<PlayerScope> PreparePlayer(CharacterClass characterClass)
    {
        Require(Application.isPlaying && !EditorApplication.isPaused && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active, "일시정지 없는 싱글 Play 필요");
        Require(!running, "다른 검증 실행 중");
        var inventory = InventoryController.Instance;
        Require(inventory?.BoundPlayer != null && inventory.BoundPlayer.IsComplete, "실제 플레이어/인벤토리 연결 없음");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Character/Player/" + characterClass + ".prefab");
        Require(prefab != null && prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), "정식 플레이어 프리팹 없음 또는 Missing Script");
        var scope = new PlayerScope(inventory, characterClass);
        running = true;
        try
        {
            scope.Prepare(prefab);
            await Task.Delay(150);
            Require(scope.Context.IsComplete && scope.Context.Effects.CanExecute, "PlayerContext 불완전 또는 피해 권한 없음");
            scope.Context.Equipment.SetActiveCharacterClass(characterClass);
            scope.PreserveEquipment();
            return scope;
        }
        catch { scope.Dispose(); throw; }
    }

    /// <summary>SW 수정: 기존 플레이어·장비·유물·적 AI를 보존하고 검증용 정식 프리팹과 임시 아이템·적을 끝에 정리한다.</summary>
    private sealed class PlayerScope : IDisposable
    {
        private readonly InventoryController inventory;
        private readonly PlayerContext previous;
        private readonly CharacterClass previousClass, characterClass;
        private readonly List<KeyValuePair<EquipSlotType, InventoryItem>> preserved = new();
        private readonly List<(InventoryItem item, InventoryPlacementSnapshot placement)> relics = new();
        private readonly List<InventoryItem> temporary = new();
        private readonly List<WBH_EnemyController> enemies = new();
        private readonly List<(WBH_EnemyController enemy, bool enabled)> paused = new();
        private readonly List<Collider> hiddenColliders = new();
        private BuffDefinitionSO noCrit;
        private GameObject player;
        public PlayerContext Context { get; private set; }

        public PlayerScope(InventoryController inventory, CharacterClass characterClass)
        {
            this.inventory = inventory;
            this.characterClass = characterClass;
            previous = inventory.BoundPlayer;
            previousClass = inventory.EquipmentSystem.CurrentCharacterClass ?? CharacterClass.Fighter;
        }

        public void Prepare(GameObject prefab)
        {
            foreach (var enemy in Object.FindObjectsByType<WBH_EnemyController>(FindObjectsSortMode.None))
            {
                paused.Add((enemy, enemy.enabled));
                enemy.GetComponent<WBH_EnemyMovement>()?.SetControlEnable(false);
                enemy.enabled = false;
                // 기존 적이 검증 사격·범위를 가로채지 않게 충돌체만 잠시 끈다(Dispose에서 복원).
                foreach (var collider in enemy.GetComponentsInChildren<Collider>())
                    if (collider.enabled) { collider.enabled = false; hiddenColliders.Add(collider); }
            }
            foreach (var spawner in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(m => m.GetType().Name.Contains("WaveSpawner") && m.enabled))
                spawner.enabled = false;
            previous.gameObject.SetActive(false);
            player = Object.Instantiate(prefab, previous.transform.position, previous.transform.rotation);
            Context = player.GetComponent<PlayerContext>() ?? player.AddComponent<PlayerContext>();
            Context.Controller.SetControlEnable(false);
            if (player.TryGetComponent(out PlayerActionInputHandler input)) input.enabled = false;
            if (player.TryGetComponent(out WBH_PlayerInputHandler legacy)) legacy.enabled = false;
            Require(Context.BindSinglePlayerInventory(inventory), "실제 인벤토리 연결 실패");
            player.GetComponent<T_PlayerCombat>().Initialize(Object.FindFirstObjectByType<WBH_ProjectileSpawner>());
        }

        public void PreserveEquipment()
        {
            foreach (var pair in inventory.EquipmentSystem.GetEquippedItems().ToArray())
            {
                Unequip(pair.Key);
                preserved.Add(pair);
            }
            foreach (var item in inventory.GetAllInventoryItems().Where(i => i?.itemData?.definition?.itemId?.StartsWith("item.relic.", StringComparison.Ordinal) == true).ToArray())
            {
                var placement = InventoryPlacementSnapshot.Capture(inventory.PlayerGrid, item);
                Require(inventory.TryRemoveInventoryItem(item) == InventoryRemoveResult.Success, "기존 유물 보존 실패");
                relics.Add((item, placement));
            }
        }

        public ItemInstance Equip(ItemDefinitionSO definition, EquipSlotType slot, ElementType element = ElementType.None)
        {
            var data = ItemDataCreator.CreateItemData(definition);
            data.rolledElement = element;
            InventoryItem item = Add(data);
            Require(new EquipmentTransaction(inventory.EquipmentSystem).TryEquip(inventory.PlayerGrid, item, InventoryPlacementSnapshot.Capture(inventory.PlayerGrid, item), slot).IsSuccess, "장착 실패: " + definition.itemId);
            return data;
        }

        public ItemInstance AddRelic(ItemDefinitionSO definition)
        {
            var data = ItemDataCreator.CreateItemData(definition);
            Add(data);
            return data;
        }

        private InventoryItem Add(ItemInstance data)
        {
            var added = inventory.TryAddItemData(data);
            Require(added.Result == InventoryAddResult.Success, $"아이템 추가 실패: {data.definition.itemId}, {added.Result}");
            InventoryItem item = inventory.PlayerGrid.GetItemAt(added.X, added.Y);
            temporary.Add(item);
            return item;
        }

        public async Task WaitVisual(ItemInstance item)
        {
            var presenter = Context.GetComponent<PlayerWeaponVisualPresenter>();
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (!presenter.IsVisualReady || presenter.CurrentVisualItemId != item.definition.itemId)
            {
                Require(string.IsNullOrEmpty(presenter.VisualLoadError), presenter.VisualLoadError);
                Require(DateTime.UtcNow < deadline, "무기 외형 로드 시간 초과");
                await Task.Delay(25);
            }
        }

        public void NoCritical()
        {
            noCrit = ScriptableObject.CreateInstance<BuffDefinitionSO>();
            noCrit.name = "SkillLink no critical"; noCrit.duration = 0f;
            noCrit.statEffects = new[] { new FixedStatValue { statType = StatType.critRateFlat, value = -1000f } };
            Context.GetComponent<PlayerBuffManager>().ApplyBuff(noCrit);
        }

        public WBH_EnemyController Spawn(Vector3 position, float hp = 100000f)
        {
            var provider = Object.FindFirstObjectByType<WBH_EnemyDataProvider>();
            var pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
            Require(provider.TryCreateEnemyInfo(EnemyId, new WBH_EnemyStatContext(1, "normal", 1), out WBH_EnemyInfo info), "적 데이터 없음");
            info = info.Clone(); info.maxHP = hp;
            var enemy = pool.Get(EnemyId);
            enemies.Add(enemy);
            enemy.transform.SetPositionAndRotation(position, Quaternion.identity);
            enemy.GetComponent<EnemyKillReward>()?.Initialize(Context.Wallet);
            enemy.GetComponent<WBH_EnemyView>()?.Initialize(Object.FindFirstObjectByType<WBH_FloatTextPoolManager>(), Object.FindFirstObjectByType<WBH_HighEnemyHpbarView>());
            enemy.Initialize(info, pool, Object.FindFirstObjectByType<WBH_EffectSpawner>(), Object.FindFirstObjectByType<WBH_ProjectileSpawner>(), YJ_SfxPlayer.Instance);
            enemy.gameObject.SetActive(true);
            enemy.enabled = false;
            enemy.GetComponent<WBH_EnemyMovement>().SetControlEnable(false);
            Physics.SyncTransforms();
            return enemy;
        }

        /// <summary>SW 수정: 한 검사가 끝나면 임시 적·아이템·버프를 지우고 새 플레이어로 다음 검사를 시작해 쿨타임·버프가 섞이지 않게 한다.</summary>
        public async Task Reset()
        {
            Cleanup(restorePlayer: false);
            Prepare(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Character/Player/" + characterClass + ".prefab"));
            await Task.Delay(150);
            Require(Context.IsComplete && Context.Effects.CanExecute, "Reset 뒤 PlayerContext 불완전");
            Context.Equipment.SetActiveCharacterClass(characterClass);
        }

        private void Unequip(EquipSlotType slot)
        {
            Require(inventory.EquipmentSystem.TryGetEquippedItem(slot, out InventoryItem item), "해제할 장비 없음: " + slot);
            Require(inventory.PlayerGrid.TryFindEmptySpaceForItem(item, item.isRotated, out InventoryPlacementSnapshot destination), "장비 보존 공간 없음: " + slot);
            Require(new EquipmentTransaction(inventory.EquipmentSystem).TryUnequip(slot, inventory.PlayerGrid, destination).IsSuccess, "장비 해제 실패: " + slot);
        }

        private void Cleanup(bool restorePlayer)
        {
            var pool = Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
            foreach (var enemy in enemies.Distinct().Where(e => e != null))
            {
                enemy.GetComponent<WBH_StatusEffectController>()?.ClearAllStatusEffects();
                enemy.enabled = true;
                if (enemy.gameObject.activeSelf) pool.Return(enemy);
            }
            enemies.Clear();
            foreach (var item in temporary)
            {
                var equipped = inventory.EquipmentSystem.GetEquippedItems().FirstOrDefault(p => ReferenceEquals(p.Value, item));
                if (equipped.Value != null) Unequip(equipped.Key);
                if (inventory.PlayerGrid.ContainsItem(item)) inventory.TryRemoveInventoryItem(item);
            }
            temporary.Clear();
            if (noCrit != null) { Object.Destroy(noCrit); noCrit = null; }
            if (player != null) { player.SetActive(false); Object.Destroy(player); player = null; }
            if (!restorePlayer) return;
            inventory.EquipmentSystem.SetActiveCharacterClass(previousClass);
            previous.gameObject.SetActive(true);
            previous.BindSinglePlayerInventory(inventory);
            foreach (var pair in preserved)
                new EquipmentTransaction(inventory.EquipmentSystem).TryEquip(inventory.PlayerGrid, pair.Value, InventoryPlacementSnapshot.Capture(inventory.PlayerGrid, pair.Value), pair.Key);
            foreach (var (item, placement) in relics)
                inventory.TryAddItemAt(item, placement.Rect.X, placement.Rect.Y);
            foreach (var (enemy, enabled) in paused.Where(p => p.enemy != null))
                enemy.enabled = enabled;
            foreach (var collider in hiddenColliders.Where(c => c != null))
                collider.enabled = true;
        }

        public void Dispose()
        {
            try { Cleanup(restorePlayer: true); }
            finally { running = false; }
        }
    }
}
