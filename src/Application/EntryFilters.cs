using System;
using System.Collections.Generic;
using System.Linq;

namespace Application;

public static class EntryFilters
{
    private static readonly int[] FundingHours = new[] { 0, 8, 16 };

    public static bool IsFundingBlackout(DateTimeOffset timeUtc, int windowMinutes)
    {
        if (windowMinutes <= 0)
            return false;

        foreach (var dayOffset in new[] { -1, 0, 1 })
        {
            var date = timeUtc.Date.AddDays(dayOffset);
            foreach (var hour in FundingHours)
            {
                var funding = new DateTimeOffset(date, TimeSpan.Zero).AddHours(hour);
                var diff = Math.Abs((timeUtc - funding).TotalMinutes);
                if (diff <= windowMinutes)
                    return true;
            }
        }

        return false;
    }

    public static decimal PercentileRank(IReadOnlyList<decimal> values, decimal current)
    {
        if (values.Count == 0)
            return 0m;

        var less = values.Count(v => v < current);
        var equal = values.Count(v => v == current);
        return ((less + 0.5m * equal) / values.Count) * 100m;
    }
}

