using System;
using System.Collections.Generic;

namespace GalCompanion
{
    internal sealed class HeatmapCell
    {
        public DateTime Date;
        public long Seconds;

        /// <summary>0＝遊んでいない、1〜4 が濃さ。</summary>
        public int Level;

        public int Week;
        public int Day;
    }

    /// <summary>
    /// GitHub の草と同じ並べ方。週が列、曜日が行。
    /// 描画と切り離してあるので、日付の詰め方だけをテストできる。
    /// </summary>
    internal static class HeatmapLayout
    {
        public const int Days = 7;

        public static int Level(long seconds, long max)
        {
            if (seconds <= 0)
            {
                return 0;
            }
            if (max <= 0)
            {
                return 1;
            }
            var ratio = (double)seconds / max;
            if (ratio <= 0.25) return 1;
            if (ratio <= 0.50) return 2;
            if (ratio <= 0.75) return 3;
            return 4;
        }

        /// <summary>
        /// 最後の週に endDate が入るように、weeks 週ぶんを並べる。
        /// 週の始まりは日曜。列の高さを揃えるため端の空きも欠かさず埋める。
        /// </summary>
        public static List<HeatmapCell> Build(
            Dictionary<DateTime, long> daily, DateTime endDate, int weeks,
            DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)
        {
            var cells = new List<HeatmapCell>();
            if (weeks <= 0)
            {
                return cells;
            }

            var end = endDate.Date;
            var offset = ((int)end.DayOfWeek - (int)firstDayOfWeek + Days) % Days;
            var lastWeekStart = end.AddDays(-offset);
            var firstWeekStart = lastWeekStart.AddDays(-(weeks - 1) * Days);

            long max = 0;
            foreach (var pair in daily)
            {
                if (pair.Key >= firstWeekStart && pair.Key <= end && pair.Value > max)
                {
                    max = pair.Value;
                }
            }

            for (var week = 0; week < weeks; week++)
            {
                for (var day = 0; day < Days; day++)
                {
                    var date = firstWeekStart.AddDays(week * Days + day);
                    if (date > end)
                    {
                        break;
                    }

                    long seconds;
                    daily.TryGetValue(date, out seconds);
                    cells.Add(new HeatmapCell
                    {
                        Date = date,
                        Seconds = seconds,
                        Level = Level(seconds, max),
                        Week = week,
                        Day = day,
                    });
                }
            }
            return cells;
        }
    }
}
