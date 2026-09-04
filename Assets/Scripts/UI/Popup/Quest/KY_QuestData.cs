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
    public string description;
    public KY_QuestConditionData[] conditions;
    public string reward;
}
