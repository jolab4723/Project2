using System;

namespace DataSystem
{
    /// <summary>
    /// QuestTable.xlsx(첫 번째 시트) 한 줄. QuestDefinitionSO 하나에 대응한다.
    ///
    /// questId는 세이브/조회 키이자(변경 금지), 새 퀘스트를 처음 만들 때만 생성될 에셋 파일명으로도 쓰인다
    /// (기존 questId면 파일명이 달라도 그 에셋을 그대로 갱신한다 - QuestTableSOImporter 참고).
    /// </summary>
    [Serializable]
    public class QuestTableRow
    {
        public string questId;
        public string questName;
        public string description;

        /// <summary>
        /// 이 퀘스트가 요구하는 조건들. 한 조건은 "조건타입:대상ID:목표수치:설명" 형태로 적고,
        /// 여러 조건(AND)이면 ';'로 구분한다.
        /// 조건타입은 QuestConditionType 이름(KillEnemy/CollectItem)을 그대로 쓴다.
        /// 대상ID는 KillEnemy는 비워두면 아무 적이나 인정, CollectItem은 ItemDefinitionSO.itemId를 적는다.
        /// 예: "KillEnemy::5:적 5마리 처치"
        ///     "KillEnemy::5:적 5마리 처치;CollectItem:item.potion.blue:2:파란 물약 2개 수집"
        /// </summary>
        public string conditions;

        public int rewardGold;

        /// <summary>보상 아이템의 ItemDefinitionSO.itemId. 비워두면 아이템 보상 없음.</summary>
        public string rewardItemId;

        public int rewardItemCount;

        /// <summary>기획 메모. 변환에는 사용하지 않는다.</summary>
        public string note;
    }
}
