using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/*
 * DamageText (★★★★★)
가장 구현이 쉽고 전투의 타격감을 크게 높여준다.
HP Bar (★★★★★)
이미 HpChanged 이벤트가 있으니 이벤트만 구독하면 금방 끝난다.
Flash Shader (★★★★☆)
구현 난이도는 조금 있지만 재사용성이 매우 높다.
Knockback Motion (★★★★★)
애니메이션이 없는 적까지 커버할 수 있고, 구현 대비 체감 효과가 크다.
 */
[RequireComponent(typeof(WBH_EnemyStatus))]
public class WBH_EnemyView : MonoBehaviour
{
    [SerializeField] private bool externalPresentation;
    [SerializeField] private Transform damageTextRoot;
    [SerializeField] private Vector2 damageTextSpacingPixels = new Vector2(56f, 28f);
    [SerializeField] private Vector2 creditTextOffset = new Vector2(1f, -0.25f);

    [Header("Hp Bar")]
    [SerializeField] private GameObject hpBarRoot;
    [SerializeField] private Slider hpBarSlider;
    [SerializeField] private float hpBarVisibleTime = 2f;

    [Header("Hit Flash")]
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private float hitFlashDuration = 0.12f;
    [SerializeField] private float hitFlashIntensity = 1f;

    private MaterialPropertyBlock propertyBlock;
    private static readonly int HitStrengthID = Shader.PropertyToID("_HitStrength");

    private WBH_FloatTextPoolManager poolManager;
    private WBH_EnemyStatus status;
    private WBH_EnemyController controller;
    private WBH_EnemyStatusEffectController statusEffects;
    private WBH_HighEnemyHpbarView highEnemyHpView; // !@ 차후 UI 와 합일 필요
    private EnemyKillReward killReward;
    private Camera mainCamera;
    private System.Func<bool> canPresent;
    private bool localEventsSubscribed;
    private bool hasPresentedHealth;
    private float presentedCurrentHp;
    private float presentedMaxHp;
    private double damageTextBurstStartedAt = double.NegativeInfinity;
    private int damageTextBurstIndex;

    private Coroutine hideHpBarCoroutine;
    private Coroutine hitFlashCoroutine;

    private float damageFlashStrength; // 피격 시, emission 값
    private float selfDestructFlashStrength; // 자폭병 현재 eimission 값
    private float selfDestructFlashStartStrength; // 자폭병 시작 eimission 값
    private float selfDestructFlashTargetStrength; // 자폭병 최대 eimission 값
    private float selfDestructFlashTargetDuration; // 자폭병 최대 emission 도달 시간 
    private float selfDestructFlashTargetElapsed; 
    private bool isSelfDestructFlashTransition;
    /// <summary>SW 수정: 서버의 자폭 점멸 전환을 원격 표시에도 전달합니다. duration 0은 즉시 표시입니다.</summary>
    public event System.Action<bool, float> SelfDestructFlashRequested;

    /// <summary>SW 수정: 비활성 View의 기존 외부 호출을 보존하며, 활성 View는 LateUpdate에서만 갱신합니다.</summary>
    public void TickExternalFlash(float deltaTime)
    {
        if (isActiveAndEnabled || !gameObject.activeInHierarchy ||
            (externalPresentation && (canPresent == null || !canPresent())))
            return;

        UpdateSelfDestructFlash(deltaTime);
    }


    private void Awake()
    {
        status = GetComponent<WBH_EnemyStatus>();
        controller = GetComponent<WBH_EnemyController>();
        statusEffects = GetComponent<WBH_EnemyStatusEffectController>(); // SW 수정: 상태 반응만 구독합니다.
        killReward = GetComponent<EnemyKillReward>();

        mainCamera = Camera.main;

        if(hpBarRoot != null)
            hpBarRoot.SetActive(false);

        propertyBlock = new MaterialPropertyBlock();

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();
    }

    /// <summary>SW 수정: 활성화마다 이전 표시 값을 초기화하고 로컬 표시일 때만 상태 이벤트를 구독합니다.</summary>
    private void OnEnable()
    {
        hasPresentedHealth = false;
        damageTextBurstStartedAt = double.NegativeInfinity;
        damageTextBurstIndex = 0;
        HideHpBar();
        SetLocalEventSubscription(!externalPresentation);
    }

    /// <summary>SW 수정: 비활성화 시 상태 구독, 체력 바와 점멸을 정리해 재사용 시 표시가 남지 않게 합니다.</summary>
    private void OnDisable()
    {
        SetLocalEventSubscription(false);
        HideHpBar();
        hasPresentedHealth = false;

        ResetFlash();
    }

