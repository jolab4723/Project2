using UnityEngine;
using UnityEditor;

public class KY_BakeScaleToSize : MonoBehaviour
{
    [MenuItem("Tools/Bake Scale To Size")]
    static void BakeSelected()
    {
        foreach (var obj in Selection.gameObjects)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt == null) continue;

            Vector3 scale = rt.localScale;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x * scale.x, rt.sizeDelta.y * scale.y);
            rt.localScale = Vector3.one;
        }
        Debug.Log("Scale → Size 변환 완료");
    }
}