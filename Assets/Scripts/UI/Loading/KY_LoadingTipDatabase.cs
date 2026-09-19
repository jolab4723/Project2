using UnityEngine;

/// <summary>
/// 로딩 화면에서 표시할 팁 목록이다.
/// 이미지는 Resources 경로로, 문구는 항목별로 한 곳에서 관리한다.
/// </summary>
public static class KY_LoadingTipDatabase
{
    public readonly struct Entry
    {
        public readonly string ImageResourcePath;
        public readonly string Title;
        public readonly string Detail;

        public Entry(string imageResourcePath, string title, string detail)
        {
            ImageResourcePath = imageResourcePath;
            Title = title;
            Detail = detail;
        }
    }

    private static readonly Entry[] Entries =
    {
        new(
            "Images/LoadingImage/LoadingImage_1",
            "데브리스-9",
            "이곳은 오염으로 인해 인간이 살 수 없는 버려진 땅이었지만, 수백 년간 쌓인 로봇 잔해들이 스스로를 복구하며 기괴한 메카 생태계가 만들어졌습니다."
            ),
        new(
            "Images/LoadingImage/LoadingImage_2",
            "스캐빈저",
            "이 위험천만한 행성에 뛰어들어 폭주하는 로봇들을 부수고 부품을 쓸어 담는 자들을 은하계에서는 스캐빈저라고 부릅니다."
            ),
        new(
            "Images/LoadingImage/LoadingImage_3",
            "연산 코어",
            "로봇들이 품고 있는 희귀 연산 코어는 블랙 마켓에서 높은 가격에 거래됩니다."
            ),
        new(
            "Images/LoadingImage/LoadingImage_4",
            "더 깊은 곳으로",
            "쓸 만한 것은 거의 남지 않았습니다. 행성의 겉표면은 이미 휩쓸고 지나가 고철밖에 남지 않았습니다.\n돈을 벌기 위해서는 더 깊은 곳으로 들어가야 합니다."
            ),
        new(
            "Images/LoadingImage/LoadingImage_5",
            "크레딧",
            "크레딧은 은하계 전역에서 사용되는 표준 디지털 화폐입니다.\n중앙 정부와 거대 기업 연합이 공동으로 관리하는 통합 결제 시스템으로, 대부분의 행성과 우주 정거장에서 통용됩니다."
            ),
        new(
            "Images/LoadingImage/LoadingImage_6",
            "서기 3026",
            "무분별한 산업 확장과 전쟁으로 끔찍한 자원 고갈이 심화되었습니다.\n이제 가치 없다고 여겨진 고철 사이에서 쓸 만한 것을 찾아내야 합니다."
            ),
    };

    public static int Count => Entries.Length;
    
    /// <summary>인덱스에 맞는 로딩 팁을 반환한다.</summary>
    public static Entry Get(int index)
    {
        return Entries[Mathf.Clamp(index, 0, Entries.Length - 1)];
    }
}
