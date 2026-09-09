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

    private void Awake()
    {
        if(hpBarRoot != null)
        {
            hpBarRoot.SetActive(false);
        }
        if(bossHpBarRoot != null)
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
    }

    // 언어가 바뀌면 현재 화면에 떠 있는 엘리트/보스 이름표를 다시 계산해서 즉시 반영한다.
    private void HandleLanguageChanged(GameLanguage _)
    {
        if (eliteEnemy != null && enemyNameText != null)
            enemyNameText.text = ResolveEnemyName(eliteEnemy.Info);

        if (bossEnemy != null && bossEnemyNameText != null)
            bossEnemyNameText.text = ResolveEnemyName(bossEnemy.Info);
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

    private static void ShowBar(GameObject barRoot, KY_HUDAnimator animator)
    {
        if (barRoot == null)
            return;

        bool wasActive = barRoot.activeSelf;

        barRoot.SetActive(true);

        if(wasActive)
        {
            animator?.SlideIn();
        }
    }

    private static void HideBar(GameObject barRoot, KY_HUDAnimator animator)
    {
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

        if (eliteEnemy == enemy)
        {
            ShowBar(hpBarRoot, eliteHudAni);
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

        enemyNameText.text = ResolveEnemyName(enemy.Info);
        ShowBar(hpBarRoot, eliteHudAni);
        UpdateEliteHp(eliteStatus.CurrentHp, eliteStatus.MaxHealth);
    }

    public void BindBoss(WBH_EnemyController enemy)
    {
        if (enemy == null || enemy.Info == null || enemy.Info.enemyGrade != EnemyGrade.Boss)
            return;

        if(bossEnemy == enemy)
        {
            ShowBar(bossHpBarRoot, bossHudAni);
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

        ShowBar(bossHpBarRoot, bossHudAni);
        UpdateBossHp(bossStatus.CurrentHp, bossStatus.MaxHealth);
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

        HideBar(hpBarRoot, eliteHudAni);
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

        HideBar(bossHpBarRoot, bossHudAni);
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

    private void UpdateEliteHp(float currentHp, float maxHp)
    {
        if (maxHp <= 0f)
            return;

        hpSlider.value = currentHp / maxHp;

        if (hpText != null)
            hpText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";
    }
    private void UpdateBossHp(float currentHp, float maxHp)
    {
        if (maxHp <= 0f)
            return;

        bossHpSlider.value = currentHp / maxHp;

        if (bossHpText != null)
            bossHpText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";
    }
}
