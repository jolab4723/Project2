using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(DestructionDamageStrengthScaler))]
public sealed class EnemyDestructionService : MonoBehaviour
{
    private const float WarmupDepth = 10000f;

    [Serializable]
    private sealed class PoolEntry
    {
        [SerializeField, InspectorName("파괴 연출 프리팹")]
        public GameObject VisualPrefab;

        [SerializeField, Min(0), InspectorName("미리 준비할 수량")]
        public int PrewarmCount = 4;

        [SerializeField, Min(0), InspectorName("최대 풀 크기")]
        public int MaxPoolSize = 12;
    }

    private sealed class PoolState
    {
        internal readonly PoolEntry Config;
        internal readonly Queue<PoolItem> Ready = new Queue<PoolItem>();
        internal readonly List<PoolItem> Items = new List<PoolItem>();
        internal bool CreateInProgress;
        internal bool ExhaustionWarned;

        internal PoolState(PoolEntry config)
        {
            Config = config;
        }
    }

    private sealed class PoolItem
    {
        internal EnemyDestructionVisual Visual;
        internal Action<EnemyDestructionVisual> CompletedHandler;
        internal bool InUse;
    }

    private static readonly List<EnemyDestructionService> ActiveServices =
        new List<EnemyDestructionService>();

    [SerializeField, InspectorName("파괴 연출 풀 목록")]
    private PoolEntry[] poolEntries = Array.Empty<PoolEntry>();

    private DestructionDamageStrengthScaler damageStrengthScaler;

    private readonly Dictionary<GameObject, PoolState> statesByPrefab =
        new Dictionary<GameObject, PoolState>();
    private readonly HashSet<GameObject> unregisteredWarnings =
        new HashSet<GameObject>();

    internal static bool TryGet(
        Scene scene,
        out EnemyDestructionService service)
    {
        service = null;
        if (!scene.IsValid())
        {
            return false;
        }

        for (int i = ActiveServices.Count - 1; i >= 0; i--)
        {
            EnemyDestructionService candidate = ActiveServices[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                ActiveServices.RemoveAt(i);
                continue;
            }

            if (candidate.gameObject.scene.handle != scene.handle)
            {
                continue;
            }

            if (service != null)
            {
                service = null;
                return false;
            }

            service = candidate;
        }

        return service != null;
    }

    internal bool TryPlay(
        GameObject visualPrefab,
        float baseDirectionalForce,
        EnemyDestructionRequest request)
    {
        if (!statesByPrefab.TryGetValue(
                visualPrefab,
                out PoolState primary))
        {
            WarnUnregistered(visualPrefab);
            return false;
        }

        PoolItem item = Rent(primary);
        if (item == null)
        {
            TryStartExpansion(primary);
            WarnExhausted(primary);
            return false;
        }

        Transform visualTransform = item.Visual.transform;
        visualTransform.SetParent(null, false);
        visualTransform.SetPositionAndRotation(
            request.Position,
            request.Rotation);
        // 부모가 없으므로 localScale에 world scale 절대값을 넣을 수 있다.
        visualTransform.localScale = request.WorldScale;

        float forceMultiplier = damageStrengthScaler != null
            ? damageStrengthScaler.EvaluateMultiplier(
                request.KillingDamage,
                request.MaxHealth)
            : 1f;

        item.Visual.Play(
            request.Position,
            request.Rotation,
            request.ImpactPoint,
            request.AttackDirection,
            Mathf.Max(0f, baseDirectionalForce),
            forceMultiplier);

        primary.ExhaustionWarned = false;
        return true;
    }

    private void Awake()
    {
        damageStrengthScaler = GetComponent<DestructionDamageStrengthScaler>();
    }

    private void OnEnable()
    {
        RegisterService();
        if (!Application.isPlaying)
        {
            return;
        }

        InitializeRuntimePools();
        StartCoroutine(InitializeLinkedPools());
    }

    private IEnumerator InitializeLinkedPools()
    {
        // 다른 컴포넌트의 Awake에서 생성되는 WBH 적 풀까지 준비된 뒤 연결 프리팹을 찾는다.
        yield return null;
        RegisterLinkedVisualPrefabs();
        yield return PrewarmAll();
    }

