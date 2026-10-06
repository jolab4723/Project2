using System;
using System.Collections.Generic;

/// <summary>
/// 진행 중이거나 완료된 퀘스트 하나의 런타임 상태(세이브 대상). 디자인 데이터(QuestDefinitionSO)는
/// questId로만 참조하고 직접 들고 있지 않는다 - 저장 파일에 SO 전체가 그대로 직렬화되는 걸 피하기 위함.
/// </summary>
[Serializable]
public class ActiveQuestData
{
    public string questId;

    /// <summary>QuestDefinitionSO.conditions와 같은 순서/개수로 진행도를 저장한다.</summary>
    public List<int> conditionProgress = new List<int>();

    public bool isCompleted;

    // SW 수정 : 목표 완료와 실제 지급을 분리하고, 실패한 보상만 다음 시도에 남긴다.
    /// <summary>새 지급 기록인지 구분한다. 구형 완료 저장은 보상을 다시 지급하지 않는다.</summary>
    public bool rewardInitialized;
    public bool goldPaid;
    public int itemsGranted;
}
