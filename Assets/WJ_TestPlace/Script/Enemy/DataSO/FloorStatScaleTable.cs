using System.Collections.Generic;
using DataSystem;
using Newtonsoft.Json;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// FloorStatScaleExcelToJson이 만든 JSON(Resources/DataFiles/EnemyData/2. JSONFile/FloorStatScale.json)을
    /// 읽어서 floor로 배율을 조회하는 런타임 유틸리티. EnemyDatabaseSO(적 하나하나의 기본 스탯 SO 모음)와
    /// 역할이 분리되어 있다 - 이쪽은 SO가 아니라 순수 조회 테이블이라 JSON을 그대로 쓴다.
    ///
    /// !! 실제로 적을 생성할 때 EnemyDefinitionSO의 기본 스탯에 이 배율을 곱하는 지점(스폰 로직 연동)은
    ///    이 클래스의 책임 밖이다 - 그쪽에서 GetByFloor(floor)를 불러 곱해서 쓰면 된다.
    ///
    /// 2026-10-01: 시트 키가 (act, floor)가 됐다(맵 층 번호는 액트마다 1부터, 층 수는 11/12/13).
    /// WBH_EnemyStatContext.floor는 int 하나라서, 액트 순서대로 이어 붙인 "누적 번호"를 쓴다
    /// (Act1 1~11 → 1~11, Act2 1~12 → 12~23, Act3 1~13 → 24~36). 스폰 쪽은 맵 노드의 액트·층을
    /// ToContextFloor로 바꿔 컨텍스트에 넣고, GetByFloor는 그 누적 번호로 조회한다.
    /// 액트를 모르는 기존 호출(1층 고정 테스트 등)은 그대로 Act1 1층이 된다.
    /// </summary>
    public static class FloorStatScaleTable
    {
        private const string ResourcesPath = "DataFiles/EnemyData/2. JSONFile/FloorStatScale";

        // 누적 번호(WBH_EnemyStatContext.floor) -> 행
        private static Dictionary<int, FloorStatScaleRow> _byFloor;
        // 액트 -> (그 액트 앞까지의 누적 층 수, 그 액트의 층 수)
        private static Dictionary<int, (int offset, int count)> _actRanges;

        /// <summary>
        /// 맵 노드의 (액트, 그 액트 안의 층)을 WBH_EnemyStatContext.floor에 넣을 누적 번호로 바꾼다.
        /// 표에 없는 액트는 층 번호를 그대로, 표 범위를 넘는 층은 그 액트의 첫/마지막 층으로 맞춘다(경고).
        /// </summary>
        public static int ToContextFloor(int act, int floor)
        {
            EnsureLoaded();

            if (!_actRanges.TryGetValue(act, out var range))
            {
                Debug.LogWarning($"[FloorStatScaleTable] Act{act} 배율 데이터가 없습니다. 층 번호({floor})를 그대로 씁니다.");
                return floor;
            }

            int clamped = Mathf.Clamp(floor, 1, range.count);
            if (clamped != floor)
                Debug.LogWarning($"[FloorStatScaleTable] Act{act} {floor}층은 표 범위(1~{range.count}) 밖이라 {clamped}층 배율을 씁니다.");

            return range.offset + clamped;
        }

        /// <summary>
        /// 누적 번호(ToContextFloor 결과, WBH_EnemyStatContext.floor)로 배율을 조회한다.
        /// 데이터가 없으면 전부 1배(변화 없음)인 기본값을 반환한다.
        /// </summary>
        public static FloorStatScaleRow GetByFloor(int floor)
        {
            EnsureLoaded();

            if (_byFloor.TryGetValue(floor, out FloorStatScaleRow row))
                return row;

            Debug.LogWarning($"[FloorStatScaleTable] {floor}층 배율 데이터가 없습니다. 기본값(1배)을 사용합니다.");
            // multipliers를 비워두면 GetMultiplier가 컬럼별로 알아서 1배(기본값)를 돌려준다.
            return new FloorStatScaleRow { floor = floor };
        }

        /// <summary>테이블이 로드됐는지. 데이터 없이도 GetByFloor는 안전하게 기본값을 주지만, 로드 자체가 실패했는지 확인할 때 쓴다.</summary>
        public static bool IsLoaded
        {
            get
            {
                EnsureLoaded();
                return _byFloor.Count > 0;
            }
        }

        private static void EnsureLoaded()
        {
            if (_byFloor != null)
                return;

            _byFloor = new Dictionary<int, FloorStatScaleRow>();
            _actRanges = new Dictionary<int, (int offset, int count)>();

            TextAsset json = Resources.Load<TextAsset>(ResourcesPath);
            if (json == null)
            {
                Debug.LogWarning($"[FloorStatScaleTable] JSON을 찾을 수 없습니다: Resources/{ResourcesPath}. " +
                                  "DataLoader/Enemy Data/3. Convert FloorStatScale To JSON(또는 0. Run All Steps)을 먼저 실행해주세요.");
                return;
            }

            List<FloorStatScaleRow> rows = JsonConvert.DeserializeObject<List<FloorStatScaleRow>>(json.text);
            if (rows == null)
            {
                Debug.LogError("[FloorStatScaleTable] FloorStatScale.json 파싱에 실패했습니다.");
                return;
            }

            // 액트별 층 수(가장 큰 floor)를 먼저 모아 액트 순서대로 누적 시작 번호를 정한다.
            var countByAct = new SortedDictionary<int, int>();
            foreach (FloorStatScaleRow row in rows)
            {
                if (row == null || row.floor <= 0)
                    continue;
                countByAct[row.act] = countByAct.TryGetValue(row.act, out int count) ? Mathf.Max(count, row.floor) : row.floor;
            }

            int offset = 0;
            foreach (var pair in countByAct)
            {
                _actRanges[pair.Key] = (offset, pair.Value);
                offset += pair.Value;
            }

            foreach (FloorStatScaleRow row in rows)
            {
                if (row == null || row.floor <= 0)
                    continue;

                int key = _actRanges[row.act].offset + row.floor;
                if (_byFloor.ContainsKey(key))
                {
                    Debug.LogWarning($"[FloorStatScaleTable] Act{row.act} {row.floor}층 중복 - 마지막 값으로 덮어씁니다.");
                }

                _byFloor[key] = row;
            }

            foreach (var pair in _actRanges)
            {
                for (int f = 1; f <= pair.Value.count; f++)
                {
                    if (!_byFloor.ContainsKey(pair.Value.offset + f))
                        Debug.LogWarning($"[FloorStatScaleTable] Act{pair.Key} {f}층 행이 비어 있습니다(그 층은 1배).");
                }
            }
        }
    }
}
