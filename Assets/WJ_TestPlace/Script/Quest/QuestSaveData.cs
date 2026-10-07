using System;
using System.Collections.Generic;

/// <summary>DataManager가 quest.json으로 그대로 저장/복원하는 최상위 래퍼.</summary>
[Serializable]
public class QuestSaveData
{
    public List<ActiveQuestData> quests = new List<ActiveQuestData>();
}
