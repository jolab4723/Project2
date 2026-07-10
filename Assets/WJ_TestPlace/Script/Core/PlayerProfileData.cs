using System;

namespace Core
{
    /// <summary>
    /// 2. 플레이어 프로필 데이터. 게임플레이 진행 상태(GameplaySaveData)와는 별개로,
    /// 계정/세이브 슬롯 수준의 메타 정보를 담는다.
    /// TODO: 실제로 필요한 필드가 정해지면 채울 것 (지금은 최소한만 잡아둠).
    /// </summary>
    [Serializable]
    public class PlayerProfileData
    {
        public string playerName;
        public string characterClass; // ItemSystem.CharacterClass와 이름 충돌 피하려 문자열로 저장
        public string lastPlayedUtc;  // DateTime.UtcNow.ToString("O") 형태로 저장 예정
    }
}
