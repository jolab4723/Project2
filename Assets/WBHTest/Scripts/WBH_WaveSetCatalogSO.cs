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

        int validCount = 0;

        foreach(WBH_WaveSetSO candidate in candidates)
        {
            if(candidate != null && candidate.WaveCount > 0)
            {
                validCount++;
            }
        }
        if (validCount == 0)
            return false;

        var random = new System.Random(seed);
        int selectedIndex = random.Next(validCount);

        foreach(WBH_WaveSetSO candidate in candidates)
        {
            if (candidate == null || candidate.WaveCount <= 0)
                continue;
            if (selectedIndex-- != 0)
                continue;

            selected = candidate;
            return true;
        }
        return false;
    }
}
