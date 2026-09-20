#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEditor;
using UnityEngine;

/// <summary>실제 적 프리팹에서 화상 틱·재적용·출처·풀 정리·보스 배율을 검사합니다.</summary>
public static class StatusEffectP4Validation_MirrorTest
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("SW/Mirror Test/Validate Status Effect P4 (Burn)")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("컴파일이 끝난 Edit Mode에서 실행하세요.");

        var objects = new List<GameObject>();
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[StatusEffectP4] " + message);
            checks++;
        }

        try
        {
            var textPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WBHTest/Prefabs/Etc/DamageText.prefab");
            using (var textData = new SerializedObject(textPrefab.GetComponent<WBH_DamageText>()))
            {
                var font = textData.FindProperty("statusFont").objectReferenceValue as TMPro.TMP_FontAsset;
                bool supportsKorean = font != null;
                foreach (char letter in "화상저항면역")
                    supportsKorean &= font != null && font.HasCharacter(letter);
                Check(supportsKorean, "저항·면역 문구의 한글 글꼴이 연결되어야 합니다.");
            }

            GameObject player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab"));
            objects.Add(player);
            WBH_ICombat attacker = player.GetComponent<PlayerContext>().Controller;
            WBH_EnemyStatusEffectController effects = CreateEnemy(false, objects);
            WBH_EnemyStatus status = effects.GetComponent<WBH_EnemyStatus>();
            var hits = new List<WBH_DamageResult>();
            status.OnDamaged += hits.Add;
            WBH_StatusEffectData burn = WBH_StatusEffectPresets.Burn1;
            burn.Attacker = attacker;
            burn.AttackId = 401u;
            effects.AddStatusEffect(burn);
            Tick(effects, 0.5f);
            Check(hits.Count == 0, "최초 반 주기에는 피해가 없어야 합니다.");
            effects.AddStatusEffect(burn);
            Tick(effects, 0.5f);
            Check(hits.Count == 1, "재적용해도 첫 틱이 밀리지 않아야 합니다.");
            Check(hits[0].Attacker == attacker && hits[0].AttackId == 401u &&
                hits[0].DamageCause == DamageCause.DoT && hits[0].ElementType == ElementType.Fire &&
                !hits[0].IsCritical, "DoT 출처·공격 번호·속성·비치명 계약을 유지해야 합니다.");
            Check(Mathf.Approximately(hits[0].FinalDamage, 10f), "기존 최대 HP 1% 피해를 유지해야 합니다.");
            Tick(effects, 2.5f);
            Check(hits.Count == 3, "긴 프레임의 누락된 틱을 처리해야 합니다.");
            Tick(effects, 10f);
            Check(hits.Count == 5 && !effects.HasStatusEffect(WBH_StatusEffectType.Burn),
                "만료 이후의 시간으로 추가 피해를 만들지 않아야 합니다.");

            effects.AddStatusEffect(burn);
            Tick(effects, 0.5f);
            burn.Value = 0.02f;
            burn.AttackId = 402u;
            effects.AddStatusEffect(burn);
            Tick(effects, 0.5f);
            Check(hits.Count == 6 && Mathf.Approximately(hits[5].FinalDamage, 20f) &&
                hits[5].AttackId == 402u, "중첩 없이 마지막 적용의 강도와 출처를 사용해야 합니다.");

            effects.gameObject.SetActive(false);
            effects.gameObject.SetActive(true);
            effects.Initialize(null);
            Tick(effects, 2f);
            Check(hits.Count == 6 && !effects.HasStatusEffect(WBH_StatusEffectType.Burn),
                "풀 반환과 재사용 뒤 이전 화상이 남지 않아야 합니다.");
            burn.Interval = 0f;
            effects.AddStatusEffect(burn);
            Check(!effects.HasStatusEffect(WBH_StatusEffectType.Burn), "0초 틱 간격은 거절해야 합니다.");
            burn.Interval = 1e-10f;
            effects.AddStatusEffect(burn);
            Check(!effects.HasStatusEffect(WBH_StatusEffectType.Burn), "float 타이머가 줄지 않는 극소 간격도 거절해야 합니다.");

            var controller = effects.GetComponent<WBH_EnemyController>();
            burn = WBH_StatusEffectPresets.Burn1;
            controller.enabled = true;
            controller.AddStatusEffect(burn);
            Check(effects.HasStatusEffect(WBH_StatusEffectType.Burn), "활성 일반 적 컨트롤러도 상태이상을 받아야 합니다.");
            controller.ResetForPool();
            Check(!effects.HasStatusEffect(WBH_StatusEffectType.Burn), "풀 반환 준비 즉시 상태이상을 해제해야 합니다.");
            controller.enabled = false;
            controller.AddStatusEffect(burn);
            Check(effects.HasStatusEffect(WBH_StatusEffectType.Burn), "AI만 끈 Mirror 컨트롤러도 상태이상을 받아야 합니다.");
            effects.ClearAllStatusEffects();
            effects.enabled = false;
            controller.AddStatusEffect(burn);
            Check(!effects.HasStatusEffect(WBH_StatusEffectType.Burn), "꺼진 상태이상 컴포넌트에는 등록하지 않아야 합니다.");
            effects.enabled = true;

            WBH_EnemyStatusEffectController boss = CreateEnemy(true, objects);
            WBH_EnemyStatus bossStatus = boss.GetComponent<WBH_EnemyStatus>();
            var responses = new List<bool>();
            boss.OnBurnResponse += responses.Add;
            foreach (float multiplier in new[] { 1f, 0.25f, 0f })
            {
                boss.ClearAllStatusEffects();
                using (var data = new SerializedObject(boss))
                {
                    data.FindProperty("bossBurnDamageMultiplier").floatValue = multiplier;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                float before = bossStatus.CurrentHp;
                boss.AddStatusEffect(WBH_StatusEffectPresets.Burn1);
                Tick(boss, 1f);
                Check(Mathf.Approximately(before - bossStatus.CurrentHp, 10f * multiplier),
                    "보스 화상 배율 " + multiplier);
                if (multiplier == 0f)
                    Check(!boss.HasStatusEffect(WBH_StatusEffectType.Burn), "면역인 보스는 화상을 등록하지 않아야 합니다.");
            }
            // 같은 번호가 없는 시험은 같은 프레임에서 하나의 반응으로 합쳐집니다.
            Check(responses.Count == 1 && !responses[0], "같은 프레임의 번호 없는 중복 시도는 한 번만 표시합니다.");
            burn.Attacker = attacker;
            burn.AttackId = 700u;
            boss.AddStatusEffect(burn);
            boss.AddStatusEffect(burn);
            Check(responses.Count == 2 && responses[1], "같은 공격의 직접타·추가타 면역 표시는 한 번이어야 합니다.");
            burn.AttackId = 699u;
            boss.AddStatusEffect(burn);
            burn.AttackId = 700u;
            boss.AddStatusEffect(burn);
            Check(responses.Count == 3, "늦게 온 다른 공격은 표시하고 이전 공격 중복은 거절합니다.");
            burn.Attacker = null;
            boss.AddStatusEffect(burn);
            Check(responses.Count == 4, "같은 번호라도 출처가 다르면 별도 반응입니다.");
            burn.Interval = 0f;
            burn.AttackId++;
            boss.AddStatusEffect(burn);
            Check(responses.Count == 4, "잘못된 요청에는 면역 문구도 표시하지 않습니다.");
            boss.Initialize(null);
            burn.Interval = 1f;
            burn.AttackId = 700u;
            boss.AddStatusEffect(burn);
            Check(responses.Count == 5, "재사용 뒤에는 이전 공격 번호도 새로 표시합니다.");
            Debug.Log($"[StatusEffectP4] PASS {checks} checks. 실제 프리팹의 규칙 검사이며 Host 처치·원격 표시는 별도입니다.");
        }
        finally
        {
            foreach (GameObject instance in objects)
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>저장 프리팹은 바꾸지 않고 시험 인스턴스의 체력·권한만 준비합니다.</summary>
    private static WBH_EnemyStatusEffectController CreateEnemy(bool boss, List<GameObject> objects)
    {
        string name = boss ? "Boss_Act_01_MirrorTest" : "Normal_Melee_MirrorTest";
        GameObject instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/SW/TEST/MirrorCombat/Prefabs/" + name + ".prefab"));
        objects.Add(instance);
        instance.transform.position = new Vector3(42000f, 0f, 42000f);
        var authority = instance.GetComponent<NetworkEnemyAuthority_MirrorTest>();
        WBH_EnemyInfo info = authority.EnemyInfo.Clone();
        info.maxHP = 1000f;
        var status = instance.GetComponent<WBH_EnemyStatus>();
        var controller = instance.GetComponent<WBH_EnemyController>();
        typeof(WBH_EnemyController).GetField("info", PrivateInstance).SetValue(controller, info);
        typeof(WBH_EnemyController).GetField("status", PrivateInstance).SetValue(controller, status);
        typeof(WBH_EnemyController).GetField("statusEffectController", PrivateInstance)
            .SetValue(controller, instance.GetComponent<WBH_EnemyStatusEffectController>());
        status.Initialize(info);
        typeof(NetworkIdentity).GetProperty("isServer").SetValue(instance.GetComponent<NetworkIdentity>(), true);
        var effects = instance.GetComponent<WBH_EnemyStatusEffectController>();
        // Edit Mode의 일반 MonoBehaviour는 Awake를 자동 호출하지 않습니다.
        typeof(WBH_EnemyStatusEffectController).GetMethod("Awake", PrivateInstance).Invoke(effects, null);
        effects.Initialize(null);
        return effects;
    }

    /// <summary>실시간 기다림 없이 실제 상태 컨트롤러의 틱·만료 경로에 경과 시간을 전달합니다.</summary>
    private static void Tick(WBH_EnemyStatusEffectController effects, float seconds)
    {
        typeof(WBH_EnemyStatusEffectController).GetMethod("UpdateEffects", PrivateInstance)
            .Invoke(effects, new object[] { seconds });
    }
}
#endif
