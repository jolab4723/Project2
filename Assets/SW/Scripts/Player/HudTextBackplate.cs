using TMPro;
using UnityEngine;

/// <summary>
/// SW 수정 : 전투 씬마다 배치를 바꾸지 않고 HUD 문구 뒤에 글자 길이에 맞춘 반투명 띠를 깔아 배경 위에서도 읽히게 한다.
/// 띠는 문구의 형제 오브젝트로 문구 바로 앞 순서에 두며, 문구가 비거나 꺼지면 함께 숨는다.
/// </summary>
[RequireComponent(typeof(UnityEngine.UI.Image))]
public sealed class HudTextBackplate : MonoBehaviour
{
    private const string SpritePath = "UI/T_HudTextBackplate";

    private TMP_Text target;
    private RectTransform rect;
    private UnityEngine.UI.Image image;
    private Vector2 padding;

    /// <summary>문구 앞에 띠를 한 번만 만든다. 이미 있으면 기존 띠를 돌려준다.</summary>
    public static HudTextBackplate Attach(TMP_Text text, Vector2 padding)
    {
        if (text == null || text.transform.parent == null)
            return null;
        Transform existing = text.transform.parent.Find(text.name + " Backplate");
        if (existing != null && existing.TryGetComponent(out HudTextBackplate found))
            return found;

        var go = new GameObject(text.name + " Backplate", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(HudTextBackplate));
        go.layer = text.gameObject.layer;
        go.transform.SetParent(text.transform.parent, false);
        go.transform.SetSiblingIndex(text.transform.GetSiblingIndex());

        var plate = go.GetComponent<HudTextBackplate>();
        plate.target = text;
        plate.padding = padding;
        plate.rect = (RectTransform)go.transform;
        plate.rect.anchorMin = text.rectTransform.anchorMin;
        plate.rect.anchorMax = text.rectTransform.anchorMax;
        plate.rect.pivot = new Vector2(0.5f, 0.5f);
        plate.image = go.GetComponent<UnityEngine.UI.Image>();
        plate.image.sprite = Resources.Load<Sprite>(SpritePath);
        plate.image.type = UnityEngine.UI.Image.Type.Sliced;
        plate.image.raycastTarget = false;
        plate.LateUpdate();
        return plate;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        bool visible = target.isActiveAndEnabled && !string.IsNullOrEmpty(target.text);
        if (visible)
        {
            // textBounds는 문구 RectTransform의 피벗 기준 로컬 좌표다. 같은 부모·앵커이므로 그대로 위치에 더한다.
            Bounds bounds = target.textBounds;
            visible = bounds.size.x > 0.01f;
            if (visible)
            {
                rect.anchoredPosition = target.rectTransform.anchoredPosition + (Vector2)bounds.center;
                rect.sizeDelta = new Vector2(bounds.size.x, bounds.size.y) + padding * 2f;
            }
        }
        if (image.enabled != visible)
            image.enabled = visible;
    }
}
