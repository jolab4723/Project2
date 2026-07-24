using System;
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

        if (tuningPanel == null)
        {
            tuningPanel = GetComponent<ArtificerRuntimeTuningPanel>();
        }
        if (tuningPanel != null && !tuningPanel.IsRespawning)
        {
            tuningPanel.RegisterTargetAndApply(
                visual.GetComponent<ArtificerRuntimeTuningTarget>());
        }

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
        {
            available = new Stack<EnemyDestructionVisual>();
            availableByPrefab.Add(prefab, available);
        }

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
            GameObject instance = Instantiate(prefab, transform);
            visual = instance.GetComponent<EnemyDestructionVisual>();
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
