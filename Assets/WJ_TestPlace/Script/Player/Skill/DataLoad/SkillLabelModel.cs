using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class SkillLabelJsonData
    {
        public List<SkillLabelRow> korLabels = new List<SkillLabelRow>();
        public List<SkillLabelRow> engLabels = new List<SkillLabelRow>();
        public List<SkillLabelRow> jpnLabels = new List<SkillLabelRow>();
        public List<SkillLabelRow> chnLabels = new List<SkillLabelRow>();
    }

    /// <summary>SkillDataLabel.xlsx의 KOR/ENG/JPN/CHN 시트 한 줄. skillId는 ActiveSkillId 이름(예: "FighterHalfCircleSlash")과 1:1로 맞춘다.</summary>
    [Serializable]
    public class SkillLabelRow
    {
        public string skillId;
        public string skillDescription;
        public string evolution1Description;
        public string evolution2Description;
        public string evolution3Description;
        public string enhancement1Description;
        public string enhancement2Description;
        public string enhancement3Description;
    }
}
