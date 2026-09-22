using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// 공격 테스트용 허수아비(타격 연습 대상). 이동/공격 없이 맞기만 하면 되므로,
/// WBH_EnemyMovement/WBH_EnemyCombat/WBH_EnemyPattern 등 서로 [RequireComponent]로 얽혀있는
/// 기존 적 AI 컴포넌트 체인에 의존하지 않는 독립 컴포넌트로 만들었다.
/// WBH_EnemyStatus/WBH_EnemyController가 하던 WBH_ICombat/WBH_ICombatStatus 구현 부분만
/// 필요한 만큼 그대로 옮겨왔다(원본 두 파일은 서로 RequireComponent로 묶여 있어 그대로 재사용 불가).
///
/// !! "적 0번"으로 만들려고 WBH_ 접두사를 뗀 복사본 프레임워크(EnemyStatus/StatusEffectController 등)를
///    한 번 붙였다가 다시 뗐다 - 실제 적 적용은 결국 BH님 파일(WBH_EnemyStatus 등)을 직접 수정하는
///    쪽으로 방향이 정해져서, 복사본 전용으로 만들었던 부분은 다시 정리했다.
///
/// 버프(EnemyBuffManager)는 실제 적과 같은 컴포넌트를 그대로 쓴다 - WBH_EnemyStatus와 이 클래스가
/// 둘 다 IStatBuffTarget(ApplyBuffStatSet)을 구현해서, EnemyBuffManager가 대상 타입을 몰라도
/// 똑같이 동작한다(예전엔 DummyBuffManager라는 별도 컴포넌트였는데, 실제 적과 중복 구현이라 통합함).
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
public class TrainingDummyStatus : MonoBehaviour, WBH_ICombat, WBH_ICombatStatus, ItemSystem.IStatBuffTarget
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

    [Tooltip("허수아비의 기초 방어력. 방어감소 디버프를 테스트하려면 0보다 커야 실제 피해량 차이가 보인다.")]
    [SerializeField] private float defensePower = 20f;

    private float currentHp;
    private float currentDefensePower;
    private float lastHitTime = float.NegativeInfinity;
    private Coroutine hideHpBarCoroutine;
    private Coroutine knockbackRoutine;
    private Coroutine airborneRoutine;
    private float airborneGroundY; // 에어본 시작 전 지면 높이. 도중에 넉백이 끼어들 때 지면으로 되돌리기 위해 기억해둔다.

    // ----- WBH_ICombat -----
    public WBH_ICombatStatus Status => this;

    public void TakeDamage(WBH_DamageResult result)
    {
        currentHp = Mathf.Max(0f, currentHp - result.FinalDamage);
        lastHitTime = Time.time;

        Debug.Log($"[TrainingDummy] {result.FinalDamage:F0} 데미지 (크리티컬={result.IsCritical}) / 남은 체력 {currentHp:F0}", this);
        UpdateHpBar();
    }

    /// <summary>도트 데미지(화상 등) 전용 - WBH_DamageResult(크리티컬 등)가 필요 없는 경로.</summary>
    public void ApplyDotDamage(float damage)
    {
        currentHp = Mathf.Max(0f, currentHp - damage);
        lastHitTime = Time.time;
        UpdateHpBar();
    }

    private float buffDefensePercent;
    private float buffDefenseFlat;
    private float buffMoveSpeedPercent;

    /// <summary>PlayerBuffManager와 같은 BuffTracker 기반 버프 레이어 결과를 반영한다. EnemyBuffManager가 호출한다.</summary>
    public void ApplyBuffStatSet(StatSet statSet)
    {
        buffDefensePercent = statSet.defensePowerPercent;
        buffDefenseFlat = statSet.defensePowerFlat;
        buffMoveSpeedPercent = statSet.moveSpeedPercent;
        RecalculateDefense();
    }

    private void RecalculateDefense()
    {
        currentDefensePower = Mathf.Max(0f, defensePower * (1f + buffDefensePercent / 100f) + buffDefenseFlat);
    }

    /// <summary>버프로 인한 이동속도 배율(1 = 변화 없음). 허수아비는 자체 이동속도 스탯이 없어서(DummyFollowPlayer의
    /// followSpeed가 유일한 이동속도 값) moveSpeedFlat 가산은 반영할 기준값이 없어 percent만 배율로 반영한다.
    /// DummyFollowPlayer가 자신의 followSpeed에 이 값을 곱해서 쓴다.</summary>
    public float MoveSpeedMultiplier => Mathf.Max(0f, 1f + buffMoveSpeedPercent / 100f);

    /// <summary>
    /// 허수아비는 이동 AI/공격이 없어서 실제로 눈에 보이는 반응이 있는 넉백/에어본(위치 이동)만
    /// 반영한다. 기절/둔화 등은 애초에 허수아비가 움직이지 않으니 시각적으로 확인할 게 없어
    /// 생략했다(WBH_EnemyStatusEffectController.ApplyKnockback/ApplyAirborne과 같은 방식으로 구현).
    /// </summary>
    public void AddStatusEffect(WBH_StatusEffectData data)
    {
        switch (data.Type)
        {
            case WBH_StatusEffectType.KnockBack:
                if (knockbackRoutine != null)
                    StopCoroutine(knockbackRoutine);

                if (airborneRoutine != null)
                {
                    // 에어본 도중 넉백이 끼어들면, 넉백은 수평 이동만 하고 Y를 안 건드리기 때문에
                    // 뜬 높이를 그대로 시작점으로 삼아 넉백이 끝나도 계속 공중에 남는 문제가 있었다.
                    // 넉백을 시작하기 전에 먼저 지면으로 되돌린다.
                    StopCoroutine(airborneRoutine);
                    airborneRoutine = null;

                    Vector3 grounded = transform.position;
                    grounded.y = airborneGroundY;
                    transform.position = grounded;
                }

                knockbackRoutine = StartCoroutine(KnockbackRoutine(data.Direction, data.Force, data.Duration));
                break;

            case WBH_StatusEffectType.Airborne:
                if (airborneRoutine != null)
                    StopCoroutine(airborneRoutine);

                if (knockbackRoutine != null)
                {
                    StopCoroutine(knockbackRoutine);
                    knockbackRoutine = null;
                }

                airborneGroundY = transform.position.y;
                airborneRoutine = StartCoroutine(AirborneRoutine(data.Height, data.Duration));
                break;
        }
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float force, float duration)
    {
        Vector3 start = transform.position;
        Vector3 rawEnd = start + direction.normalized * force;

        // 벽 등 NavMesh 밖으로 밀려나지 않도록 경로를 검사한다(FighterSkillController.ExecuteDash와 같은 방식).
        Vector3 end = NavMesh.Raycast(start, rawEnd, out NavMeshHit hit, NavMesh.AllAreas) ? hit.position : rawEnd;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        transform.position = end;
        knockbackRoutine = null;
    }

    private IEnumerator AirborneRoutine(float height, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = start + Vector3.up * (Mathf.Sin(t * Mathf.PI) * height);
            yield return null;
        }

        transform.position = start;
        airborneRoutine = null;
    }

    // ----- WBH_ICombatStatus -----
    public float CurrentHp => currentHp;
    public float MaxHealth => maxHp;
    public float AttackPower => 0f;
    public float DefensePower => currentDefensePower;
    public float CritRate => 0f;
    public float CritMult => 1f;
    public float Pen => 0f;
    public float FireBonus => 0f;
    public float IceBonus => 0f;
    public float ElectricBonus => 0f;
    /// <summary>허수아비는 Marked(받는 데미지 증가) 디버프를 반영하지 않아서 항상 1(영향 없음).</summary>
    public float DamageTakenModifier => 1f;
    // 허수아비는 공격하지 않는다. 인터페이스 구현만 채운다.
    public float NormalDamageModifier => 1f;
    public float SkillDamageModifier => 1f;

    /// <summary>허수아비는 죽지 않고 체력만 초기화되므로 항상 false.</summary>
    public bool IsDead => false;

    private void Awake()
    {
        currentHp = maxHp;
        currentDefensePower = defensePower;

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
