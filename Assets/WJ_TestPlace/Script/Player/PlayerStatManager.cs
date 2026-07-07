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
/// </summary>
public class PlayerStatManager : MonoBehaviour
{
    public static PlayerStatManager Instance { get; private set; }

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
    }

    private void OnDisable()
    {
        if (equipmentSystem != null)
            equipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;
    }

    private void HandleEquipmentChanged(EquippedItemInfo[] infos)
    {
        Recalculate();
    }
    private void Awake()
    {
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

        // 초기 스폰 시 0으로 시작하지 않도록 풀피로 세팅.
        // 마나는 PlayerManaManager가 자체적으로 Start()에서 풀충전 처리함.
        if (Stat.currentHealth <= 0)
            Stat.currentHealth = Stat.maxHealth;
    }

    private void Update()
    {
        // 테스트용: K키로 레벨업 트리거 (L키는 PlayerLevelManager 테스트 출력에서 이미 쓰고 있어서 다른 키로 배치)
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            LevelUp();
    }

    /// <summary>
    /// 세 레이어를 전부 다시 모아서 PlayerStat을 갱신한다.
    /// 장비 착용/해제, 레벨업, 버프 적용/해제 시 호출.
    /// </summary>
    public void Recalculate()
    {
        StatSet character = GetCharacterStatSet();
        StatSet equipment = EquipProvider != null ? EquipProvider.GetStatSet() : StatSet.Zero;
        StatSet buff = BuffProvider != null ? BuffProvider.GetStatSet() : StatSet.Zero;

        Stat.Recalculate(character, equipment, buff);
    }

    /// <summary>레벨을 올리고 전체 재계산까지 한 번에 처리.</summary>
    [ContextMenu("레벨업 테스트")]
    public void LevelUp()
    {
        Stat.currentLevel++;
        Recalculate();
    }

    /// <summary>
    /// 레벨을 1로 초기화하고 풀피/풀마나로 리셋한다. (테스트 버튼용)
    /// Recalculate를 두 번 부르는 이유: 첫 호출로 레벨1 기준 maxHealth/maxMana를 먼저 확정하고,
    /// 그 다음 currentHealth/currentMana를 채운 뒤 OnStatChanged를 다시 발행해 UI에 반영시키기 위함.
    /// </summary>
    [ContextMenu("레벨 초기화 테스트")]
    public void ResetLevel()
    {
        Stat.currentLevel = 1;
        Recalculate();

        Stat.currentHealth = Stat.maxHealth;
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
