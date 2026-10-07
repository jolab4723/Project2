using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectMask2D))]
public class YJ_MinimapEnemy : MonoBehaviour
{
    private const float OutsideIconScale = 0.5f;
    private const float IconScaleTransitionDuration = 0.1f;

    [Header("추적 대상")]
    [SerializeField] private Transform player;
    private bool usesExternalPlayer; // SW 수정

    [Header("등급별 아이콘")]
    [SerializeField] private Sprite normalIconSprite;
    [SerializeField] private Sprite advancedIconSprite;
    [SerializeField] private Sprite eliteIconSprite;
    [SerializeField] private Sprite bossIconSprite;
    [SerializeField] private Sprite hiddenIconSprite;

    [Header("미니맵 범위")]
    [Min(0.01f)]
    [SerializeField] private float worldRadius = 25f;
    [Min(0.05f)]
    [SerializeField] private float searchInterval = 0.25f;
    [Min(0f)]
    [SerializeField] private float edgePadding = 2f;

    private readonly Dictionary<WBH_EnemyController, Image> enemyIcons = new();
    private readonly Dictionary<WBH_EnemyController, IconScaleTransition> iconScaleTransitions = new();
    private readonly HashSet<WBH_EnemyController> detectedEnemies = new();
    private readonly List<WBH_EnemyController> iconsToRemove = new();
    private readonly Stack<Image> iconPool = new();

    private RectTransform iconArea;
    private float searchTimer;
    private float playerSearchTimer;

    private void Awake()
    {
        iconArea = (RectTransform)transform;

        if (!TryGetComponent(out RectMask2D _))
        {
            gameObject.AddComponent<RectMask2D>();
        }
    }

    private void OnEnable()
    {
        searchTimer = 0f;
        FindPlayer();
    }

    private void OnDisable()
    {
        ReleaseAllIcons();
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            if (enemyIcons.Count > 0)
            {
                ReleaseAllIcons();
            }

            playerSearchTimer -= Time.unscaledDeltaTime;
            if (playerSearchTimer <= 0f)
            {
                FindPlayer();
            }

            return;
        }

        searchTimer -= Time.unscaledDeltaTime;
        if (searchTimer <= 0f)
        {
            RefreshVisibleEnemies();
            searchTimer = searchInterval;
        }

