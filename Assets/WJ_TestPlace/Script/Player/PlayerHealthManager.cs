using UnityEngine;

/// <summary>
/// 플레이어 체력 리소스 전담 관리자. PlayerManaManager와 완전히 같은 패턴.
/// 최대 체력(MaxHealth)은 PlayerStatManager가 계산한 캐릭터+장비+버프 합산 결과를 그대로 따라가고,
/// 현재 체력(CurrentHealth)은 여기서 독립적으로 들고 관리한다.
///
/// !! PlayerStat에 있던 currentHealth/TakeDamage/Heal을 이쪽으로 옮겨왔습니다.
///    maxHealth 자체는 여전히 PlayerStat 소관입니다 - "파생된 스탯값"이라 스탯 시스템에 두는 게 맞다고 판단했습니다.
///    (지금은 체력 자동 재생 스탯이 없어서 PlayerManaManager와 달리 Update()에 회복 틱은 없습니다.
///    나중에 체력 재생 스탯이 생기면 PlayerManaManager의 regenTimer 패턴을 그대로 가져오면 됨.)
/// </summary>
public class PlayerHealthManager : MonoBehaviour
{
    public static PlayerHealthManager Instance { get; private set; }

    /// <summary>PlayerStatManager.Stat.maxHealth를 그대로 따라가는 값. 외부에서 직접 바꿀 수 없음.</summary>
    public int MaxHealth { get; private set; }

    /// <summary>현재 체력.</summary>
    public int CurrentHealth { get; private set; }

    /// <summary>체력이 바뀔 때마다 발행. UI 등에서 구독해서 갱신.</summary>
    public event System.Action OnHealthChanged;

    /// <summary>체력이 0이 됐을 때 한 번만 발행. 전투 시스템에서 사망 처리에 사용하면 됨.</summary>
    public event System.Action OnDeath;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PlayerHealthManager] 이미 인스턴스가 존재해서 중복 오브젝트를 제거합니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        RefreshMaxHealth();
        CurrentHealth = MaxHealth; // 시작 시 풀피
        OnHealthChanged?.Invoke();
    }

    private void Update()
    {
        RefreshMaxHealth();
    }

    /// <summary>
    /// PlayerStatManager가 계산한 최종 maxHealth를 가져와서 갱신한다.
    /// 장비 해제 등으로 maxHealth가 줄어들어 CurrentHealth가 초과 상태가 되면 clamp한다.
    /// </summary>
    private void RefreshMaxHealth()
    {
        int newMax = PlayerStatManager.Instance != null ? PlayerStatManager.Instance.Stat.maxHealth : 0;
        if (newMax == MaxHealth)
            return;

        MaxHealth = newMax;

        if (CurrentHealth > MaxHealth)
        {
            CurrentHealth = MaxHealth;
            OnHealthChanged?.Invoke();
        }
    }

    /// <summary>체력을 최대치로 즉시 채운다. (포션, 휴식 등)</summary>
    public void FillHealth()
    {
        CurrentHealth = MaxHealth;
        OnHealthChanged?.Invoke();
    }

    /// <summary>
    /// 세이브 데이터 로드 등 외부에서 정확한 값으로 직접 설정할 때 사용. 0~MaxHealth로 clamp된다.
    /// TakeDamage/Heal과 달리 증감량이 아니라 절대값을 그대로 받는다는 점이 다름.
    /// </summary>
    public void SetCurrentHealth(int value)
    {
        CurrentHealth = Mathf.Clamp(value, 0, MaxHealth);
        OnHealthChanged?.Invoke();
    }

    /// <summary>amount만큼 데미지를 받는다. 0 밑으로는 안 내려가며, 0이 되면 OnDeath를 발행한다.</summary>
    public void TakeDamage(int amount)
    {
        if (amount <= 0)
            return;

        bool wasAlive = CurrentHealth > 0;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke();

        if (wasAlive && CurrentHealth <= 0)
            OnDeath?.Invoke();
    }

    /// <summary>amount만큼 체력을 회복한다 (최대치를 넘지 않음).</summary>
    public void Heal(int amount)
    {
        if (amount <= 0)
            return;

        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke();
    }
}
