using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ItemSystem;

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

    private IStatSetProvider EquipProvider => equipManagerBehaviour as IStatSetProvider;
    private IStatSetProvider BuffProvider => buffManagerBehaviour as IStatSetProvider;

    /// <summary>최종 합산된 플레이어 스탯. 외부에서는 이걸 참조.</summary>
    public PlayerStat Stat { get; private set; }

    private void OnEnable()
    {
        if (equipmentSystem != null)
            equipmentSystem.OnEquipmentChanged += HandleEquipmentChanged;

        if (PassiveSkillManager.Instance != null)
            PassiveSkillManager.Instance.OnProfileChanged += Recalculate;
    }

    private void OnDisable()
    {
        if (equipmentSystem != null)
            equipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;

        if (PassiveSkillManager.Instance != null)
            PassiveSkillManager.Instance.OnProfileChanged -= Recalculate;
    }

    private void HandleEquipmentChanged(EquippedItemInfo[] infos)
    {
        Recalculate();
    }

    private void Awake()
    {
        All.Add(this);

        var identity = GetComponent<Mirror.NetworkIdentity>();
        if (identity != null && !identity.isLocalPlayer)
        {
            // 다른 플레이어의 스탯도 계산 자체는 필요하니 Stat은 만들어두되, Instance로는 등록 안 함.
            Stat = new PlayerStat(startLevel);
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

        Stat = new PlayerStat(startLevel);
        Recalculate();

        // 초기 스폰 시 체력/마나는 PlayerHealthManager/PlayerManaManager가 각각 자체적으로 Start()에서 풀충전 처리함.
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
    /// !! 패시브 스킬은 캐릭터별 컴포넌트가 아니라 전역 PassiveSkillManager.Instance를 직접 참조한다
    ///    (equip/buff처럼 Inspector에 캐릭터별로 꽂아주는 방식이 아님 - DataManager 참조 방식과 동일).
    /// </summary>
    public void Recalculate()
    {
        StatSet character = GetCharacterStatSet();
        StatSet equipment = EquipProvider != null ? EquipProvider.GetStatSet() : StatSet.Zero;
        StatSet buff = BuffProvider != null ? BuffProvider.GetStatSet() : StatSet.Zero;
        StatSet passive = PassiveSkillManager.Instance != null ? PassiveSkillManager.Instance.GetStatSet() : StatSet.Zero;

        Stat.Recalculate(character, equipment, buff, passive);
    }

    /// <summary>레벨을 올리고 전체 재계산까지 한 번에 처리.</summary>
    [ContextMenu("레벨업 테스트")]
    public void LevelUp()
    {
        Stat.currentLevel++;
        Recalculate();
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
/// PlayerEquipManager, PlayerBuffManager가 구현 예정.
/// </summary>
public interface IStatSetProvider
{
    StatSet GetStatSet();
}
