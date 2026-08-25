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
    [SerializeField] private Transform damageTextRoot;
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
    private WBH_HighEnemyHpbarView highEnemyHpView; // !@ 차후 UI 와 합일 필요
    private EnemyKillReward killReward;
    private Camera mainCamera;

    private Coroutine hideHpBarCoroutine;
    private Coroutine hitFlashCoroutine;

    private float damageFlashStrength; // 피격 시, emission 값
    private float selfDestructFlashStrength; // 자폭병 현재 eimission 값
    private float selfDestructFlashStartStrength; // 자폭병 시작 eimission 값
    private float selfDestructFlashTargetStrength; // 자폭병 최대 eimission 값
    private float selfDestructFlashTargetDuration; // 자폭병 최대 emission 도달 시간 
    private float selfDestructFlashTargetElapsed; 
    private bool isSelfDestructFlashTransition; 


    private void Awake()
    {
        status = GetComponent<WBH_EnemyStatus>();
        controller = GetComponent<WBH_EnemyController>();
        killReward = GetComponent<EnemyKillReward>();

        mainCamera = Camera.main;

        if(hpBarRoot != null)
            hpBarRoot.SetActive(false);

        propertyBlock = new MaterialPropertyBlock();

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnEnable()
    {
        status.OnDamaged += ViewOnDamaged;
        status.OnHpChanged += UpdateHpBar;

        if(killReward != null)
        {
            killReward.OnCreditGranted += ShowCreditReward;
        }
    }

    private void OnDisable()
    {
        status.OnDamaged -= ViewOnDamaged;
        status.OnHpChanged -= UpdateHpBar;

        if (killReward != null)
        {
            killReward.OnCreditGranted -= ShowCreditReward;
        }

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
    private void LateUpdate()
    {
        UpdateSelfDestructFlash(Time.deltaTime); 

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
    private void ViewOnDamaged(WBH_DamageResult result)
    {
        bool isSelfAttack = ReferenceEquals(result.Attacker, controller);

        if (!isSelfAttack)
        {
            // 데미지 텍스트 처리
            WBH_DamageText damageText = poolManager.GetDamageText();

            damageText.Show(damageTextRoot.position, result);

            // 적 적색으로 깜빡임 처리
            PlayHitFlash();
        }

        // 엘리트 적일 경우 UI 갱신 !@ 차후 합일 시 수정
        if (!(result.Attacker is T_PlayerController))
            return;

        if (controller.Info.enemyGrade != EnemyGrade.Elite)
            return;

        highEnemyHpView?.BindElite(controller);
    }

    private void ShowCreditReward(int amount)
    {
        if (poolManager == null || amount <= 0)
            return;

        WBH_CreditText creditText = poolManager.GetCreditText();

        Vector3 basePosition = damageTextRoot != null ? damageTextRoot.position : transform.position + Vector3.up;

        Camera targetCam = mainCamera != null ? mainCamera : Camera.main;

        Vector3 offset = targetCam != null ? 
            targetCam.transform.right * creditTextOffset.x + targetCam.transform.up * creditTextOffset.y 
            : Vector3.right * creditTextOffset.x + Vector3.down * - creditTextOffset.y;

        creditText.Show(basePosition + offset, amount);
    }

    // 노말, 어드밴스드 적 hp 바 갱신
    private void UpdateHpBar(float currentHp, float maxHp)
    {
        if (hpBarSlider == null || hpBarRoot == null)
            return;

        hpBarSlider.value = currentHp / maxHp;

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
    public void SetSelfDestructFlash(bool visible,float transitionDuration)
    {
        float targetStrength = visible ? 1f : 0f;

        selfDestructFlashStartStrength = selfDestructFlashStrength;
        selfDestructFlashTargetStrength = targetStrength;

        selfDestructFlashTargetDuration = Mathf.Max(0.01f, transitionDuration);

        selfDestructFlashTargetElapsed = 0f;
        isSelfDestructFlashTransition = true;
    }

    public void SetSelfDestructFlash(bool visible)
    {
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
