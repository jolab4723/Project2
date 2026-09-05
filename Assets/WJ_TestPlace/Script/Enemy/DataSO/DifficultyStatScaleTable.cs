using System.Collections.Generic;
using DataSystem;
using Newtonsoft.Json;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// DifficultyStatScaleExcelToJson이 만든 JSON(Resources/DataFiles/EnemyData/2. JSONFile/DifficultyStatScale.json)을
    /// 읽어서 난이도 이름으로 배율을 조회하는 런타임 유틸리티. FloorStatScaleTable과 역할이 같되
    /// 키가 층(int)이 아니라 난이도 이름(string, 대소문자 무시)이다.
    ///
    /// !! 실제로 적을 생성할 때 EnemyDefinitionSO의 기본 스탯에 이 배율을 곱하는 지점(스폰 로직 연동)은
    ///    이 클래스의 책임 밖이다 - 그쪽에서 GetByDifficulty(difficultyName)을 불러 곱해서 쓰면 된다.
    /// </summary>
    public static class DifficultyStatScaleTable
    {
        private const string ResourcesPath = "DataFiles/EnemyData/2. JSONFile/DifficultyStatScale";

        private static Dictionary<string, DifficultyStatScaleRow> _byName;

        /// <summary>해당 난이도의 배율을 조회한다(대소문자 무시). 데이터가 없으면 전부 1배(변화 없음)인 기본값을 반환한다.</summary>
        public static DifficultyStatScaleRow GetByDifficulty(string difficultyName)
        {
            EnsureLoaded();

            string key = (difficultyName ?? string.Empty).Trim();
            if (_byName.TryGetValue(key, out DifficultyStatScaleRow row))
                return row;

            Debug.LogWarning($"[DifficultyStatScaleTable] 난이도 '{difficultyName}' 배율 데이터가 없습니다. 기본값(1배)을 사용합니다.");
            // multipliers를 비워두면 GetMultiplier가 컬럼별로 알아서 1배(기본값)를 돌려준다.
            return new DifficultyStatScaleRow { difficultyName = difficultyName };
        }

        /// <summary>테이블이 로드됐는지. 데이터 없이도 GetByDifficulty는 안전하게 기본값을 주지만, 로드 자체가 실패했는지 확인할 때 쓴다.</summary>
        public static bool IsLoaded
        {
            get
            {
                EnsureLoaded();
                return _byName.Count > 0;
            }
        }

        private static void EnsureLoaded()
        {
            if (_byName != null)
                return;

            _byName = new Dictionary<string, DifficultyStatScaleRow>(System.StringComparer.OrdinalIgnoreCase);

            TextAsset json = Resources.Load<TextAsset>(ResourcesPath);
            if (json == null)
            {
                Debug.LogWarning($"[DifficultyStatScaleTable] JSON을 찾을 수 없습니다: Resources/{ResourcesPath}. " +
                                  "DataLoader/Enemy Data/4. Convert DifficultyStatScale To JSON(또는 0. Run All Steps)을 먼저 실행해주세요.");
                return;
            }

            List<DifficultyStatScaleRow> rows = JsonConvert.DeserializeObject<List<DifficultyStatScaleRow>>(json.text);
            if (rows == null)
            {
                Debug.LogError("[DifficultyStatScaleTable] DifficultyStatScale.json 파싱에 실패했습니다.");
                return;
            }

            foreach (DifficultyStatScaleRow row in rows)
            {
                if (_byName.ContainsKey(row.difficultyName))
                {
                    Debug.LogWarning($"[DifficultyStatScaleTable] 난이도 '{row.difficultyName}' 중복 - 마지막 값으로 덮어씁니다.");
                }

                _byName[row.difficultyName] = row;
            }
        }
    }
}
