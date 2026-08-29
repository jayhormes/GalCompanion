using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GalCompanion.Tests
{
    public class SessionLogTests
    {
        private static readonly Guid A = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid B = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static PlaySession Session(
            Guid id, string startUtc, int seconds, string device = "PC", string name = "ゲーム")
        {
            return new PlaySession
            {
                GameId = id,
                StartUtc = DateTime.Parse(startUtc, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal
                    | System.Globalization.DateTimeStyles.AssumeUniversal),
                Seconds = seconds,
                Device = device,
                GameName = name,
            };
        }

        // --- 1 行の往復 ---

        [Fact]
        public void A_line_round_trips()
        {
            var original = Session(A, "2026-08-30T01:46:24Z", 5400, "ALLY", "モザイクの天使");

            var parsed = SessionLog.ParseLine(SessionLog.FormatLine(original));

            Assert.Equal(original.GameId, parsed.GameId);
            Assert.Equal(original.StartUtc, parsed.StartUtc);
            Assert.Equal(5400, parsed.Seconds);
            Assert.Equal("ALLY", parsed.Device);
            Assert.Equal("モザイクの天使", parsed.GameName);
        }

        [Fact]
        public void Tabs_and_newlines_in_the_name_cannot_break_the_line()
        {
            var line = SessionLog.FormatLine(Session(A, "2026-08-30T01:00:00Z", 60, "P\tC", "a\tb\nc"));

            Assert.Equal(5, line.Split('\t').Length);
            Assert.Equal("a b c", SessionLog.ParseLine(line).GameName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("#galcompanion-sessions\t1")]
        [InlineData("not-a-guid\t2026-08-30T01:00:00Z\t60")]
        [InlineData("11111111-1111-1111-1111-111111111111\tnot-a-date\t60")]
        [InlineData("11111111-1111-1111-1111-111111111111\t2026-08-30T01:00:00Z\tx")]
        [InlineData("11111111-1111-1111-1111-111111111111\t2026-08-30T01:00:00Z\t-5")]
        [InlineData("11111111-1111-1111-1111-111111111111\t2026-08-30T01:00:00Z")]
        public void Broken_lines_are_dropped_not_fatal(string line)
        {
            Assert.Null(SessionLog.ParseLine(line));
        }

        [Fact]
        public void A_line_without_the_optional_columns_still_parses()
        {
            var parsed = SessionLog.ParseLine("11111111-1111-1111-1111-111111111111\t2026-08-30T01:00:00Z\t60");

            Assert.Equal(60, parsed.Seconds);
            Assert.Equal(string.Empty, parsed.Device);
        }

        [Fact]
        public void Start_time_is_kept_to_the_second_in_utc()
        {
            var parsed = SessionLog.ParseLine(
                "11111111-1111-1111-1111-111111111111\t2026-08-30T10:00:00+02:00\t60");

            Assert.Equal(DateTimeKind.Utc, parsed.StartUtc.Kind);
            Assert.Equal(new DateTime(2026, 8, 30, 8, 0, 0, DateTimeKind.Utc), parsed.StartUtc);
        }

        // --- ファイル全体 ---

        [Fact]
        public void Parse_skips_the_header_and_blank_lines()
        {
            var text = SessionLog.Header + "\n\n"
                + SessionLog.FormatLine(Session(A, "2026-08-30T01:00:00Z", 60)) + "\r\n\n";

            Assert.Single(SessionLog.Parse(text));
        }

        [Fact]
        public void Parse_survives_a_truncated_file()
        {
            var text = SessionLog.Header + "\n"
                + SessionLog.FormatLine(Session(A, "2026-08-30T01:00:00Z", 60)) + "\n"
                + "11111111-1111";

            Assert.Single(SessionLog.Parse(text));
        }

        [Fact]
        public void Serialize_writes_the_header_and_sorts_by_time()
        {
            var text = SessionLog.Serialize(new[]
            {
                Session(A, "2026-08-30T03:00:00Z", 60),
                Session(A, "2026-08-30T01:00:00Z", 60),
            });

            var lines = text.Split('\n');
            Assert.Equal(SessionLog.Header, lines[0]);
            Assert.Contains("T01:00:00Z", lines[1]);
        }

        [Fact]
        public void Serialize_and_parse_round_trip_a_whole_file()
        {
            var sessions = new[]
            {
                Session(A, "2026-08-30T01:00:00Z", 60),
                Session(B, "2026-08-31T01:00:00Z", 120, "ALLY", "べつのゲーム"),
            };

            var reparsed = SessionLog.Parse(SessionLog.Serialize(sessions));

            Assert.Equal(2, reparsed.Count);
            Assert.Equal(180, reparsed.Sum(s => s.Seconds));
        }

        [Fact]
        public void Parse_of_nothing_is_an_empty_list()
        {
            Assert.Empty(SessionLog.Parse(null));
            Assert.Empty(SessionLog.Parse(string.Empty));
        }

        // --- 二台ぶんの合流 ---

        [Fact]
        public void Merge_is_the_union_of_both_machines()
        {
            var pc = new[] { Session(A, "2026-08-30T01:00:00Z", 60, "PC") };
            var ally = new[] { Session(A, "2026-08-31T01:00:00Z", 120, "ALLY") };

            var merged = SessionLog.Merge(pc, ally);

            Assert.Equal(2, merged.Count);
            Assert.Equal(180, merged.Sum(s => s.Seconds));
        }

        [Fact]
        public void Merge_does_not_double_count_the_same_session()
        {
            var one = Session(A, "2026-08-30T01:00:00Z", 60);
            var merged = SessionLog.Merge(new[] { one }, new[] { one });

            Assert.Single(merged);
        }

        [Fact]
        public void Merge_keeps_the_longer_record_of_the_same_session()
        {
            // 片方が途中で落ちて短く記録されていることがある
            var truncated = Session(A, "2026-08-30T01:00:00Z", 60);
            var complete = Session(A, "2026-08-30T01:00:00Z", 3600);

            Assert.Equal(3600, SessionLog.Merge(new[] { truncated }, new[] { complete })[0].Seconds);
            Assert.Equal(3600, SessionLog.Merge(new[] { complete }, new[] { truncated })[0].Seconds);
        }

        [Fact]
        public void Merge_keeps_two_games_started_at_the_same_moment_apart()
        {
            var merged = SessionLog.Merge(
                new[] { Session(A, "2026-08-30T01:00:00Z", 60) },
                new[] { Session(B, "2026-08-30T01:00:00Z", 60) });

            Assert.Equal(2, merged.Count);
        }

        [Fact]
        public void Merge_is_stable_when_one_side_is_empty_or_null()
        {
            var pc = new[] { Session(A, "2026-08-30T01:00:00Z", 60) };

            Assert.Single(SessionLog.Merge(pc, new List<PlaySession>()));
            Assert.Single(SessionLog.Merge(null, pc));
            Assert.Empty(SessionLog.Merge(null, null));
        }

        [Fact]
        public void Merging_twice_changes_nothing()
        {
            var pc = new[] { Session(A, "2026-08-30T01:00:00Z", 60) };
            var ally = new[] { Session(B, "2026-08-31T01:00:00Z", 120) };

            var once = SessionLog.Merge(pc, ally);
            var twice = SessionLog.Merge(once, SessionLog.Merge(pc, ally));

            Assert.Equal(once.Count, twice.Count);
        }

        // --- 集計 ---

        [Fact]
        public void TotalsByGame_adds_up_per_game()
        {
            var totals = SessionLog.TotalsByGame(new[]
            {
                Session(A, "2026-08-30T01:00:00Z", 60),
                Session(A, "2026-08-31T01:00:00Z", 120),
                Session(B, "2026-08-31T01:00:00Z", 30),
            });

            Assert.Equal(180, totals[A]);
            Assert.Equal(30, totals[B]);
        }

        [Fact]
        public void DailyTotals_uses_local_dates()
        {
            var start = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);
            var totals = SessionLog.DailyTotals(new[]
            {
                new PlaySession { GameId = A, StartUtc = start, Seconds = 3600 },
            });

            Assert.Equal(3600, totals[start.ToLocalTime().Date]);
        }

        [Fact]
        public void DailyTotals_splits_a_session_that_crosses_midnight()
        {
            // 現地時間で 23:00 に始めて 2 時間
            var localStart = DateTime.SpecifyKind(
                new DateTime(2026, 8, 30, 23, 0, 0), DateTimeKind.Local);
            var totals = SessionLog.DailyTotals(new[]
            {
                new PlaySession
                {
                    GameId = A,
                    StartUtc = localStart.ToUniversalTime(),
                    Seconds = 7200,
                },
            });

            Assert.Equal(3600, totals[new DateTime(2026, 8, 30)]);
            Assert.Equal(3600, totals[new DateTime(2026, 8, 31)]);
        }

        [Fact]
        public void DailyTotals_still_marks_a_zero_length_session()
        {
            var start = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);
            var totals = SessionLog.DailyTotals(new[]
            {
                new PlaySession { GameId = A, StartUtc = start, Seconds = 0 },
            });

            Assert.True(totals.ContainsKey(start.ToLocalTime().Date));
            Assert.Equal(0, totals[start.ToLocalTime().Date]);
        }
    }
}
