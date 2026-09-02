using EnemySystem;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent] // 중복금지 속성
public class WBH_EnemyDataProvider : MonoBehaviour
{
    [Header("Generated Enemy Data")]
    [SerializeField] private EnemyDatabaseSO enemyDatabase;
    //[SerializeField] private 


    private bool isLoaded;

    private void Awake()
    {
        Load();
    }

    //public bool TryCreateEnemyInfo(int runtimeId, int floor, out WBH_EnemyInfo result)
    //{
    //    result = null;

    //    if(!isLoaded && !Load())
    //        return false;

    //    if(!enemiesById.TryGetValue(runtimeId, out WBH_EnemyDataRow enemyRow))
    //    {
    //        Log.Error($"EnemyData 에 {runtimeId} 가 없습니다.");
    //        return false;
    //    }

    //    if(!scalesByFloor.TryGetValue(floor, out WBH_FloorStatScaleRow floorScale))
    //    {
    //        Log.Error($"FloorStatScale 에 {floor}층 데이터가 없습니다.");
    //        return false;
    //    }
    //    if(!Enum.TryParse(enemyRow.enemyGrade, true, out EnemyGrade enemyGrade))
    //    {
    //        Log.Error($"잘못된 enemyGrade 입니다. ID = {runtimeId}, 값 = {enemyRow.enemyGrade}");
    //        return false;
    //    }
    //    if(!Enum.TryParse(enemyRow.attackType, true, out EnemyType enemyType))
    //    {
    //        Log.Error($"잘못된 attackType 입니다. ID = {runtimeId}, 값 = {enemyRow.attackType}");
    //        return false;
    //    }

    //    result = new WBH_EnemyInfo
    //    {
    //        id = enemyRow.runtimeId,
    //        enemyName = enemyRow.enemyId, // !@ 차후 EnemyLabel

    //    }
    //}

    private void Load()
    {

    }
}
