using EnemySystem;
using UnityEngine;

/// <summary>
/// WBH 보스/엘리트 체력바 Prefab을 Mirror 서버가 확정한 체력 상태에 연결하는 클라이언트 전용 어댑터다.
/// 표시는 연결된 <see cref="WBH_HighEnemyHpbarView"/>의 외부 입력 API로 전달한다.
/// 보스 씬에서는 보스 체력바만, 엘리트 씬에서는 엘리트 체력바만 상호 배타적으로 노출하여 중첩을 원천 방지한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkBossHealthBar : MonoBehaviour
{
    private const float SearchInterval = 0.25f;

    [SerializeField] private WBH_HighEnemyHpbarView productionView;
    [SerializeField] private NetworkEnemyWaveSpawner waveSpawner;

    private NetworkEnemyAuthority boundEnemy;
    private Transform boundPlayer;
    private bool ownsExternalBinding;
    private float nextSearchAt;
    private float displayedHealth = float.NaN;
    private float displayedMaxHealth = float.NaN;

    public NetworkEnemyAuthority BoundBoss => (boundEnemy != null && IsBoss(boundEnemy)) ? boundEnemy : null;
    public NetworkEnemyAuthority BoundElite => (boundEnemy != null && IsElite(boundEnemy)) ? boundEnemy : null;
    public bool IsVisible => ownsExternalBinding && productionView != null && productionView.IsExternalVisible;

    private void OnDisable() => ClearEnemy();

    private void Update()
    {
        if (!Mirror.NetworkClient.active || productionView == null || !productionView.isActiveAndEnabled ||
            waveSpawner == null || !waveSpawner.isActiveAndEnabled)
        {
            ClearEnemy();
            return;
        }

        PlayerContext localContext = (Mirror.NetworkManager.singleton as MirrorNetworkManager)?.LocalPlayerContext;
        if (localContext == null || !localContext.gameObject.activeInHierarchy)
        {
            ClearEnemy();
            return;
        }

        Transform localPlayer = localContext.transform;
        if (boundPlayer != localPlayer || !IsValidTarget(boundEnemy))
            ClearEnemy();

        if (boundEnemy == null)
        {
            if (Time.unscaledTime < nextSearchAt)
                return;

            nextSearchAt = Time.unscaledTime + SearchInterval;
            BindEnemy(FindTargetEnemy(localPlayer), localPlayer);
            return;
        }

        RefreshDisplay();
    }

    private void BindEnemy(NetworkEnemyAuthority enemy, Transform localPlayer)
    {
        if (boundEnemy == enemy && boundPlayer == localPlayer)
            return;

        ClearEnemy();
        if (enemy == null)
            return;

        boundEnemy = enemy;
        boundPlayer = localPlayer;
        RefreshDisplay();
    }

    private void ClearEnemy()
    {
        // 네트워크에서 연결했던 View만 한 번 해제하여 싱글 체력바의 바인딩을 건드리지 않는다.
        if (ownsExternalBinding && productionView != null)
            productionView.ClearExternal();

        ownsExternalBinding = false;
        boundEnemy = null;
        boundPlayer = null;
        displayedHealth = float.NaN;
        displayedMaxHealth = float.NaN;
    }

    private void RefreshDisplay()
    {
        if (boundEnemy == null)
            return;

        float currentHealth = boundEnemy.CurrentHealth;
        float maxHealth = boundEnemy.MaxHealth;
        if (currentHealth == displayedHealth && maxHealth == displayedMaxHealth)
        {
            return;
        }

        displayedHealth = currentHealth;
        displayedMaxHealth = maxHealth;
        bool isBoss = IsBoss(boundEnemy);
        if (!isBoss && !IsWithinEliteDistance(boundEnemy, boundPlayer))
            return;

        // 공통 View가 거리 이탈로 숨긴 뒤에는 같은 체력으로 다시 열지 않는다.
        // 대상과 체력 캐시는 유지하고, 거리 내에서 체력이 실제로 바뀔 때만 재연결한다.
        if (!ownsExternalBinding || !productionView.IsExternalVisible)
        {
            productionView.BindExternal(
                boundEnemy.transform, boundEnemy.EnemyInfo, currentHealth, maxHealth, isBoss, boundPlayer);
            ownsExternalBinding = true;
            return;
        }

        productionView.UpdateExternalHealth(currentHealth, maxHealth, boundEnemy.IsDead);
    }

    private bool IsValidTarget(NetworkEnemyAuthority enemy)
    {
        if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDead ||
            enemy.CurrentHealth <= 0f || enemy.MaxHealth <= 0f)
            return false;

        return waveSpawner.IsBossSession
            ? IsBoss(enemy)
            : !IsBoss(enemy) && IsElite(enemy) && enemy.CurrentHealth < enemy.MaxHealth;
    }

    private bool IsWithinEliteDistance(NetworkEnemyAuthority enemy, Transform localPlayer)
    {
        return localPlayer != null &&
               (localPlayer.position - enemy.transform.position).sqrMagnitude <=
               productionView.HideDistance * productionView.HideDistance;
    }

    private NetworkEnemyAuthority FindTargetEnemy(Transform localPlayer)
    {
        NetworkEnemyAuthority[] enemies =
            FindObjectsByType<NetworkEnemyAuthority>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        if (waveSpawner.IsBossSession)
        {
            // 보스 씬: 오직 보스만 탐색 (엘리트는 절대 탐색/노출하지 않음)
            foreach (var enemy in enemies)
            {
                if (IsValidTarget(enemy))
                    return enemy;
            }
            return null;
        }
        else
        {
            // 엘리트/일반 씬: 엘리트 몬스터만 탐색
            foreach (var enemy in enemies)
            {
                if (IsValidTarget(enemy) && IsWithinEliteDistance(enemy, localPlayer))
                    return enemy;
            }
            return null;
        }
    }

    private static bool IsBoss(NetworkEnemyAuthority enemy)
    {
        return enemy != null && (enemy.EnemyInfo?.enemyAttackType == EnemyAttackType.Boss || enemy.EnemyInfo?.enemyGrade == EnemyGrade.Boss);
    }

    private static bool IsElite(NetworkEnemyAuthority enemy)
    {
        return enemy != null && enemy.EnemyInfo?.enemyGrade == EnemyGrade.Elite;
    }
}
