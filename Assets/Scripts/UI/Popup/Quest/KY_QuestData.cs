/// <summary>퀘스트 조건 한 줄을 UI에 표시하기 위한 데이터다.</summary>
[System.Serializable]
public class KY_QuestConditionData
{
    public string description;
    public int current;
    public int required;
}

/// <summary>퀘스트 목록과 상세 팝업이 표시할 UI 전용 데이터다.</summary>
[System.Serializable]
public class KY_QuestData
{
    public string questName;
    public string objectiveTypeLabel;
    public string description;
    public KY_QuestConditionData[] conditions;
    public string reward;
    public KY_QuestRewardData[] rewardItems; // SW 수정
    // SW 수정 : 표시 상태는 실제 퀘스트의 목표 완료와 보상 지급 결과를 따른다.
    public bool isCompleted;
    public bool rewardPending;
}

/// <summary>보상 슬롯이 표시할 아이콘과 총수량, 미수령 수량이다. 실제 지급은 게임 상태 소유자가 처리한다.</summary>
[System.Serializable]
public class KY_QuestRewardData
{
    public UnityEngine.Sprite icon;
    public string name;
    public int amount;
    public int remainingAmount;
    public bool questCompleted;
}