    /// <summary>SW 수정: 로컬 피해·체력·화상·보상 이벤트 구독을 한곳에서 중복 없이 전환합니다.</summary>
    private void SetLocalEventSubscription(bool subscribe)
    {
        if (localEventsSubscribed == subscribe)
            return;

        if (subscribe)
        {
            if (status != null)
            {
                status.OnDamaged += ViewOnDamaged;
                status.OnHpChanged += UpdateHpBar;
            }
            if (statusEffects != null)
                statusEffects.OnBurnResponse += ShowBurnResponse;
            if (killReward != null)
                killReward.OnCreditGranted += ShowCreditReward;
        }
        else
        {
            if (status != null)
            {
                status.OnDamaged -= ViewOnDamaged;
                status.OnHpChanged -= UpdateHpBar;
            }
            if (statusEffects != null)
                statusEffects.OnBurnResponse -= ShowBurnResponse;
            if (killReward != null)
                killReward.OnCreditGranted -= ShowCreditReward;
        }

        localEventsSubscribed = subscribe;
    }

    /// <summary>SW 수정: 활성 상태와 외부 표시 권한을 함께 확인합니다.</summary>
    private bool CanPresent => isActiveAndEnabled &&
        (!externalPresentation || (canPresent != null && canPresent()));

    /// <summary>SW 수정: 로컬 상태 이벤트 대신 외부에서 확정한 표시 값과 표시 가능 여부를 받습니다.</summary>
    public void BindExternalPresentation(System.Func<bool> canPresent)
    {
        externalPresentation = true;
        this.canPresent = canPresent;
        SetLocalEventSubscription(false);
        hasPresentedHealth = false;
        HideHpBar();
    }

    /// <summary>SW 수정: 진행 중인 피격 점멸과 자폭 점멸 상태를 모두 초기화합니다.</summary>
    private void ResetFlash()
    {
        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        damageFlashStrength = 0f;
        selfDestructFlashStrength = 0f;
        selfDestructFlashStartStrength = 0f;
        selfDestructFlashTargetStrength = 0f;
        selfDestructFlashTargetDuration = 0f;
        selfDestructFlashTargetElapsed = 0f;
        isSelfDestructFlashTransition = false;

        SetHitStrength(0);
    }

