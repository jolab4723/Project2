using System;
using System.Collections.Generic;
using UnityEngine;

public enum CursorType
{
    Default,
    Enemy,
    NPC,
    Interact,
    Drag,
    DragBlocked,
    WorldItem
}

public class YJ_CursorManager : Singleton<YJ_CursorManager>
{
    [Serializable]
    private class CursorSetting
    {
        public CursorType type;
        public Texture2D texture;
        public Vector2 hotspot;
    }

    [SerializeField] private List<CursorSetting> cursorSettings;

    private readonly Dictionary<CursorType, CursorSetting> settingMap = new();

    private CursorType? currentType;
    private bool isDragging;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
            return;

        settingMap.Clear();

        if (cursorSettings == null)
            return;

        foreach (CursorSetting setting in cursorSettings)
            settingMap[setting.type] = setting;

        SetCursor(CursorType.Default);
    }

    public void SetCursor(CursorType type)
    {
        if (isDragging && type != CursorType.Drag)
            return;

        ApplyCursor(type);
    }

    public void BeginDragCursor()
    {
        isDragging = true;
        ApplyCursor(CursorType.Drag);
    }

    public void EndDragCursor()
    {
        isDragging = false;
        ApplyCursor(CursorType.Default);
    }

    public void ResetCursor()
    {
        if (isDragging)
            return;

        ApplyCursor(CursorType.Default);
    }

    private void ApplyCursor(CursorType type)
    {
        if (currentType.HasValue && currentType.Value == type)
            return;

        if (!settingMap.TryGetValue(type, out CursorSetting setting))
            return;

        currentType = type;
        Cursor.SetCursor(setting.texture, setting.hotspot, CursorMode.Auto);
    }
}
