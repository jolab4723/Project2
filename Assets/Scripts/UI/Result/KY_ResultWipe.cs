using System.Collections;
using UnityEngine;

/// <summary>검은 판이 왼쪽에서 덮고 오른쪽으로 빠져나간다. 호출자가 완료를 기다린다.</summary>
public sealed class KY_ResultWipe : MonoBehaviour
{
    [SerializeField] private RectTransform blackPanel;
    [SerializeField, Min(0)] private float duration = 0.45f;
    [SerializeField] private bool coveredOnAwake = true;
    private bool busy;
    public bool IsBusy => busy;
    private void Awake() { if (blackPanel) { blackPanel.gameObject.SetActive(coveredOnAwake); Position(0); } }
    public IEnumerator Cover() { yield return Slide(-1, 0, false); }
    public IEnumerator Reveal() { yield return Slide(0, 1, true); }
    private IEnumerator Slide(float from, float to, bool hideAfter)
    {
        while (busy) yield return null;
        if (!blackPanel) yield break;
        busy = true;
        try
        {
            blackPanel.gameObject.SetActive(true);
            Position(from);
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
                Position(Mathf.Lerp(from, to, t));
                yield return null;
            }
            Position(to);
            if (hideAfter) blackPanel.gameObject.SetActive(false);
        }
        finally { busy = false; }
    }
    private void Position(float fraction)
    {
        var parent = blackPanel.parent as RectTransform;
        float width = parent ? parent.rect.width : Screen.width;
        blackPanel.anchoredPosition = new Vector2(fraction * (width + 4), 0);
    }
    private void OnDisable() { busy = false; if (blackPanel) blackPanel.gameObject.SetActive(false); }
}
