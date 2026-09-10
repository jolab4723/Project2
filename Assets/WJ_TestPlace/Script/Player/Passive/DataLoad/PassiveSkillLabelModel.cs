using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class PassiveSkillLabelJsonData
    {
        public List<PassiveSkillLabelRow> korLabels = new List<PassiveSkillLabelRow>();
        public List<PassiveSkillLabelRow> engLabels = new List<PassiveSkillLabelRow>();
        public List<PassiveSkillLabelRow> jpnLabels = new List<PassiveSkillLabelRow>();
        public List<PassiveSkillLabelRow> chnLabels = new List<PassiveSkillLabelRow>();
    }

    /// <summary>
    /// PassiveSkillDataLabel.xlsx의 KOR/ENG/JPN/CHN 시트 한 줄.
    /// passiveId는 Core.PassiveSkillId enum 이름(예: "MaxHealth")과 1:1로 맞춘다.
    /// </summary>
    [Serializable]
    public class PassiveSkillLabelRow
    {
        public string passiveId;
        public string passiveName;
        public string passiveDescription;
    }
}
