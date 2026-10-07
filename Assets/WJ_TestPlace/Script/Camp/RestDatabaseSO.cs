using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캠프 휴식(회복 NPC)의 밸런스 수치를 담는 에셋. 수치는 RestData.xlsx에서만 관리하고
/// DataLoader/Rest Data 파이프라인으로 이 에셋을 생성·갱신한다(UILabelDatabaseSO와 같은 패턴).
/// </summary>
[CreateAssetMenu(fileName = "RestDatabase", menuName = "Data/Rest Database")]
public class RestDatabaseSO : ScriptableObject
{
    public const string DefaultRestId = "default";

    [Serializable]
    public class RestEntry
    {
        public string restId;
        public float baseHealPercent;
        public int creditPerAct;
        public int potionRechargeAmount;
    }

    [SerializeField] private List<RestEntry> restSettings = new List<RestEntry>();

    /// <summary>restId로 설정을 찾는다. 없으면 null.</summary>
    public RestEntry Get(string restId)
    {
        if (string.IsNullOrEmpty(restId))
            return null;

        foreach (RestEntry entry in restSettings)
        {
            if (entry != null && entry.restId == restId)
                return entry;
        }

        return null;
    }

    /// <summary>기본 설정(restId = "default"). 그 행이 없으면 첫 행, 그것도 없으면 null.</summary>
    public RestEntry Default
    {
        get
        {
            RestEntry entry = Get(DefaultRestId);
            if (entry != null)
                return entry;

            return restSettings.Count > 0 ? restSettings[0] : null;
        }
    }
}
