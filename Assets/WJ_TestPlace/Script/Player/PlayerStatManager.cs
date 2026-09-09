using ItemSystem;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 캐릭터(PlayerLevelManager) + 장비(PlayerEquipManager) + 버프(PlayerBuffManager) 레이어를
/// 합산해서 PlayerStat.Recalculate를 호출하고, 최종 스탯을 외부에서 참조할 수 있게 제공하는 매니저.
///
/// PlayerEquipManager/PlayerBuffManager는 아직 구현 전이라, IStatSetProvider 인터페이스만
/// 정의해두고 비어있으면 StatSet.Zero로 취급한다. 나중에 두 매니저를 구현할 때
/// IStatSetProvider만 구현하면 이 클래스는 손대지 않아도 자동으로 연결된다.
///
/// !! 멀티플레이 대비: Instance는 "내 캐릭터"만 가리킨다 (PlayerHealthManager와 동일 패턴).
/// </summary>
public class PlayerStatManager : MonoBehaviour
{
    public static PlayerStatManager Instance { get; private set; }

    /// <summary>씬에 존재하는 모든 캐릭터의 스탯 매니저 (나 + 다른 플레이어).</summary>
    public static readonly List<PlayerStatManager> All = new List<PlayerStatManager>();

    [Header("레이어 소스")]
    [SerializeField] private PlayerLevelManager levelManager;

    [Tooltip("IStatSetProvider를 구현한 컴포넌트. 아직 없으면 비워둬도 됨 (StatSet.Zero로 취급)")]
    [SerializeField] private MonoBehaviour equipManagerBehaviour;

    [Tooltip("IStatSetProvider를 구현한 컴포넌트. 아직 없으면 비워둬도 됨 (StatSet.Zero로 취급)")]
    [SerializeField] private MonoBehaviour buffManagerBehaviour;

    [Header("초기값")]
    [SerializeField] private int startLevel = 1;

    [Header("Equipment System")]
    [SerializeField] private EquipmentSystem equipmentSystem;

    // 장비 장착 변동시 발생하는 이벤트
    public event System.Action<EquippedWeaponInfo?> OnWeaponInfoChanged;
    private IStatSetProvider EquipProvider => equipManagerBehaviour as IStatSetProvider;
    private IStatSetProvider BuffProvider => buffManagerBehaviour as IStatSetProvider;

    /// <summary>최종 합산된 플레이어 스탯. 외부에서는 이걸 참조.</summary>
    public PlayerStat Stat { get; private set; }

    private StatSet? passiveStats; // SW 수정
    private PassiveSkillManager subscribedPassiveManager;

    /// <summary>Awake 전 호출도 허용한다. 이미 생성한 Stat과 레벨·경험치는 유지한다.</summary>
    public PlayerStat EnsureInitialized() => Stat ??= new PlayerStat(startLevel);

    /// <summary>소유자가 검증한 개인 패시브를 사용한다. null은 싱글의 기존 프로필 소스로 복귀한다.</summary>
    public void SetPassiveStats(StatSet? value)
    {
        passiveStats = value;
        RefreshPassiveSubscription();
        Recalculate();
    }

    private bool UsesLocalPassiveProfile => !passiveStats.HasValue && GetComponent<Mirror.NetworkIdentity>() == null;

    private void RefreshPassiveSubscription()
    {
        PassiveSkillManager source = isActiveAndEnabled && UsesLocalPassiveProfile ? PassiveSkillManager.Instance : null;
        if (subscribedPassiveManager == source) return;
        if (subscribedPassiveManager != null) subscribedPassiveManager.OnProfileChanged -= Recalculate;
        subscribedPassiveManager = source;
        if (subscribedPassiveManager != null) subscribedPassiveManager.OnProfileChanged += Recalculate;
    }

    /// <summary>현재 레벨. UI 등 외부에서 Stat.currentLevel을 직접 건드리지 않고 읽기 전용으로 조회.</summary>
    public int CurrentLevel => Stat.currentLevel;

    /// <summary>현재 레벨에서 쌓인 경험치. 다음 레벨까지 필요한 양은 ExpToNextLevel 참고.</summary>
    public float CurrentExp => Stat.currentExp;

