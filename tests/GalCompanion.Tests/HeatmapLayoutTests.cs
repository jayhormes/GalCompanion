using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GalCompanion.Tests
{
    public class HeatmapLayoutTests
    {
        private static readonly DateTime Sat = new DateTime(2026, 8, 29); // 週六

        [Theory]
        [InlineData(0, 100, 0)]
        [InlineData(-5, 100, 0)]
        [InlineData(1, 100, 1)]
        [InlineData(25, 100, 1)]
        [InlineData(26, 100, 2)]
        [InlineData(50, 100, 2)]
        [InlineData(75, 100, 3)]
        [InlineData(100, 100, 4)]
        public void Level_buckets_by_quarter_of_the_busiest_day(long seconds, long max, int expected)
        {
            Assert.Equal(expected, HeatmapLayout.Level(seconds, max));
        }

        [Fact]
        public void Level_of_the_only_day_is_still_visible()
        {
            Assert.Equal(1, HeatmapLayout.Level(60, 0));
        }

        [Fact]
        public void The_last_cell_is_the_end_date()
        {
            var cells = HeatmapLayout.Build(new Dictionary<DateTime, long>(), Sat, 4);
            Assert.Equal(Sat, cells.Last().Date);
        }

        [Fact]
        public void Covers_exactly_the_requested_number_of_weeks()
        {
            var cells = HeatmapLayout.Build(new Dictionary<DateTime, long>(), Sat, 4);

            Assert.Equal(4, cells.Max(c => c.Week) + 1);
            // 最終週は土曜まで＝日〜土の 7 日、前 3 週も 7 日ずつ
            Assert.Equal(28, cells.Count);
            Assert.Equal(Sat.AddDays(-27), cells.First().Date);
        }

        [Fact]
        public void Stops_at_the_end_date_mid_week()
        {
            var wed = new DateTime(2026, 8, 26);
            var cells = HeatmapLayout.Build(new Dictionary<DateTime, long>(), wed, 2);

            Assert.Equal(wed, cells.Last().Date);
            Assert.Equal(11, cells.Count); // 7 + 日〜水の 4
        }

        [Fact]
        public void Every_cell_sits_on_its_own_weekday_row()
        {
            var cells = HeatmapLayout.Build(new Dictionary<DateTime, long>(), Sat, 3);

            foreach (var cell in cells)
            {
                Assert.Equal((int)cell.Date.DayOfWeek, cell.Day);
            }
        }

        [Fact]
        public void Carries_the_daily_totals_onto_the_right_days()
        {
            var daily = new Dictionary<DateTime, long>
            {
                { Sat, 7200 },
                { Sat.AddDays(-3), 1800 },
            };

            var cells = HeatmapLayout.Build(daily, Sat, 4);

            Assert.Equal(7200, cells.Single(c => c.Date == Sat).Seconds);
            Assert.Equal(4, cells.Single(c => c.Date == Sat).Level);
            Assert.Equal(1, cells.Single(c => c.Date == Sat.AddDays(-3)).Level);
            Assert.Equal(0, cells.Single(c => c.Date == Sat.AddDays(-1)).Level);
        }

        [Fact]
        public void Days_outside_the_window_do_not_set_the_scale()
        {
            var daily = new Dictionary<DateTime, long>
            {
                { Sat, 100 },
                { Sat.AddDays(-400), 999999 },   // 範囲外の大物に引っぱられない
            };

            var cells = HeatmapLayout.Build(daily, Sat, 4);

            Assert.Equal(4, cells.Single(c => c.Date == Sat).Level);
        }

        [Fact]
        public void A_non_positive_week_count_gives_nothing()
        {
            Assert.Empty(HeatmapLayout.Build(new Dictionary<DateTime, long>(), Sat, 0));
        }

        [Fact]
        public void Honours_a_different_first_day_of_week()
        {
            var cells = HeatmapLayout.Build(
                new Dictionary<DateTime, long>(), Sat, 2, DayOfWeek.Monday);

            Assert.Equal(DayOfWeek.Monday, cells.First().Date.DayOfWeek);
            Assert.Equal(Sat, cells.Last().Date);
        }
    }
}
