using UnityEngine;
using Core;

/// <summary>
/// 현재 활성화된 PlayerProfileData(영구 크레딧/패시브 스킬트리)를 들고 있으면서
/// 크레딧으로 패시브 스킬 레벨을 해금/적용하는 로직을 전담하는 매니저.
/// 스킬별 이름/최대 레벨/레벨당 수치/해금 비용은 PassiveSkillDatabase(고정 밸런스 데이터) 참고.
///
/// !! 저장/불러오기는 전부 DataManager가 전담한다(DataManager.SavePassiveData/LoadPassiveData).
///    이 클래스는 CurrentProfile을 들고 있고 SetActiveProfile로 주입받기만 할 뿐, 파일 입출력은 하지 않는다.
///
/// !! GetStatSet()(IStatSetProvider)은 8개 스탯형 스킬(체력/공격력/방어력/속도/크리티컬/쿨감/속성보너스)만
///    합산해서 돌려준다. PlayerStatManager에 실제로 연결하는 건 별도 작업.
/// </summary>
public class PassiveSkillManager : Singleton<PassiveSkillManager>, IStatSetProvider
{
    [Tooltip("12개 패시브 스킬의 이름/최대레벨/레벨당 수치/해금비용을 담은 에셋")]
    [SerializeField] private PassiveSkillDatabaseSO database;

    public PlayerProfileData CurrentProfile { get; private set; }

    /// <summary>id 스킬의 디자인 데이터(이름/최대레벨/수치/비용)를 조회한다. database가 안 물려있으면 null.</summary>
    public PassiveSkillDefinition GetDefinition(PassiveSkillId id)
    {
        if (database == null)
        {
            Debug.LogWarning("[PassiveSkillManager] database(PassiveSkillDatabaseSO)가 연결되지 않았습니다.");
            return null;
        }

        return database.Get(id);
    }

    /// <summary>크레딧/스킬 레벨이 바뀔 때마다 발행. UI 등에서 구독해서 갱신.</summary>
    public event System.Action OnProfileChanged;

    public void SetActiveProfile(PlayerProfileData profile)
    {
        CurrentProfile = profile;
        OnProfileChanged?.Invoke();
    }

    /// <summary>실제로 적용 중인(효과에 반영되는) 레벨. 미습득/프로필 없음이면 0.</summary>
    public int GetCurrentLevel(PassiveSkillId id)
    {
        var entry = FindEntry(id);
        return entry != null ? entry.currentLevel : 0;
    }

    /// <summary>크레딧으로 해금한 최고 레벨(추가 비용 없이 자유롭게 오갈 수 있는 상한선). 미습득/프로필 없음이면 0.</summary>
    public int GetUnlockedLevel(PassiveSkillId id)
    {
        var entry = FindEntry(id);
        return entry != null ? entry.unlockedLevel : 0;
    }

    /// <summary>
    /// id 스킬을 targetLevel까지 해금하는 데 필요한 크레딧 총합.
    /// targetLevel이 이미 해금된 레벨 이하면 0 (추가 비용 없음).
    /// </summary>
    public int GetUnlockCostToLevel(PassiveSkillId id, int targetLevel)
    {
        var definition = GetDefinition(id);
        if (definition == null)
            return 0;

        int unlockedLevel = GetUnlockedLevel(id);
        int cost = 0;
        for (int lvl = unlockedLevel + 1; lvl <= targetLevel; lvl++)
            cost += definition.GetUnlockCost(lvl);

        return cost;
    }

    /// <summary>
    /// id 스킬의 적용 레벨을 targetLevel로 맞춘다.
    /// targetLevel이 이미 해금된 범위면 즉시 무료로 적용, 그 이상이면 크레딧을 소모해서 먼저 해금한 뒤 적용한다.
    /// 크레딧이 부족하면 아무것도 바꾸지 않고 false를 반환한다.
    /// </summary>
    public bool TryApplyLevel(PassiveSkillId id, int targetLevel)
    {
        if (CurrentProfile == null)
        {
            Debug.LogWarning("[PassiveSkillManager] CurrentProfile이 없어 패시브 레벨을 적용할 수 없습니다.");
            return false;
        }

        var definition = GetDefinition(id);
        if (definition == null)
        {
            Debug.LogWarning($"[PassiveSkillManager] {id}에 대한 PassiveSkillDefinition이 없습니다.");
            return false;
        }

        targetLevel = Mathf.Clamp(targetLevel, 0, definition.maxLevel);

        // SW 수정: 효과가 정의되지 않은 패시브('미정')는 크레딧을 받고 해금하지 않는다. 적용 해제(0)는 허용한다.
        if (targetLevel > 0 && !IsAvailable(definition))
        {
            Debug.LogWarning($"[PassiveSkillManager] {id}는 효과가 정해지지 않아 해금할 수 없습니다.");
            return false;
        }

        var entry = FindOrCreateEntry(id);

        if (targetLevel <= entry.unlockedLevel)
        {
            entry.currentLevel = targetLevel;
            OnProfileChanged?.Invoke();
            DataManager.Instance?.SavePassiveData();
            return true;
        }

        int cost = GetUnlockCostToLevel(id, targetLevel);
        if (CurrentProfile.credit < cost)
            return false;

        CurrentProfile.credit -= cost;
        entry.unlockedLevel = targetLevel;
        entry.currentLevel = targetLevel;
        OnProfileChanged?.Invoke();
        DataManager.Instance?.SavePassiveData();
        return true;
    }

