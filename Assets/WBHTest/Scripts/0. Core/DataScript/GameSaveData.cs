using System;

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
        public bool needsPlayerInitialization; // 플레이어 초기화(새 게임시 사용)
    }
}
