using System;
using UnityEngine;

// 최종적으로 YJ_StageManager 에 연결할 SO. 엘리트 스테이지와 노말 스테이지를 구분하고 각 스테이지에 맞는 웨이브를 소환.
[CreateAssetMenu(fileName = "WaveSetCatalog",
                 menuName = "WBH/Enemy/Wave Set Catalog")]

public sealed class WBH_WaveSetCatalogSO : ScriptableObject
{
    [SerializeField] private WBH_WaveSetSO[] normalWaveSets;
    [SerializeField] private WBH_WaveSetSO[] eliteWaveSets;

    public bool TrySelect(bool eliteStage, int seed, out WBH_WaveSetSO selected)
    {
        WBH_WaveSetSO[] candidates = eliteStage ? eliteWaveSets : normalWaveSets;

        selected = null;

        if (candidates == null || candidates.Length == 0)
            return false;

        var random = new System.Random(seed);

        selected = candidates[random.Next(candidates.Length)];
        return selected != null;
    }
}
