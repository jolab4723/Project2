using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>저장 상태를 소유하지 않는 Unknown 전용 폐기 선택창.</summary>
public class YJ_UnknownDiscardPanel : MonoBehaviour
{
    public sealed class Entry
    {
        public string id;
        public string text;
        public Sprite icon;
    }

    private readonly List<string> selected = new();
    private int required;
    private TMP_Text counter;
    private Button confirm;
    private Action<List<string>> onConfirm;
    private Action onCancel;
    private bool closed;

    public static YJ_UnknownDiscardPanel Open(Canvas parent, TMP_FontAsset font, List<Entry> entries,
        int count, Action<List<string>> confirmAction, Action cancelAction)
    {
        var root = Rect("UnknownDiscardPanel", parent.rootCanvas.transform, Vector2.zero, Vector2.one);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32760;
        root.gameObject.AddComponent<GraphicRaycaster>();
        root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .85f);
        var panel = root.gameObject.AddComponent<YJ_UnknownDiscardPanel>();
        panel.required = count; panel.onConfirm = confirmAction; panel.onCancel = cancelAction;
        panel.Build(font, entries);
        return panel;
    }

    private void Build(TMP_FontAsset font, List<Entry> entries)
    {
        var box = Rect("Window", transform, new Vector2(.16f, .08f), new Vector2(.84f, .92f));
        box.gameObject.AddComponent<Image>().color = new Color(.07f, .09f, .13f, 1);
        Text("Title", box, font, "폐기할 아이템 선택", new Vector2(.05f, .88f), new Vector2(.95f, .98f), 30);
        counter = Text("Counter", box, font, "", new Vector2(.05f, .81f), new Vector2(.95f, .89f), 20);

        var viewport = Rect("Viewport", box, new Vector2(.05f, .17f), new Vector2(.95f, .8f));
        viewport.gameObject.AddComponent<Image>().color = new Color(.025f, .03f, .04f, 1);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Items", viewport, new Vector2(0, 1), Vector2.one);
        content.pivot = new Vector2(.5f, 1);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8; layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childControlHeight = true; layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;

        foreach (var entry in entries)
        {
            var row = Rect("Item_" + entry.id, content, Vector2.zero, Vector2.one);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = Mathf.Max(100, entry.text.Split('\n').Length * 25 + 12);
            var background = row.gameObject.AddComponent<Image>();
            background.color = new Color(.14f, .17f, .22f, 1);
            var button = row.gameObject.AddComponent<Button>(); button.targetGraphic = background;
            if (entry.icon != null)
            {
                var icon = Rect("Icon", row, new Vector2(.01f, .1f), new Vector2(.11f, .9f)).gameObject.AddComponent<Image>();
                icon.sprite = entry.icon; icon.preserveAspect = true; icon.raycastTarget = false;
            }
            var label = Text("Label", row, font, entry.text, new Vector2(.13f, .05f), new Vector2(.98f, .95f), 18);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            button.onClick.AddListener(() =>
            {
                if (closed) return;
                if (!selected.Remove(entry.id) && selected.Count < required) selected.Add(entry.id);
                background.color = selected.Contains(entry.id) ? new Color(.48f, .17f, .14f, 1) : new Color(.14f, .17f, .22f, 1);
                Refresh();
            });
        }
        var cancel = MakeButton("Cancel", box, font, "취소", new Vector2(.05f, .04f), new Vector2(.47f, .13f));
        cancel.onClick.AddListener(() => { var callback = onCancel; Close(); callback?.Invoke(); });
        confirm = MakeButton("Confirm", box, font, "선택한 아이템 폐기", new Vector2(.53f, .04f), new Vector2(.95f, .13f));
        confirm.onClick.AddListener(() =>
        {
            if (closed || selected.Count != required) return;
            var ids = new List<string>(selected); var callback = onConfirm;
            Close(); callback?.Invoke(ids);
        });
        Refresh();
    }

    private void Refresh()
    {
        counter.text = $"선택 {selected.Count} / {required} · 장착 아이템 제외 · 확정 시 영구 폐기";
        confirm.interactable = selected.Count == required;
    }

    public void Close()
    {
        if (closed) return;
        closed = true; onConfirm = null; onCancel = null;
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string value, Vector2 min, Vector2 max, int size)
    {
        var text = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.richText = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Button MakeButton(string name, Transform parent, TMP_FontAsset font, string label, Vector2 min, Vector2 max)
    {
        var rect = Rect(name, parent, min, max);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.25f, .3f, .4f, 1);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        Text("Label", rect, font, label, Vector2.zero, Vector2.one, 22);
        return button;
    }
}
