using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WBH_HighEnemyHpbarView : MonoBehaviour
{
    [Header("HUD Animation")]
    [SerializeField] private KY_HUDAnimator eliteHudAni;
    [SerializeField] private KY_HUDAnimator bossHudAni;

    [Header("Elite")]
    [SerializeField] private GameObject hpBarRoot;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TMP_Text enemyNameText;
    [SerializeField] private TMP_Text hpText;

    [Header("Boss")]
    [SerializeField] private GameObject bossHpBarRoot;
    [SerializeField] private Slider bossHpSlider;
    [SerializeField] private TMP_Text bossEnemyNameText;
    [SerializeField] private TMP_Text bossHpText;

    [Header("Hide Condition")]
    [SerializeField] private Transform player;
    [SerializeField] private float hideDistance = 30f;

    [Header("Localization")]
    [Tooltip("적 이름 다국어 DB. 비워두면 Resources에서 공용 DB를 자동으로 찾아 쓴다.")]
    [SerializeField] private EnemyLabelDatabaseSO enemyLabels;

    private WBH_EnemyController eliteEnemy;
    private WBH_EnemyStatus eliteStatus;

    private WBH_EnemyController bossEnemy;
    private WBH_EnemyStatus bossStatus;

    private EnemyGrade targetGrade;
    private Transform externalEnemy;
    private Transform externalPlayer;
    private WBH_EnemyInfo externalInfo;
    private bool hasExternalBinding;
    private bool eliteBarVisible;
    private bool bossBarVisible;

    /// <summary>SW 수정: 외부 표시 어댑터도 원본과 같은 엘리트 체력바 숨김 거리를 사용한다.</summary>
    public float HideDistance => hideDistance;
    /// <summary>SW 수정: 외부 대상의 체력바가 현재 화면에 표시되는지 반환한다.</summary>
    public bool IsExternalVisible => hasExternalBinding &&
        ((eliteBarVisible && hpBarRoot != null && hpBarRoot.activeInHierarchy) ||
         (bossBarVisible && bossHpBarRoot != null && bossHpBarRoot.activeInHierarchy));

    private void Awake()
    {
        if (hpBarRoot != null)
        {
            hpBarRoot.SetActive(false);
        }
        if (bossHpBarRoot != null)
        {
            bossHpBarRoot.SetActive(false);
        }

        // 씬에서 직접 안 배선해도(다른 맵/스테이지 씬 등) Resources의 공용 DB를 자동으로 찾아 쓴다.
        if (enemyLabels == null)
            enemyLabels = Resources.Load<EnemyLabelDatabaseSO>("DataFiles/EnemyData/3. GeneratedAssets/LabelData/EnemyLabelDatabase");
    }

    // 엘리트의 경우 플레이어와 기존 타겟(엘리트)의 거리가 멀어지면 hp바 비활성화
    private void Update()
    {
        UpdateEliteVisibility();
        UpdateBossVisibility();
        UpdateExternalVisibility();
    }

    private void OnEnable()
    {
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        if (YJ_LanguageManager.Instance != null)
            YJ_LanguageManager.Instance.LanguageChanged -= HandleLanguageChanged;

        UnbindElite();
        UnbindBoss();
        ClearExternal();
    }

    // 언어가 바뀌면 현재 화면에 떠 있는 엘리트/보스 이름표를 다시 계산해서 즉시 반영한다.
    private void HandleLanguageChanged(GameLanguage _)
    {
        if (eliteEnemy != null && enemyNameText != null)
            enemyNameText.text = ResolveEnemyName(eliteEnemy.Info);

        if (bossEnemy != null && bossEnemyNameText != null)
            bossEnemyNameText.text = ResolveEnemyName(bossEnemy.Info);

        if (hasExternalBinding)
            UpdateExternalName();
    }

    /// <summary>언어 반응형 적 이름 DB가 있으면 그 값을, 없으면 스폰 시점에 저장된 이름을 그대로 반환한다.</summary>
    private string ResolveEnemyName(WBH_EnemyInfo info)
    {
        if (info == null)
            return string.Empty;

        return enemyLabels != null ? enemyLabels.GetName(info.enemyId) : info.enemyName;
    }

    public void Initialize(Transform localPlayer)
    {
        player = localPlayer;
    }

    /// <summary>SW 수정: 공용 HUD의 거리 판정에 사용할 로컬 플레이어를 연결한다.</summary>
    public void BindPlayer(Transform localPlayer) => player = localPlayer;

    /// <summary>SW 수정: 논리 표시 상태를 함께 갱신하여 다시 표시할 때만 등장 애니메이션을 재생한다.</summary>
    private static void ShowBar(GameObject barRoot, KY_HUDAnimator animator, ref bool isVisible)
    {
        if (barRoot == null)
            return;

        // SlideOut은 루트를 비활성화하지 않으므로 논리 표시 상태도 함께 확인한다.
        bool wasActive = isVisible && barRoot.activeSelf;
        isVisible = true;

        barRoot.SetActive(true);

        if (!wasActive)
        {
            animator?.SlideIn();
        }
    }

    /// <summary>SW 수정: 표시 중인 체력바만 한 번 숨기고 퇴장 애니메이션을 유지한다.</summary>
    private static void HideBar(GameObject barRoot, KY_HUDAnimator animator, ref bool isVisible)
    {
        if (!isVisible)
            return;

        isVisible = false;

        if (barRoot == null)
            return;

        if(animator != null && barRoot.activeInHierarchy)
        {
            animator.SlideOut();
            return;
        }
        barRoot.SetActive(false);
    }

    public void BindElite(WBH_EnemyController enemy)
    {
        if (enemy == null || enemy.Info == null || enemy.Info.enemyGrade != EnemyGrade.Elite || player == null)
            return;

        ClearExternal();

        if (eliteEnemy == enemy)
        {
            ShowBar(hpBarRoot, eliteHudAni, ref eliteBarVisible);
            UpdateEliteHp(eliteStatus.CurrentHp, eliteStatus.MaxHealth);
            return;
        }

        UnbindElite();

        eliteEnemy = enemy;
        eliteStatus = enemy.GetComponent<WBH_EnemyStatus>();

        if (eliteStatus == null)
        {
            UnbindElite();
            return;
        }

        eliteStatus.OnHpChanged += UpdateEliteHp;
        eliteStatus.OnDead += UnbindElite;

        if (enemyNameText != null)
            enemyNameText.text = ResolveEnemyName(enemy.Info);

        ShowBar(hpBarRoot, eliteHudAni, ref eliteBarVisible);
        UpdateEliteHp(eliteStatus.CurrentHp, eliteStatus.MaxHealth);
    }

    public void BindBoss(WBH_EnemyController enemy)
    {
        if (enemy == null || enemy.Info == null || enemy.Info.enemyGrade != EnemyGrade.Boss)
            return;

        ClearExternal();

        if(bossEnemy == enemy)
        {
            ShowBar(bossHpBarRoot, bossHudAni, ref bossBarVisible);
            UpdateBossHp(bossStatus.CurrentHp, bossStatus.MaxHealth);
            return;
        }

        UnbindBoss();

        bossEnemy = enemy;
        bossStatus = enemy.GetComponent<WBH_EnemyStatus>();

        if(bossStatus == null)
        {
            UnbindBoss();
            return;
        }

        bossStatus.OnHpChanged += UpdateBossHp;
        bossStatus.OnDead += UnbindBoss;

        if(bossEnemyNameText != null)
        {
            bossEnemyNameText.text = ResolveEnemyName(enemy.Info);
        }

        ShowBar(bossHpBarRoot, bossHudAni, ref bossBarVisible);
        UpdateBossHp(bossStatus.CurrentHp, bossStatus.MaxHealth);
    }

    /// <summary>SW 수정: 서버에서 전달된 적 상태를 기존 엘리트/보스 HUD로 표시한다.</summary>
    public void BindExternal(Transform enemy, WBH_EnemyInfo info, float currentHealth, float maxHealth, bool isBoss, Transform localPlayer)
    {
        if (enemy == null || info == null || !enemy.gameObject.activeInHierarchy ||
            currentHealth <= 0f || maxHealth <= 0f || (!isBoss && localPlayer == null))
        {
            ClearExternal();
            return;
        }

        EnemyGrade grade = isBoss ? EnemyGrade.Boss : EnemyGrade.Elite;
        if (!hasExternalBinding || externalEnemy != enemy || targetGrade != grade)
        {
            UnbindElite();
            UnbindBoss();
        }

        externalEnemy = enemy;
        externalPlayer = localPlayer;
        externalInfo = info;
        targetGrade = grade;
        hasExternalBinding = true;

        UpdateExternalHealth(currentHealth, maxHealth, false);
        if (!hasExternalBinding)
            return;

        UpdateExternalName();
        if (isBoss)
            ShowBar(bossHpBarRoot, bossHudAni, ref bossBarVisible);
        else
            ShowBar(hpBarRoot, eliteHudAni, ref eliteBarVisible);
    }

    /// <summary>SW 수정: 외부 체력을 갱신하고 사망하거나 유효한 대상이 사라지면 표시를 해제한다.</summary>
    public void UpdateExternalHealth(float currentHealth, float maxHealth, bool isDead)
    {
        if (!hasExternalBinding)
            return;

        if (isDead || currentHealth <= 0f || maxHealth <= 0f)
        {
            ClearExternal();
            return;
        }

        UpdateExternalVisibility();
        if (!hasExternalBinding)
            return;

        if (targetGrade == EnemyGrade.Boss)
            UpdateBossHp(currentHealth, maxHealth);
        else
            UpdateEliteHp(currentHealth, maxHealth);
    }

    /// <summary>SW 수정: 외부 대상 연결만 해제하여 기존 싱글 체력바 구독과 수명을 분리한다.</summary>
    public void ClearExternal()
    {
        if (!hasExternalBinding)
            return;

        hasExternalBinding = false;
        externalEnemy = null;
        externalPlayer = null;
        externalInfo = null;

        if (targetGrade == EnemyGrade.Boss)
            HideBar(bossHpBarRoot, bossHudAni, ref bossBarVisible);
        else
            HideBar(hpBarRoot, eliteHudAni, ref eliteBarVisible);
    }

    /// <summary>SW 수정: 외부 대상 이름도 원본 다국어 조회로 갱신한다.</summary>
    private void UpdateExternalName()
    {
        TMP_Text nameText = targetGrade == EnemyGrade.Boss ? bossEnemyNameText : enemyNameText;
        if (nameText != null)
            nameText.text = ResolveEnemyName(externalInfo);
    }

    private void UnbindElite()
    {
        if(eliteStatus != null)
        {
            eliteStatus.OnHpChanged -= UpdateEliteHp;
            eliteStatus.OnDead -= UnbindElite;
        }
        eliteEnemy = null;
        eliteStatus = null;

        HideBar(hpBarRoot, eliteHudAni, ref eliteBarVisible);
    }

    private void UnbindBoss()
    {
        if (bossStatus != null)
        {
            bossStatus.OnHpChanged -= UpdateBossHp;
            bossStatus.OnDead -= UnbindBoss;
        }
        bossEnemy = null;
        bossStatus = null;

        HideBar(bossHpBarRoot, bossHudAni, ref bossBarVisible);
    }

    private void UpdateEliteVisibility()
    {
        if (eliteEnemy == null)
            return;

        if(!eliteEnemy.gameObject.activeInHierarchy)
        {
            UnbindElite();
            return;
        }
        
        if (player == null)
            return;

        float sqrDistance = (player.position - eliteEnemy.transform.position).sqrMagnitude;

        if(sqrDistance > hideDistance * hideDistance)
        {
            UnbindElite();
        }
    }
    private void UpdateBossVisibility()
    {
        if (bossEnemy == null)
            return;
        if(!bossEnemy.gameObject.activeInHierarchy)
        {
            UnbindBoss();
        }
    }

    /// <summary>SW 수정: 외부 대상의 소멸과 로컬 플레이어의 거리 이탈을 기존 숨김 조건에 반영한다.</summary>
    private void UpdateExternalVisibility()
    {
        if (!hasExternalBinding)
            return;

        if (externalEnemy == null || !externalEnemy.gameObject.activeInHierarchy)
        {
            ClearExternal();
            return;
        }

        if (targetGrade != EnemyGrade.Elite)
            return;

        if (externalPlayer == null ||
            (externalPlayer.position - externalEnemy.position).sqrMagnitude > hideDistance * hideDistance)
        {
            ClearExternal();
        }
    }

    private void UpdateEliteHp(float currentHp, float maxHp)
    {
        if (maxHp <= 0f)
            return;

        if (hpSlider != null)
            hpSlider.value = currentHp / maxHp;

        if (hpText != null)
            hpText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";
    }
    private void UpdateBossHp(float currentHp, float maxHp)
    {
        if (maxHp <= 0f)
            return;

        if (bossHpSlider != null)
            bossHpSlider.value = currentHp / maxHp;

        if (bossHpText != null)
            bossHpText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";
    }
}
