using System;

namespace Core
{
    /// <summary>3-3. 플레이어 스테이터스 세이브 데이터 (레벨/경험치/체력/마나/크레딧).</summary>
    [Serializable]
    public class PlayerStatusData
    {
        public int playerLevel;
        public float playerExp;
        public float currentHealth;
        public float currentMana;
        public int gold;
        // WJ 이우진 추가(2026-10-07): 남은 포션 사용 횟수. -1은 저장값 없음(새 게임·이전 저장)이라 최대치로 시작한다.
        public int potionCharges = -1;
    }
}
