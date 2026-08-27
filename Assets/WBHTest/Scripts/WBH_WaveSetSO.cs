using System;
using System.Collections.Generic;
using UnityEngine;

// 1스테이지에서 웨이브 횟수와 웨이브별 적 등급과 소환 숫자를 설정. 실질적인 적 종류(근접, 원거리, 자폭병 등)은 SpawnArea에서 등급 내에서 랜덤하게 결정된다.
[Serializable]
public sealed class WBH_WaveGradeCount
{
    public EnemyGrade grade;
    [Min(0)] public int count;
}

[Serializable]
public sealed class WBH_WaveData
{
    public WBH_WaveGradeCount[] enemies;
}

[CreateAssetMenu(fileName = "WaveSet_", menuName = "WBH/Enemy/Wave Set")]

public sealed class WBH_WaveSetSO : ScriptableObject
{
    [SerializeField] private string waveSetId;
    [SerializeField] private WBH_WaveData[] waves;

    public string WaveSetId => waveSetId;
    public IReadOnlyList<WBH_WaveData> Waves => waves;
    public int WaveCount => waves != null ? waves.Length : 0;

    public WBH_WaveData GetWave(int index)
    {
        if (waves == null || index < 0 || index >= waves.Length)
            return null;

        return waves[index];
    }
}
