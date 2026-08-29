using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WBH_EnemyStatTable
{
    public WBH_EnemyInfo[] enemies;
}

[Serializable]
public sealed class WBH_FloorScalingTable
{
    public WBH_EnemyInfo[] enemies;
}

[Serializable]
public sealed class WBH_FloorScalingRow
{
    public int floor;
    public float maxHpMultiplier = 1f;
    public float atkMultiplier = 1f;
    public float defMultiplier = 1f;
}

[DisallowMultipleComponent] // 중복금지 속성
public class WBH_EnemyDataProvider : MonoBehaviour
{
    [SerializeField] private TextAsset enemyStatJson;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
