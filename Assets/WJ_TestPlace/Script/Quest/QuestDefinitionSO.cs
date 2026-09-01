using UnityEngine;
using ItemSystem;

/// <summary>
/// 퀘스트 하나의 디자인 데이터(불변, 게임플레이 중 값이 바뀌지 않음). 실제 진행 상태는
/// ActiveQuestData(런타임+세이브 데이터)가 questId로 이 에셋을 가리켜서 따로 들고 있는다.
/// </summary>
[CreateAssetMenu(menuName = "Quest/Quest Definition")]
public class QuestDefinitionSO : ScriptableObject
{
    [Tooltip("저장/조회용 고유 ID. questName(표시용 이름)과 분리 - 이름이 바뀌어도 세이브가 안 깨지게.")]
    public string questId;

    public string questName;

    [TextArea]
    public string description;

    [Tooltip("이 퀘스트가 요구하는 조건들. 여러 개를 넣으면 전부 충족돼야 완료된다.")]
    public QuestConditionDefinition[] conditions = new QuestConditionDefinition[0];

    [Header("보상")]
    public int rewardGold;

    [Tooltip("비워두면 아이템 보상 없음.")]
    public ItemDefinitionSO rewardItem;

    [Min(1)]
    public int rewardItemCount = 1;
}
