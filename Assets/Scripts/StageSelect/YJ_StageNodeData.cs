using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 선택 맵이 어느 Act의 규칙과 프리팹을 사용할지 구분합니다.
/// </summary>
public enum StageActType
{
    Act1 = 1,
    Act2 = 2,
    Act3 = 3
}

/// <summary>
/// 노드가 진입 시 실행할 스테이지 종류를 구분합니다.
/// </summary>
public enum StageNodeType
{
    Start = 0,
    Battle = 1,
    Elite = 2,
    Event = 3,
    Camp = 5,
    Boss = 6
}

/// <summary>
/// 생성된 노드 한 개의 식별 정보, 배치 정보, 연결 상태와 진행 상태를 보관합니다.
/// </summary>
[DisallowMultipleComponent]
public class YJ_StageNodeData : MonoBehaviour
{
    // 저장 데이터와 노드 연결에서 사용하는 고유 ID입니다.
    public string id;
    // 이 노드가 소속된 Act입니다.
    public StageActType act;
    // 1부터 시작하는 노드의 층 번호입니다.
    public int floor;
    // 같은 층 안에서 왼쪽부터 구분하는 노드 인덱스입니다.
    public int nodeIndex;
    // 전투, 캠프, 보스 등 노드의 스테이지 종류입니다.
    public StageNodeType type;
    // 노드를 선택한 뒤 불러올 씬 이름을 저장할 때 사용합니다.
    public string sceneName;
    // 라인 거리와 교차 여부를 계산할 때 사용하는 UI 배치 좌표입니다.
    public Vector2 position;
    // 이 노드에서 직접 이동할 수 있는 다음 층 노드 ID 목록입니다.
    public List<string> nextNodeIds = new();
    // 플레이어가 실제로 선택하여 완료한 노드인지 나타냅니다.
    public bool cleared;
    // 현재 진행 상태에서 클릭 가능한 노드인지 나타냅니다.
    public bool available;

    /// <summary>
    /// 프리팹으로 생성된 노드에 런타임 데이터와 초기 진행 상태를 설정합니다.
    /// </summary>
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
