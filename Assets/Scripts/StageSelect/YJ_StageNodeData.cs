using System.Collections.Generic;
using UnityEngine;

public enum StageActType
{
    Act1 = 1,
    Act2 = 2,
    Act3 = 3
}

public enum StageNodeType
{
    Start = 0,
    Battle = 1,
    Elite = 2,
    Event = 3,
    Camp = 5,
    Boss = 6
}

[DisallowMultipleComponent]
public class YJ_StageNodeData : MonoBehaviour
{
    public string id;
    public StageActType act;
    public int floor;
    public int nodeIndex;
    public StageNodeType type;
    public string sceneName;
    public Vector2 position;
    public List<string> nextNodeIds = new();
    public bool cleared;
    public bool available;

    public void Initialize(
        string nodeId,
        StageActType stageAct,
        int floorNumber,
        int index,
        StageNodeType nodeType,
        Vector2 anchoredPosition)
    {
        id = nodeId;
        act = stageAct;
        floor = floorNumber;
        nodeIndex = index;
        type = nodeType;
        position = anchoredPosition;
        cleared = false;
        available = false;
        nextNodeIds.Clear();
    }
}
