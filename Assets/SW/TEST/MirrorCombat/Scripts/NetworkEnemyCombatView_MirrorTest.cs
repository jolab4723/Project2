using ItemSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mirror 테스트 적의 서버 확정 체력과 데미지를 기존 Act1 표시 자산으로 보여주는 클라이언트 전용 어댑터다.
/// <para>BH 원본 <c>WBH_EnemyView</c>는 로컬 <c>WBH_EnemyStatus</c> 이벤트와 초기화된 풀을 전제로 하므로
/// 네트워크 적에서는 끈 상태를 유지한다. 이 복제본은 <c>NetworkEnemyAuthority_MirrorTest</c>의 복제 결과만 읽는다.</para>
/// <para>체력바는 적 Prefab에 이미 있는 Slider를 재사용하고, 데미지 숫자는 씬에 하나인
/// <c>WBH_DamageTextPoolManager</c>를 지연 탐색해 모든 클라이언트에서 같은 서버 판정값을 표시한다.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkEnemyAuthority_MirrorTest))]
public sealed class NetworkEnemyCombatView_MirrorTest : MonoBehaviour
{
    [SerializeField] private NetworkEnemyAuthority_MirrorTest authority;
    [SerializeField] private Transform damageTextRoot;
    [SerializeField] private GameObject healthBarRoot;
    [SerializeField] private Slider healthBarSlider;

    private WBH_DamageTextPoolManager damageTextPool;
    private Camera mainCamera;
    private uint observedDamagePresentationCount;
    private bool initialized;
    private bool missingPoolReported;

    private void Awake()
    {
        ResolveReferences();
        if (healthBarRoot != null)
            healthBarRoot.SetActive(false);
    }

    private void Start()
    {
        observedDamagePresentationCount = authority != null
            ? authority.ReceivedDamagePresentationCount
            : 0;
        initialized = true;

        if (!Mirror.NetworkClient.active)
            return;

        RefreshHealthBar();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif

    private void LateUpdate()
    {
        if (!Mirror.NetworkClient.active)
            return;

        mainCamera ??= Camera.main;
        RefreshHealthBar();

        if (healthBarRoot != null && healthBarRoot.activeSelf && mainCamera != null)
            healthBarRoot.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);

        if (!initialized || authority == null ||
            observedDamagePresentationCount == authority.ReceivedDamagePresentationCount)
        {
            return;
        }

        observedDamagePresentationCount = authority.ReceivedDamagePresentationCount;
        ShowDamage(authority.LastDamage, authority.LastDamageCritical);
    }

    private void RefreshHealthBar()
    {
        if (authority == null || healthBarRoot == null || healthBarSlider == null)
            return;

        bool visible = !authority.IsDead && authority.MaxHealth > 0f;
        if (healthBarRoot.activeSelf != visible)
            healthBarRoot.SetActive(visible);

        healthBarSlider.minValue = 0f;
        healthBarSlider.maxValue = 1f;
        healthBarSlider.value = visible
            ? Mathf.Clamp01(authority.CurrentHealth / authority.MaxHealth)
            : 0f;
    }

    private void ShowDamage(float damage, bool critical)
    {
        if (damage <= 0f)
            return;

        damageTextPool ??=
            FindFirstObjectByType<WBH_DamageTextPoolManager>(FindObjectsInactive.Exclude);
        if (damageTextPool == null)
        {
            if (!missingPoolReported)
            {
                Debug.LogWarning(
                    "[NetworkEnemyCombatView_MirrorTest] 씬의 데미지 텍스트 풀을 찾지 못했습니다.",
                    this);
                missingPoolReported = true;
            }

            return;
        }

        WBH_DamageText damageText = damageTextPool.GetDamageText();
        Vector3 position = damageTextRoot != null
            ? damageTextRoot.position
            : transform.position + Vector3.up * 1.5f;
        damageText.Show(
            position,
            new WBH_DamageResult(null, damage, critical, ElementType.None));
    }

    private void ResolveReferences()
    {
        authority ??= GetComponent<NetworkEnemyAuthority_MirrorTest>();

        if (damageTextRoot == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "DamageTextRoot")
                    continue;

                damageTextRoot = child;
                break;
            }
        }

        healthBarSlider ??= GetComponentInChildren<Slider>(true);
        if (healthBarRoot == null && healthBarSlider != null)
        {
            Canvas canvas = healthBarSlider.GetComponentInParent<Canvas>(true);
            healthBarRoot = canvas != null ? canvas.gameObject : healthBarSlider.gameObject;
        }
    }
}