    private void RegisterLinkedVisualPrefabs()
    {
        EnemyDestructionLink[] links =
            FindObjectsByType<EnemyDestructionLink>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < links.Length; i++)
        {
            EnemyDestructionLink link = links[i];
            if (link == null ||
                link.gameObject.scene.handle != gameObject.scene.handle ||
                link.DestructionVisualPrefab == null ||
                statesByPrefab.ContainsKey(link.DestructionVisualPrefab))
            {
                continue;
            }

            var entry = new PoolEntry
            {
                VisualPrefab = link.DestructionVisualPrefab
            };
            statesByPrefab.Add(entry.VisualPrefab, new PoolState(entry));
        }
    }

    public void ReturnAllActive()
    {
        foreach (PoolState state in statesByPrefab.Values)
        {
            for (int i = 0; i < state.Items.Count; i++)
            {
                PoolItem item = state.Items[i];
                if (item != null && item.Visual != null && item.InUse)
                {
                    HandleCompleted(state, item);
                }
            }
        }
    }

    private void OnDisable()
    {
        UnregisterService();
        CleanupRuntimePools();
    }

    private void RegisterService()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid())
        {
            return;
        }

        ActiveServices.Remove(this);
        ActiveServices.Add(this);

        int sameSceneCount = 0;
        for (int i = 0; i < ActiveServices.Count; i++)
        {
            EnemyDestructionService service = ActiveServices[i];
            if (service != null &&
                service.isActiveAndEnabled &&
                service.gameObject.scene.handle == scene.handle)
            {
                sameSceneCount++;
            }
        }

        if (sameSceneCount > 1)
        {
            Debug.LogError(
                $"[{nameof(EnemyDestructionService)}] {scene.name}: " +
                "같은 씬에 활성 서비스가 여러 개라 파괴 연출 요청을 중단합니다.",
                this);
        }
    }

    private void UnregisterService()
    {
        ActiveServices.Remove(this);
    }

    private void InitializeRuntimePools()
    {
        statesByPrefab.Clear();
        unregisteredWarnings.Clear();

        if (poolEntries == null)
        {
            return;
        }

        for (int i = 0; i < poolEntries.Length; i++)
        {
            PoolEntry entry = poolEntries[i];
            if (entry == null || entry.VisualPrefab == null)
            {
                continue;
            }

            if (statesByPrefab.ContainsKey(entry.VisualPrefab))
            {
                Debug.LogError(
                    $"[{nameof(EnemyDestructionService)}] " +
                    $"중복된 파괴 연출 프리팹 설정을 무시합니다: " +
                    entry.VisualPrefab.name,
                    this);
                continue;
            }

            statesByPrefab.Add(entry.VisualPrefab, new PoolState(entry));
        }
    }

    private IEnumerator PrewarmAll()
    {
        foreach (PoolState state in statesByPrefab.Values)
        {
            int count = Mathf.Min(
                state.Config.PrewarmCount,
                state.Config.MaxPoolSize);
            if (count <= 0)
            {
                continue;
            }

            state.CreateInProgress = true;
            yield return WarmNewItems(state, count);
            state.CreateInProgress = false;
        }
    }

    private IEnumerator WarmNewItems(PoolState state, int count)
    {
        var warming = new List<PoolItem>(count);
        Vector3 warmupPosition =
            transform.position + Vector3.down * WarmupDepth;

        for (int i = 0; i < count; i++)
        {
            PoolItem item = CreateTrackedItem(state, warmupPosition);
            if (item == null)
            {
                continue;
            }

            item.Visual.gameObject.SetActive(true);
            warming.Add(item);
        }

        if (warming.Count == 0)
        {
            yield break;
        }

        // EnemyDestructionVisual.Start와 Artificer.Start를 완료한다.
        yield return null;
        yield return null;

        for (int i = warming.Count - 1; i >= 0; i--)
        {
            PoolItem item = warming[i];
            if (item.Visual == null || !item.Visual.IsStartupCompleted)
            {
                Debug.LogError(
                    $"[{nameof(EnemyDestructionService)}] " +
                    "두 프레임 안에 준비되지 않은 파괴 연출을 제거합니다.",
                    this);
                RemoveAndDestroy(state, item);
                warming.RemoveAt(i);
                continue;
            }

            item.Visual.Play(
                warmupPosition,
                Quaternion.identity,
                warmupPosition,
                Vector3.forward,
                0f);
        }

        // 첫 실전 사망 프레임에 렌더 캐시를 만들지 않도록 실제 경로를 1회 실행한다.
        yield return null;

        for (int i = 0; i < warming.Count; i++)
        {
            PoolItem item = warming[i];
            if (item.Visual == null)
            {
                continue;
            }

            item.Visual.ReturnToPoolNow();
            item.Visual.transform.SetParent(transform, false);
            SubscribeCompleted(state, item);
            state.Ready.Enqueue(item);
        }
    }

    private PoolItem CreateTrackedItem(
        PoolState state,
        Vector3 warmupPosition)
    {
        GameObject instance = Instantiate(
            state.Config.VisualPrefab,
            transform,
            false);
        instance.SetActive(false);
        instance.transform.position = warmupPosition;

        EnemyDestructionVisual visual =
            instance.GetComponent<EnemyDestructionVisual>();
        if (visual == null)
        {
            Debug.LogError(
                $"[{nameof(EnemyDestructionService)}] " +
                "프리팹 루트에 EnemyDestructionVisual이 없습니다: " +
                state.Config.VisualPrefab.name,
                state.Config.VisualPrefab);
            Destroy(instance);
            return null;
        }

        var item = new PoolItem { Visual = visual };
        state.Items.Add(item);
        return item;
    }

    private void SubscribeCompleted(PoolState state, PoolItem item)
    {
        if (item.CompletedHandler != null)
        {
            return;
        }

        item.CompletedHandler =
            _ => HandleCompleted(state, item);
        item.Visual.Completed += item.CompletedHandler;
    }

    private void HandleCompleted(PoolState state, PoolItem item)
    {
        if (item == null || item.Visual == null || !item.InUse)
        {
            return;
        }

        // ReturnToPoolNow가 다른 상태 변화를 일으켜도 재진입하지 않게 먼저 표시한다.
        item.InUse = false;
        item.Visual.ReturnToPoolNow();
        item.Visual.transform.SetParent(transform, false);

        if (!isActiveAndEnabled)
        {
            return;
        }

        state.Ready.Enqueue(item);
        state.ExhaustionWarned = false;
    }

    private PoolItem Rent(PoolState state)
    {
        while (state.Ready.Count > 0)
        {
            PoolItem item = state.Ready.Dequeue();
            if (item == null || item.Visual == null)
            {
                continue;
            }

            if (!item.InUse && item.Visual.IsStartupCompleted)
            {
                item.InUse = true;
                return item;
            }

            RemoveAndDestroy(state, item);
        }

        return null;
    }

    private void TryStartExpansion(PoolState state)
    {
        if (state.CreateInProgress ||
            state.Items.Count >= state.Config.MaxPoolSize)
        {
            return;
        }

        state.CreateInProgress = true;
        StartCoroutine(ExpandOne(state));
    }

    private IEnumerator ExpandOne(PoolState state)
    {
        yield return WarmNewItems(state, 1);
        state.CreateInProgress = false;
    }

    private void RemoveAndDestroy(PoolState state, PoolItem item)
    {
        if (item == null)
        {
            return;
        }

        state.Items.Remove(item);
        item.InUse = false;

        if (item.Visual == null)
        {
            return;
        }

        if (item.CompletedHandler != null)
        {
            item.Visual.Completed -= item.CompletedHandler;
        }

        item.Visual.ReturnToPoolNow();
        Destroy(item.Visual.gameObject);
    }

    private void CleanupRuntimePools()
    {
        StopAllCoroutines();

        foreach (PoolState state in statesByPrefab.Values)
        {
            state.CreateInProgress = false;
            for (int i = 0; i < state.Items.Count; i++)
            {
                PoolItem item = state.Items[i];
                if (item == null || item.Visual == null)
                {
                    continue;
                }

                if (item.CompletedHandler != null)
                {
                    item.Visual.Completed -= item.CompletedHandler;
                }

                item.InUse = false;
                item.Visual.ReturnToPoolNow();
                Destroy(item.Visual.gameObject);
            }

            state.Ready.Clear();
            state.Items.Clear();
        }

        statesByPrefab.Clear();
        unregisteredWarnings.Clear();
    }

    private void WarnUnregistered(GameObject visualPrefab)
    {
        if (visualPrefab == null || !unregisteredWarnings.Add(visualPrefab))
        {
            return;
        }

        Debug.LogWarning(
            $"[{nameof(EnemyDestructionService)}] " +
            "풀 목록에 등록되지 않은 파괴 연출이라 이번 요청을 생략합니다: " +
            visualPrefab.name,
            this);
    }

    private void WarnExhausted(PoolState state)
    {
        if (state.ExhaustionWarned)
        {
            return;
        }

        state.ExhaustionWarned = true;
        Debug.LogWarning(
            $"[{nameof(EnemyDestructionService)}] " +
            "준비된 파괴 연출이 없어 이번 요청을 생략했습니다: " +
            state.Config.VisualPrefab.name,
            this);
    }

    private void OnValidate()
    {
        if (poolEntries == null)
        {
            poolEntries = Array.Empty<PoolEntry>();
            return;
        }

        for (int i = 0; i < poolEntries.Length; i++)
        {
            PoolEntry entry = poolEntries[i];
            if (entry == null)
            {
                continue;
            }

            entry.MaxPoolSize = Mathf.Max(0, entry.MaxPoolSize);
            entry.PrewarmCount = Mathf.Clamp(
                entry.PrewarmCount,
                0,
                entry.MaxPoolSize);
        }
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ActiveServices.Clear();
    }
}
