using System;
using ItemSystem;
using UnityEditor;
using UnityEngine;

/// <summary>공통 피해 계산 순서·출처·스냅샷 계약을 작은 독립 검사로 보존한다.</summary>
public static class MirrorStage3CombatValidation
{
    [MenuItem("SW/Mirror Test/Validate Stage3 Player Assets")]
    public static void ValidatePlayerAssets()
    {
        foreach (string character in new[] { "Fighter", "Gunner" })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/Character/Player/" + character + ".prefab");
            var mirror = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorPlayerContext/Prefabs/" +
                (character == "Fighter" ? "FighterNetworkPlayer" : "GunnerNetworkPlayer_MirrorTest") + ".prefab");
            var sourceSkill = (Component)source.GetComponent<FighterSkillController>() ?? source.GetComponent<GunnerSkillController>();
            var mirrorSkill = mirror.GetComponent(sourceSkill.GetType());
            var sourceSkills = new SerializedObject(sourceSkill).FindProperty("skills");
            var mirrorSkills = new SerializedObject(mirrorSkill).FindProperty("skills");
            var authority = mirror.GetComponent<FighterSkillAuthority_MirrorTest>();
            Require(mirror.GetComponents<WBH_PlayerEffect>().Length == 1, character + " 이펙트 컴포넌트 단일 소유");
            Require(new SerializedObject(authority).FindProperty("playerEffect").objectReferenceValue == mirror.GetComponent<WBH_PlayerEffect>(),
                character + " 스킬/애니메이션 이펙트 참조 일치");
            Require(mirror.GetComponent<Animator>().runtimeAnimatorController ==
                source.GetComponent<Animator>().runtimeAnimatorController, character + " 원본 애니메이터 공유");
            Require(authority.SkillCount == sourceSkills.arraySize && mirrorSkills.arraySize == sourceSkills.arraySize,
                character + " 궁극기를 포함한 스킬 개수");
            for (int i = 0; i < sourceSkills.arraySize; i++)
                Require(authority.GetSkillDefinition(i) == sourceSkills.GetArrayElementAtIndex(i).objectReferenceValue &&
                    mirrorSkills.GetArrayElementAtIndex(i).objectReferenceValue == sourceSkills.GetArrayElementAtIndex(i).objectReferenceValue,
                    character + " 스킬 데이터 " + i);
            var sourceEffects = new SerializedObject(source.GetComponent<WBH_PlayerEffect>());
            var mirrorEffects = new SerializedObject(mirror.GetComponent<WBH_PlayerEffect>());
            var bindings = sourceEffects.FindProperty("effectBindings");
            var cues = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < bindings.arraySize; i++)
                Require(cues.Add(bindings.GetArrayElementAtIndex(i).FindPropertyRelative("cue").intValue),
                    character + " 원본 이펙트 큐 중복");
            if (character == "Fighter") Require(cues.Contains(1431), "파이터 궁극기 진화3 버프 큐");
            else
            {
                var projectile = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/SW/TEST/MirrorCombat/Prefabs/GunnerProjectile_MirrorTest.prefab").GetComponent<NetworkEnemyProjectile_MirrorTest>());
                var grenade = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/WBHTest/Prefabs/Projectile/GunnerGrenade.prefab").GetComponent<WBH_Projectile>());
                Require(projectile.FindProperty("playerGrenadeExplosionEffect").objectReferenceValue ==
                    new SerializedObject(source.GetComponent<T_PlayerCombat>()).FindProperty("basicGrenadeEffect").objectReferenceValue,
                    "원본 유탄 공통 폭발 이펙트");
                Require(projectile.FindProperty("showPlayerGrenadeRange").boolValue == grenade.FindProperty("showExplosionRange").boolValue &&
                    projectile.FindProperty("playerGrenadeRangeColor").colorValue == grenade.FindProperty("explosionRangeColor").colorValue &&
                    projectile.FindProperty("playerGrenadeRangeDuration").floatValue == grenade.FindProperty("explosionRangeDuration").floatValue,
                    "원본 유탄 범위 표시");
            }
            var property = sourceEffects.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = property.propertyType != SerializedPropertyType.ObjectReference;
                if (!property.propertyPath.StartsWith("effectBindings", StringComparison.Ordinal) &&
                    property.propertyPath is not ("chargeEffect" or "chargeRangeEffect")) continue;
                var counterpart = mirrorEffects.FindProperty(property.propertyPath);
                Require(counterpart != null, character + " 이펙트 항목 " + property.propertyPath);
                if (property.propertyType == SerializedPropertyType.Generic) continue;
                bool equal = property.propertyType == SerializedPropertyType.ObjectReference
                    ? ReferenceKey(property.objectReferenceValue, source.transform) ==
                      ReferenceKey(counterpart.objectReferenceValue, mirror.transform)
                    : SerializedProperty.DataEquals(property, counterpart);
                Require(equal, character + " 이펙트/사운드/앵커 불일치 " + property.propertyPath);
            }
            foreach (var component in mirror.GetComponentsInChildren<Component>(true))
                Require(component != null, character + " Missing Script");
        }
        Debug.Log("[MirrorStage3CombatValidation] PASS actual player skill definitions, VFX/SFX fields, owned anchors and Missing Script checks.");
    }

    private static string ReferenceKey(UnityEngine.Object value, Transform owner)
    {
        if (value == null) return "null";
        Transform transform = value is Component component ? component.transform : (value as GameObject)?.transform;
        if (transform != null && (transform == owner || transform.IsChildOf(owner)))
            return "owner/" + AnimationUtility.CalculateTransformPath(transform, owner) + ":" + value.GetType().FullName;
        return AssetDatabase.GetAssetPath(value) + ":" + value.GetInstanceID();
    }

    [MenuItem("SW/Mirror Test/Validate Stage3 Damage Rules")]
    public static void ValidateDamageRules()
    {
        var attacker = new Combat();
        var target = new Combat();
        var source = new WBH_CombatManager.DamageSourceSnapshot(attacker);
        var effect = ScriptableObject.CreateInstance<WBH_EffectData>();
        var randomState = UnityEngine.Random.state;
        try
        {
            var request = new WBH_DamageRequest(attacker, target, WBH_AttackType.Normal, ElementType.Fire,
                2f, WBH_StatusEffectPresets.Burn1, effect, Vector3.right, Vector3.back, DamageCause.Direct, 37u);
            var result = WBH_CombatManager.CalculateDamage(request, source);
            Require(Mathf.Approximately(result.FinalDamage, 930f) && result.IsCritical,
                "기본 공격/속성/치명/방어/받는 피해 배율의 적용 순서");
            Require(result.EffectData == effect && result.HitPosition == Vector3.right &&
                result.HitEffectDirection == Vector3.back && result.Attacker == attacker && result.AttackId == 37u,
                "피격 연출과 공격 출처 보존");
            Require(result.StatusEffect.Value.Attacker == attacker && result.StatusEffect.Value.AttackId == 37u,
                "지속 피해의 공격자와 공격 ID");
            Require(request.StatusEffect.Value.Attacker == null && request.StatusEffect.Value.AttackId == 0,
                "원본 상태이상 프리셋 값 보존");
            WBH_CombatManager.ProcessDamage(request);
            Require(target.Hits == 1 && target.LastDamage.FinalDamage == result.FinalDamage &&
                target.AppliedStatus.Value.Attacker == attacker && target.AppliedStatus.Value.AttackId == 37u,
                "기존 ProcessDamage도 같은 결과와 상태 출처 적용");

            var skill = new WBH_DamageRequest(attacker, target, WBH_AttackType.Skill, ElementType.Fire, 2f,
                null, effect, Vector3.right, Vector3.back, DamageCause.Effect, 38u);
            Require(Mathf.Approximately(WBH_CombatManager.CalculateDamage(skill, source).FinalDamage, 1230f),
                "AttackType과 DamageCause를 별도로 유지");
            Require(Mathf.Approximately(WBH_CombatManager.CalculateDamage(skill, source, false).FinalDamage, 592.5f),
                "치명타가 금지된 후속 피해");
            attacker.AttackPower = 900f;
            Require(Mathf.Approximately(WBH_CombatManager.CalculateDamage(request, source).FinalDamage, 930f),
                "후속 피해 중 스탯 변화가 기존 스냅샷을 바꾸지 않음");
            Require(WBH_CombatManager.CalculateDamage(request,
                new WBH_CombatManager.DamageSourceSnapshot(attacker)).FinalDamage > 930f,
                "다음 요청은 새 스탯 반영");
            target.DefensePower = 99999f;
            Require(WBH_CombatManager.CalculateDamage(request, source).FinalDamage == 1f, "최소 피해 1");
            target.IsDead = true;
            WBH_CombatManager.ProcessDamage(request);
            Require(target.Hits == 1, "사망한 대상 중복 피해 거절");
            Debug.Log("[MirrorStage3CombatValidation] PASS damage formula, metadata, status attribution, snapshot, minimum and dead-target rules.");
        }
        finally
        {
            UnityEngine.Random.state = randomState;
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException("[MirrorStage3CombatValidation] " + message);
    }

    private sealed class Combat : WBH_ICombat, WBH_ICombatStatus
    {
        public WBH_ICombatStatus Status => this;
        public float CurrentHp => 10000f;
        public float MaxHealth => 10000f;
        public float AttackPower { get; set; } = 100f;
        public float DefensePower { get; set; } = 40f;
        public float CritRate => 1f;
        public float CritMult => 2f;
        public float Pen => 10f;
        public float FireBonus => 0.25f;
        public float IceBonus => 0f;
        public float ElectricBonus => 0f;
        public float DamageTakenModifier => 1.5f;
        public float NormalDamageModifier => 1.3f;
        public float SkillDamageModifier => 1.7f;
        public bool IsDead { get; set; }
        public int Hits;
        public WBH_DamageResult LastDamage;
        public WBH_StatusEffectData? AppliedStatus;
        public void TakeDamage(WBH_DamageResult result) { Hits++; LastDamage = result; }
        public void AddStatusEffect(WBH_StatusEffectData data) => AppliedStatus = data;
    }
}
