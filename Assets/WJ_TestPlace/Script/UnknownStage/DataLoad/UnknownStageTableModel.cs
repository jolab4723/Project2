using System;

namespace DataSystem
{
    /// <summary>
    /// UnknownStageTable.xlsx(UnknownLangTable 시트) 한 줄.
    /// 실제 텍스트(제목/설명/선택지 문구)는 전부 UnknownStageLabel.xlsx 쪽에 stageId로 들어있고,
    /// 이 테이블에는 스테이지 식별자와 선택지 개수, 배경 이미지 리소스 이름만 있다.
    /// stageId는 UnknownStageLabel.xlsx의 stageId와 1:1로 맞춰져 있다.
    /// backgroundImage는 Assets/Resources/Images/Background/UnknownStage/{backgroundImage}.png를 가리킨다.
    /// </summary>
    [Serializable]
    public class UnknownStageTableRow
    {
        public string stageId;
        public string stageName;
        public int choiceNumber;
        public string backgroundImage;
    }
}
