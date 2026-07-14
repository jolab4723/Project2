using System.Collections.Generic;
using UnityEngine;

public enum StageNodeType
{
    Start,
    Battle,
    Elite,
    Event,
    Shop,
    Boss
}

[System.Serializable]
public class YJ_StageNodeData : MonoBehaviour
{
    public string id;
    public StageNodeType type;
    public string sceneName;
    public Vector2 position;
    public List<string> nextNodeIds = new();
    public bool cleared;
    public bool available;
}
