using System;
using System.Collections.Generic;

namespace DataSystem
{
    [Serializable]
    public class RestDataJsonData
    {
        public List<RestDataRow> restSettings = new List<RestDataRow>();
    }

    /// <summary>
    /// RestData.xlsx의 Rest 시트 한 줄. 캠프 휴식(회복 NPC)의 밸런스 수치를 담는다.
    /// 지금은 default 한 줄만 쓰지만, Act별로 비용을 다르게 하고 싶으면 restId를 나눠 행을 추가하면 된다.
    /// memo 컬럼은 시트를 읽는 사람을 위한 설명이라 여기(코드)로는 가져오지 않는다.
    /// </summary>
    [Serializable]
    public class RestDataRow
    {
        public string restId;

        /// <summary>캠프 회복 증가 패시브(CampHealBonus) 미해금 시 쓰는, 최대 체력 대비 회복 비율(%).</summary>
        public float baseHealPercent;

        /// <summary>휴식 1회 비용 = 이 값 × 현재 액트 번호(Act1=1, Act2=2, Act3=3).</summary>
        public int creditPerAct;

        /// <summary>휴식 1회로 채우는 포션 충전 수(최대 충전량을 넘지 않는다).</summary>
        public int potionRechargeAmount;
    }
}
