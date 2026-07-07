using UnityEngine;

/// <summary>
/// 플레이어 마나 리소스 전담 관리자.
/// 최대 마나(MaxMana)는 PlayerStatManager가 계산한 캐릭터+장비+버프 합산 결과를 그대로 따라가고,
/// 현재 마나(CurrentMana)는 여기서 독립적으로 들고 관리한다.
///
/// !! PlayerStat에 있던 currentMana/RegenerateMana를 이쪽으로 옮겨왔습니다.
///    (예전에 "체력도 나중에 별도 클래스로 분리할 예정"이라고 하신 것과 같은 방향으로,
///    마나를 리소스 관리 클래스로 먼저 분리했습니다. maxMana 자체는 여전히 PlayerStat 소관입니다 -
///    "파생된 스탯값"이라 스탯 시스템에 두는 게 맞다고 판단했습니다.)
/// </summary>
public class PlayerManaManager : MonoBehaviour
{
    public static PlayerManaManager Instance { get; private set; }

    [Tooltip("마나 회복 틱 간격(초). 기본 1초 = mpRegen 스탯값만큼 1초마다 회복.")]
    [SerializeField] private float regenTickInterval = 1f;

    [Header("회복 제어")]
    [Tooltip("마나 회복 배율. 1 = 기본, 2 = 2배 속도, 0 = 사실상 정지와 동일")]
    [SerializeField] private float regenMultiplier = 1f;

    [Tooltip("true면 회복 타이머가 멈추고 전혀 회복하지 않음 (예: 전투 중, 침묵 상태 등)")]
    public bool IsRegenPaused;

    /// <summary>외부(버프 등)에서 배율을 읽거나 바꿀 때 사용.</summary>
    public float RegenMultiplier
    {
        get => regenMultiplier;
        set => regenMultiplier = value;
    }

    /// <summary>PlayerStatManager.Stat.maxMana를 그대로 따라가는 값. 외부에서 직접 바꿀 수 없음.</summary>
    public int MaxMana { get; private set; }

    /// <summary>현재 마나. 소수점 회복(mpRegen이 소수일 때)을 위해 float로 관리.</summary>
    public float CurrentMana { get; private set; }

    /// <summary>마나가 바뀔 때마다 발행. UI 등에서 구독해서 갱신.</summary>
    public event System.Action OnManaChanged;

    private float regenTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PlayerManaManager] 이미 인스턴스가 존재해서 중복 오브젝트를 제거합니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        RefreshMaxMana();
        CurrentMana = MaxMana; // 시작 시 풀충전
        OnManaChanged?.Invoke();
    }

    private void Update()
    {
        RefreshMaxMana();

        if (CurrentMana >= MaxMana || IsRegenPaused)
        {
            regenTimer = 0f;
            return;
        }

        regenTimer += Time.deltaTime;
        if (regenTimer < regenTickInterval)
            return;

        regenTimer -= regenTickInterval;
        RestoreMana(GetMpRegen() * regenMultiplier);
    }

    private float GetMpRegen()
    {
        return PlayerStatManager.Instance != null ? PlayerStatManager.Instance.Stat.mpRegen : 0f;
    }

    /// <summary>
    /// PlayerStatManager가 계산한 최종 maxMana를 가져와서 갱신한다.
    /// 장비 해제 등으로 maxMana가 줄어들어 CurrentMana가 초과 상태가 되면 clamp한다.
    /// </summary>
    private void RefreshMaxMana()
    {
        int newMax = PlayerStatManager.Instance != null ? PlayerStatManager.Instance.Stat.maxMana : 0;
        if (newMax == MaxMana)
            return;

        MaxMana = newMax;

        if (CurrentMana > MaxMana)
        {
            CurrentMana = MaxMana;
            OnManaChanged?.Invoke();
        }
    }

    /// <summary>마나를 최대치로 즉시 채운다. (포션, 휴식 등)</summary>
    public void FillMana()
    {
        CurrentMana = MaxMana;
        OnManaChanged?.Invoke();
    }

    /// <summary>amount만큼 마나가 있는지 미리 확인 (실제로 깎지 않음). 스킬 사용 가능 여부 체크용.</summary>
    public bool HasEnoughMana(float amount)
    {
        return CurrentMana >= amount;
    }

    /// <summary>
    /// 마나를 amount만큼 소모한다. 부족하면 아무 것도 깎지 않고 false를 반환한다.
    /// (스킬 시전 실패 처리 등에 사용. 무조건 깎이길 원하면 필요할 때 별도 메서드로 추가하면 됨)
    /// </summary>
    public bool UseMana(float amount)
    {
        if (amount <= 0f)
            return true;

        if (CurrentMana < amount)
            return false;

        CurrentMana -= amount;
        OnManaChanged?.Invoke();
        return true;
    }

    /// <summary>마나를 amount만큼 회복한다 (최대치를 넘지 않음).</summary>
    public void RestoreMana(float amount)
    {
        if (amount <= 0f)
            return;

        CurrentMana = Mathf.Min(MaxMana, CurrentMana + amount);
        OnManaChanged?.Invoke();
    }
}
