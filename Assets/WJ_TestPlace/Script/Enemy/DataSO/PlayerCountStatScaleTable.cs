using System.Collections.Generic;
using DataSystem;
using Newtonsoft.Json;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// PlayerCountStatScaleExcelToJson이 만든 JSON(Resources/DataFiles/EnemyData/2. JSONFile/PlayerCountStatScale.json)을
    /// 읽어서 접속 인원 수로 배율을 조회하는 런타임 유틸리티. FloorStatScaleTable과 역할이 같되
    /// 키가 층(floor)이 아니라 인원 수(playerCount)다.
    ///
    /// !! 실제로 적을 생성할 때 EnemyDefinitionSO의 기본 스탯에 이 배율을 곱하는 지점(스폰 로직 연동)은
    ///    이 클래스의 책임 밖이다 - 그쪽에서 GetByPlayerCount(playerCount)를 불러 곱해서 쓰면 된다.
    /// </summary>
    public static class PlayerCountStatScaleTable
    {
        private const string ResourcesPath = "DataFiles/EnemyData/2. JSONFile/PlayerCountStatScale";

        private static Dictionary<int, PlayerCountStatScaleRow> _byPlayerCount;

        /// <summary>해당 인원 수의 배율을 조회한다. 데이터가 없으면 전부 1배(변화 없음)인 기본값을 반환한다.</summary>
        public static PlayerCountStatScaleRow GetByPlayerCount(int playerCount)
        {
            EnsureLoaded();

            if (_byPlayerCount.TryGetValue(playerCount, out PlayerCountStatScaleRow row))
                return row;

            Debug.LogWarning($"[PlayerCountStatScaleTable] 인원 수 {playerCount}에 대한 배율 데이터가 없습니다. 기본값(1배)을 사용합니다.");
            // multipliers를 비워두면 GetMultiplier가 컬럼별로 알아서 1배(기본값)를 돌려준다.
            return new PlayerCountStatScaleRow { playerCount = playerCount };
        }

        /// <summary>테이블이 로드됐는지. 데이터 없이도 GetByPlayerCount는 안전하게 기본값을 주지만, 로드 자체가 실패했는지 확인할 때 쓴다.</summary>
        public static bool IsLoaded
        {
            get
            {
                EnsureLoaded();
                return _byPlayerCount.Count > 0;
            }
        }

        private static void EnsureLoaded()
        {
            if (_byPlayerCount != null)
                return;

            _byPlayerCount = new Dictionary<int, PlayerCountStatScaleRow>();

            TextAsset json = Resources.Load<TextAsset>(ResourcesPath);
            if (json == null)
            {
                Debug.LogWarning($"[PlayerCountStatScaleTable] JSON을 찾을 수 없습니다: Resources/{ResourcesPath}. " +
                                  "DataLoader/Enemy Data/5. Convert PlayerCountStatScale To JSON(또는 0. Run All Steps)을 먼저 실행해주세요.");
                return;
            }

            List<PlayerCountStatScaleRow> rows = JsonConvert.DeserializeObject<List<PlayerCountStatScaleRow>>(json.text);
            if (rows == null)
            {
                Debug.LogError("[PlayerCountStatScaleTable] PlayerCountStatScale.json 파싱에 실패했습니다.");
                return;
            }

            foreach (PlayerCountStatScaleRow row in rows)
            {
                if (_byPlayerCount.ContainsKey(row.playerCount))
                {
                    Debug.LogWarning($"[PlayerCountStatScaleTable] 인원 수 {row.playerCount} 중복 - 마지막 값으로 덮어씁니다.");
                }

                _byPlayerCount[row.playerCount] = row;
            }
        }
    }
}
