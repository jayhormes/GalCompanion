using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GalCompanion.Tests
{
    public class ImportPlanTests
    {
        private static LunaGame Luna(string title, params (int day, int minutes)[] sessions)
        {
            var game = new LunaGame { Uid = title, GamePath = @"D:\x\" + title + ".exe", Title = title };
            foreach (var s in sessions)
            {
                var start = new DateTime(2026, 1, s.day, 20, 0, 0);
                game.Sessions.Add(new LunaSession { Start = start, End = start.AddMinutes(s.minutes) });
            }
            return game;
        }

        private static MatchResult Matched(LunaGame luna, ulong currentPlaytime)
        {
            return new MatchResult
            {
                Luna = luna,
                Kind = MatchKind.Path,
                Playnite = new PlayniteGame { Id = Guid.NewGuid(), Name = luna.Title, Playtime = currentPlaytime },
            };
        }

        [Fact]
        public void Sums_the_sessions_into_playtime()
        {
            var plan = ImportPlan.Build(new List<MatchResult> { Matched(Luna("A", (2, 120), (3, 30)), 0) }, false);

            Assert.Equal(PlanAction.Write, plan[0].Action);
            Assert.Equal(9000, plan[0].LunaSeconds);
            Assert.Equal(9000ul, plan[0].NewPlaytime);
            Assert.Equal(2, plan[0].SessionCount);
        }

        [Fact]
        public void Records_the_first_and_last_session()
        {
            var plan = ImportPlan.Build(new List<MatchResult> { Matched(Luna("A", (5, 60), (2, 60)), 0) }, false);

            Assert.Equal(new DateTime(2026, 1, 2, 20, 0, 0), plan[0].FirstSession);
            Assert.Equal(new DateTime(2026, 1, 5, 21, 0, 0), plan[0].LastSession);
        }

        [Fact]
        public void Keeps_an_existing_playtime_by_default()
        {
            var plan = ImportPlan.Build(new List<MatchResult> { Matched(Luna("A", (2, 120)), 999) }, false);

            Assert.Equal(PlanAction.KeepExisting, plan[0].Action);
            Assert.Equal(999ul, plan[0].NewPlaytime);
        }

        [Fact]
        public void Overwrite_replaces_an_existing_playtime()
        {
            var plan = ImportPlan.Build(new List<MatchResult> { Matched(Luna("A", (2, 120)), 999) }, true);

            Assert.Equal(PlanAction.Write, plan[0].Action);
            Assert.Equal(7200ul, plan[0].NewPlaytime);
        }

        [Fact]
        public void Unmatched_games_are_reported_not_written()
        {
            var plan = ImportPlan.Build(
                new List<MatchResult> { new MatchResult { Luna = Luna("A", (2, 60)), Kind = MatchKind.None } },
                false);

            Assert.Equal(PlanAction.Unmatched, plan[0].Action);
        }

        [Fact]
        public void A_matched_game_with_no_sessions_is_skipped()
        {
            var plan = ImportPlan.Build(new List<MatchResult> { Matched(Luna("A"), 0) }, false);

            Assert.Equal(PlanAction.NoSessions, plan[0].Action);
            Assert.Equal(0ul, plan[0].NewPlaytime);
        }

        [Fact]
        public void Every_input_appears_exactly_once_in_the_plan()
        {
            var matches = new List<MatchResult>
            {
                Matched(Luna("A", (2, 60)), 0),
                Matched(Luna("B"), 0),
                new MatchResult { Luna = Luna("C", (2, 60)), Kind = MatchKind.None },
            };

            var plan = ImportPlan.Build(matches, false);

            Assert.Equal(3, plan.Count);
            Assert.Equal(new[] { "A", "B", "C" }, plan.Select(p => p.Luna.Title).ToArray());
        }
    }
}
