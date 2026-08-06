using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class WBH_Indicator : MonoBehaviour
{
    [SerializeField] private GameObject indicatorObject;
    private Material mat;

    public void Awake()
    {
        Hide();
        mat = indicatorObject.GetComponent<Material>();
    }


    public void Show()
    {
        if (indicatorObject != null)
            indicatorObject.SetActive(true);
    }

    public void Hide()
    {
        if (indicatorObject != null)
            indicatorObject.SetActive(false);
    }

    public void SetSize(float worldWidth, float worldlLength)
    {
        Transform indicator = indicatorObject.transform;
        Vector3 parentScale = indicator.parent.lossyScale;

        indicator.localScale = new Vector3(worldWidth / Mathf.Abs(parentScale.x), worldWidth / Mathf.Abs(parentScale.y), 1f);
    }

    public IEnumerator PlayCharge(float duration)
    {
        Show();

        float time = 0f;

        while(time < duration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Clamp01(time / duration);

            SetAlpha(alpha);

            yield return null;
        }
        SetAlpha(1f);
    }

    private void SetAlpha(float alpha)
    {
        Color c = mat.color;
        c.a = alpha;
        mat.color = c;
    }
}
