using GalCompanion;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace LunaImport.Tests
{
    public class GalCompanionWriterTests
    {
        private static readonly Guid GameId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static PlanEntry Entry(params (string startLocal, int minutes)[] sessions)
        {
            var luna = new LunaGame { Title = "モザイクの天使" };
            foreach (var s in sessions)
            {
                var start = DateTime.Parse(s.startLocal);
                luna.Sessions.Add(new LunaSession { Start = start, End = start.AddMinutes(s.minutes) });
            }
            return new PlanEntry
            {
                Luna = luna,
                Playnite = new PlayniteGame { Id = GameId, Name = "モザイクの天使" },
                Action = PlanAction.Write,
            };
        }

        [Fact]
        public void Converts_every_session_onto_the_matched_playnite_game()
        {
            var converted = GalCompanionWriter.Convert(Entry(("2026-01-02T20:00:00", 120)));

            Assert.Single(converted);
            Assert.Equal(GameId, converted[0].GameId);
            Assert.Equal(7200, converted[0].Seconds);
            Assert.Equal("LunaTranslator", converted[0].Device);
            Assert.Equal("モザイクの天使", converted[0].GameName);
            Assert.Equal(DateTimeKind.Utc, converted[0].StartUtc.Kind);
        }

        [Fact]
        public void Drops_zero_length_sessions()
        {
            Assert.Empty(GalCompanionWriter.Convert(Entry(("2026-01-02T20:00:00", 0))));
        }

        [Fact]
        public void Merge_writes_a_log_the_plugin_can_read_back()
        {
            var text = GalCompanionWriter.Merge(null, new[] { Entry(("2026-01-02T20:00:00", 60)) });

            var parsed = SessionLog.Parse(text);
            Assert.Single(parsed);
            Assert.Equal(3600, parsed[0].Seconds);
        }

        [Fact]
        public void Merge_keeps_what_the_plugin_already_recorded()
        {
            var existing = SessionLog.Serialize(new[]
            {
                new PlaySession
                {
                    GameId = GameId,
                    StartUtc = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
                    Seconds = 600,
                    Device = "JAY-PC",
                    GameName = "モザイクの天使",
                },
            });

            var text = GalCompanionWriter.Merge(existing, new[] { Entry(("2026-01-02T20:00:00", 60)) });

            Assert.Equal(2, SessionLog.Parse(text).Count);
        }

        [Fact]
        public void Running_the_import_twice_adds_nothing_the_second_time()
        {
            var entries = new[] { Entry(("2026-01-02T20:00:00", 60), ("2026-01-03T20:00:00", 30)) };

            var once = GalCompanionWriter.Merge(null, entries);
            var twice = GalCompanionWriter.Merge(once, entries);

            Assert.Equal(2, SessionLog.Parse(twice).Count);
        }

        [Fact]
        public void Write_creates_the_extension_data_folder_and_reports_what_was_added()
        {
            using (var dir = new TempDir())
            {
                var added = GalCompanionWriter.Write(dir.Path, new[] { Entry(("2026-01-02T20:00:00", 60)) });

                Assert.Equal(1, added);
                Assert.True(File.Exists(GalCompanionWriter.LogPath(dir.Path)));

                var again = GalCompanionWriter.Write(dir.Path, new[] { Entry(("2026-01-02T20:00:00", 60)) });
                Assert.Equal(0, again);
                Assert.False(File.Exists(GalCompanionWriter.LogPath(dir.Path) + ".tmp"));
            }
        }

        [Fact]
        public void Backup_of_a_log_that_is_not_there_yet_is_not_an_error()
        {
            using (var dir = new TempDir())
            {
                Assert.Null(GalCompanionWriter.Backup(dir.Path, Path.Combine(dir.Path, "bk"), "x"));
            }
        }

        [Fact]
        public void Backup_copies_the_existing_log()
        {
            using (var dir = new TempDir())
            {
                GalCompanionWriter.Write(dir.Path, new[] { Entry(("2026-01-02T20:00:00", 60)) });

                var copy = GalCompanionWriter.Backup(dir.Path, Path.Combine(dir.Path, "bk"), "stamp");

                Assert.True(File.Exists(copy));
            }
        }
    }
}
