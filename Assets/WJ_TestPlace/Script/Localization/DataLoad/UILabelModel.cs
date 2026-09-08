using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class UILabelJsonData
    {
        public List<UILabelRow> korLabels = new List<UILabelRow>();
        public List<UILabelRow> engLabels = new List<UILabelRow>();
        public List<UILabelRow> jpnLabels = new List<UILabelRow>();
        public List<UILabelRow> chnLabels = new List<UILabelRow>();
    }

    /// <summary>
    /// UILabel.xlsx의 KOR/ENG/JPN/CHN 시트 한 줄. QuestLabelRow/StatLabelRow와 같은 범용 key/label
    /// 구조를 그대로 쓴다. 특정 퀘스트/시스템에 종속되지 않은, 여러 화면이 공유하는 고정 UI 문구
    /// (탭 이름, 버튼 라벨, 옵션 라벨 등)를 담는다 - 화면별 접두사(예: "settings_ui.")로 key를 구분한다.
    /// UILabelDatabaseSO.GetLabel(key)를 참고.
    /// </summary>
    [Serializable]
    public class UILabelRow
    {
        public string key;
        public string label;
    }
}
