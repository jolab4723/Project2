using System;
using System.Collections.Generic;

namespace Core
{
    /// <summary>3-1. 인벤토리 세이브 데이터. 그리드에 있는 아이템 + 장착된 아이템을 모두 포함한다.</summary>
    [Serializable]
    public class InventorySaveData
    {
        // 0은 크기 정보가 없는 구형 저장이다. 임의 크기로 보상을 배치하지 않는다.
        public int gridWidth;
        public int gridHeight;
        public List<ItemSaveData> items = new List<ItemSaveData>();
    }
}
