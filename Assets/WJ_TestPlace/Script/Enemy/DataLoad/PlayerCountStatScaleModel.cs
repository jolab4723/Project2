using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DataSystem
{
    /// <summary>
    /// EnemyData.xlsx의 playerCountStatScale 시트 한 줄. 접속 인원 수(playerCount)별로
    /// EnemyDefinitionSO의 기본 스탯에 곱해줄 배율을 담는다 - FloorStatScaleRow/DifficultyStatScaleRow와
    /// 같은 구조이되 키가 인원 수(int)다. SO로 만들지 않고 JSON 그대로 두고 런타임에
    /// PlayerCountStatScaleTable이 읽는다.
    ///
    /// playerCount를 뺀 나머지 배율 컬럼(hpMultiplierForPlayerCount 등)은 고정된 필드가 아니라 컬럼
    /// 이름 그대로 multipliers 딕셔너리에 담긴다 - 엑셀에 새 배율 컬럼을 추가하고 파이프라인만 다시
    /// 돌리면 코드 수정 없이 자동으로 반영된다(PlayerCountStatScaleExcelToJson 참고).
    /// </summary>
    [Serializable]
    public class PlayerCountStatScaleRow
    {
        public int playerCount;

        /// <summary>playerCount를 뺀 나머지 모든 배율 컬럼. 키는 엑셀 헤더 이름 그대로(예: "hpMultiplierForPlayerCount").</summary>
        public Dictionary<string, float> multipliers = new Dictionary<string, float>();

        /// <summary>컬럼 이름으로 배율을 조회한다. 없으면 배율 없음(1배)으로 취급한다.</summary>
        public float GetMultiplier(string columnName, float defaultValue = 1f) =>
            multipliers != null && multipliers.TryGetValue(columnName, out float value) ? value : defaultValue;

        // [JsonIgnore]: multipliers와 값이 중복되므로 JSON에는 안 실리게 한다.
        [JsonIgnore] public float HpMultiplier => GetMultiplier("hpMultiplierForPlayerCount");
    }
}
