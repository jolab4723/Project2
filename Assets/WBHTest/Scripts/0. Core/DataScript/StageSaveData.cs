using System;

namespace Core
{
    /// <summary>
    /// 3-4. 스테이지 진행 데이터. 스테이지/레벨 진행 시스템 자체가 아직 프로젝트에 없어서 자리만 잡아둠.
    /// TODO: 스테이지 시스템이 만들어지면 실제 필드(클리어한 스테이지 ID 목록, 최고 기록 등) 채우기.
    ///
    /// WJ 이우진 추가(2026-10-06): 싱글 디스크 모드의 스테이지 선택 맵(예전 stage_map_save.json)을 여기에 담는다.
    /// 캐릭터·인벤토리와 같은 gamesave.json에 있어야 저장 주인(계정/게스트)·클라우드 업로드·실패 시 되돌리기가 함께 적용되고,
    /// 노드 완료와 캐릭터 저장이 한 번의 파일 교체로 확정된다. 읽기·쓰기는 YJ_StageSaveService → DataManager 경로만 쓴다.
    /// </summary>
    [Serializable]
    public class StageSaveData
    {
        /// <summary>맵이 저장돼 있는지. JsonUtility는 빈 객체도 항상 기록하므로 "맵 없음"은 이 값으로 구분한다.</summary>
        public bool hasMap;

        /// <summary>스테이지 선택 맵과 진행(노드·현재 노드·클리어 기록). hasMap이 false면 의미 없는 기본값이다.</summary>
        public StageMapSaveData map = new StageMapSaveData();
    }
}
