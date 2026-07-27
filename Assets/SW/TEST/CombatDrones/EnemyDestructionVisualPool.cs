using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDestructionVisualPool : MonoBehaviour
{
    private readonly Dictionary<GameObject, Stack<EnemyDestructionVisual>>
        availableByPrefab =
            new Dictionary<GameObject, Stack<EnemyDestructionVisual>>();
    private readonly Dictionary<EnemyDestructionVisual, GameObject>
        prefabByInstance =
            new Dictionary<EnemyDestructionVisual, GameObject>();
    private readonly HashSet<EnemyDestructionVisual> activeVisuals =
        new HashSet<EnemyDestructionVisual>();

    private ArtificerRuntimeTuningPanel tuningPanel;

    public int ActiveInstanceCount => activeVisuals.Count;
    public int TotalInstanceCount => prefabByInstance.Count;
    public bool IsPrewarming { get; private set; }

    public IEnumerator Prewarm(GameObject visualPrefab, int desiredCount)
    {
        if (visualPrefab == null || desiredCount <= 0)
            yield break;

        int missingCount = Mathf.Max(
            0,
            desiredCount - CountInstancesForPrefab(visualPrefab));
        if (missingCount == 0)
            yield break;

        IsPrewarming = true;
        var warming = new List<EnemyDestructionVisual>(missingCount);
        for (int i = 0; i < missingCount; i++)
        {
            EnemyDestructionVisual visual = CreateVisual(visualPrefab);
            if (visual == null)
                continue;

            visual.gameObject.SetActive(true);
            RegisterTuningTarget(visual);
            warming.Add(visual);
        }

        // EnemyDestructionVisual.Start()와 Artificer.Start()를 모두 완료한다.
        yield return null;
        yield return null;

        // 첫 사망 프레임에서 Mesh/RenderParams를 만들지 않도록 화면 밖에서
        // 한 프레임만 실제 분해 경로를 실행해 렌더 캐시까지 준비한다.
        Vector3 warmupPosition = transform.position + Vector3.down * 10000f;
        for (int i = 0; i < warming.Count; i++)
        {
            warming[i].Play(
                warmupPosition,
                Quaternion.identity,
                warmupPosition,
                Vector3.forward,
                0f);
        }
        yield return null;

        for (int i = 0; i < warming.Count; i++)
        {
            EnemyDestructionVisual visual = warming[i];
            if (visual == null)
                continue;

            visual.ReturnToPoolNow();
            GetAvailableStack(visualPrefab).Push(visual);
        }
        IsPrewarming = false;
    }

    public EnemyDestructionVisual Play(
        GameObject visualPrefab,
        Vector3 worldPosition,
        Quaternion worldRotation,
        Vector3 worldImpactPoint,
        Vector3 attackDirection,
        float directionalForce,
        Action onReadyToReplaceSource = null)
    {
        EnemyDestructionVisual visual = Rent(visualPrefab);
        if (visual == null)
        {
            return null;
        }

        activeVisuals.Add(visual);
        visual.gameObject.SetActive(true);
        RegisterTuningTarget(visual);

        visual.Play(
            worldPosition,
            worldRotation,
            worldImpactPoint,
            attackDirection,
            directionalForce,
            onReadyToReplaceSource);
        return visual;
    }

    public void ReturnAll()
    {
        if (activeVisuals.Count == 0)
        {
            return;
        }

        var snapshot = new List<EnemyDestructionVisual>(activeVisuals);
        for (int i = 0; i < snapshot.Count; i++)
        {
            Return(snapshot[i]);
        }
    }

    private EnemyDestructionVisual Rent(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        if (!availableByPrefab.TryGetValue(prefab, out var available))
            available = GetAvailableStack(prefab);

        EnemyDestructionVisual visual = null;
        while (available.Count > 0 && visual == null)
        {
            visual = available.Pop();
        }

        if (visual == null)
        {
            // Adaptive high-water mark: if every retained instance of this
            // prefab is busy, grow by one. Completed instances are returned
            // to the stack and future deaths reuse them without shrinking.
            visual = CreateVisual(prefab);
            if (visual == null)
                return null;
        }

        visual.transform.SetParent(transform, true);
        visual.gameObject.SetActive(false);
        return visual;
    }

    private void HandleVisualCompleted(EnemyDestructionVisual visual)
    {
        Return(visual);
    }

    private void Return(EnemyDestructionVisual visual)
    {
        if (visual == null ||
            !prefabByInstance.TryGetValue(visual, out GameObject prefab))
        {
            return;
        }

        // Only an actually rented instance may be returned. This prevents the
        // same visual from being pushed twice and then rented concurrently by
        // two deaths.
        if (!activeVisuals.Remove(visual))
        {
            return;
        }

        visual.ReturnToPoolNow();

        if (!availableByPrefab.TryGetValue(prefab, out var available))
        {
            available = new Stack<EnemyDestructionVisual>();
            availableByPrefab.Add(prefab, available);
        }
        available.Push(visual);
    }

    private EnemyDestructionVisual CreateVisual(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, transform);
        EnemyDestructionVisual visual =
            instance.GetComponent<EnemyDestructionVisual>();
        if (visual == null)
        {
            Debug.LogError(
                "[Enemy Manual Test] 파괴 연출 프리팹 루트에 " +
                "EnemyDestructionVisual이 없습니다: " + prefab.name,
                prefab);
            Destroy(instance);
            return null;
        }

        visual.Completed += HandleVisualCompleted;
        prefabByInstance.Add(visual, prefab);
        return visual;
    }

    private Stack<EnemyDestructionVisual> GetAvailableStack(GameObject prefab)
    {
        if (!availableByPrefab.TryGetValue(prefab, out var available))
        {
            available = new Stack<EnemyDestructionVisual>();
            availableByPrefab.Add(prefab, available);
        }
        return available;
    }

    private int CountInstancesForPrefab(GameObject prefab)
    {
        int count = 0;
        foreach (GameObject sourcePrefab in prefabByInstance.Values)
        {
            if (sourcePrefab == prefab)
                count++;
        }
        return count;
    }

    private void RegisterTuningTarget(EnemyDestructionVisual visual)
    {
        if (visual == null)
            return;

        if (tuningPanel == null)
            tuningPanel = GetComponent<ArtificerRuntimeTuningPanel>();
        if (tuningPanel != null && !tuningPanel.IsRespawning)
        {
            tuningPanel.RegisterTargetAndApply(
                visual.GetComponent<ArtificerRuntimeTuningTarget>());
        }
    }

    private void OnDestroy()
    {
        foreach (EnemyDestructionVisual visual in prefabByInstance.Keys)
        {
            if (visual != null)
            {
                visual.Completed -= HandleVisualCompleted;
            }
        }
    }
}
