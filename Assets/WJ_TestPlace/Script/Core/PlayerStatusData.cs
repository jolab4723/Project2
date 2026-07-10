using System;

namespace Core
{
    /// <summary>3-3. 플레이어 스테이터스 세이브 데이터 (레벨/경험치/체력/마나/골드).</summary>
    [Serializable]
    public class PlayerStatusData
    {
        public int playerLevel;
        public float playerExp;
        public int currentHealth;
        public float currentMana;
        public int gold;
    }
}
