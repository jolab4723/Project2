using System;

namespace Core
{
    /// <summary>
    /// 플레이어 프로필 데이터. 캐릭터 하나의 영구 진행 상태(게임 세션과 무관하게 유지).
    /// SinglePlayerSlotData/MultiplayerSlotData 양쪽에서 재사용된다.
    /// 게임플레이 진행 상태(GameSaveData, 런 단위 - 인벤토리/스테이터스 등)와는 별개.
    /// </summary>
    [Serializable]
    public class PlayerProfileData
    {
        /// <summary>기기에서 자동 생성된 GUID. 계정 시스템이 없어서 이걸 고유 식별자로 사용.</summary>
        public string playerId;
        public string playerName;
        public string characterClass; // ItemSystem.CharacterClass와 이름 충돌 피하려 문자열로 저장
        public float playTimeSeconds;

        /// <summary>영구 크레딧. 새 프로필 기본값은 2000. 런 종료 시 PlayerStatusData.gold(런 전용)가 여기 더해진다. 스킬 포인트 구매에 사용.</summary>
        public int credit = 2000;


        public PassiveSkillTreeData passiveSkillTree = new PassiveSkillTreeData();

        public string lastPlayedUtc; // DateTime.UtcNow.ToString("O") 형태로 저장

        public void ApplyCredit(int creditAmount)
        {
            credit += creditAmount;
        }
    }
}