    /// <summary>
    /// 현재 레벨에서 다음 레벨까지 필요한 경험치. 만렙(다음 레벨 데이터 없음)이면 0.
    /// 경험치 바 UI에서 CurrentExp / ExpToNextLevel로 진행률을 계산할 때 사용.
    /// </summary>
    public float ExpToNextLevel => levelManager != null ? levelManager.GetExpToNextLevel(Stat.currentLevel) : 0f;


    private void OnEnable()
    {
        // Awake는 오브젝트 생애 동안 한 번만 돌아서, 한 번 비활성화됐다가(OnDisable에서 Instance 해제)
        // 다시 활성화되는 경우(캐릭터 전환을 껐다 켰다로 반복) Awake가 재실행 안 되므로 여기서 다시
        // 등록해줘야 한다 - 안 그러면 재활성화한 캐릭터의 Instance가 계속 null로 남는다(125번).
        if (Instance != this)
        {
            var identity = GetComponent<Mirror.NetworkIdentity>();
            bool isLocal = identity == null || identity.isLocalPlayer;

            if (isLocal)
            {
                if (Instance != null)
                {
                    Debug.LogWarning("[PlayerStatManager] 이미 인스턴스가 존재해서 다시 활성화된 오브젝트를 등록하지 않습니다.");
                }
                else
                {
                    Instance = this;
                }
            }
        }

        if (equipmentSystem != null)
            equipmentSystem.OnEquipmentChanged += HandleEquipmentChanged;

        RefreshPassiveSubscription();
    }

    private void OnDisable()
    {
        if (equipmentSystem != null)
            equipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;

        if (subscribedPassiveManager != null)
            subscribedPassiveManager.OnProfileChanged -= Recalculate;
        subscribedPassiveManager = null;

        // 기존엔 OnDestroy에서만 Instance를 비워서, 캐릭터를 SetActive(false)로 비활성화만 해도
        // (파괴 아님) Instance가 그 캐릭터를 계속 가리키고 있었다 - 이후 다른 캐릭터가 Awake될 때
        // "이미 인스턴스가 있다"고 오판해서 새 캐릭터를 통째로 Destroy하거나(124번에서 실측 확인),
        // 장비/스탯을 읽는 쪽이 정적 Instance를 참조하면 비활성화된 캐릭터의 장비가 계속 표시되는
        // 문제가 있었다. 비활성화 시점에도 즉시 자리를 비워준다.
        if (Instance == this)
            Instance = null;
    }

   
    private void HandleEquipmentChanged(EquippedItemInfo[] infos)
    {
        Recalculate();

        if (TryGetEquippedWeaponInfo(out var weaponInfo))
            OnWeaponInfoChanged?.Invoke(weaponInfo);
        else
            OnWeaponInfoChanged?.Invoke(null);
    }

    /// <summary>현재 장착 중인 무기의 타입/속성/강화 수치. 전투 시스템에서 공격의 속성(인챈트)을 판정할 때 사용.</summary>
    public bool TryGetEquippedWeaponInfo(out EquippedWeaponInfo weaponInfo)
    {
        weaponInfo = default;

        if (equipmentSystem == null)
            return false;

        if (!equipmentSystem.TryGetEquippedItemInstance(
                EquipSlotType.Weapon,
                out ItemInstance weapon))

            return false;


        weaponInfo = new EquippedWeaponInfo(
            weapon.definition.weaponType,
            weapon.rolledElement,
            weapon.upgradeLevel);

        return true;
    }

    private void Awake()
    {
        All.Add(this);

        var identity = GetComponent<Mirror.NetworkIdentity>();
        if (identity != null && !identity.isLocalPlayer)
        {
            // 다른 플레이어의 스탯도 계산 자체는 필요하니 Stat은 만들어두되, Instance로는 등록 안 함.
            EnsureInitialized();
            Recalculate();
            return;
        }

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PlayerStatManager] 이미 인스턴스가 존재해서 중복 오브젝트를 제거합니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (equipManagerBehaviour != null && EquipProvider == null)
            Debug.LogWarning($"[PlayerStatManager] {equipManagerBehaviour.GetType().Name}은(는) IStatSetProvider를 구현하지 않았습니다.");

