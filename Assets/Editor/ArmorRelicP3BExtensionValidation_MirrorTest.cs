#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Mirror;
using ItemSystem;

/// <summary>폐기용 1인 Host에서 실제 인벤토리 요청과 회피 상태 진입으로 B1/R2를 검사합니다.</summary>
public static class ArmorRelicP3BExtensionValidation_MirrorTest
{
    private static int checks;
    private static bool running;

    /// <summary>아이템을 추가하고 장비를 바꾸므로 폐기할 1인 Host에서만 실행합니다.</summary>
    [MenuItem("SW/Mirror Test/Validate P3B Boots and Mana Relay (Disposable Host)")]
    public static async void Validate()
    {
        if (running)
            return;

        if (!EditorUtility.DisplayDialog("폐기용 Host 검사", "시험 부츠와 유물을 지급하고 장착·드랍합니다. 검사 후 이 세션을 종료하세요.", "시작", "취소"))
            return;

        try
        {
            await Run();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    /// <summary>실패한 동작을 바로 알리고 통과한 항목 수를 셉니다.</summary>
    private static void Check(bool valid, string message)
    {
        if (!valid)
            throw new InvalidOperationException("[P3BExtension] " + message);
        checks++;
    }

    /// <summary>게임 시간 기준으로 기다리되 Host 종료나 일시 정지에는 중단합니다.</summary>
    private static async Task Wait(float seconds)
    {
        float until = Time.time + seconds;
        double deadline = EditorApplication.timeSinceStartup + seconds * 4 + 5;
        while (Time.time < until)
        {
            if (!Application.isPlaying || !NetworkServer.active || EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Host가 종료되었거나 게임 시간이 멈췄습니다.");
            await Task.Delay(30);
        }
    }

    /// <summary>실제 생성된 아이템 정의를 ID로 찾습니다.</summary>
    private static ItemDefinitionSO Definition(string id) => AssetDatabase.FindAssets("t:ItemDefinitionSO",
        new[] { "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items" })
        .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid)))
        .Single(definition => definition.itemId == id);

