using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(DestructionDamageStrengthScaler))]
public sealed class EnemyDestructionService : MonoBehaviour
{
    private enum OverflowPolicy
    {
        Skip,
        UseReadyFallback
    }

    [Serializable]
    private sealed class PoolEntry
    {
        [SerializeField, InspectorName("파괴 연출 프리팹")]
        public GameObject VisualPrefab;

        [SerializeField, Min(0), InspectorName("미리 준비할 수량")]
        public int PrewarmCount = 4;

        [SerializeField, Min(0), InspectorName("최대 풀 크기")]
        public int MaxPoolSize = 12;

        [SerializeField, InspectorName("풀이 부족할 때")]
        public OverflowPolicy Overflow = OverflowPolicy.Skip;

        [SerializeField, InspectorName("준비된 대체 연출 프리팹")]
        public GameObject FallbackPrefab;
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
        internal bool InReadyQueue;
    }

    private static readonly Dictionary<int, HashSet<EnemyDestructionService>>
        ServicesByScene =
            new Dictionary<int, HashSet<EnemyDestructionService>>();

    [SerializeField, InspectorName("파괴 연출 풀 목록")]
    private PoolEntry[] poolEntries = Array.Empty<PoolEntry>();

    [SerializeField, InspectorName("데미지별 파괴 세기")]
    private DestructionDamageStrengthScaler damageStrengthScaler;

    [SerializeField, Min(100f), InspectorName("화면 밖 준비 위치 깊이")]
    private float warmupDepth = 10000f;

    private readonly Dictionary<GameObject, PoolState> statesByPrefab =
        new Dictionary<GameObject, PoolState>();
    private readonly HashSet<GameObject> unregisteredWarnings =
        new HashSet<GameObject>();

    private int registeredSceneHandle = -1;
    private bool isRegistered;

    internal static bool TryGet(
        Scene scene,
        out EnemyDestructionService service)
    {
        service = null;
        if (!scene.IsValid() ||
            !ServicesByScene.TryGetValue(
                scene.handle,
                out HashSet<EnemyDestructionService> candidates))
        {
            return false;
        }

        candidates.RemoveWhere(candidate =>
            candidate == null ||
            !candidate.isActiveAndEnabled ||
            candidate.registeredSceneHandle != scene.handle ||
            candidate.gameObject.scene.handle != scene.handle);

        if (candidates.Count == 0)
        {
            ServicesByScene.Remove(scene.handle);
            return false;
        }

        if (candidates.Count != 1)
        {
            return false;
        }

        foreach (EnemyDestructionService candidate in candidates)
        {
            service = candidate;
            return true;
        }

        return false;
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

        PoolState selectedState = primary;
        PoolItem item = Rent(primary);
        if (item == null)
        {
            TryStartExpansion(primary);
            if (primary.Config.Overflow == OverflowPolicy.UseReadyFallback &&
                primary.Config.FallbackPrefab != null &&
                primary.Config.FallbackPrefab != visualPrefab &&
                statesByPrefab.TryGetValue(
                    primary.Config.FallbackPrefab,
                    out PoolState fallback))
            {
                item = Rent(fallback);
                selectedState = fallback;
            }
        }

        if (item == null)
        {
            WarnExhausted(primary);
            return false;
        }

        item.InReadyQueue = false;
        item.InUse = true;

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

        selectedState.ExhaustionWarned = false;
        return true;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        RegisterService();
        if (!Application.isPlaying)
        {
            return;
        }

        InitializeRuntimePools();
        StartCoroutine(PrewarmAll());
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

        registeredSceneHandle = scene.handle;
        if (!ServicesByScene.TryGetValue(
                registeredSceneHandle,
                out HashSet<EnemyDestructionService> services))
        {
            services = new HashSet<EnemyDestructionService>();
            ServicesByScene.Add(registeredSceneHandle, services);
        }

        services.Add(this);
        isRegistered = true;
        if (services.Count > 1)
        {
            Debug.LogError(
                $"[{nameof(EnemyDestructionService)}] {scene.name}: " +
                "같은 씬에 활성 서비스가 여러 개라 파괴 연출 요청을 중단합니다.",
                this);
        }
    }

    private void UnregisterService()
    {
        if (isRegistered &&
            ServicesByScene.TryGetValue(
                registeredSceneHandle,
                out HashSet<EnemyDestructionService> services))
        {
            services.Remove(this);
            if (services.Count == 0)
            {
                ServicesByScene.Remove(registeredSceneHandle);
            }
        }

        isRegistered = false;
        registeredSceneHandle = -1;
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
            transform.position + Vector3.down * warmupDepth;

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
            EnqueueReady(state, item);
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

        EnqueueReady(state, item);
        state.ExhaustionWarned = false;
    }

    private static void EnqueueReady(PoolState state, PoolItem item)
    {
        if (item.InReadyQueue)
        {
            return;
        }

        item.InReadyQueue = true;
        state.Ready.Enqueue(item);
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

            item.InReadyQueue = false;
            if (!item.InUse && item.Visual.IsStartupCompleted)
            {
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
        item.InReadyQueue = false;
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
                item.InReadyQueue = false;
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

    private void ResolveReferences()
    {
        if (damageStrengthScaler == null)
        {
            damageStrengthScaler = GetComponent<DestructionDamageStrengthScaler>();
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
        warmupDepth = Mathf.Max(100f, warmupDepth);
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
            if (entry.FallbackPrefab == entry.VisualPrefab)
            {
                entry.FallbackPrefab = null;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ServicesByScene.Clear();
    }
}
