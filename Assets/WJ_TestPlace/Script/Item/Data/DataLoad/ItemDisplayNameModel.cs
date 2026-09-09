using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class ItemDisplayNameJsonData
    {
        public List<ItemDisplayNameRow> korLabels = new List<ItemDisplayNameRow>();
        public List<ItemDisplayNameRow> engLabels = new List<ItemDisplayNameRow>();
        public List<ItemDisplayNameRow> jpnLabels = new List<ItemDisplayNameRow>();
        public List<ItemDisplayNameRow> chnLabels = new List<ItemDisplayNameRow>();
    }

    /// <summary>
    /// ItemDisplayName.xlsx의 KOR/ENG/JPN/CHN 시트 한 줄. QuestLabelRow/UILabelRow와 같은 범용
    /// key/label 구조를 그대로 쓴다. 등급/속성/분류/직업/무기/방어구/스탯 이름의 key 규칙은
    /// ItemDisplayNames.cs 상단 주석을 참고.
    /// </summary>
    [Serializable]
    public class ItemDisplayNameRow
    {
        public string key;
        public string label;
    }
}
