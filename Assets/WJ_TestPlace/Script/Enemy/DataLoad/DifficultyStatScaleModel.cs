using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DataSystem
{
    /// <summary>
    /// EnemyData.xlsx의 difficultyStatScale 시트 한 줄. 난이도(difficultyName)별로 EnemyDefinitionSO의
    /// 기본 스탯에 곱해줄 배율을 담는다 - FloorStatScaleRow와 같은 구조이되 키가 층(floor)이 아니라
    /// 난이도 이름이다. SO로 만들지 않고 JSON 그대로 두고 런타임에 DifficultyStatScaleTable이 읽는다.
    ///
    /// 2026-09-04 엑셀 개편 전에는 FloorStatScale 시트 안에 difficulty 컬럼이 섞여 있었으나
    /// (값이 바뀌는 행에만 적고 아래는 빈칸으로 이어받는 carry-forward 방식), 층 배율과 난이도 배율이
    /// 서로 다른 축이라 별도 시트/모델로 분리됐다.
    ///
    /// difficultyName을 뺀 나머지 배율 컬럼(hpMultiplier 등)은 고정된 필드가 아니라 컬럼 이름 그대로
    /// multipliers 딕셔너리에 담긴다 - 엑셀에 새 배율 컬럼을 추가하고 파이프라인만 다시 돌리면 코드
    /// 수정 없이 자동으로 반영된다(DifficultyStatScaleExcelToJson 참고).
    /// </summary>
    [Serializable]
    public class DifficultyStatScaleRow
    {
        public string difficultyName;

        /// <summary>difficultyName을 뺀 나머지 모든 배율 컬럼. 키는 엑셀 헤더 이름 그대로(예: "hpMultiplier").</summary>
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
