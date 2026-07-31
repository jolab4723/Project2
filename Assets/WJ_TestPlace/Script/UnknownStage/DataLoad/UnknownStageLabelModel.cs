using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class UnknownStageLabelJsonData
    {
        public List<UnknownStageLabelRow> korLabels = new List<UnknownStageLabelRow>();
        public List<UnknownStageLabelRow> engLabels = new List<UnknownStageLabelRow>();
        public List<UnknownStageLabelRow> jpnLabels = new List<UnknownStageLabelRow>();
        public List<UnknownStageLabelRow> chnLabels = new List<UnknownStageLabelRow>();
    }

    /// <summary>UnknownStageLabel.xlsx의 KOR/ENG/JPN/CHN 시트 한 줄. stageId는 문자열 키(예: stage.unknown.overcharge).</summary>
    [Serializable]
    public class UnknownStageLabelRow
    {
        public string stageId;
        public string stageName;
        public string stageDescription;
        public string choice1Name;
        public string choice1Description;
        public string choice2Name;
        public string choice2Description;
        public string choice3Name;
        public string choice3Description;
    }
}
