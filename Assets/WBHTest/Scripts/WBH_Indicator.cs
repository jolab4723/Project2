using System.Collections;
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

    public void SetSize(float width, float length)
    {
        indicatorObject.transform.localScale = new Vector3(width, length, 1f);
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