        UpdateIconPositions();
    }

    /// <summary>SW 수정: 멀티에서는 원격 플레이어가 아닌 로컬 플레이어를 미니맵 중심으로 연결한다.</summary>
    public void BindPlayer(Transform owner)
    {
        usesExternalPlayer = true;
        player = owner;
    }

    private void FindPlayer()
    {
        playerSearchTimer = 1f;

        if (player != null || usesExternalPlayer)
            return;

        T_PlayerController playerController = FindFirstObjectByType<T_PlayerController>();
        if (playerController != null)
        {
            player = playerController.transform;
            searchTimer = 0f;
        }
    }

    private void RefreshVisibleEnemies()
    {
        detectedEnemies.Clear();

        WBH_EnemyController[] enemies = FindObjectsByType<WBH_EnemyController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (WBH_EnemyController enemy in enemies)
        {
            if (enemy == null || enemy.Info == null || enemy.Status == null || enemy.Status.IsDead)
                continue;

            detectedEnemies.Add(enemy);

            if (enemyIcons.TryGetValue(enemy, out Image icon))
            {
                SetIconSprite(icon, GetEnemyIconSprite(enemy.Info.enemyGrade));
            }
            else
            {
                enemyIcons.Add(enemy, GetIcon(GetEnemyIconSprite(enemy.Info.enemyGrade)));
                iconScaleTransitions.Add(enemy, new IconScaleTransition());
            }
        }

        iconsToRemove.Clear();
        foreach (KeyValuePair<WBH_EnemyController, Image> pair in enemyIcons)
        {
            if (pair.Key == null || !detectedEnemies.Contains(pair.Key))
            {
                iconsToRemove.Add(pair.Key);
            }
        }

        foreach (WBH_EnemyController enemy in iconsToRemove)
        {
            Image icon = enemyIcons[enemy];
            enemyIcons.Remove(enemy);
            iconScaleTransitions.Remove(enemy);
            ReleaseIcon(icon);
        }
    }

    private void UpdateIconPositions()
    {
        Vector3 playerPosition = player.position;

        foreach (KeyValuePair<WBH_EnemyController, Image> pair in enemyIcons)
        {
            WBH_EnemyController enemy = pair.Key;
            if (enemy == null)
                continue;

            Vector3 offset = enemy.transform.position - playerPosition;
            RectTransform iconRect = pair.Value.rectTransform;
            iconRect.anchoredPosition = GetMinimapPosition(offset, out bool isOutside);
            UpdateIconScale(enemy, iconRect, isOutside ? OutsideIconScale : 1f);
        }

    }

    private void UpdateIconScale(
        WBH_EnemyController enemy,
        RectTransform iconRect,
        float targetScale)
    {
        if (!iconScaleTransitions.TryGetValue(enemy, out IconScaleTransition transition))
        {
            transition = new IconScaleTransition();
            iconScaleTransitions.Add(enemy, transition);
        }

        if (!Mathf.Approximately(transition.TargetScale, targetScale))
        {
            transition.StartScale = iconRect.localScale.x;
            transition.TargetScale = targetScale;
            transition.ElapsedTime = 0f;
        }

        transition.ElapsedTime = Mathf.Min(
            transition.ElapsedTime + Time.unscaledDeltaTime,
            IconScaleTransitionDuration);

        float progress = transition.ElapsedTime / IconScaleTransitionDuration;
        float scale = Mathf.Lerp(
            transition.StartScale,
            transition.TargetScale,
            progress);
        iconRect.localScale = Vector3.one * scale;
    }

    private Vector2 GetMinimapPosition(Vector3 worldOffset, out bool isOutside)
    {
        float halfWidth = Mathf.Max(0f, iconArea.rect.width * 0.5f - edgePadding);
        float halfHeight = Mathf.Max(0f, iconArea.rect.height * 0.5f - edgePadding);
        Vector2 normalizedPosition = new Vector2(
            worldOffset.x / worldRadius,
            worldOffset.z / worldRadius);

        float largestAxis = Mathf.Max(
            Mathf.Abs(normalizedPosition.x),
            Mathf.Abs(normalizedPosition.y));
        isOutside = largestAxis > 1f;

        if (isOutside)
            normalizedPosition /= largestAxis;

        return new Vector2(
            normalizedPosition.x * halfWidth,
            normalizedPosition.y * halfHeight);
    }

    private Image GetIcon(Sprite sprite)
    {
        Image icon;

        if (iconPool.Count > 0)
        {
            icon = iconPool.Pop();
        }
        else
        {
            GameObject iconObject = new GameObject("Enemy Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(iconArea, false);
            icon = iconObject.GetComponent<Image>();
        }

        icon.gameObject.name = "Enemy Icon";
        SetIconSprite(icon, sprite);

        RectTransform iconRect = icon.rectTransform;
        iconRect.SetParent(iconArea, false);
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.localScale = Vector3.one;
        icon.raycastTarget = false;
        icon.gameObject.SetActive(true);

        return icon;
    }

    private Sprite GetEnemyIconSprite(EnemyGrade grade)
    {
        return grade switch
        {
            EnemyGrade.Normal => normalIconSprite,
            EnemyGrade.Advanced => advancedIconSprite,
            EnemyGrade.Elite => eliteIconSprite,
            EnemyGrade.Boss => bossIconSprite,
            EnemyGrade.Hidden => hiddenIconSprite,
            _ => null
        };
    }

    private void SetIconSprite(Image icon, Sprite sprite)
    {
        icon.sprite = sprite;

        icon.color = Color.white;
        icon.enabled = icon.sprite != null;

        if (icon.sprite != null)
        {
            icon.SetNativeSize();
        }
    }

    private void ReleaseIcon(Image icon)
    {
        if (icon == null)
            return;

        icon.gameObject.SetActive(false);
        iconPool.Push(icon);
    }

    private void ReleaseAllIcons()
    {
        foreach (Image icon in enemyIcons.Values)
        {
            ReleaseIcon(icon);
        }

        enemyIcons.Clear();
        iconScaleTransitions.Clear();
        detectedEnemies.Clear();
        iconsToRemove.Clear();
    }

    private sealed class IconScaleTransition
    {
        public float StartScale = 1f;
        public float TargetScale = 1f;
        public float ElapsedTime = IconScaleTransitionDuration;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        worldRadius = Mathf.Max(0.01f, worldRadius);
        searchInterval = Mathf.Max(0.05f, searchInterval);
    }
#endif
}
