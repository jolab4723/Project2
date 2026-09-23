using UnityEngine;
using ItemSystem;

/// <summary>
/// 포션 사용/효과 적용/충전을 담당하는 매니저.
///
/// 충전(사용 가능 횟수)은 포션마다 다른 값이 아니라 플레이어 전체가 공유하는 고정값 하나다 -
/// 어떤 포션을 장착해도 이 하나의 풀을 같이 쓰고, 최대치도 포션 데이터가 아니라 이 매니저의
/// 기본값(basePotionCharges)을 따른다. 사용해도 아이템 자체는 사라지지 않고, 캠프에서 다시 채운다.
///
/// 실제 입력(Q키)은 PlayerActionInputHandler가 TryUsePotion()을 불러주는 형태로 이미 연결됨.
///
/// !! 멀티플레이 대비: PlayerBuffManager/PlayerHealthManager와 동일한 로컬 전용 패턴.
/// </summary>
public class PotionUseManager : MonoBehaviour
{
    public static PotionUseManager Instance { get; private set; }

    [Tooltip("플레이어가 기본으로 가지는 포션 충전 최대치. 포션 종류와 무관하게 고정값이다.")]
    [SerializeField] private int basePotionCharges = 3;

    [Tooltip("포션 사용 후 다시 쓸 수 있을 때까지의 대기 시간(초). 쿨타임 감소 스탯의 영향을 받지 않는 고정값이다.")]
    [SerializeField, Min(0f)] private float useCooldownSeconds = 1f;

    // 장착된 포션이 어떤 효과(회복/능력치 증가)인지 알아야 해서 여전히 참조가 필요하다.
    // InventoryController.Instance를 그때그때 찾는 대신 PlayerStatManager와 같은 방식(직렬화 참조)을 쓴다 -
    // 런타임에 찾으면 스크립트 실행 순서 문제로 초기화 시점에 아직 준비 안 됐을 수 있다.
    [SerializeField] private EquipmentSystem equipmentSystem;

    /// <summary>플레이어가 지금 가진 남은 충전량(공유 풀). UI 등에서 표시용으로 읽으면 된다.</summary>
    public int CurrentCharges => useState.CurrentCharges; // SW 수정: 공통 규칙이 충전량을 소유합니다.

    /// <summary>플레이어의 최대 충전량. 포션 종류와 무관한 고정값이다.</summary>
    public int MaxCharges => basePotionCharges;

    /// <summary>포션 사용 쿨타임 전체 길이(초). 쿨타임 감소와 무관한 고정값이다.</summary>
    public float UseCooldownSeconds => useCooldownSeconds;

    /// <summary>남은 쿨타임(초). 사용 가능하면 0.</summary>
    public float RemainingCooldown =>
        useState.RemainingCooldown;

    /// <summary>지금 쿨타임이 끝나서 쓸 수 있는 상태인지.</summary>
    public bool IsCooldownReady => useState.IsCooldownReady;

    /// <summary>
    /// SW 수정: 미러와 같은 규칙을 사용하되 충전과 쿨타임은 이 플레이어만 소유합니다.
    ///
    /// !! 일부러 PlayerStat의 쿨타임 감소(cdr)를 타지 않는다. 스킬 쿨타임과 달리 포션 연타를 막는
    ///    최소 간격이라, 쿨감이 높아진다고 줄어들면 의미가 없어진다.
    /// </summary>
    private readonly PotionUseState useState = new PotionUseState();
    private PlayerHealthManager health;
    private PlayerBuffManager buffs;

    private void Awake()
    {
        var identity = GetComponent<Mirror.NetworkIdentity>();
        if (identity != null && !identity.isLocalPlayer)
            return;

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PotionUseManager] 이미 인스턴스가 존재해서 중복 오브젝트를 제거합니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>SW 수정: 게임 시작 시 기존처럼 포션 충전을 최대치로 채웁니다.</summary>
    private void Start()
    {
        RechargeAllPotions(); // 게임 시작 시 항상 최대치로 시작
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 기존엔 OnDestroy에서만 Instance를 비워서, SetActive(false)로 비활성화만 해도(파괴 아님) Instance가
    // 계속 이 캐릭터를 가리키고 있었다 - 이후 다른 캐릭터가 Awake될 때 "이미 인스턴스가 있다"고 오판해서
    // 새 캐릭터를 통째로 Destroy하는 문제가 있었다(PlayerStatManager에서 실측 확인, 125번).
    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    // OnDisable과 짝을 이루는 재등록 - Awake는 생애 한 번만 돌아서, 한 번 비활성화됐다가 다시 활성화되는
    // 캐릭터는 Awake가 재실행 안 되므로 여기서 다시 등록해줘야 Instance가 null로 안 남는다(125번).
    private void OnEnable()
    {
        if (Instance == this)
            return;

        var identity = GetComponent<Mirror.NetworkIdentity>();
        if (identity != null && !identity.isLocalPlayer)
            return;

        if (Instance != null)
        {
            Debug.LogWarning("[PotionUseManager] 이미 인스턴스가 존재해서 다시 활성화된 오브젝트를 등록하지 않습니다.");
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 현재 장착된 포션을 사용한다. 장착된 포션이 없거나, 공유 풀 충전이 없거나,
    /// 아직 사용 쿨타임이 안 끝났으면 조용히 실패한다.
    /// SW 수정: 해당 플레이어의 포션을 공통 규칙으로 사용합니다.
    /// 기존 충전·고정 쿨타임을 유지하고 사망 상태나 효과 대상이 없으면 충전을 소비하지 않습니다.
    /// </summary>
    public bool TryUsePotion()
    {
        if (!TryGetEquippedPotion(out ItemInstance potion))
            return false;

        health ??= GetComponent<PlayerHealthManager>();
        buffs ??= GetComponent<PlayerBuffManager>();
        return useState.TryUse(potion.definition, health, buffs, useCooldownSeconds);
    }

    /// <summary>
    /// 공유 충전 풀을 최대치까지 채운다. 캠프 스테이지에서 호출할 용도.
    /// SW 수정: 공통 규칙으로 충전을 채우며 남은 쿨타임은 유지합니다.
    /// </summary>
    public void RechargeAllPotions()
    {
        useState.Recharge(MaxCharges);
    }

    /// <summary>
    /// 현재 장착된 포션 인스턴스를 가져온다. 장착된 게 없거나 포션이 아니면 false.
    /// SW 수정: 기존 장비 연결을 유지하고 공통 규칙으로 장착 포션을 찾습니다.
    /// </summary>
    public bool TryGetEquippedPotion(out ItemInstance potion)
    {
        potion = null;

        if (equipmentSystem == null)
            equipmentSystem = InventoryController.GetLocalEquipmentSystem(this);

        return PotionUseState.TryGetEquippedPotion(equipmentSystem, out potion);
    }
}
