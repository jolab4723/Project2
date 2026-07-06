[System.Serializable]
public class KY_QuestConditionData
{
    public string description;
    public int current;
    public int required;
}

[System.Serializable]
public class KY_QuestData
{
    public string questName;
    public string description;
    public KY_QuestConditionData[] conditions;
    public string reward;
}