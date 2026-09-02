using System;
using UnityEngine;

/// <summary>
/// 퀘스트 하나가 요구하는 조건 하나(디자인 데이터). QuestDefinitionSO.conditions에 여러 개를 넣으면
/// 여러 조건을 동시에 요구하는 퀘스트가 되고, 그 조건 전부가 충족돼야 퀘스트가 완료된다.
/// </summary>
[Serializable]
public class QuestConditionDefinition
{
    public QuestConditionType conditionType;

    [Tooltip("조건 대상 ID. KillEnemy는 비워두면 아무 적이나 인정(현재 유일하게 실제로 동작하는 방식 - " +
             "QuestConditionType 주석 참고), CollectItem은 ItemDefinitionSO.itemId를 넣는다.")]
    public string targetId;

    [Min(1)]
    public int requiredCount = 1;

    [Tooltip("UI에 표시할 조건 설명. 예: \"고철 로봇 5마리 처치\"")]
    public string description;
}
