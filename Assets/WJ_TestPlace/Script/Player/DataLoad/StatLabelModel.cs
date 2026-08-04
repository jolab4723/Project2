using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class StatLabelJsonData
    {
        public List<StatLabelRow> korLabels = new List<StatLabelRow>();
        public List<StatLabelRow> engLabels = new List<StatLabelRow>();
        public List<StatLabelRow> jpnLabels = new List<StatLabelRow>();
        public List<StatLabelRow> chnLabels = new List<StatLabelRow>();
    }

    /// <summary>StatDataLabel.xlsx의 KOR/ENG/JPN/CHN 시트 한 줄. statKey는 PlayerStat.cs의 실제 필드명과 1:1로 맞춘다.</summary>
    [Serializable]
    public class StatLabelRow
    {
        public string statKey;
        public string label;
    }
}
