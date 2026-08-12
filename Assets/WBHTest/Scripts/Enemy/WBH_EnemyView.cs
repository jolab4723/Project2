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

    private WBH_DamageTextPoolManager poolManager;
    private WBH_EnemyStatus status;
    private WBH_EnemyController controller;
    private WBH_HighEnemyHpbarView eliteView; // !@ 차후 UI 와 합일 필요
    private Camera mainCamera;

    private Coroutine hideHpBarCoroutine;
    private Coroutine hitFlashCoroutine;


    private void Awake()
    {
        status = GetComponent<WBH_EnemyStatus>();
        controller = GetComponent<WBH_EnemyController>();
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
    }

    private void OnDisable()
    {
        status.OnDamaged -= ViewOnDamaged;
        status.OnHpChanged -= UpdateHpBar;
    }

    // 메인카메라를 바라보는 코드
    private void LateUpdate()
    {
        if (hpBarRoot == null || !hpBarRoot.activeSelf || mainCamera == null)
            return;

        hpBarRoot.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);
    }

    public void Initialize(WBH_DamageTextPoolManager poolManager, WBH_HighEnemyHpbarView eliteView)
    {
        this.poolManager = poolManager;
        this.eliteView = eliteView;
    }

    // 피격 시 보여주는 처리
    private void ViewOnDamaged(WBH_DamageResult result)
    {
        // 데미지 텍스트 처리
        WBH_DamageText damageText = poolManager.GetDamageText();

        damageText.Show(damageTextRoot.position, result);

        // 적 적색으로 깜빡임 처리
        PlayHitFlash();

        // 엘리트 적일 경우 UI 갱신 !@ 차후 합일 시 수정
        if (!(result.Attacker is T_PlayerController))
            return;

        if (controller.Info.enemyGrade != EnemyGrade.Elite)
            return;

        eliteView?.BindElite(controller);
    }

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

    private void PlayHitFlash()
    {
        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);

        hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
    }


    private IEnumerator HitFlashRoutine()
    {
        SetHitStrength(hitFlashIntensity);

        yield return new WaitForSeconds(hitFlashDuration);

        SetHitStrength(0f);

        hitFlashCoroutine = null;
    }

    private void SetHitStrength(float value)
    {
        foreach(Renderer renderer in renderers)
        {
            renderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetFloat(HitStrengthID, value);

            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