        if (buffManagerBehaviour != null && BuffProvider == null)
            Debug.LogWarning($"[PlayerStatManager] {buffManagerBehaviour.GetType().Name}은(는) IStatSetProvider를 구현하지 않았습니다.");

        EnsureInitialized();
        Recalculate();

        // 초기 스폰 시 체력/마나는 PlayerHealthManager/PlayerManaManager가 각각 자체적으로 Start()에서 풀충전 처리함.
    }

    private void Start()
    {
        // 다른 레이어의 Awake가 모두 끝난 뒤 초기 수치를 계산한다.
        RefreshPassiveSubscription();
        Recalculate();
    }

    private void OnDestroy()
    {
        All.Remove(this);
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 네 레이어를 전부 다시 모아서 PlayerStat을 갱신한다.
    /// 장비 착용/해제, 레벨업, 버프 적용/해제, 패시브 스킬 변경 시 호출.
    /// 싱글은 기존 프로필, 네트워크 캐릭터는 소유자가 명시한 개인 패시브만 사용한다.
    /// </summary>
    public void Recalculate()
    {
        EnsureInitialized();
        GetLayerStatSets(out StatSet character, out StatSet equipment, out StatSet buff, out StatSet passive);

        Stat.Recalculate(character, equipment, buff, passive);
    }

    /// <summary>
    /// 캐릭터/장비/버프/패시브 네 레이어의 StatSet을 각각 가져온다.
    /// 스탯 UI처럼 레이어별 기여분을 따로 보여줘야 하는 외부 코드에서 사용.
    /// </summary>
    public void GetLayerStatSets(out StatSet character, out StatSet equipment, out StatSet buff, out StatSet passive)
    {
        EnsureInitialized();
        character = GetCharacterStatSet();
        equipment = EquipProvider != null ? EquipProvider.GetStatSet() : StatSet.Zero;
        buff = BuffProvider != null ? BuffProvider.GetStatSet() : StatSet.Zero;
        passive = passiveStats ?? (UsesLocalPassiveProfile && PassiveSkillManager.Instance != null
            ? PassiveSkillManager.Instance.GetStatSet() : StatSet.Zero);
    }

    /// <summary>
    /// 스킬 범위 flat/% 보너스를 4단 공식(CalcFinal)을 거치지 않고 네 레이어에서 그대로 합산해서 낸다.
    /// Stat.skillRange(CalcFinal 결과)를 그대로 쓰면 buff/equip의 % 가 이미 캐릭터의 작은 flat 값에
    /// 한 번 곱해져 들어가 있어서, FighterSkillController가 스킬 자체 사거리에 %를 또 곱하면 같은
    /// 보너스가 두 번 적용되는 문제가 있었다(직접 검증 중 발견). 그래서 이 스탯만 CalcFinal을 우회해서
    /// 순수 원시 합으로 따로 낸다 - flat은 4개 레이어 전부, %는 캐릭터 레이어가 안 쓰는 걸 감안해 나머지 3개.
    /// </summary>
    public void GetSkillRangeBonus(out float flatBonus, out float percentBonus)
    {
        GetLayerStatSets(out StatSet character, out StatSet equipment, out StatSet buff, out StatSet passive);
        flatBonus = character.skillRangeFlat + equipment.skillRangeFlat + buff.skillRangeFlat + passive.skillRangeFlat;
        percentBonus = equipment.skillRangePercent + buff.skillRangePercent + passive.skillRangePercent;
    }

    /// <summary>
    /// 실제 플레이어와 장비 상태를 변경하지 않고 현재 장비 구성과 후보 장비 교체 후 구성의
    /// 최종 스탯을 각각 계산한다. 두 계산 모두 현재 캐릭터, 버프(유물 고유 효과 포함), 패시브 조건을 동일하게 사용한다.
    /// </summary>
    /// <returns>PlayerEquipManager와 후보 아이템이 유효해 두 최종 스탯을 계산했으면 true.</returns>
    public bool TryCalculateStatsAfterReplacing(
        EquipSlotType replacingSlot,
        ItemInstance candidateItem,
        out PlayerStat currentStats,
        out PlayerStat candidateStats)
    {
        currentStats = null;
        candidateStats = null;

        if (Stat == null)
            return false;

        PlayerEquipManager equipManager = equipManagerBehaviour as PlayerEquipManager;

        if (equipManager == null)
        {
            Debug.LogWarning("[PlayerStatManager] PlayerEquipManager를 찾을 수 없습니다.");

            return false;
        }

        StatSet currentEquipment = equipManager.GetStatSet();

        if (!equipManager.TryGetStatSetAfterReplacing(
                replacingSlot,
                candidateItem,
                out StatSet candidateEquipment))
        {
            return false;
        }

        currentStats = CalculateStatsForComparison(currentEquipment);
        candidateStats = CalculateStatsForComparison(candidateEquipment);

        return true;
    }

    /// <summary>
    /// 전달받은 장비 합계에 현재 캐릭터 기본값, 버프(유물 고유 효과 포함), 패시브를 결합해
    /// 비교 전용 PlayerStat을 만든다. 새 객체에서 계산하므로 실제 Stat과 이벤트 구독자는 변경되지 않는다.
    /// </summary>
    private PlayerStat CalculateStatsForComparison(
        StatSet equipmentStats)
    {
        PlayerStat calculatedStats =
            new PlayerStat(Stat.currentLevel, Stat.currentExp);

        GetLayerStatSets(out StatSet character, out _, out StatSet buff, out StatSet passive);

        calculatedStats.Recalculate(character, equipmentStats, buff, passive);

        return calculatedStats;
    }

    /// <summary>레벨을 올리고 전체 재계산까지 한 번에 처리.</summary>
    [ContextMenu("레벨업 테스트")]
    public void LevelUp()
    {
        Stat.currentLevel++;
        Recalculate();
    }

    /// <summary>
    /// 경험치를 얻는다(적 처치 등). PlayerLevelManager의 레벨별 expToNextLevel을 넘기면 레벨업까지
    /// 이어서 처리하고(한 번에 여러 레벨을 오를 만큼 큰 경험치도 while로 대응), 레벨업이 실제로
    /// 일어났을 때만 재계산 + 체력/마나를 최대치로 채운다. KY_GameEvents.ExpChanged로 HUD 경험치
    /// 바를 갱신한다(이미 있던 이벤트지만 지금까지 실제 게임플레이에서 발행된 적이 없었음).
    /// 만렙(expToNextLevel이 0인 레벨, 현재 데이터 기준 20)에서는 경험치를 아예 받지 않는다.
    /// </summary>
    public void GainExp(float amount)
    {
        if (Stat == null || amount <= 0f)
            return;

        float expToNext = levelManager != null ? levelManager.GetExpToNextLevel(Stat.currentLevel) : 0f;
        if (expToNext <= 0f)
            return; // 만렙 - 더 이상 경험치 획득 불가

        Stat.GainExp(amount);

        bool leveledUp = false;

        while (expToNext > 0f && Stat.currentExp >= expToNext)
        {
            Stat.currentExp -= expToNext;
            Stat.currentLevel++;
            leveledUp = true;
            expToNext = levelManager != null ? levelManager.GetExpToNextLevel(Stat.currentLevel) : 0f;
        }

        if (leveledUp)
        {
            Recalculate();

            // FillHealth/FillMana는 각자 캐시해둔 MaxHealth/MaxMana를 그대로 쓰는데, 그 캐시는
            // 보통 Update()에서만 갱신된다. Recalculate() 직후 같은 프레임에 바로 채우려면 먼저
            // 캐시를 최신 Stat.maxHealth/maxMana로 강제 갱신해야 레벨업 직후 최대치로 채워진다.
            var healthManager = GetComponent<PlayerHealthManager>();
            healthManager?.RefreshMaxHealth();
            healthManager?.FillHealth();

            var manaManager = GetComponent<PlayerManaManager>();
            manaManager?.RefreshMaxMana();
            manaManager?.FillMana();
        }

        // expToNext가 0이면 더 올릴 레벨이 없는 만렙 상태 - 0으로 나누기를 피하려고 바를 꽉 찬 것으로 표시.
        if (expToNext > 0f)
            KY_GameEvents.ExpChanged(Stat.currentExp, expToNext);
        else
            KY_GameEvents.ExpChanged(1f, 1f);
    }

    /// <summary>
    /// 레벨을 1로 초기화하고 재계산한다. (테스트 버튼용)
    /// 현재 체력/마나는 PlayerHealthManager/PlayerManaManager가 maxHealth/maxMana 변화를 자체 감지해서
    /// clamp/보정을 알아서 처리하므로 여기서는 따로 건드리지 않음.
    /// </summary>
    [ContextMenu("레벨 초기화 테스트")]
    public void ResetLevel()
    {
        Stat.currentLevel = 1;
        Recalculate();
    }

    /// <summary>
    /// 현재 최종 스탯 전체를 콘솔에 출력한다. (테스트용)
    /// 레이어별 원본 값도 같이 찍어서, 어느 레이어에서 값이 들어왔는지 바로 확인할 수 있게 한다.
    /// </summary>
    [ContextMenu("전체 스탯 로그 출력")]
    public void LogAllStats()
    {
        if (Stat == null)
        {
            Debug.LogWarning("[PlayerStatManager] Stat이 아직 생성되지 않았습니다. (Play 모드에서 실행해주세요)");
            return;
        }

        GetLayerStatSets(out StatSet character, out StatSet equipment, out StatSet buff, out StatSet passive);

        var sb = new StringBuilder();
        sb.AppendLine($"===== [{name}] 전체 스탯 (레벨 {Stat.currentLevel}) =====");
        sb.AppendLine($"경험치 {Stat.currentExp}");
        sb.AppendLine("--- 최종 스탯 ---");
        sb.AppendLine($"  최대 체력 {Stat.maxHealth}" + CurrentHealthSuffix());
        sb.AppendLine($"  최대 마나 {Stat.maxMana}" + CurrentManaSuffix());
        sb.AppendLine($"  공격력 {Stat.attackPower}");
        sb.AppendLine($"  방어력 {Stat.defensePower}");
        sb.AppendLine($"  관통력 {Stat.pen}");
        sb.AppendLine($"  이동속도 {Stat.moveSpeed:F2}");
        sb.AppendLine($"  공격속도 {Stat.attackSpeed:F2}");
        sb.AppendLine($"  치명타 확률 {Stat.critRate:F1}%");
        sb.AppendLine($"  치명타 피해 {Stat.critMult:F2}");
        sb.AppendLine($"  쿨타임 감소 {Stat.cdr:F1}%");
        sb.AppendLine($"  마나 재생 {Stat.mpRegen:F2}");
        sb.AppendLine($"  스킬 사거리 {Stat.skillRange:F2}");
        sb.AppendLine($"  화염 피해 {Stat.fireBonus:F1} / 빙결 피해 {Stat.iceBonus:F1} / 전기 피해 {Stat.electricBonus:F1}");

        sb.AppendLine("--- 레이어별 기여분 (Flat / Percent) ---");
        AppendLayer(sb, "캐릭터", character);
        AppendLayer(sb, "장비", equipment);
        AppendLayer(sb, "버프(유물 고유 효과 포함)", buff);
        AppendLayer(sb, "패시브", passive);

        sb.Append("=======================================");
        Debug.Log(sb.ToString());
    }

    private string CurrentHealthSuffix()
    {
        var health = GetComponent<PlayerHealthManager>();
        return health != null ? $" (현재 {health.CurrentHealth})" : string.Empty;
    }

    private string CurrentManaSuffix()
    {
        var mana = GetComponent<PlayerManaManager>();
        return mana != null ? $" (현재 {mana.CurrentMana:F0})" : string.Empty;
    }

    /// <summary>한 레이어에서 0이 아닌 값만 모아서 한 줄로 출력한다. 값이 전부 0이면 "(없음)".</summary>
    private static void AppendLayer(StringBuilder sb, string layerName, StatSet set)
    {
        var parts = new List<string>();

        void Add(string label, float flat, float percent)
        {
            if (!Mathf.Approximately(flat, 0f))
                parts.Add($"{label} +{flat:F2}");
            if (!Mathf.Approximately(percent, 0f))
                parts.Add($"{label} +{percent:F2}%");
        }

        Add("체력", set.maxHealthFlat, set.maxHealthPercent);
        Add("마나", set.maxManaFlat, 0f);
        Add("공격력", set.attackPowerFlat, set.attackPowerPercent);
        Add("방어력", set.defensePowerFlat, set.defensePowerPercent);
        Add("관통력", set.penFlat, set.penPercent);
        Add("이동속도", set.moveSpeedFlat, set.moveSpeedPercent);
        Add("공격속도", set.attackSpeedFlat, set.attackSpeedPercent);
        Add("치명타확률", set.critRateFlat, 0f);
        Add("치명타피해", set.critMultFlat, set.critMultPercent);
        Add("쿨감", set.cdrFlat, 0f);
        Add("마나재생", set.mpRegenFlat, set.mpRegenPercent);
        Add("스킬사거리", set.skillRangeFlat, set.skillRangePercent);
        Add("화염", set.fireBonusFlat, set.fireBonusPercent);
        Add("빙결", set.iceBonusFlat, set.iceBonusPercent);
        Add("전기", set.electricBonusFlat, set.electricBonusPercent);

        sb.AppendLine($"  [{layerName}] " + (parts.Count == 0 ? "(없음)" : string.Join(", ", parts)));
    }

    private StatSet GetCharacterStatSet()
    {
        if (levelManager == null)
        {
            Debug.LogWarning("[PlayerStatManager] levelManager가 연결되지 않았습니다.");
            return StatSet.Zero;
        }

        var stats = levelManager.GetStatsForLevel(Stat.currentLevel);
        return ToCharacterStatSet(stats);
    }

    /// <summary>
    /// PlayerLevelManager가 주는 Dictionary(StatType -> float)를 StatSet으로 변환한다.
    /// 캐릭터 레이어는 Flat만 채우고 Percent는 0으로 둔다 (PlayerStat의 기존 가정과 일치).
    ///
    /// !! StatType과 StatSet의 필드 이름/구성이 완전히 1:1은 아니라서 명시적으로 매핑함:
    ///    - healthFlat -> maxHealthFlat (이름만 다름, 같은 의미)
    ///    - penetrationFlat -> penFlat (StatType엔 Percent 버전이 없음)
    ///    - mpMaxFlat -> maxManaFlat (StatSet/PlayerStat에 원래 없어서 이번에 추가함)
    /// </summary>
    private static StatSet ToCharacterStatSet(Dictionary<StatType, float> stats)
    {
        float Get(StatType type) => stats.TryGetValue(type, out var v) ? v : 0f;

        return new StatSet
        {
            maxHealthFlat = Get(StatType.healthFlat),
            attackPowerFlat = Get(StatType.attackPowerFlat),
            defensePowerFlat = Get(StatType.defensePowerFlat),
            moveSpeedFlat = Get(StatType.moveSpeedFlat),
            attackSpeedFlat = Get(StatType.attackSpeedFlat),
            critRateFlat = Get(StatType.critRateFlat),
            critMultFlat = Get(StatType.critMultFlat),
            cdrFlat = Get(StatType.cdrFlat),
            mpRegenFlat = Get(StatType.mpRegenFlat),
            maxManaFlat = Get(StatType.mpMaxFlat),
            penFlat = Get(StatType.penetrationFlat),
            skillRangeFlat = Get(StatType.skillRangeFlat),
            fireBonusFlat = Get(StatType.fireBonusFlat),
            iceBonusFlat = Get(StatType.iceBonusFlat),
            electricBonusFlat = Get(StatType.electricBonusFlat),
        };
    }
}

/// <summary>
/// 장비/버프 등 스탯 레이어 하나를 StatSet으로 제공하는 컴포넌트가 구현해야 하는 계약.
/// </summary>
public interface IStatSetProvider
{
    StatSet GetStatSet();
}
