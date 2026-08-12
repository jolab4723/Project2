using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WBH_EliteHpbarView : MonoBehaviour
{
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

    private WBH_EnemyController targetEnemy;
    private WBH_EnemyStatus targetStatus;

    private EnemyGrade targetGrade;

    private void Awake()
    {
        hpBarRoot.SetActive(false);
    }

    // 엘리트의 경우 플레이어와 기존 타겟(엘리트)의 거리가 멀어지면 hp바 비활성화
    private void Update()
    {
        if (targetEnemy == null)
            return;
        
        if(!targetEnemy.gameObject.activeInHierarchy)
        {
            Unbind();
            return;
        }

        if (targetGrade == EnemyGrade.Boss)
            return;

        if(targetGrade == EnemyGrade.Elite)
        {
            if (player == null)
                return;

            float sqrDistance = (player.position - targetEnemy.transform.position).sqrMagnitude;

            if(sqrDistance > hideDistance * hideDistance)
            {
                Unbind();
            }
        }
    }

    private void OnDisable()
    {
        Unbind();
    }

    public void Initialize(Transform localPlayer)
    {
        player = localPlayer;
    }

    public void Bind(WBH_EnemyController enemy)
    {
        if (enemy == null || enemy.Info == null)
            return;

        EnemyGrade grade = enemy.Info.enemyGrade;

        if (grade != EnemyGrade.Elite && grade != EnemyGrade.Boss)
            return;

        if(grade == EnemyGrade.Elite && player == null)
        {
            Log.Warning("EliteView 에 플레이어 초기화가 되지 않았습니다");
            return;
        }

        // 기존과 동일한 대상 공격 시
        if(targetEnemy == enemy)
        {
            hpBarRoot.SetActive(true);
            UpdateHp(targetStatus.CurrentHp, targetStatus.MaxHealth);
            return;
        }

        // 다른 엘리트 공격 시
        Unbind();

        targetEnemy = enemy;
        targetGrade = grade;
        targetStatus = enemy.GetComponent<WBH_EnemyStatus>();

        if(targetStatus == null)
        {
            Unbind();
            return;
        }

        targetStatus.OnHpChanged += UpdateHp;
        targetStatus.OnDead += HandleTargetDead;

        enemyNameText.text = enemy.Info.enemyName;
        hpBarRoot.SetActive(true);

        UpdateHp(targetStatus.CurrentHp, targetStatus.MaxHealth);
    }

    private void Unbind()
    {
        if(targetStatus != null)
        {
            targetStatus.OnHpChanged -= UpdateHp;
            targetStatus.OnDead -= HandleTargetDead;
        }

        targetEnemy = null;
        targetStatus = null;

        if (hpBarRoot != null)
            hpBarRoot.SetActive(false);
    }
    private void HandleTargetDead()
    {
        Unbind();
    }
    private void UpdateHp(float currentHp, float maxHp)
    {
        if (maxHp <= 0f)
            return;

        hpSlider.value = currentHp / maxHp;

        if (hpText != null)
            hpText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";
    }
}
