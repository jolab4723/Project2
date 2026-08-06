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

    public void SetWorldSize(float worldWidth,  float worldLength, float thickness = 0.01f)
    {
        Transform indicator = indicatorObject.transform;
        Vector3 parentScale = indicator.parent.lossyScale;

        indicator.localScale = new Vector3(worldWidth / Mathf.Abs(parentScale.x), thickness / Mathf.Abs(parentScale.y), worldLength / Mathf.Abs(parentScale.z));
        indicator.localPosition = Vector3.forward * (worldLength * 0.5f / Mathf.Abs(parentScale.z));
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
