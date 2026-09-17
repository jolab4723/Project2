using System.Collections;
using UnityEngine;

/// <summary>Dedicated preview driver for BossAttackIndicator. Space = replay.</summary>
public class BossAttackTelegraphPreview : MonoBehaviour
{
    [SerializeField] private BossAttackTelegraph telegraph;
    [Min(0.5f)] public float radius = 5f;
    [Min(0.05f)] public float warningTime = 1.2f;
    public bool autoLoop = true;
    [Min(0f)] public float holdAfter = 0.5f;
    [Header("Direction Test")]
    public bool overrideDirection = false;
    public TelegraphDirection direction = TelegraphDirection.Outward;

    private Coroutine loop;

    private void Reset()
    {
        telegraph = GetComponentInChildren<BossAttackTelegraph>(true);
    }

    private void OnEnable()
    {
        if (telegraph == null)
            telegraph = GetComponentInChildren<BossAttackTelegraph>(true);
        if (telegraph == null)
            telegraph = FindFirstObjectByType<BossAttackTelegraph>();
        if (autoLoop)
            Play();
    }

    private void OnDisable()
    {
        Stop();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            Play();
    }

    [ContextMenu("Play")]
    public void Play()
    {
        if (telegraph == null)
            return;
        Stop();
        loop = StartCoroutine(CoLoop());
    }

    [ContextMenu("Stop")]
    public void Stop()
    {
        if (loop != null)
        {
            StopCoroutine(loop);
            loop = null;
        }
    }

    private IEnumerator CoLoop()
    {
        var waitInterval = new WaitForSeconds(0.25f);
        while (true)
        {
            telegraph.previewProgress = -1f;
            if (overrideDirection)
            {
                telegraph.direction = direction;
            }
            telegraph.Show(warningTime, radius);
            yield return new WaitForSeconds(warningTime + holdAfter);
            telegraph.Hide();
            if (!autoLoop)
                break;
            yield return waitInterval;
        }
        loop = null;
    }
}
