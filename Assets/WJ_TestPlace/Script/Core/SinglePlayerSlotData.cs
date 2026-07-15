using System;

namespace Core
{
    /// <summary>싱글플레이 세이브 슬롯. 프로젝트에서 고정 1개만 사용.</summary>
    [Serializable]
    public class SinglePlayerSlotData
    {
        public PlayerProfileData profile = new PlayerProfileData();
    }
}
