using System;
using System.Collections.Generic;

[Serializable]
public class StageMapSaveData
{
    public int saveVersion = 1; // 향후 저장 방식 변경 대응

    public StageActType act;
    public int mapSeed; // 디버깅과 맵 식별용

    public int clearedFloor; // 실제 클리어한 노드
    public string lastClearedNodeId; // 현재 플레이어 위치
    public string pendingNodeId; // 선택했지만 아직 클리어하지 못한 노드

    public List<string> clearedNodeIds = new(); // 클리어된 노드들
    public List<string> visitedNodeIds = new(); // 플레이어가 지나온 노드들
    public List<StageNodeSaveData> nodes = new(); // 노드 종류와 실제 배치 좌표
}

[Serializable]
public class StageNodeSaveData
{
    public string id; // 노드 이름 ex. A1_F02_N02 : Act1 2층 2번째 노드
    public int floor; // 노드의 층
    public int nodeIndex;  // 노드 인덱스
    public StageNodeType type; // 노드 타입
    public string sceneName; // 씬 이름

    public float positionX; // 노드의 X 위치
    public float positionY; // 노드의 Y 위치
 
    public List<string> nextNodeIds = new(); // 경로가 연결된 다음 층 노드들
}

/* 변환된 JSON 데이터 예시

{
  "saveVersion": 1,
  "act": 1,
  "mapSeed": 1928374,
  "clearedFloor": 2,
  "lastClearedNodeId": "A1_F02_N02",
  "pendingNodeId": "",
  "clearedNodeIds": [
    "A1_F01_N01",
    "A1_F02_N02"
  ],
  "visitedNodeIds": [
    "A1_F01_N01",
    "A1_F02_N02"
  ],
  "nodes": [
    {
      "id": "A1_F02_N02",
      "floor": 2,
      "nodeIndex": 1,
      "type": 1,
      "sceneName": "Act1_Stage2",
      "positionX": 120.0,
      "positionY": -1540.0,
      "nextNodeIds": [
        "A1_F03_N01",
        "A1_F03_N02"
      ]
    }
  ]
}

*/