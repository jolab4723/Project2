using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 공격 테스트용 허수아비(타격 연습 대상). 이동/공격 없이 맞기만 하면 되므로,
/// WBH_EnemyMovement/WBH_EnemyCombat/WBH_EnemyPattern 등 서로 [RequireComponent]로 얽혀있는
/// 기존 적 AI 컴포넌트 체인에 의존하지 않는 독립 컴포넌트로 만들었다.
/// WBH_EnemyStatus/WBH_EnemyController가 하던 WBH_ICombat/WBH_ICombatStatus 구현 부분만
/// 필요한 만큼 그대로 옮겨왔다(원본 두 파일은 서로 RequireComponent로 묶여 있어 그대로 재사용 불가).
///
/// 이 컴포넌트 하나만 붙이면 그 자체로 공격 대상이 된다. 플레이어의 근접 공격(SectorAttack)과
/// 투사체는 Physics.OverlapSphere + TryGetComponent&lt;WBH_ICombat&gt;로 대상을 찾으므로,
/// 같은 GameObject에 Collider를 두고 GameObject의 레이어를 플레이어 공격이 보는 enemyLayer로
/// 맞춰야 실제로 맞는다.
///
/// - 기초 체력 3000, 이동/공격 기능 없음(애초에 그런 컴포넌트를 붙이지 않아서 자연히 없음).
/// - 마지막으로 맞은 지 resetDelayAfterLastHit(기본 4초)가 지나면 체력을 최대치로 되돌린다.
///   사망 처리를 하지 않으므로(IsDead 항상 false) 몇 번을 때려도 사라지지 않고 계속 때릴 수 있다.
/// - 원본 Normal_Melee_01의 HP바(NormalMonsterHpBar/HpBar/Slider) 자식은 그대로 옮겨왔지만
///   그걸 갱신하던 WBH_EnemyView는 붙일 수 없으므로(WBH_EnemyStatus 전용), 여기서 같은 방식으로
///   직접 갱신한다. 이름으로 자동 탐색하므로 별도 인스펙터 연결이 필요 없다.
/// </summary>
public class TrainingDummyStatus : MonoBehaviour, WBH_ICombat, WBH_ICombatStatus
{
    [Tooltip("허수아비의 기초 체력.")]
    [SerializeField] private float maxHp = 3000f;

    [Tooltip("마지막으로 맞은 뒤 이 시간(초)이 지나면 체력을 최대치로 되돌린다.")]
    [SerializeField] private float resetDelayAfterLastHit = 4f;

    [Tooltip("HP바를 담은 오브젝트. 비워두면 자식 중 \"HpBar\"라는 이름을 자동으로 찾는다.")]
    [SerializeField] private GameObject hpBarRoot;

    [Tooltip("체력 표시 Slider. 비워두면 자식에서 자동으로 찾는다.")]
    [SerializeField] private Slider hpBarSlider;

    [Tooltip("피격 후 HP바를 계속 보여줄 시간(초).")]
    [SerializeField] private float hpBarVisibleTime = 2f;

    private float currentHp;
    private float lastHitTime = float.NegativeInfinity;
    private Coroutine hideHpBarCoroutine;

    // ----- WBH_ICombat -----
    public WBH_ICombatStatus Status => this;

    public void TakeDamage(WBH_DamageResult result)
    {
        currentHp = Mathf.Max(0f, currentHp - result.FinalDamage);
        lastHitTime = Time.time;

        Debug.Log($"[TrainingDummy] {result.FinalDamage:F0} 데미지 (크리티컬={result.IsCritical}) / 남은 체력 {currentHp:F0}", this);
        UpdateHpBar();
    }

    /// <summary>허수아비는 상태이상을 적용받을 필요가 없어 비워둔다. WBH_ICombat 계약만 충족시킨다.</summary>
    public void AddStatusEffect(WBH_StatusEffectData data) { }

    // ----- WBH_ICombatStatus -----
    public float CurrentHp => currentHp;
    public float MaxHealth => maxHp;
    public float AttackPower => 0f;
    public float DefensePower => 0f;
    public float CritRate => 0f;
    public float CritMult => 1f;
    public float FireBonus => 0f;
    public float IceBonus => 0f;
    public float ElectricBonus => 0f;

    /// <summary>허수아비는 죽지 않고 체력만 초기화되므로 항상 false.</summary>
    public bool IsDead => false;

    private void Awake()
    {
        currentHp = maxHp;

        if (hpBarSlider == null)
        {
            var found = transform.Find("NormalMonsterHpBar/HpBar");
            if (found != null)
            {
                hpBarRoot = hpBarRoot != null ? hpBarRoot : found.gameObject;
                hpBarSlider = found.GetComponentInChildren<Slider>(true);
            }
        }

        if (hpBarRoot != null)
            hpBarRoot.SetActive(false);
    }

    private void Update()
    {
        if (currentHp >= maxHp)
            return;

        if (Time.time - lastHitTime >= resetDelayAfterLastHit)
        {
            currentHp = maxHp;
            UpdateHpBar();
        }
    }

    private void UpdateHpBar()
    {
        if (hpBarSlider == null || hpBarRoot == null)
            return;

        hpBarSlider.value = currentHp / maxHp;

        hpBarRoot.SetActive(true);

        if (hideHpBarCoroutine != null)
            StopCoroutine(hideHpBarCoroutine);

        hideHpBarCoroutine = StartCoroutine(HideHpBarRoutine());
    }

    private IEnumerator HideHpBarRoutine()
    {
        yield return new WaitForSeconds(hpBarVisibleTime);

        hpBarRoot.SetActive(false);
        hideHpBarCoroutine = null;
    }
}
