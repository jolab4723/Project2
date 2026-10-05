using System;
using System.Collections.Generic;
using ItemSystem;

namespace Core
{
    /// <summary>
    /// 3. 게임 플레이 데이터 전체를 묶는 세이브 컨테이너. JsonUtility로 직렬화/역직렬화된다.
    /// 하위 카테고리(3-1~3-4)는 각자 별도로도 세이브/로드할 수 있지만, 이 클래스는 그 전부를
    /// 한 번에 저장할 때("전체 저장") 쓰는 묶음이다.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        public PlayerStatusData status = new PlayerStatusData();     // 3-3
        public InventorySaveData inventory = new InventorySaveData(); // 3-1
        public SkillTreeSaveData skillTree = new SkillTreeSaveData(); // 3-2 (자리만 잡아둠)
        public StageSaveData stage = new StageSaveData();             // 3-4 (자리만 잡아둠)
        public ActiveSkillSaveData activeSkill = new ActiveSkillSaveData(); // 3-5
        // SW 수정 : 보상 지급 결과와 인벤토리·크레딧을 같은 파일 교체로 확정한다. null은 구형 저장이다.
        public QuestSaveData quests;
        public bool needsPlayerInitialization; // 플레이어 초기화(새 게임시 사용)
        public CharacterClass selectedCharacter = CharacterClass.Fighter; // 이번 게임에서 사용할 클래스
        public List<UnknownStageChoiceRecord> unknownStageChoices = new();
        public List<UnknownStageBuffRecord> unknownStageBuffs = new();
        public string lastCompletedUnknownBattleKey; // 클리어 저장 후 포탈 이동 전 재로드를 구분한다.
    }

    [Serializable]
    public class UnknownStageChoiceRecord
    {
        public string nodeKey; // Act + 맵 seed + 노드 ID. 새 게임은 목록 자체를 초기화한다.
        public string stageId;
        public int choiceIndex;
    }

    [Serializable]
    public class UnknownStageBuffRecord
    {
        public string effectKey; // nodeKey + choiceIndex + effectIndex
        public string stageId;
        public string displayName;
        public YJ_UnknownEffectLifetime lifetime; // 기본값 ThisRun: 기존 저장과 호환.
        public string battleKey; // NextBattle: 비어 있으면 대기, 값이 있으면 해당 전투 전용.
        // 선택 당시 수치의 복제본. 이후 SO 편집이 이미 받은 보상을 바꾸지 않는다.
        public FixedStatValue[] statEffects;
    }
}