    /// <summary>기존 서버 보상 경로를 사용해 시험 아이템을 지급합니다.</summary>
    private static ItemInstance Grant(PlayerContext player, string id)
    {
        var item = new ItemInstance { instanceId = Guid.NewGuid().ToString(), definition = Definition(id) };
        var result = typeof(PlayerInventorySync_MirrorTest)
            .GetMethod("ServerGrantQuestReward", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(player.GetComponent<PlayerInventorySync_MirrorTest>(), new object[] { item });
        Check(result.ToString() == "Success", "시험 아이템을 서버 가방에 추가: " + id);
        return item;
    }

    /// <summary>동일 효과 원본에서 적용된 버프 수를 확인합니다.</summary>
    private static int Count(PlayerContext player, UniqueEffectSO effect) =>
        player.Buffs.ActiveBuffs.Count(buff => ReferenceEquals(buff.source, effect));

    /// <summary>실제 장착 요청·회피 상태·소유권 변경을 통해 B1과 R2를 검사합니다.</summary>
    public static async Task<string> Run()
    {
        if (running || !Application.isPlaying || !NetworkServer.active || !NetworkClient.ready ||
            NetworkClient.localPlayer == null || NetworkServer.connections.Count != 1 ||
            !UnityEngine.SceneManagement.SceneManager.GetActiveScene().path.StartsWith("Assets/SW/TEST/"))
            throw new InvalidOperationException("다른 검사가 없는 SW TEST의 폐기용 1인 Host에서만 실행하세요.");

        var player = NetworkClient.localPlayer.GetComponent<PlayerContext>();
        var provider = player.GetComponent<PlayerRelicEffectProvider_MirrorTest>();
        if (player.Health.CurrentHealth <= 0f || provider == null || !provider.enabled ||
            player.Inventory.PlayerGrid.GetAllItems().Any(item => item.itemData.definition.itemId == "item.relic.manarelay"))
            throw new InvalidOperationException("살아 있는 플레이어와 활성 유물 Provider가 필요합니다. 기존 마나 중계기는 먼저 가방에서 빼주세요.");

        var sync = player.GetComponent<PlayerInventorySync_MirrorTest>();
        bool oldPause = player.Mana.IsRegenPaused;
        float oldMana = player.Mana.CurrentMana;
        checks = 0;
        running = true;
        try
        {
            player.Mana.IsRegenPaused=true;
            player.Mana.FillMana();
            var boots=Grant(player,"item.armor.boots.discardedboostermodule");
            await Wait(0.1f);
            Check(sync.TryRequestEquipmentChange(boots.instanceId,true,EquipSlotType.Boots,-1,-1,false,out _),"UI와 같은 부츠 장착 요청");
            await Wait(0.15f);
            Check(player.Equipment.TryGetEquippedItemInstance(EquipSlotType.Boots,out var equipped) && equipped.instanceId==boots.instanceId,"실제 장착 확정");
            var thrust=(TriggeredBuffUniqueEffectSO)boots.definition.uniqueEffect;
            float speed=player.Stats.Stat.moveSpeed;
            player.StateMachine.ChangeState(PlayerState.Idle);
            player.StateMachine.ChangeState(PlayerState.Dodge);
            Check(Count(player,thrust)==1 && Mathf.Approximately(player.Stats.Stat.moveSpeed,speed*1.2f),"회피 진입 +20% 이동 버프 1개");
            Check(player.ItemTriggers.GetRemainingCooldown(equipped)>5.8f,"6초 쿨다운 시작");
            await Wait(2.1f);
            Check(Count(player,thrust)==0 && Mathf.Approximately(player.Stats.Stat.moveSpeed,speed),"2초 만료와 기본 속도 복원");
            player.StateMachine.ChangeState(PlayerState.Idle);
            player.StateMachine.ChangeState(PlayerState.Dodge);
            Check(Count(player,thrust)==0,"쿨다운 중 회피 재발동 차단");
            await Wait(4.1f);
            player.StateMachine.ChangeState(PlayerState.Idle);
            player.StateMachine.ChangeState(PlayerState.Dodge);
            Check(Count(player,thrust)==1,"쿨다운 종료 후 재발동");
            await Wait(2.1f);

            float regen=player.Stats.Stat.mpRegen;
            var first=Grant(player,"item.relic.manarelay");
            await Wait(0.2f);
            var aura=(FieldAuraUniqueEffectSO)first.definition.uniqueEffect;
            Check(Count(player,aura)==1 && Mathf.Approximately(player.Stats.Stat.mpRegen,regen*1.15f),"혼자 있어도 R2 +15% 적용");
            var second=Grant(player,"item.relic.manarelay");
            await Wait(0.2f);
            Check(Count(player,aura)==1 && Mathf.Approximately(player.Stats.Stat.mpRegen,regen*1.15f),"유물 두 사본 비중첩");
            provider.enabled=false;
            Check(Count(player,aura)==0,"Provider 비활성화 즉시 해제");
            provider.enabled=true;
            await Wait(0.2f);
            Check(Count(player,aura)==1,"재활성화 시 실제 소유 가방으로 복구");
            Check(sync.TryRequestDropInventoryItem(first.instanceId,out _),"첫 사본 드랍 요청");
            await Wait(0.2f);
            Check(Count(player,aura)==1,"남은 사본 효과 유지");
            Check(sync.TryRequestDropInventoryItem(second.instanceId,out _),"마지막 사본 드랍 요청");
            await Wait(0.2f);
            Check(Count(player,aura)==0 && Mathf.Approximately(player.Stats.Stat.mpRegen,regen),"마지막 소유 해제 후 복원");
            var result=$"[P3BExtension] PASS {checks} checks; {player.Equipment.CurrentCharacterClass}; movement={speed}->{speed*1.2f}; regen={regen}->{regen*1.15f}. 네트워크 요청 검사이며 포인터 입력·원격 검증은 별도입니다.";
            Debug.Log(result);
            return result;
        }
        finally
        {
            running = false;
            if (provider != null)
                provider.enabled = true;
            if (player != null && player.Mana != null)
            {
                player.Mana.SetCurrentMana(oldMana);
                player.Mana.IsRegenPaused = oldPause;
            }
        }
    }
}

#endif
