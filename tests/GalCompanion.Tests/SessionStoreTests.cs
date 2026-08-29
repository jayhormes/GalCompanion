using System;
using System.IO;
using System.Linq;
using Xunit;

namespace GalCompanion.Tests
{
    public class SessionStoreTests
    {
        private static readonly Guid A = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static PlaySession Session(string startUtc, int seconds)
        {
            return new PlaySession
            {
                GameId = A,
                StartUtc = DateTime.Parse(startUtc, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal
                    | System.Globalization.DateTimeStyles.AssumeUniversal),
                Seconds = seconds,
                Device = "PC",
                GameName = "モザイクの天使",
            };
        }

        [Fact]
        public void Load_of_a_missing_file_is_empty_not_an_error()
        {
            using (var dir = new TempDir())
            {
                Assert.Empty(new SessionStore(dir.Path).Load());
            }
        }

        [Fact]
        public void Append_creates_the_file_with_a_header_once()
        {
            using (var dir = new TempDir())
            {
                var store = new SessionStore(dir.Path);
                store.Append(Session("2026-08-30T01:00:00Z", 60));
                store.Append(Session("2026-08-30T03:00:00Z", 120));

                var text = File.ReadAllText(store.Path_);
                Assert.Equal(1, text.Split('\n').Count(l => l.StartsWith("#")));
                Assert.Equal(2, store.Load().Count);
                Assert.Equal(180, store.Load().Sum(s => s.Seconds));
            }
        }

        [Fact]
        public void Append_creates_the_directory_if_it_is_not_there_yet()
        {
            using (var dir = new TempDir())
            {
                var nested = Path.Combine(dir.Path, "a", "b");
                var store = new SessionStore(nested);
                store.Append(Session("2026-08-30T01:00:00Z", 60));

                Assert.Single(store.Load());
            }
        }

        [Fact]
        public void ReplaceAll_rewrites_the_whole_file()
        {
            using (var dir = new TempDir())
            {
                var store = new SessionStore(dir.Path);
                store.Append(Session("2026-08-30T01:00:00Z", 60));
                store.Append(Session("2026-08-30T03:00:00Z", 120));

                store.ReplaceAll(new[] { Session("2026-08-30T05:00:00Z", 30) });

                var loaded = store.Load();
                Assert.Single(loaded);
                Assert.Equal(30, loaded[0].Seconds);
                Assert.False(File.Exists(store.Path_ + ".tmp"));
            }
        }

        [Fact]
        public void Round_trip_keeps_the_japanese_title()
        {
            using (var dir = new TempDir())
            {
                var store = new SessionStore(dir.Path);
                store.Append(Session("2026-08-30T01:00:00Z", 60));

                Assert.Equal("モザイクの天使", store.Load()[0].GameName);
            }
        }
    }
}
