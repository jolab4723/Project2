using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ItemSystem;
using Mirror.BouncyCastle.Asn1.BC;

namespace Core
{
    public class DataManager : Singleton<DataManager>, IManagerModule
    {
        [Header("정적 데이터")]
        [Tooltip("모든 아이템 정의를 itemId로 조회할 수 있는 데이터베이스")]
        [SerializeField] private ItemDatabaseSO itemDatabase;

        public ItemDatabaseSO ItemDatabase => itemDatabase;
        public bool IsStaticDataLoaded { get; private set; }
        public string ModuleName => "DataManager";

        #region Json const Name

        private const string ItemStatPoolFileName = "ItemStatPool.json";

        #endregion


        // DataManager 활성화

        public void Activate()
        {
            if (itemDatabase == null)
            {
                Debug.LogWarning("[DataManager] itemDatabase가 연결되지 않았습니다.");
                IsStaticDataLoaded = false;
                return;
            }

            LoadAllData();

            IsStaticDataLoaded = true;
            Debug.Log("[DataManager] 활성화 완료 (itemDatabase 연결됨, 아이템 " + itemDatabase.allItems.Count + "개)");
        }

        #region Load Data

        public void LoadAllData()
        {
            LoadItemData();
        }

        // 아이템 데이터 로드
        private void LoadItemData()
        {
            // 1. 아이템 데이터 Excel (또는 FireBase) -> Json 변환
            DataSystem.ItemTableExcelToJson.ConvertExcelToJsonFromMenu();

            // 2. 아이템 데이터 Json -> SO 변환

        }

        #endregion

        #region SaveData

        public void SaveAllData()
        {

        }

        #endregion



    }
}
