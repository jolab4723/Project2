using System;
using System.Collections.Generic;

namespace Core
{
    /// <summary>
    /// 멀티플레이 세이브 슬롯. 다크소울 스타일로 3개가 서로 완전히 독립된 파티/캐릭터용.
    /// 공유 파티 세이브 방식 - 게스트의 프로필(패시브 스킬/크레딧 등)도 전부 이 호스트 슬롯 안에
    /// 통째로 저장된다. 게스트는 이 슬롯 밖에서는 독립적으로 존재하지 않는다.
    /// 모든 참가자의 세이브/로드는 호스트가 담당한다.
    /// </summary>
    [Serializable]
    public class MultiplayerSlotData
    {
        /// <summary>게스트가 참가할 때 입력하는 방 ID.</summary>
        public string roomId;

        public PlayerProfileData hostProfile = new PlayerProfileData();

        /// <summary>참가한 게스트들. 최대 3명 (호스트 포함 최대 4인).</summary>
        public List<GuestEntry> guests = new List<GuestEntry>();
    }

    [Serializable]
    public class GuestEntry
    {
        /// <summary>게스트 기기에서 생성된 고유 ID. 재접속 시 같은 프로필을 찾는 데 사용.</summary>
        public string guestPlayerId;
        public PlayerProfileData profile = new PlayerProfileData();
    }
}
