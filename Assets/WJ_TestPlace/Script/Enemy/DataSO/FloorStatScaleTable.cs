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
    /// </summary>
    public static class FloorStatScaleTable
    {
        private const string ResourcesPath = "DataFiles/EnemyData/2. JSONFile/FloorStatScale";

        private static Dictionary<int, FloorStatScaleRow> _byFloor;

        /// <summary>해당 층의 배율을 조회한다. 데이터가 없으면 전부 1배(변화 없음)인 기본값을 반환한다.</summary>
        public static FloorStatScaleRow GetByFloor(int floor)
        {
            EnsureLoaded();

            if (_byFloor.TryGetValue(floor, out FloorStatScaleRow row))
                return row;

            Debug.LogWarning($"[FloorStatScaleTable] {floor}층 배율 데이터가 없습니다. 기본값(1배)을 사용합니다.");
            return new FloorStatScaleRow
            {
                difficulty = 1f,
                floor = floor,
                hpMultiplier = 1f,
                atkMultiplier = 1f,
                defMultiplier = 1f,
                expMultiplier = 1f,
                creditMultiplier = 1f,
            };
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

            foreach (FloorStatScaleRow row in rows)
            {
                if (_byFloor.ContainsKey(row.floor))
                {
                    Debug.LogWarning($"[FloorStatScaleTable] floor {row.floor} 중복 - 마지막 값으로 덮어씁니다.");
                }

                _byFloor[row.floor] = row;
            }
        }
    }
}
