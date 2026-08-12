using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 마나 리소스 전담 관리자.
/// 최대 마나(MaxMana)는 PlayerStatManager가 계산한 캐릭터+장비+버프 합산 결과를 그대로 따라가고,
/// 현재 마나(CurrentMana)는 여기서 독립적으로 들고 관리한다.
///
/// !! MaxMana를 int에서 float로 전환함 (CurrentMana는 원래부터 float).
///    UseMana(소모성)는 Mathf.Floor, RestoreMana(증가성)는 Mathf.Ceil로 amount를 보정해서 적용한다.
///    SetCurrentMana(세이브 로드 등 절대값 지정)는 보정 없이 그대로 clamp만 한다.
///
/// !! 멀티플레이 대비: Instance는 "내 캐릭터"만 가리킨다 (PlayerHealthManager와 동일 패턴).
///    PlayerStatManager도 GetComponent로 같은 캐릭터 것만 찾아서 쓴다.
/// </summary>
public class PlayerManaManager : MonoBehaviour
{
    public static PlayerManaManager Instance { get; private set; }

    /// <summary>씬에 존재하는 모든 캐릭터의 마나 매니저 (나 + 다른 플레이어).</summary>
    public static readonly List<PlayerManaManager> All = new List<PlayerManaManager>();

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
    public float MaxMana { get; private set; }

    /// <summary>현재 마나.</summary>
    public float CurrentMana { get; private set; }

    /// <summary>마나가 바뀔 때마다 발행. UI 등에서 구독해서 갱신.</summary>
    
    public event System.Action OnManaChanged;

    private float regenTimer;
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
            Debug.LogWarning("[PlayerManaManager] 이미 인스턴스가 존재해서 중복 오브젝트를 제거합니다.");
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
        return statManager != null ? statManager.Stat.mpRegen : 0f;
    }

    /// <summary>
    /// PlayerStatManager가 계산한 최종 maxMana를 가져와서 갱신한다.
    /// 장비 해제 등으로 maxMana가 줄어들어 CurrentMana가 초과 상태가 되면 clamp한다.
    /// 기본적으로 Update()에서 매 프레임 호출되지만, Recalculate() 직후 같은 프레임에 FillMana()를
    /// 불러야 하는 경우(레벨업 등)는 캐시된 MaxMana가 아직 갱신 전이라 외부에서 먼저 호출해야 한다.
    /// </summary>
    public void RefreshMaxMana()
    {
        float newMax = statManager != null ? statManager.Stat.maxMana : 0f;
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

    /// <summary>
    /// 세이브 데이터 로드 등 외부에서 정확한 값으로 직접 설정할 때 사용. 0~MaxMana로 clamp된다.
    /// UseMana/RestoreMana와 달리 증감량이 아니라 절대값을 그대로 받는다는 점이 다름 (올림/버림 보정 없음).
    /// </summary>
    public void SetCurrentMana(float value)
    {
        CurrentMana = Mathf.Clamp(value, 0f, MaxMana);
        OnManaChanged?.Invoke();
    }

    /// <summary>amount만큼 마나가 있는지 미리 확인 (실제로 깎지 않음). 스킬 사용 가능 여부 체크용.</summary>
    public bool HasEnoughMana(float amount)
    {
        return CurrentMana >= amount;
    }

    /// <summary>
    /// 마나를 amount만큼 소모한다. 소모성 처리라 amount는 Mathf.Floor로 버림 처리한 뒤 적용한다.
    /// 부족하면 아무 것도 깎지 않고 false를 반환한다.
    /// </summary>
    public bool UseMana(float amount)
    {
        float cost = Mathf.Floor(amount);
        if (cost <= 0f)
            return true;

        if (CurrentMana < cost)
            return false;

        CurrentMana -= cost;
        OnManaChanged?.Invoke();
        return true;
    }

    /// <summary>마나를 amount만큼 회복한다 (최대치를 넘지 않음). 증가성 처리라 amount는 Mathf.Ceil로 올림 처리한 뒤 적용한다.</summary>
    public void RestoreMana(float amount)
    {
        float restoreAmount = Mathf.Ceil(amount);
        if (restoreAmount <= 0f)
            return;

        CurrentMana = Mathf.Min(MaxMana, CurrentMana + restoreAmount);
        OnManaChanged?.Invoke();
    }
}