    /// <summary>
    /// SW 수정: 실제 효과 값이 하나라도 있는 패시브만 해금할 수 있다. 표시 쪽도 같은 기준을 쓰도록 공개한다.
    /// </summary>
    public bool IsAvailable(PassiveSkillId id) => IsAvailable(GetDefinition(id));

    public static bool IsAvailable(PassiveSkillDefinition definition)
    {
        if (definition == null || definition.maxLevel <= 0)
            return false;
        if (definition.extraRerollCount > 0)
            return true;
        if (definition.valuesPerLevel == null)
            return false;

        foreach (float value in definition.valuesPerLevel)
        {
            if (value != 0f)
                return true;
        }
        return false;
    }

    /// <summary>btn_SkillClear - 모든 패시브의 적용 레벨을 0으로 되돌린다. 해금 진행도(unlockedLevel)는 유지됨.</summary>
    public void ResetAllCurrentLevels()
    {
        if (CurrentProfile == null)
            return;

        foreach (var entry in CurrentProfile.passiveSkillTree.learnedSkills)
            entry.currentLevel = 0;

        OnProfileChanged?.Invoke();
        DataManager.Instance?.SavePassiveData();
    }

    private PassiveSkillEntry FindEntry(PassiveSkillId id)
    {
        if (CurrentProfile == null)
            return null;

        foreach (var entry in CurrentProfile.passiveSkillTree.learnedSkills)
        {
            if (entry.id == id)
                return entry;
        }

        return null;
    }

    private PassiveSkillEntry FindOrCreateEntry(PassiveSkillId id)
    {
        var existing = FindEntry(id);
        if (existing != null)
            return existing;

        var newEntry = new PassiveSkillEntry { id = id, unlockedLevel = 0, currentLevel = 0 };
        CurrentProfile.passiveSkillTree.learnedSkills.Add(newEntry);
        return newEntry;
    }

    /// <summary>id 스킬의 현재 적용 레벨 기준 효과 수치를 반환한다 (미습득이면 0).</summary>
    public float GetEffectValue(PassiveSkillId id)
    {
        var definition = GetDefinition(id);
        return definition != null ? definition.GetValue(GetCurrentLevel(id)) : 0f;
    }

    // ===================== 부활 / 캠프 / 상점 강화 (스탯 외 개별 효과) =====================

    public bool HasRevive => GetCurrentLevel(PassiveSkillId.Revive) > 0;
    public float ReviveHealthPercent => GetEffectValue(PassiveSkillId.Revive);

    public bool HasCampHealBonus => GetCurrentLevel(PassiveSkillId.CampHealBonus) > 0;
    public float CampHealPercent => GetEffectValue(PassiveSkillId.CampHealBonus);

    public bool HasShopEnhance => GetCurrentLevel(PassiveSkillId.ShopEnhance) > 0;
    public float ShopDiscountPercent => GetEffectValue(PassiveSkillId.ShopEnhance);
    public int ShopExtraRerollCount => HasShopEnhance ? GetDefinition(PassiveSkillId.ShopEnhance).extraRerollCount : 0;

    // ===================== IStatSetProvider (8개 스탯형 스킬 합산) =====================

    public StatSet GetStatSet()
    {
        float allSpeed = GetEffectValue(PassiveSkillId.AllSpeed);
        float allElementalBonus = GetEffectValue(PassiveSkillId.AllElementalBonus);

        return new StatSet
        {
            maxHealthPercent = GetEffectValue(PassiveSkillId.MaxHealth),
            attackPowerPercent = GetEffectValue(PassiveSkillId.AttackPower),
            defensePowerPercent = GetEffectValue(PassiveSkillId.DefensePower),
            moveSpeedPercent = allSpeed,
            attackSpeedPercent = allSpeed,
            critRateFlat = GetEffectValue(PassiveSkillId.CritRate),
            critMultFlat = GetEffectValue(PassiveSkillId.CritDamage),
            cdrFlat = GetEffectValue(PassiveSkillId.CooldownReduction),
            fireBonusFlat = allElementalBonus,
            iceBonusFlat = allElementalBonus,
            electricBonusFlat = allElementalBonus,
        };
    }
}
