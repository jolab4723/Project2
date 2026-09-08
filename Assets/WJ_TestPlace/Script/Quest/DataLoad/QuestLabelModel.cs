using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class QuestLabelJsonData
    {
        public List<QuestLabelRow> korLabels = new List<QuestLabelRow>();
        public List<QuestLabelRow> engLabels = new List<QuestLabelRow>();
        public List<QuestLabelRow> jpnLabels = new List<QuestLabelRow>();
        public List<QuestLabelRow> chnLabels = new List<QuestLabelRow>();
    }

    /// <summary>
    /// QuestLabel.xlsx의 KOR/ENG/JPN/CHN 시트 한 줄. StatLabelRow와 같은 범용 key/label 구조를 그대로 쓴다
    /// (StatLabelDatabaseSO 참고) - 퀘스트 하나당 고정 필드를 만드는 대신, 키 하나로 퀘스트 콘텐츠와
    /// 팝업 UI 문구를 전부 표현한다.
    ///
    /// key 규칙:
    ///  - 퀘스트 콘텐츠: "{questId}.name", "{questId}.description", "{questId}.condition.{index}"(0부터)
    ///  - 퀘스트 UI 고정 문구: "quest_ui."로 시작(예: "quest_ui.reroll", "quest_ui.reward_credit" - 후자처럼
    ///    {0}/{1} 같은 자리표시자가 들어간 건 string.Format으로 채워서 쓴다).
    /// QuestLabelDatabaseSO.GetLabel(key)를 참고.
    /// </summary>
    [Serializable]
    public class QuestLabelRow
    {
        public string key;
        public string label;
    }
}
