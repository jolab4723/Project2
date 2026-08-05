using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class UniqueEffectLabelJsonData
    {
        public List<UniqueEffectLabelRow> korLabels = new List<UniqueEffectLabelRow>();
        public List<UniqueEffectLabelRow> engLabels = new List<UniqueEffectLabelRow>();
        public List<UniqueEffectLabelRow> jpnLabels = new List<UniqueEffectLabelRow>();
        public List<UniqueEffectLabelRow> chnLabels = new List<UniqueEffectLabelRow>();
    }

    /// <summary>
    /// UniqueEffectLabel.xlsx의 KOR/ENG/JPN/CHN 시트 한 줄.
    /// uniqueEffectId는 UniqueEffectTable.xlsx의 uniqueEffectId(예: "UE_CyberneticCore")와 1:1로 맞춘다.
    /// effectDescription은 완성 문구가 아니라 {0},{1}... 자리를 가진 템플릿이며,
    /// 원본(KOR)과 같은 개수의 자리를 유지해야 한다.
    /// </summary>
    [Serializable]
    public class UniqueEffectLabelRow
    {
        public string uniqueEffectId;
        public string effectName;
        public string effectDescription;
    }
}
