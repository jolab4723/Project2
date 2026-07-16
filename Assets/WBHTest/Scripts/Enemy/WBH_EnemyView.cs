using UnityEngine;

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

    private WBH_DamageTextPoolManager poolManager;
    private WBH_EnemyStatus status;


    private void Start()
    {
        status = GetComponent<WBH_EnemyStatus>();
    }

    private void OnEnable()
    {
        status.OnDamaged += ShowDamageText;
    }

    private void OnDisable()
    {
        status.OnDamaged -= ShowDamageText;
    }

    public void Initialize(WBH_DamageTextPoolManager poolManager)
    {
        this.poolManager = poolManager;
    }

    // 데미지 텍스트 처리
    private void ShowDamageText(WBH_DamageResult result)
    {
        WBH_DamageText damageText = poolManager.GetDamageText();

        damageText.Show(damageTextRoot.position, result);
    }
}
