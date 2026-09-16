using HighlightPlus;
using System;

[Serializable]
public sealed class WBH_WavePercentRange
{
    public int minPercent = 15;
    public int maxPercent = 25;
}

public class WBH_WaveCountBuilder
{
    public static bool TryDistribute(int total, int waveCount, WBH_WavePercentRange[] ranges, int seed, out int[] counts, out string error)
    {
        counts = null;
        error = null;

        if(waveCount <= 0  || total < waveCount || ranges == null || ranges.Length != waveCount)
        {
            error = "웨이브 수, 전체 적 수 또는 비율 배열을 확인하세요.";
            return false;
        }

        var min = new long[waveCount];
        var max = new long[waveCount];
        var minSuffix = new long[waveCount + 1];
        var maxSuffix = new long[waveCount + 1];

        for(int i = waveCount -1; i >=0; i--)
        {
            var range = ranges[i];

            if(range == null || range.minPercent < 0 || range.maxPercent > 100 || range.minPercent > range.maxPercent)
            {
                error = $"{i + 1}웨이브의 비율 설정이 잘못됐습니다.";
                return false;
            }

            // 최소는 올림, 최대는 내림하여 int 로
            min[i] = Math.Max(1L, ((long)total * range.minPercent + 99) / 100);
            max[i] = (long)total * range.maxPercent / 100;

            if (min[i] > max[i])
            {
                error = $"{i + 1} 웨이브의 비율을 만족하는 정수가 없습니다.";
                return false;
            }

            minSuffix[i] = minSuffix[i + 1] + min[i];
            maxSuffix[i] = maxSuffix[i + 1] + max[i];
        }

        if(total < minSuffix[0] || total > maxSuffix[0])
        {
            error = "전체 적 수를 웨이브별 비율로 분배할 수 없습니다.";
            return false;
        }

        var random = new Random(seed);
        counts = new int[waveCount];
        long remaining = total;

        for(int i = 0; i < waveCount; i++)
        {
            long low = Math.Max(min[i], remaining - maxSuffix[i + 1]);
            long high = Math.Min(max[i], remaining - minSuffix[i + 1]);

            int count = (int)(low + (long)(random.NextDouble() * (high - low + 1)));

            counts[i] = count;
            remaining -= count;
        }
        return true;
    }
}
