using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 체력 리소스 전담 관리자. PlayerManaManager와 완전히 같은 패턴.
/// 최대 체력(MaxHealth)은 PlayerStatManager가 계산한 캐릭터+장비+버프 합산 결과를 그대로 따라가고,
/// 현재 체력(CurrentHealth)은 여기서 독립적으로 들고 관리한다.
///
/// !! MaxHealth/CurrentHealth를 int에서 float로 전환함.
///    TakeDamage(소모성)는 Mathf.Floor, Heal(증가성)는 Mathf.Ceil로 amount를 보정해서 적용한다
///    (둘 다 플레이어에게 불리하지 않은 방향으로 반올림).
///    SetCurrentHealth(세이브 로드 등 절대값 지정)는 보정 없이 그대로 clamp만 한다.
///
/// !! 멀티플레이 대비: Instance는 "내 캐릭터"만 가리킨다 (Mirror NetworkIdentity가 있고
///    isLocalPlayer가 false면 Instance로 등록 안 함). NetworkIdentity가 아예 없으면
///    (지금처럼 싱글플레이 테스트 중이면) 항상 등록되어 기존과 동일하게 동작한다.
///    다른 캐릭터(다른 플레이어)의 체력을 보고 싶을 때는 All 목록에서 찾으면 됨.
///    PlayerStatManager도 "전역 Instance"가 아니라 같은 캐릭터의 컴포넌트를 GetComponent로
///    찾아서 쓴다 (안 그러면 남의 캐릭터 체력이 내 스탯을 참조하게 됨).
/// </summary>
public class PlayerHealthManager : MonoBehaviour
{
    public static PlayerHealthManager Instance { get; private set; }

    /// <summary>씬에 존재하는 모든 캐릭터의 체력 매니저 (나 + 다른 플레이어). 헬스바 등 조회용.</summary>
    public static readonly List<PlayerHealthManager> All = new List<PlayerHealthManager>();

    /// <summary>PlayerStatManager.Stat.maxHealth를 그대로 따라가는 값. 외부에서 직접 바꿀 수 없음.</summary>
    public float MaxHealth { get; private set; }

    /// <summary>현재 체력.</summary>
    public float CurrentHealth { get; private set; }

    /// <summary>체력이 바뀔 때마다 발행. UI 등에서 구독해서 갱신.</summary>
    public event System.Action OnHealthChanged;

    /// <summary>체력이 0이 됐을 때 한 번만 발행. 전투 시스템에서 사망 처리에 사용하면 됨.</summary>
    public event System.Action OnDeath;

    /// <summary>데미지를 받을 때마다 발행 (보정된 실제 데미지량 전달). 회복와 구별해서 발행되기 때문에 "피격 시" 발동 조건에 쓸 수 있음.</summary>
    public event System.Action<float> OnDamageTaken;

    /// <summary>SW 수정: 피해가 체력에 적용되기 전에 보호막 등이 처리한 뒤 남은 피해를 반환합니다.</summary>
    public event System.Func<float, float> OnBeforeDamageApplied;

    private PlayerStatManager statManager;

    private void Awake()
    {
        All.Add(this);
        statManager = GetComponent<PlayerStatManager>();

        var identity = GetComponent<Mirror.NetworkIdentity>();
        if (identity != null && !identity.isLocalPlayer)
            return;

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PlayerHealthManager] 이미 인스턴스가 존재해서 중복 오브젝트를 제거합니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        All.Remove(this);
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
            Debug.LogWarning("[PlayerHealthManager] 이미 인스턴스가 존재해서 다시 활성화된 오브젝트를 등록하지 않습니다.");
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
    /// 기본적으로 Update()에서 매 프레임 호출되지만, Recalculate() 직후 같은 프레임에 FillHealth()를
    /// 불러야 하는 경우(레벨업 등)는 캐시된 MaxHealth가 아직 갱신 전이라 외부에서 먼저 호출해야 한다.
    /// </summary>
    public void RefreshMaxHealth()
    {
        float newMax = statManager != null ? statManager.Stat.maxHealth : 0f;
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
    /// TakeDamage/Heal과 달리 증감량이 아니라 절대값을 그대로 받는다는 점이 다름 (올림/버림 보정 없음).
    /// </summary>
    public void SetCurrentHealth(float value)
    {
        CurrentHealth = Mathf.Clamp(value, 0f, MaxHealth);
        OnHealthChanged?.Invoke();
    }

    /// <summary>
    /// SW 수정: amount만큼 피해를 소수점 아래로 버린 뒤 적용합니다.
    /// 보호막이 먼저 피해를 줄이고, 남은 피해만 체력에 적용합니다.
    /// OnDamageTaken은 보호막으로 모두 막아도 처음 받은 피해량을 전달하며, 체력이 0이 되면 OnDeath를 발행합니다.
    /// </summary>
    public void TakeDamage(float amount)
    {
        float damageAmount = Mathf.Floor(amount);
        if (damageAmount <= 0f || CurrentHealth <= 0f)
            return;

        float remainingDamage = damageAmount;
        if (OnBeforeDamageApplied != null)
        {
            foreach (System.Func<float, float> reduceDamage in OnBeforeDamageApplied.GetInvocationList())
                remainingDamage = Mathf.Clamp(reduceDamage(remainingDamage), 0f, remainingDamage);
        }

        float actualDamage = Mathf.Min(CurrentHealth, remainingDamage);
        if (actualDamage > 0f)
        {
            CurrentHealth -= actualDamage;
            OnHealthChanged?.Invoke();
        }
        // SW 수정: 보호막으로 모두 막아도 피격 효과의 조건은 처음 받은 피해량을 사용합니다.
        OnDamageTaken?.Invoke(damageAmount);

        if (actualDamage > 0f && CurrentHealth <= 0f)
            OnDeath?.Invoke();
    }

    /// <summary>amount만큼 체력을 회복한다 (최대치를 넘지 않음). 증가성 처리라 amount는 Mathf.Ceil로 올림 처리한 뒤 적용한다.</summary>
    public void Heal(float amount)
    {
        float healAmount = Mathf.Ceil(amount);
        if (healAmount <= 0f)
            return;

        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + healAmount);
        OnHealthChanged?.Invoke();
    }
}
