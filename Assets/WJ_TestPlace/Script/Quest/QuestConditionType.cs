/// <summary>
/// 퀘스트 조건 종류. 새 종류를 추가하려면 여기 값을 늘리고 QuestManager에 Report 메서드와
/// ReportProgress 처리 분기를 추가하면 된다(조건 자체를 여러 개 동시에 갖는 구조는 QuestManager가
/// 이미 지원하므로, 이 enum 값만 늘어나면 됨).
/// </summary>
public enum QuestConditionType
{
    /// <summary>적 처치. targetId를 비워두면 아무 적이나 인정된다.
    /// !! 현재 WBH_EnemyController.OnEnemyDead가 "누가 죽었는지" 정보를 안 줘서, targetId를 비운
    ///    "아무 적이나" 조건만 실제로 진행된다. 특정 적 종류를 구분하려면 그 이벤트에 식별 정보가
    ///    추가돼야 한다(BH님 담당 - 이번엔 건드리지 않음).</summary>
    KillEnemy,

    /// <summary>아이템 획득(인벤토리에 추가됨). targetId = ItemDefinitionSO.itemId.</summary>
    CollectItem,
}