    // 메인카메라를 바라보는 코드
    /// <summary>SW 수정: 표시할 수 없으면 체력 바와 점멸을 정리하고, 표시 중에는 점멸과 카메라 방향을 갱신합니다.</summary>
    private void LateUpdate()
    {
        if (!CanPresent)
        {
            HideHpBar();
            if (damageFlashStrength != 0f || selfDestructFlashStrength != 0f || isSelfDestructFlashTransition)
                ResetFlash();
            return;
        }

        UpdateSelfDestructFlash(Time.deltaTime); 

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (hpBarRoot == null || !hpBarRoot.activeSelf || mainCamera == null)
            return;

        hpBarRoot.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);
    }

    public void Initialize(WBH_FloatTextPoolManager poolManager, WBH_HighEnemyHpbarView eliteView)
    {
        this.poolManager = poolManager;
        this.highEnemyHpView = eliteView;
    }

    // 피격 시 보여주는 처리
    /// <summary>SW 수정: 로컬 피해도 공통 피해 표시를 사용하고, 표시 가능한 엘리트 적만 체력 UI에 연결합니다.</summary>
    private void ViewOnDamaged(WBH_DamageResult result)
    {
        bool isSelfAttack = ReferenceEquals(result.Attacker, controller);
        ShowDamage(result, transform.position, isSelfAttack);

        // 엘리트 적일 경우 UI 갱신 !@ 차후 합일 시 수정
        if (!CanPresent || !(result.Attacker is T_PlayerController))
            return;

        if (controller == null || controller.Info == null || controller.Info.enemyGrade != EnemyGrade.Elite)
            return;

        highEnemyHpView?.BindElite(controller);
    }

    /// <summary>SW 수정: 일반 플레이의 화상 저항·면역도 기존 데미지 텍스트 풀로 표시합니다.</summary>
    private void ShowBurnResponse(bool immune)
    {
        ShowBurnResponse(immune, transform.position);
    }

    /// <summary>SW 수정: 로컬 피해와 외부 피해가 같은 숫자 포맷, 풀, 배치와 피격 Flash를 사용합니다.</summary>
    public bool ShowDamage(WBH_DamageResult result, Vector3 enemyPosition, bool selfAttack = false)
    {
        if (!CanPresent || selfAttack || !float.IsFinite(result.FinalDamage))
            return false;

        // 적 적색으로 깜빡임 처리
        PlayHitFlash();

        // 데미지 텍스트 처리
        WBH_DamageText damageText = RentDamageText(enemyPosition);
        if (damageText == null)
            return false;

        damageText.Show(damageText.transform.position, result);
        return true;
    }

    /// <summary>SW 수정: 화상 반응도 숫자와 같은 풀과 연속 타격 배치를 사용합니다.</summary>
    public bool ShowBurnResponse(bool immune, Vector3 enemyPosition)
    {
        WBH_DamageText damageText = RentDamageText(enemyPosition);
        if (damageText == null)
            return false;

        damageText.ShowBurnResponse(damageText.transform.position, immune);
        return true;
    }

    /// <summary>SW 수정: 주입된 텍스트 풀을 재사용하고, 참조가 없을 때만 활성 풀을 찾습니다.</summary>
    private bool ResolvePool()
    {
        if (poolManager == null)
            poolManager = FindFirstObjectByType<WBH_FloatTextPoolManager>(FindObjectsInactive.Exclude);

        return poolManager != null;
    }

    /// <summary>SW 수정: 표시 권한과 카메라·풀을 확인한 뒤 적의 표시 기준점에 피해 텍스트를 배치합니다.</summary>
    private WBH_DamageText RentDamageText(Vector3 enemyPosition)
    {
        if (!CanPresent)
            return null;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null || !ResolvePool())
            return null;

        WBH_DamageText damageText = poolManager.GetDamageText();
        if (damageText == null)
            return null;

        Vector3 anchorOffset = damageTextRoot != null
            ? damageTextRoot.position - transform.position
            : Vector3.up * 1.5f;
        damageText.Initialize(poolManager);
        damageText.transform.position = OffsetDamageText(enemyPosition + anchorOffset);
        return damageText;
    }

    /// <summary>SW 수정: 짧은 시간에 연속 발생한 피해 텍스트를 화면 픽셀 간격으로 좌우와 위쪽에 나눠 배치합니다.</summary>
    private Vector3 OffsetDamageText(Vector3 position)
    {
        double currentTime = Time.unscaledTimeAsDouble;
        if (currentTime < damageTextBurstStartedAt || currentTime - damageTextBurstStartedAt >= 0.12d)
        {
            damageTextBurstStartedAt = currentTime;
            damageTextBurstIndex = 0;
        }

        int damageTextIndex = damageTextBurstIndex++;
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(position);
        if (screenPosition.z <= 0f)
            return position;

        screenPosition.x += ((damageTextIndex & 1) == 0 ? -0.5f : 0.5f) * damageTextSpacingPixels.x;
        screenPosition.y += (damageTextIndex / 2) * damageTextSpacingPixels.y;
        return mainCamera.ScreenToWorldPoint(screenPosition);
    }

    /// <summary>SW 수정: 표시 권한과 풀을 확인해 보상 텍스트를 표시하고, 풀에서 받지 못하면 표시를 건너뜁니다.</summary>
    private void ShowCreditReward(int amount)
    {
        if (!CanPresent || amount <= 0 || !ResolvePool())
            return;

        WBH_CreditText creditText = poolManager.GetCreditText();
        if (creditText == null)
            return;

        Vector3 basePosition = damageTextRoot != null ? damageTextRoot.position : transform.position + Vector3.up;

        Camera targetCam = mainCamera != null ? mainCamera : Camera.main;

        Vector3 offset = targetCam != null ? 
            targetCam.transform.right * creditTextOffset.x + targetCam.transform.up * creditTextOffset.y 
            : Vector3.right * creditTextOffset.x + Vector3.down * - creditTextOffset.y;

        creditText.Show(basePosition + offset, amount);
    }

    /// <summary>SW 수정: 외부 체력 값이 바뀔 때만 기존 2초 노출을 갱신하며, 사망 시 즉시 숨깁니다.</summary>
    public void PresentHealth(float currentHealth, float maxHealth, bool isDead)
    {
        if (!CanPresent || !float.IsFinite(currentHealth) || !float.IsFinite(maxHealth) || maxHealth <= 0f)
        {
            HideHpBar();
            return;
        }

        bool isFirstHealthPresentation = !hasPresentedHealth;
        bool hasHealthChanged = isFirstHealthPresentation || currentHealth != presentedCurrentHp || maxHealth != presentedMaxHp;
        hasPresentedHealth = true;
        presentedCurrentHp = currentHealth;
        presentedMaxHp = maxHealth;

        if (isDead || currentHealth <= 0f)
        {
            HideHpBar();
            return;
        }

        // 원본 Initialize는 HP 이벤트를 보내지 않으므로 첫 전체 체력 수신도 바를 띄우지 않습니다.
        if (!hasHealthChanged || (isFirstHealthPresentation && currentHealth >= maxHealth))
            return;

        UpdateHpBar(currentHealth, maxHealth);
    }

    // 노말, 어드밴스드 적 hp 바 갱신
    /// <summary>SW 수정: 표시 가능한 유효 체력만 비율로 반영하고, 체력이 없으면 체력 바를 즉시 숨깁니다.</summary>
    private void UpdateHpBar(float currentHp, float maxHp)
    {
        if (!CanPresent || hpBarSlider == null || hpBarRoot == null ||
            !float.IsFinite(currentHp) || !float.IsFinite(maxHp) || maxHp <= 0f)
            return;

        if (currentHp <= 0f)
        {
            HideHpBar();
            return;
        }

        hpBarSlider.value = Mathf.Clamp01(currentHp / maxHp);

        ShowHpBar();
    }

    private void ShowHpBar()
    {
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

    /// <summary>SW 수정: 체력 바 자동 숨김 대기를 취소하고 현재 체력 바를 즉시 숨깁니다.</summary>
    private void HideHpBar()
    {
        if (hideHpBarCoroutine != null)
        {
            StopCoroutine(hideHpBarCoroutine);
            hideHpBarCoroutine = null;
        }

        if (hpBarRoot != null && hpBarRoot.activeSelf)
            hpBarRoot.SetActive(false);
    }

    // 피격 시, 붉은 반짝임 처리.
    private void PlayHitFlash()
    {
        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);

        hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        damageFlashStrength = hitFlashIntensity;
        ApplyFlashStrength();

        yield return new WaitForSeconds(hitFlashDuration);

        damageFlashStrength = 0f;
        ApplyFlashStrength();

        hitFlashCoroutine = null;
    }

    private void SetHitStrength(float value)
    {
        foreach(Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null)
                continue;

            Material[] materials = targetRenderer.sharedMaterials;

            for(int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                if (materials[materialIndex] == null)
                    continue;

                targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);

                propertyBlock.SetFloat(HitStrengthID, value);

                targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
        }
    }

    // 자폭병 반짝임
    /// <summary>SW 수정: 자폭 점멸 전환 요청을 전달한 뒤 외부 표시 권한이 있는 경우에만 로컬 점멸을 적용합니다.</summary>
    public void SetSelfDestructFlash(bool visible,float transitionDuration)
    {
        SelfDestructFlashRequested?.Invoke(visible, transitionDuration);
        if (externalPresentation && (canPresent == null || !canPresent()))
            return;

        float targetStrength = visible ? 1f : 0f;

        selfDestructFlashStartStrength = selfDestructFlashStrength;
        selfDestructFlashTargetStrength = targetStrength;

        selfDestructFlashTargetDuration = Mathf.Max(0.01f, transitionDuration);

        selfDestructFlashTargetElapsed = 0f;
        isSelfDestructFlashTransition = true;
    }

    /// <summary>SW 수정: 즉시 자폭 점멸 요청을 전달한 뒤 외부 표시 권한이 있는 경우에만 로컬 점멸을 적용합니다.</summary>
    public void SetSelfDestructFlash(bool visible)
    {
        SelfDestructFlashRequested?.Invoke(visible, 0f);
        if (externalPresentation && (canPresent == null || !canPresent()))
            return;

        selfDestructFlashStrength = visible ? 1f : 0f;
        ApplyFlashStrength();
    }

    // 자폭병 반짝임 적용
    private void ApplyFlashStrength()
    {
        SetHitStrength(Mathf.Max(damageFlashStrength, selfDestructFlashStrength));
    }

    private void UpdateSelfDestructFlash(float deltaTime)
    {
        if (!isSelfDestructFlashTransition)
            return;

        selfDestructFlashTargetElapsed += deltaTime;

        float t = Mathf.Clamp01(selfDestructFlashTargetElapsed / selfDestructFlashTargetDuration); 

        float easedT = t * t * (3f - 2f * t); // 시작과 끝 부드럽게 처리.

        selfDestructFlashStrength = Mathf.Lerp(selfDestructFlashStartStrength, selfDestructFlashTargetStrength, easedT); // 일정속도의 선형변화는 easedT 대신 t 사용

        ApplyFlashStrength() ;

        if (t < 1f)
            return;

        selfDestructFlashStrength = selfDestructFlashTargetStrength;

        isSelfDestructFlashTransition = false;
    }
}
