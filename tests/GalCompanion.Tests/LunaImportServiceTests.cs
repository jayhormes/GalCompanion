using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace GalCompanion.Tests
{
    public class LunaImportServiceTests
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
                SessionCount = sessions.Length,
            };
        }

        [Fact]
        public void Converts_every_session_onto_the_matched_playnite_game()
        {
            var converted = LunaImportService.ToSessions(Entry(("2026-01-02T20:00:00", 120)));

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
            Assert.Empty(LunaImportService.ToSessions(Entry(("2026-01-02T20:00:00", 0))));
        }

        [Fact]
        public void Writes_a_log_the_plugin_reads_back()
        {
            using (var dir = new TempDir())
            {
                var store = new SessionStore(dir.Path);
                var added = new LunaImportService(store)
                    .WriteSessions(new[] { Entry(("2026-01-02T20:00:00", 60)) });

                Assert.Equal(1, added);
                var reloaded = store.Load();
                Assert.Single(reloaded);
                Assert.Equal(3600, reloaded[0].Seconds);
            }
        }

        [Fact]
        public void Keeps_what_the_plugin_already_recorded()
        {
            using (var dir = new TempDir())
            {
                var store = new SessionStore(dir.Path);
                store.Append(new PlaySession
                {
                    GameId = GameId,
                    StartUtc = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
                    Seconds = 600,
                    Device = "JAY-PC",
                    GameName = "モザイクの天使",
                });

                new LunaImportService(store).WriteSessions(new[] { Entry(("2026-01-02T20:00:00", 60)) });

                Assert.Equal(2, store.Load().Count);
            }
        }

        [Fact]
        public void Running_the_import_twice_adds_nothing_the_second_time()
        {
            using (var dir = new TempDir())
            {
                var service = new LunaImportService(new SessionStore(dir.Path));
                var entries = new[] { Entry(("2026-01-02T20:00:00", 60), ("2026-01-03T20:00:00", 30)) };

                Assert.Equal(2, service.WriteSessions(entries));
                Assert.Equal(0, service.WriteSessions(entries));
            }
        }

        [Fact]
        public void Backup_of_a_log_that_is_not_there_yet_is_not_an_error()
        {
            using (var dir = new TempDir())
            {
                var service = new LunaImportService(new SessionStore(dir.Path));

                Assert.Null(service.BackupSessions(Path.Combine(dir.Path, "bk"), "x"));
            }
        }

        [Fact]
        public void Backup_copies_the_existing_log()
        {
            using (var dir = new TempDir())
            {
                var service = new LunaImportService(new SessionStore(dir.Path));
                service.WriteSessions(new[] { Entry(("2026-01-02T20:00:00", 60)) });

                Assert.True(File.Exists(service.BackupSessions(Path.Combine(dir.Path, "bk"), "stamp")));
            }
        }

        [Fact]
        public void Describe_pulls_the_real_exe_out_of_locale_emulator_arguments()
        {
            var game = LunaImportService.Describe(
                GameId, "モザイクの天使", @"D:\Games\mosaic", 0, 0, null,
                new[]
                {
                    new KeyValuePair<string, string>(
                        @"C:\LE\LEProc.exe", @"-runas ""{GUID}"" ""D:\Games\mosaic\game.exe"""),
                });

            Assert.Contains(@"C:\LE\LEProc.exe", game.ActionPaths);
            Assert.Contains(@"D:\Games\mosaic\game.exe", game.ActionPaths);
        }

        [Fact]
        public void Describe_tolerates_a_game_with_no_actions()
        {
            var game = LunaImportService.Describe(GameId, "x", null, 5, 1, null, null);

            Assert.Empty(game.ActionPaths);
            Assert.Equal(string.Empty, game.InstallDirectory);
            Assert.Equal(5ul, game.Playtime);
        }

        [Fact]
        public void Plan_refuses_a_folder_that_is_not_luna()
        {
            using (var dir = new TempDir())
            {
                var missing = Path.Combine(dir.Path, "nope");

                var error = Assert.Throws<InvalidOperationException>(
                    () => LunaImportService.Plan(missing, new List<PlayniteGame>(), false));
                Assert.Contains(missing, error.Message);
            }
        }

        [Theory]
        [InlineData(0, "0h")]
        [InlineData(-5, "0h")]
        [InlineData(120, "2m")]
        [InlineData(3600, "1.0h")]
        [InlineData(45000, "12.5h")]
        public void Hours_are_formatted_for_the_report(long seconds, string expected)
        {
            Assert.Equal(expected, LunaImportService.FormatHours(seconds));
        }
    }
}
