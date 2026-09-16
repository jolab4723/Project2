using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class BuffLabelJsonData
    {
        public List<BuffLabelRow> korLabels = new List<BuffLabelRow>();
        public List<BuffLabelRow> engLabels = new List<BuffLabelRow>();
        public List<BuffLabelRow> jpnLabels = new List<BuffLabelRow>();
        public List<BuffLabelRow> chnLabels = new List<BuffLabelRow>();
    }

    /// <summary>BuffLabel.xlsx의 KOR/ENG/JPN/CHN 시트 한 줄. buffId는 BuffTable.xlsx의 buffId와 1:1로 맞춘다.</summary>
    [Serializable]
    public class BuffLabelRow
    {
        public string buffId;
        public string buffName;
    }
}
