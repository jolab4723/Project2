using System;
using System.Collections.Generic;

namespace Core
{
    /// <summary>3-1. 인벤토리 세이브 데이터. 그리드에 있는 아이템 + 장착된 아이템을 모두 포함한다.</summary>
    [Serializable]
    public class InventorySaveData
    {
        public List<ItemSaveData> items = new List<ItemSaveData>();
    }
}
