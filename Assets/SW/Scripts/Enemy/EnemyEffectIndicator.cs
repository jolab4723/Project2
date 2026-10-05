using TMPro;
using UnityEngine;

/// <summary>SW 수정 : 적 위 냉각 단계와 지원 표식을 표시하며 판정·소유권은 변경하지 않는다.</summary>
public sealed class EnemyEffectIndicator : MonoBehaviour
{
    private TMP_Text label;
    private int cooling;
    private bool marked;
    private float coolingUntil, markUntil;

    public void SetCooling(int count, float seconds)
    {
        cooling = count;
        coolingUntil = Time.unscaledTime + seconds;
        Refresh();
    }

    public void SetSupportMark(bool active, float seconds)
    {
        marked = active;
        markUntil = Time.unscaledTime + seconds;
        Refresh();
    }

    private void Refresh()
    {
        if (label == null && (cooling > 0 || marked))
        {
            var child = new GameObject("Effect Indicator");
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.up * 2.5f;
            label = child.AddComponent<TextMeshPro>();
            label.fontSize = 4f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
        }

        if (label == null)
            return;

        label.gameObject.SetActive(cooling > 0 || marked);
        label.text = marked ? "+" : cooling.ToString();
        label.color = marked ? new Color(1f, 0.9f, 0.2f) : new Color(0.25f, 0.8f, 1f);
    }

    private void LateUpdate()
    {
        bool changed = false;
        if (cooling > 0 && Time.unscaledTime >= coolingUntil)
        {
            cooling = 0;
            changed = true;
        }
        if (marked && Time.unscaledTime >= markUntil)
        {
            marked = false;
            changed = true;
        }

        if (changed)
            Refresh();
        if (label != null && Camera.main != null)
            label.transform.rotation = Camera.main.transform.rotation;
    }

    private void OnDisable()
    {
        cooling = 0;
        marked = false;
        Refresh();
    }
}
