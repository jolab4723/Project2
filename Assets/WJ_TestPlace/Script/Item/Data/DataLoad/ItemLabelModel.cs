using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class ItemLabelJsonData
    {
        public List<ItemLabelRow> korLabels = new List<ItemLabelRow>();
        public List<ItemLabelRow> engLabels = new List<ItemLabelRow>();
    }

    /// <summary>ItemDataLabel.xlsx의 KOR/ENG 시트 한 줄. itemId는 ItemDataTable.xlsx의 itemId와 1:1로 맞춘다.</summary>
    [Serializable]
    public class ItemLabelRow
    {
        public string itemId;
        public string itemName;
        public string description;
    }
}
