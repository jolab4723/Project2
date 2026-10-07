using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DataSystem
{
    /// <summary>
    /// EnemyData.xlsx의 FloorStatScale 시트 한 줄. 층(floor)별로 EnemyDefinitionSO의 기본 스탯에
    /// 곱해줄 배율을 담는다 - SO로 만들지 않고 JSON 그대로 두고 런타임에 FloorStatScaleTable이 읽는다
    /// (적 하나당 SO를 만드는 EnemyData와 달리, 층 배율은 단순 조회 테이블이라 SO화할 필요가 없음).
    ///
    /// 난이도(difficulty)는 더 이상 이 시트가 아니라 별도 difficultyStatScale 시트/DifficultyStatScaleRow로
    /// 분리됐다(2026-09-04 엑셀 개편) - 이 시트는 층만 키로 쓴다.
    ///
    /// 2026-10-01: 맵의 층 번호는 액트마다 1부터 다시 시작하고 층 수도 액트마다 달라서(11/12/13),
    /// 키를 (act, floor)로 바꿨다. act 열이 없는 예전 시트는 전부 Act1로 읽는다.
    ///
    /// act/floor를 뺀 나머지 배율 컬럼(hpMultiplier 등)은 고정된 필드가 아니라 컬럼 이름 그대로
    /// multipliers 딕셔너리에 담긴다 - 엑셀에 새 배율 컬럼을 추가하고 파이프라인만 다시 돌리면 코드
    /// 수정 없이 자동으로 반영된다(FloorStatScaleExcelToJson 참고).
    /// </summary>
    [Serializable]
    public class FloorStatScaleRow
    {
        /// <summary>액트 번호(1~3). 예전 JSON처럼 값이 없으면 1.</summary>
        public int act = 1;

        /// <summary>그 액트 안의 층 번호(1부터). 맵 노드의 floor와 같은 기준이다.</summary>
        public int floor;

        /// <summary>act/floor를 뺀 나머지 모든 배율 컬럼. 키는 엑셀 헤더 이름 그대로(예: "hpMultiplier").</summary>
        public Dictionary<string, float> multipliers = new Dictionary<string, float>();

        /// <summary>컬럼 이름으로 배율을 조회한다. 없으면 배율 없음(1배)으로 취급한다.</summary>
        public float GetMultiplier(string columnName, float defaultValue = 1f) =>
            multipliers != null && multipliers.TryGetValue(columnName, out float value) ? value : defaultValue;

        // 자주 쓰는 배율은 이름으로 바로 접근할 수 있게 편의 프로퍼티를 둔다 - 값 자체는 multipliers에
        // 그대로 있고 이건 그걸 그대로 읽어오는 것뿐이라, 엑셀에 이 컬럼이 없어져도 그냥 1배로 동작한다.
        // [JsonIgnore]: multipliers와 값이 중복되므로 JSON에는 안 실리게 한다.
        [JsonIgnore] public float HpMultiplier => GetMultiplier("hpMultiplier");
        [JsonIgnore] public float AtkMultiplier => GetMultiplier("atkMultiplier");
        [JsonIgnore] public float DefMultiplier => GetMultiplier("defMultiplier");
        [JsonIgnore] public float ExpMultiplier => GetMultiplier("expMultiplier");
        [JsonIgnore] public float CreditMultiplier => GetMultiplier("creditMultiplier");
    }
}
