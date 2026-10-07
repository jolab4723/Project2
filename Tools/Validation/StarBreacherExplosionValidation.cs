using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ItemSystem;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>SW 수정: Assets 밖 Editor run_script에서 정식 Gunner·적·장비 거래로 A2를 검증한다. 운영 저장과 런타임 시험 진입점을 추가하지 않는다.</summary>
public static class StarBreacherExplosionValidation
{
    /// <summary>SW 수정: 싱글 Play에서 기존 플레이어를 비활성화하고 정식 Gunner 원본의 상태를 그대로 연결한다. 저장된 클래스나 Spawner의 선택은 변경하지 않는다.</summary>
    public static async Task<string> PrepareSingle()
    {
        Require(Application.isPlaying && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active, "Single Play required");
        var old = Object.FindFirstObjectByType<T_PlayerController>();
        Require(old != null, "Actual player missing");
        PlayerContext context = old.GetComponent<PlayerContext>();
        if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(old)?.name != "Gunner")
        {
            Vector3 position = old.transform.position;
            Quaternion rotation = old.transform.rotation;
            old.gameObject.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Character/Player/Gunner.prefab");
            Require(prefab != null && prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), "Gunner original missing scripts");
            var gunner = Object.Instantiate(prefab, position, rotation);
            gunner.GetComponent<T_PlayerController>().SetControlEnable(false);
            var actionInput = gunner.GetComponent<PlayerActionInputHandler>();
            if (actionInput != null) actionInput.enabled = false;
            context = gunner.GetComponent<PlayerContext>() ?? gunner.AddComponent<PlayerContext>();
            Require(context.BindSinglePlayerInventory(InventoryController.Instance), "Actual Gunner inventory binding failed");
            gunner.GetComponent<T_PlayerCombat>().Initialize(Object.FindFirstObjectByType<WBH_ProjectileSpawner>());
        }
        Require(context != null && context.IsComplete, "Actual Gunner context incomplete");
        context.Equipment.SetActiveCharacterClass(CharacterClass.Gunner);
        Object.FindFirstObjectByType<YJ_MinimapPing>(FindObjectsInactive.Include)?.BindPlayer(context.transform, context.GetComponent<WBH_PlayerInputHandler>());
        Object.FindFirstObjectByType<WorldItemTooltipScanner>()?.BindPlayer(context.transform);
        await Task.Delay(100);
        ItemInstance item = Equip(context);
        // SW 수정: 실제 Addressables 완료를 기다려 늦은 외형 로드를 전투 연결 실패로 오판하지 않는다.
        var presenter=context.GetComponent<PlayerWeaponVisualPresenter>();
        Require(presenter!=null && presenter.isActiveAndEnabled,"Weapon presenter missing");
        DateTime deadline=DateTime.UtcNow.AddSeconds(10);
        while(!presenter.IsVisualReady || presenter.CurrentVisualItemId!=item.definition.itemId)
        {
            Require(string.IsNullOrEmpty(presenter.VisualLoadError),presenter.VisualLoadError);
            Require(DateTime.UtcNow<deadline,$"Visual timeout: ready={presenter.IsVisualReady}, current={presenter.CurrentVisualItemId}");
            await Task.Delay(25);
        }
        var binding = context.GetComponentInChildren<GunnerWeaponVfxBinding>();
        Require(binding != null && binding.WeaponType == GunnerWeaponType.Shotgun, "Actual equipped Shotgun visual binding missing");
        return JsonConvert.SerializeObject(new { context.name, context.IsComplete, item.definition.itemId,
            weapon = binding.WeaponType.ToString(), firePoint = FirePoint(context).position.ToString() });
    }

    /// <summary>SW 수정: 싱글의 실제 발사 원점·표면 적중점 2.99/3.01m, 최초 유효 적중, 사망·이동, 쿨다운·수명과 출처를 검증한다.</summary>
    public static async Task<string> RunBoundaries()
    {
        PlayerContext context = Context();
        ItemInstance item = Equip(context);
        Require(context.Effects.GetRemainingCooldown(item) == 0f, "Cooldown still active");
        var owned = new List<GameObject>();
        int bursts = 0;
        Vector3 shown = default;
        Action<Vector3, float> onBurst = (point, radius) => { bursts++; shown = point; Require(radius == 2.5f, "Radius mismatch"); };
        context.Effects.StarBreacherPresented += onBurst;
        Vector3 origin = FirePoint(context).position;
        Vector3 forward = context.transform.forward;
        try
        {
            var far = Spawn(origin + forward * 4f, 100000f, owned);
            var near = Spawn(origin + forward * 4f, 100000f, owned);
            SetDistance(far, origin, forward, 3.01f, context.transform.position.y);
            SetDistance(near, origin, forward, 2.99f, context.transform.position.y);
            Vector3 farPoint = Point(far, origin), nearPoint = Point(near, origin);
            context.Effects.SetDirectTargets(920001, new WBH_ICombat[] { far, near }, forward, attackOrigin: origin, shotgunAttack: true);
            Hit(context, far, farPoint, 920001);
            Require(bursts == 0 && context.Effects.GetRemainingCooldown(item) == 0f, "3.01m consumed cooldown");
            Hit(context, near, nearPoint, 920001);
            Require(bursts == 1 && Vector3.Distance(shown, nearPoint) < 0.001f, "Far first consumed first valid near hit");
            Hit(context, near, nearPoint, 920001);
            Require(bursts == 1, "Same AttackId exploded twice");
            context.Effects.SetDirectTargets(920002, new WBH_ICombat[] { near }, forward, attackOrigin: origin, shotgunAttack: true);
            Hit(context, near, nearPoint, 920002);
            Require(bursts == 1 && context.Effects.GetRemainingCooldown(item) > 0f, "Owner cooldown bypassed");
            context.Effects.ResetAttackLifetime();
            Require(context.Effects.GetRemainingCooldown(item) > 0f, "Lifetime reset removed cooldown");
            await Task.Delay(1300);
            foreach (DamageCause cause in new[] { DamageCause.Skill, DamageCause.Effect, DamageCause.DoT })
            {
                context.Effects.SetDirectTargets(920003, new WBH_ICombat[] { near }, forward, attackOrigin: origin, shotgunAttack: true);
                Hit(context, near, nearPoint, 920003, cause);
            }
            context.Effects.SetDirectTargets(920004, new WBH_ICombat[] { near }, forward, fighterAttack: true, attackOrigin: origin, shotgunAttack: false);
            Hit(context, near, nearPoint, 920004);
            Require(bursts == 1 && context.Effects.GetRemainingCooldown(item) == 0f, "Skill/Effect/DoT/Fighter source triggered");
            Return(owned); owned.Clear();
            var first = Spawn(origin + forward * 2f, 1f, owned);
            var living = Spawn(origin + forward * 2f + context.transform.right, 100000f, owned);
            Physics.SyncTransforms();
            Vector3 point = Point(first, origin), beforePosition = context.transform.position;
            float before = living.Status.CurrentHp;
            Action<WBH_DamageResult> onDirect = _ => context.transform.position = beforePosition + context.transform.right * 20f;
            first.GetComponent<WBH_EnemyStatus>().OnDamaged += onDirect;
            try
            {
                context.Effects.SetDirectTargets(920005, new WBH_ICombat[] { first }, forward, attackOrigin: origin, shotgunAttack: true);
                Hit(context, first, point, 920005, multiplier: 1000f);
            }
            finally { context.transform.position = beforePosition; first.GetComponent<WBH_EnemyStatus>().OnDamaged -= onDirect; }
            Require(first.Status.IsDead && living.Status.CurrentHp < before && bursts == 2 && Vector3.Distance(shown, point) < 0.001f,
                "Direct death or attacker movement lost saved impact");
            return JsonConvert.SerializeObject(new { bursts, nearDistance = Vector3.Distance(origin, nearPoint), farDistance = Vector3.Distance(origin, farPoint),
                tests = "2.99/3.01, far then near, once per AttackId, owner cooldown/lifetime, Skill/Effect/DoT/Fighter rejection, first death and attacker movement" });
        }
        finally { context.Effects.SetDirectTargets(0, null); context.Effects.StarBreacherPresented -= onBurst; Return(owned); }
    }

    /// <summary>SW 수정: 실제 싱글 피해 이벤트로 최대 5체·Collider 중복·벽·단차·치명타 100%의 비치명 Fire Effect·추가 Burn 부재를 검사한다.</summary>
    public static string RunTargetsAndDamage()
    {
        PlayerContext context = Context();
        Require(context.Effects.GetRemainingCooldown(Equip(context)) == 0f, "Cooldown still active");
        var owned = new List<GameObject>();
        GameObject wall = null, extra = null;
        var results = new Dictionary<WBH_EnemyController, List<WBH_DamageResult>>();
        var buff = ScriptableObject.CreateInstance<BuffDefinitionSO>();
        buff.name = "A2 critical validation"; buff.duration = 0f;
        buff.statEffects = new[] { new FixedStatValue { statType = StatType.critRateFlat, value = 100f } };
        var buffs = context.GetComponent<PlayerBuffManager>();
        try
        {
            buffs.ApplyBuff(buff);
            Vector3 origin = FirePoint(context).position, forward = context.transform.forward, right = context.transform.right;
            var first = Spawn(origin + forward * 2f, 100000f, owned);
            Physics.SyncTransforms();
            Vector3 point = Point(first, origin);
            var targets = new List<WBH_EnemyController> { first };
            for (int i = 0; i < 6; i++) targets.Add(Spawn(first.transform.position + right * (0.5f + i * 0.2f), 100000f, owned));
            var high = Spawn(first.transform.position - right + Vector3.up * 0.6f, 100000f, owned);
            var blocked = Spawn(first.transform.position - right * 2f, 100000f, owned);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "A2 wall validation";
            wall.layer = LayerMask.NameToLayer("Wall");wall.transform.position = first.transform.position - right + Vector3.up;
            wall.transform.rotation = Quaternion.LookRotation(right); wall.transform.localScale = new Vector3(2f, 2f, 0.1f);
            extra = new GameObject("A2 duplicate collider") { layer = 10 }; extra.transform.SetParent(first.transform, false);
            extra.AddComponent<SphereCollider>().radius = 0.1f;
            foreach (var enemy in targets.Concat(new[] {high,blocked}))
            {
                var list = new List<WBH_DamageResult>(); results[enemy] = list;
                enemy.GetComponent<WBH_EnemyStatus>().OnDamaged += list.Add;
            }
            Physics.SyncTransforms();
            context.Effects.SetDirectTargets(920010, new WBH_ICombat[] { first }, forward, attackOrigin: origin, shotgunAttack: true);
            Hit(context, first, point, 920010);
            var direct = results[first].Single(r => r.DamageCause == DamageCause.Direct);
            var effects = results.Values.SelectMany(x => x).Where(r => r.DamageCause == DamageCause.Effect).ToArray();
            Require(direct.IsCritical && effects.Length == 5 && effects.All(r => !r.IsCritical && r.ElementType == ElementType.Fire && r.AttackId == 920010 && !r.StatusEffect.HasValue),
                "Critical/Fire/Effect/max5/no Burn contract mismatch");
            Require(results[first].Count(r => r.DamageCause == DamageCause.Effect) == 1, "Living first target or collider dedupe mismatch");
            Require(results[high].Count == 0 && results[blocked].Count == 0, "Wall/elevation hit");
            return JsonConvert.SerializeObject(new { effects = effects.Length, directCritical = direct.IsCritical, effectCritical = effects[0].IsCritical,
                damage = effects[0].FinalDamage, tests = "max5, living first takes Direct+Effect, duplicate body collider, wall, elevation, crit100 Fire noncrit Effect no Burn" });
        }
        finally {
            foreach (var pair in results) if(pair.Key!=null)pair.Key.GetComponent<WBH_EnemyStatus>().OnDamaged-=pair.Value.Add;
            context.Effects.SetDirectTargets(0, null); buffs.RemoveBuff(buff); Object.Destroy(buff); if(wall!=null)Object.Destroy(wall); if(extra!=null)Object.Destroy(extra); Return(owned);
        }
    }

    /// <summary>SW 수정: 싱글에서 본체 중심이 반경 밖이어도 유효한 큰 Collider 표면은 적중하며 대상 0체 폭발도 쿨다운을 소비하는지 검사한다.</summary>
    public static async Task<string> RunSurfaceAndEmpty()
    {
        PlayerContext context=Context();ItemInstance item=Equip(context);
        Require(context.Effects.GetRemainingCooldown(item)==0f,"Cooldown still active");
        var owned=new List<GameObject>();GameObject surface=null;int bursts=0;
        Action<Vector3,float> onBurst=(_,_)=>bursts++;context.Effects.StarBreacherPresented+=onBurst;
        try{
            Vector3 origin=FirePoint(context).position,forward=context.transform.forward,right=context.transform.right;
            var first=Spawn(origin+forward*2f,1f,owned);Physics.SyncTransforms();Vector3 point=Point(first,origin);
            var large=Spawn(first.transform.position+right*3.5f,100000f,owned);
            surface=new GameObject("A2 large body surface"){layer=10};surface.transform.SetParent(large.transform,false);
            surface.transform.localPosition=new Vector3(0f,0.7f,0f);surface.AddComponent<SphereCollider>().radius=1.3f;
            Physics.SyncTransforms();float surfaceDistance=Vector3.Distance(Point(large,point),point);
            Require(Vector3.Distance(large.transform.position,point)>2.5f && surfaceDistance<2.5f,"Surface placement failed");
            float before=large.Status.CurrentHp;
            context.Effects.SetDirectTargets(920020,new WBH_ICombat[]{first},forward,attackOrigin:origin,shotgunAttack:true);
            Hit(context,first,point,920020,multiplier:1000f);
            Require(bursts==1 && large.Status.CurrentHp<before,"Large collider surface outside root distance missed");
            Object.Destroy(surface);surface=null;Return(owned);owned.Clear();await Task.Delay(1300);
            first=Spawn(origin+forward*2f,1f,owned);Physics.SyncTransforms();point=Point(first,origin);
            context.Effects.SetDirectTargets(920021,new WBH_ICombat[]{first},forward,attackOrigin:origin,shotgunAttack:true);
            Hit(context,first,point,920021,multiplier:1000f);
            Require(bursts==2 && context.Effects.GetRemainingCooldown(item)>0f,"Empty burst missing cooldown/presentation");
            return JsonConvert.SerializeObject(new {bursts,surfaceDistance,tests="large body collider surface, empty burst cooldown"});
        }
        finally{context.Effects.SetDirectTargets(0,null);context.Effects.StarBreacherPresented-=onBurst;if(surface!=null)Object.Destroy(surface);Return(owned);}
    }

    /// <summary>SW 수정: 실제 싱글에서 직접 피해 콜백이 공격력을 바꿔도 폭발 전체가 동일한 이전 스냅샷을 사용하고 장비 교체·비활성화로 쿨다운을 지우지 않는지 검사한다.</summary>
    public static async Task<string> RunSnapshotAndLifetime()
    {
        PlayerContext context=Context();ItemInstance item=Equip(context);
        Require(context.Effects.GetRemainingCooldown(item)==0f,"Cooldown still active");
        var owned=new List<GameObject>();var subscriptions=new Dictionary<WBH_EnemyController,Action<WBH_DamageResult>>();
        var buff=ScriptableObject.CreateInstance<BuffDefinitionSO>();buff.name="A2 snapshot validation";buff.duration=0f;
        buff.statEffects=new[]{new FixedStatValue{statType=StatType.attackPowerFlat,value=500f}};
        var buffs=context.GetComponent<PlayerBuffManager>();
        try{
            Vector3 origin=FirePoint(context).position,forward=context.transform.forward,right=context.transform.right;
            var first=Spawn(origin+forward*2f,100000f,owned);
            float baseline=0f;
            Action<WBH_DamageResult> baselineHit=r=>{if(r.DamageCause==DamageCause.Effect)baseline=r.FinalDamage;};
            subscriptions[first]=baselineHit;first.GetComponent<WBH_EnemyStatus>().OnDamaged+=baselineHit;
            Physics.SyncTransforms();context.Effects.SetDirectTargets(920030,new WBH_ICombat[]{first},forward,attackOrigin:origin,shotgunAttack:true);
            Hit(context,first,Point(first,origin),920030);Require(baseline>0f,"Baseline effect missing");
            first.GetComponent<WBH_EnemyStatus>().OnDamaged-=baselineHit;subscriptions.Clear();Return(owned);owned.Clear();
            await Task.Delay(1300);
            first=Spawn(origin+forward*2f,100000f,owned);
            var living=new[]{first,Spawn(first.transform.position+right*0.5f,100000f,owned),Spawn(first.transform.position+right,100000f,owned)};
            float attackBefore=context.Controller.Status.AttackPower;var damages=new List<float>();
            foreach(var enemy in living){
                Action<WBH_DamageResult> handler=r=>{
                    if(r.DamageCause==DamageCause.Direct)buffs.ApplyBuff(buff);
                    if(r.DamageCause==DamageCause.Effect)damages.Add(r.FinalDamage);
                };
                subscriptions[enemy]=handler;enemy.GetComponent<WBH_EnemyStatus>().OnDamaged+=handler;
            }
            Physics.SyncTransforms();context.Effects.SetDirectTargets(920031,new WBH_ICombat[]{first},forward,attackOrigin:origin,shotgunAttack:true);
            Hit(context,first,Point(first,origin),920031);
            Require(context.Controller.Status.AttackPower>attackBefore && damages.Count==3 && damages.All(d=>Mathf.Abs(d-baseline)<0.001f),"Explosion snapshot changed with Direct callback stat mutation");
            var grid=context.Inventory.PlayerGrid;var transaction=new EquipmentTransaction(context.Equipment);
            Require(context.Equipment.TryGetEquippedItem(EquipSlotType.Weapon,out InventoryItem weapon),"Equipped weapon missing");
            Require(grid.TryFindEmptySpaceForItem(weapon,weapon.isRotated,out InventoryPlacementSnapshot destination),"Unequip destination missing");
            Require(transaction.TryUnequip(EquipSlotType.Weapon,grid,destination).IsSuccess,"Unequip failed");
            Require(context.Effects.GetRemainingCooldown(item)>0f,"Unequip cleared owner cooldown");
            Require(transaction.TryEquip(grid,weapon,InventoryPlacementSnapshot.Capture(grid,weapon),EquipSlotType.Weapon).IsSuccess,"Reequip failed");
            Require(context.Effects.GetRemainingCooldown(item)>0f,"Reequip cleared owner cooldown");
            context.gameObject.SetActive(false);context.gameObject.SetActive(true);
            Require(context.BindSinglePlayerInventory(InventoryController.Instance) && context.Effects.GetRemainingCooldown(item)>0f,"Disable/rebind cleared cooldown or inventory");
            return JsonConvert.SerializeObject(new {baseline,damages,attackBefore,attackAfter=context.Controller.Status.AttackPower,tests="one burst snapshot despite callback buff, unequip/reequip, actual OnDisable/rebind owner cooldown"});
        }
        finally{
            foreach(var pair in subscriptions)if(pair.Key!=null)pair.Key.GetComponent<WBH_EnemyStatus>().OnDamaged-=pair.Value;
            context.Effects.SetDirectTargets(0,null);buffs.RemoveBuff(buff);Object.Destroy(buff);Return(owned);
        }
    }

    /// <summary>SW 수정: 실제 싱글 적의 직접 처치와 폭발 처치가 각각 보상을 한 번 지급하고 죽은 대상의 추가 요청이 정산을 반복하지 않는지 검증한다.</summary>
    public static string RunRewardsOnce()
    {
        PlayerContext context=Context();ItemInstance item=Equip(context);
        Require(context.Effects.GetRemainingCooldown(item)==0f,"Cooldown still active");
        var stats=context.GetComponent<PlayerStatManager>();
        Require(PlayerStatManager.Instance==stats,"Actual reward stat owner mismatch");
        var owned=new List<GameObject>();var subscriptions=new Dictionary<EnemyKillReward,Action<int>>();
        var paid=new Dictionary<EnemyKillReward,int>();int granted=0;
        try{
            Vector3 origin=FirePoint(context).position,forward=context.transform.forward,right=context.transform.right;
            var first=Spawn(origin+forward*2f,1f,owned);
            var targets=new[]{first,Spawn(first.transform.position+right*0.6f,1f,owned),Spawn(first.transform.position+right*1.2f,1f,owned)};
            int expected=targets.Sum(t=>t.Info.credit),goldBefore=context.Wallet.Gold,levelBefore=stats.CurrentLevel;
            float expBefore=stats.CurrentExp;
            foreach(var enemy in targets){
                var reward=enemy.GetComponent<EnemyKillReward>();Require(reward!=null,"Actual reward component missing");paid[reward]=0;
                Action<int> handler=amount=>{paid[reward]++;granted+=amount;};
                subscriptions[reward]=handler;reward.OnCreditGranted+=handler;
            }
            Physics.SyncTransforms();context.Effects.SetDirectTargets(920050,new WBH_ICombat[]{first},forward,attackOrigin:origin,shotgunAttack:true);
            Hit(context,first,Point(first,origin),920050);
            Require(targets.All(t=>t.Status.IsDead) && paid.Values.All(n=>n==1) && granted==expected && context.Wallet.Gold-goldBefore==expected,"Direct/effect reward was missing or duplicated");
            Require(stats.CurrentLevel>levelBefore || (stats.CurrentLevel==levelBefore && stats.CurrentExp>expBefore),"Actual experience did not advance");
            int goldAfter=context.Wallet.Gold,levelAfter=stats.CurrentLevel;float expAfter=stats.CurrentExp;
            foreach(var enemy in targets){
                var request=new WBH_DamageRequest(context.Controller,enemy,WBH_AttackType.Normal,ElementType.Fire,1f,null,null,
                    enemy.transform.position,-forward,DamageCause.Effect,920051);
                Require(!PlayerDamageResolver.TryProcessPlayerDamage(context,request,out _),"Dead target accepted additional damage");
            }
            Require(context.Wallet.Gold==goldAfter && stats.CurrentLevel==levelAfter && stats.CurrentExp==expAfter && paid.Values.All(n=>n==1),"Dead target repeated reward");
            return JsonConvert.SerializeObject(new {kills=targets.Length,credit=granted,events=paid.Values.ToArray(),levelBefore,levelAfter,expBefore,expAfter,tests="actual Direct+Effect kills, wallet/XP owner, reward once, dead target rejection"});
        }
        finally{
            foreach(var pair in subscriptions)if(pair.Key!=null)pair.Key.OnCreditGranted-=pair.Value;
            context.Effects.SetDirectTargets(0,null);Return(owned);
        }
    }

    /// <summary>SW 수정: 정식 장착 외형이 Shotgun으로 판정되는 실제 TryAttack→애니메이션 이벤트 경로와 싱글 HUD를 검사한다.</summary>
    public static async Task<string> RunActualAttackAndHud()
    {
        PlayerContext context = Context(); ItemInstance item = Equip(context);
        Require(context.Effects.GetRemainingCooldown(item) == 0f, "Cooldown still active");
        var owned = new List<GameObject>(); int bursts = 0;
        Action<Vector3,float> onBurst = (_,_) => bursts++;
        context.Effects.StarBreacherPresented += onBurst;
        try
        {
            Vector3 origin = FirePoint(context).position, forward = context.transform.forward;
            var first = Spawn(origin + forward * 2f, 100000f, owned);
            Physics.SyncTransforms(); float before = first.Status.CurrentHp;
            context.GetComponent<T_PlayerCombat>().TryAttack(origin + forward * 5f);
            for(int i=0;i<20 && bursts==0;i++)await Task.Delay(50);
            Require(bursts==1 && first.Status.CurrentHp<before, "Actual Shotgun basic attack failed");
            await Task.Delay(30);
            var slots = Object.FindObjectsByType<CooldownIconSlot>(FindObjectsSortMode.None).Where(s=>s.gameObject.activeInHierarchy).ToArray();
            var slot = slots.FirstOrDefault(s=>s.GetComponentsInChildren<UnityEngine.UI.Image>().Any(i=>i.sprite==item.definition.icon));
            Require(slot!=null && context.Effects.GetRemainingCooldown(item)>0f,"Actual owner HUD/icon/cooldown missing");
            slot.OnPointerEnter(null);
            string[] texts=Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Where(t=>t.gameObject.activeInHierarchy && t.text.Contains("브리처")).Select(t=>t.text).ToArray();
            Require(texts.Length>0,"Translated tooltip missing");
            return JsonConvert.SerializeObject(new {bursts, cooldown=context.Effects.GetRemainingCooldown(item),icon=true,texts, actualPrefab="Gunner"});
        }
        finally { context.Effects.StarBreacherPresented-=onBurst;Return(owned); }
    }

    /// <summary>SW 수정: 실제 싱글 폭발·아이콘·번역 툴팁이 렌더된 뒤 Editor만 일시정지해 Overlay 포함 화면을 캡처할 수 있게 한다. Play 종료 시 검증용 적과 일시정지를 정리한다.</summary>
    public static async Task<string> PrepareSingleVisual()
    {
        PlayerContext context=Context();ItemInstance item=Equip(context);
        Require(context.Effects.GetRemainingCooldown(item)==0f,"Cooldown still active");
        Vector3 origin=FirePoint(context).position,forward=context.transform.forward;
        var owned=new List<GameObject>();
        var first=Spawn(origin+forward*2f,100000f,owned);
        Physics.SyncTransforms();Vector3 point=Point(first,origin);
        context.Effects.SetDirectTargets(920040,new WBH_ICombat[]{first},forward,attackOrigin:origin,shotgunAttack:true);
        try{Hit(context,first,point,920040);}
        finally{context.Effects.SetDirectTargets(0,null);}
        await Task.Delay(40);
        var slot=Object.FindObjectsByType<CooldownIconSlot>(FindObjectsSortMode.None)
            .FirstOrDefault(s=>s.gameObject.activeInHierarchy && s.GetComponentsInChildren<UnityEngine.UI.Image>().Any(i=>i.sprite==item.definition.icon));
        Require(slot!=null,"Actual HUD icon missing");slot.OnPointerEnter(null);
        await Task.Delay(40);
        var lines=context.GetComponentsInChildren<LineRenderer>().Where(l=>l.name=="Star Breacher Explosion Presentation").ToArray();
        Require(lines.Length==1 && lines[0].sharedMaterial!=null && lines[0].positionCount==32,"Actual explosion renderer missing");
        EditorApplication.isPaused=true;
        return JsonConvert.SerializeObject(new {lines=lines.Length,icon=true,cooldown=context.Effects.GetRemainingCooldown(item),
            texts=Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Where(t=>t.gameObject.activeInHierarchy && (t.text.Contains("브리처") || t.text.Contains("2.5m"))).Select(t=>t.text).ToArray()});
    }

    /// <summary>SW 수정: 싱글의 실제 소유 플레이어·장비를 조회하며 네트워크 검증을 섞지 않는다.</summary>
    private static PlayerContext Context()
    {
        Require(Application.isPlaying && !Mirror.NetworkServer.active && !Mirror.NetworkClient.active,"Single Play required");
        var context=InventoryController.Instance?.BoundPlayer;
        Require(context!=null && context.IsComplete && context.name.StartsWith("Gunner"),"Actual Gunner required"); return context;
    }

    /// <summary>SW 수정: 기존 무기를 가방에 보존하는 정식 장비 거래로 스타 브리처를 장착한다.</summary>
    private static ItemInstance Equip(PlayerContext context)
    {
        if(context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon,out ItemInstance equipped) && equipped?.definition?.uniqueEffect is StarBreacherExplosionUniqueEffectSO)return equipped;
        var grid=context.Inventory.PlayerGrid;var transaction=new EquipmentTransaction(context.Equipment);
        if(context.Equipment.TryGetEquippedItem(EquipSlotType.Weapon,out InventoryItem old)){
            Require(grid.TryFindEmptySpaceForItem(old,old.isRotated,out InventoryPlacementSnapshot destination),"Old weapon storage unavailable");
            Require(transaction.TryUnequip(EquipSlotType.Weapon,grid,destination).IsSuccess,"Old weapon unequip failed");
        }
        var definition=AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath("139db515956b9cf4fbec78b50734d207"));
        Require(definition?.uniqueEffect is StarBreacherExplosionUniqueEffectSO,"Actual A2 data missing");
        var item=ItemDataCreator.CreateItemData(definition);var added=context.Inventory.TryAddItemData(item);
        Require(added.Result==InventoryAddResult.Success,"Item add failed");var inventoryItem=grid.GetItemAt(added.X,added.Y);
        Require(transaction.TryEquip(grid,inventoryItem,InventoryPlacementSnapshot.Capture(grid,inventoryItem),EquipSlotType.Weapon).IsSuccess,"A2 equip failed");return item;
    }

    /// <summary>SW 수정: 실제 거너의 공통 FirePoint와 적 본체에 속한 Collider의 표면을 사용한다.</summary>
    private static Transform FirePoint(PlayerContext context)=>new SerializedObject(context.GetComponent<T_PlayerCombat>()).FindProperty("firePoint").objectReferenceValue as Transform;
    private static Vector3 Point(WBH_EnemyController enemy,Vector3 origin)=>enemy.GetComponentsInChildren<Collider>().Where(c=>c.enabled && c.gameObject.layer==10).Select(c=>c.ClosestPoint(origin)).OrderBy(p=>(p-origin).sqrMagnitude).First();

    /// <summary>SW 수정: 본체 거리 대신 실제 Collider 표면의 3m 경계를 맞추고 최종 오차를 검사한다.</summary>
    private static void SetDistance(WBH_EnemyController enemy,Vector3 origin,Vector3 forward,float distance,float rootY)
    {
        for(int i=0;i<8;i++){
            Vector3 p=enemy.transform.position;p.y=rootY;enemy.transform.position=p;Physics.SyncTransforms();
            enemy.transform.position+=forward*(distance-Vector3.Distance(origin,Point(enemy,origin)));
        }
        Physics.SyncTransforms();Require(Mathf.Abs(Vector3.Distance(origin,Point(enemy,origin))-distance)<0.002f,"Collider boundary placement failed");
    }

    /// <summary>SW 수정: 싱글·서버 공통 피해 경계를 실제 적중점과 출처로 실행하며 후속 피해 내부 함수를 직접 호출하지 않는다.</summary>
    private static void Hit(PlayerContext context,WBH_EnemyController enemy,Vector3 point,uint attackId,DamageCause cause=DamageCause.Direct,float multiplier=1f)
    {
        var request=new WBH_DamageRequest(context.Controller,enemy,cause==DamageCause.Skill?WBH_AttackType.Skill:WBH_AttackType.Normal,
            ElementType.None,multiplier,null,null,point,-context.transform.forward,cause,attackId);
        Require(PlayerDamageResolver.TryProcessPlayerDamage(context,request,out _),"Actual damage failed");
    }

    /// <summary>SW 수정: 정식 Provider·Pool·적 프리팹을 초기화하고 검증용 HP만 복제 데이터에서 정한다.</summary>
    private static WBH_EnemyController Spawn(Vector3 position,float hp,List<GameObject> owned)
    {
        var provider=Object.FindFirstObjectByType<WBH_EnemyDataProvider>();var pool=Object.FindFirstObjectByType<WBH_EnemyPoolManager>();
        Require(provider.TryCreateEnemyInfo("enemy.normal.melee.working_machine",new WBH_EnemyStatContext(1,"normal",1),out WBH_EnemyInfo info),"Enemy data missing");
        info=info.Clone();info.maxHP=hp;var enemy=pool.Get("enemy.normal.melee.working_machine");enemy.transform.SetPositionAndRotation(position,Quaternion.identity);owned.Add(enemy.gameObject);
        enemy.GetComponent<EnemyKillReward>()?.Initialize(InventoryController.Instance.BoundPlayer.Wallet);
        enemy.GetComponent<WBH_EnemyView>()?.Initialize(Object.FindFirstObjectByType<WBH_FloatTextPoolManager>(),Object.FindFirstObjectByType<WBH_HighEnemyHpbarView>());
        enemy.Initialize(info,pool,Object.FindFirstObjectByType<WBH_EffectSpawner>(),Object.FindFirstObjectByType<WBH_ProjectileSpawner>(),YJ_SfxPlayer.Instance);
        enemy.GetComponent<WBH_EnemyMovement>().SetControlEnable(false);enemy.gameObject.SetActive(true);return enemy;
    }

    /// <summary>SW 수정: 대여한 실제 적만 반환하며 사망으로 이미 반환된 객체를 다시 넣지 않는다.</summary>
    private static void Return(List<GameObject> owned)
    {
        var pool=Object.FindFirstObjectByType<WBH_EnemyPoolManager>();foreach(var obj in owned.Distinct())if(obj!=null && obj.activeSelf)pool.Return(obj.GetComponent<WBH_EnemyController>());
    }
    /// <summary>SW 수정: 실제 검증의 계약 위반을 실패로 보고해 실행하지 않은 항목을 통과로 기록하지 않는다.</summary>
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
